import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ProductCard } from "../components/ProductCard";
import { ProtectedRoute } from "../components/ProtectedRoute";
import { api, ApiError, qs } from "../lib/api";
import { money, shortId } from "../lib/format";
import { dictionaries, translate } from "../lib/i18n";
import type { Product, User } from "../lib/types";
import { useAuth } from "../store/auth";
import { useUi } from "../store/ui";
import { useWishlist } from "../store/wishlist";

const product: Product = {
  id: "11111111-1111-1111-1111-111111111111",
  sku: "T-1",
  name: "Test Headphones",
  slug: "test-headphones",
  description: "desc",
  price: 199,
  currency: "USD",
  categoryId: "c",
  categoryName: "Audio",
  categorySlug: "audio",
  brand: "Aurora",
  imageUrl: "/x.svg",
  gallery: ["/x.svg"],
  specs: {},
  isActive: true,
  isFeatured: false,
  createdAtUtc: "",
};

const user = (role: "Admin" | "Customer"): User => ({
  id: "u",
  email: "a@b.c",
  fullName: "Test User",
  role,
  isActive: true,
  createdAtUtc: "",
});

describe("format helpers", () => {
  it("formats money and ids", () => {
    expect(money(12.5, "USD", "en")).toBe("$12.50");
    expect(shortId("abcdef123456")).toBe("ABCDEF12");
  });

  it("builds query strings skipping empty values", () => {
    expect(qs({ a: 1, b: "", c: undefined, d: true })).toBe("?a=1&d=true");
    expect(qs({})).toBe("");
  });
});

describe("i18n", () => {
  it("has an Arabic translation for every English key", () => {
    expect(Object.keys(dictionaries.ar).sort()).toEqual(
      Object.keys(dictionaries.en).sort(),
    );

    expect(translate("ar", "nav.cart")).not.toBe(translate("en", "nav.cart"));
  });
});

describe("theme", () => {
  it("toggles between light and dark mode", () => {
    useUi.setState({ theme: "light" });

    useUi.getState().toggleTheme();
    expect(useUi.getState().theme).toBe("dark");

    useUi.getState().toggleTheme();
    expect(useUi.getState().theme).toBe("light");
  });
});

describe("api client", () => {
  beforeEach(() => {
    useAuth.setState({ token: null, user: null });
  });

  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it("sends the bearer token and parses JSON", async () => {
    useAuth.setState({ token: "tok", user: user("Customer") });

    const fetchMock = vi
      .fn()
      .mockResolvedValue(
        new Response(JSON.stringify({ ok: true }), { status: 200 }),
      );

    vi.stubGlobal("fetch", fetchMock);

    expect(await api<{ ok: boolean }>("/api/x")).toEqual({
      ok: true,
    });

    expect((fetchMock.mock.calls[0][1] as RequestInit).headers).toMatchObject({
      Authorization: "Bearer tok",
    });
  });

  it("turns problem+json into ApiError and signs out on 401", async () => {
    useAuth.setState({ token: "tok", user: user("Customer") });

    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify({
            title: "Unauthorized",
            detail: "expired",
          }),
          { status: 401 },
        ),
      ),
    );

    await expect(api("/api/x")).rejects.toMatchObject({
      status: 401,
      message: "expired",
    });

    expect(useAuth.getState().token).toBeNull();
  });

  it("reports field validation errors", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify({
            title: "Validation failed",
            errors: {
              email: ["Invalid email"],
            },
          }),
          { status: 400 },
        ),
      ),
    );

    try {
      await api("/api/x");
      throw new Error("Expected API request to fail");
    } catch (err: unknown) {
      expect(err).toBeInstanceOf(ApiError);

      if (err instanceof ApiError) {
        expect(err.message).toBe("Invalid email");
      } else {
        throw err;
      }
    }
  });

  it("reports network failures in plain language", async () => {
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("failed")));

    await expect(api("/api/x")).rejects.toMatchObject({
      status: 0,
    });
  });
});

describe("ProductCard", () => {
  beforeEach(() => {
    useUi.setState({ lang: "en" });
    useWishlist.setState({ ids: [] });
  });

  it("shows name, price and disables add-to-cart when out of stock", () => {
    const onAdd = vi.fn();

    render(
      <MemoryRouter>
        <ProductCard
          product={product}
          stock={{
            productId: product.id,
            available: 0,
            status: "out_of_stock",
          }}
          onAdd={onAdd}
        />
      </MemoryRouter>,
    );

    expect(screen.getByText("Test Headphones")).toBeInTheDocument();
    expect(screen.getByText("$199.00")).toBeInTheDocument();

    expect(screen.getByRole("button", { name: "Out of stock" })).toBeDisabled();
  });

  it("allows adding in-stock products", () => {
    const onAdd = vi.fn();

    render(
      <MemoryRouter>
        <ProductCard
          product={product}
          stock={{
            productId: product.id,
            available: 9,
            status: "in_stock",
          }}
          onAdd={onAdd}
        />
      </MemoryRouter>,
    );

    screen.getByRole("button", { name: "Add to cart" }).click();

    expect(onAdd).toHaveBeenCalledWith(product);
  });
});

describe("ProtectedRoute", () => {
  const renderAt = (path: string) =>
    render(
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="/login" element={<p>login page</p>} />
          <Route path="/" element={<p>home page</p>} />

          <Route path="/orders" element={<ProtectedRoute />}>
            <Route index element={<p>orders page</p>} />
          </Route>

          <Route path="/admin" element={<ProtectedRoute role="Admin" />}>
            <Route index element={<p>admin page</p>} />
          </Route>
        </Routes>
      </MemoryRouter>,
    );

  it("redirects anonymous visitors to login", () => {
    useAuth.setState({ token: null, user: null });

    renderAt("/orders");

    expect(screen.getByText("login page")).toBeInTheDocument();
  });

  it("keeps customers out of admin pages", () => {
    useAuth.setState({
      token: "t",
      user: user("Customer"),
    });

    renderAt("/admin");

    expect(screen.getByText("home page")).toBeInTheDocument();
  });

  it("lets admins in", () => {
    useAuth.setState({
      token: "t",
      user: user("Admin"),
    });

    renderAt("/admin");

    expect(screen.getByText("admin page")).toBeInTheDocument();
  });
});
