#!/usr/bin/env python3
"""End-to-end smoke test against a RUNNING stack (docker compose up --build). Uses only the Python standard library.

    python3 tests/integration/smoke_test.py [--base http://localhost:5100] [--admin-email E --admin-password P]

Exercises: gateway routing, registration/login, catalog search, authorization (401/403), cart, order saga
(inventory reservation -> mock payment -> confirmation), declined-payment cancellation, inventory consistency,
reviews, chatbot fallback and (optionally) admin endpoints. Exit code 0 only if every check passes.
"""
import argparse, json, sys, time, uuid, urllib.request, urllib.error

parser = argparse.ArgumentParser()
parser.add_argument("--base", default="http://localhost:5100")
parser.add_argument("--admin-email", default=None)
parser.add_argument("--admin-password", default=None)
args = parser.parse_args()
BASE = args.base.rstrip("/")
failures, passed = [], 0


def call(method, path, body=None, token=None, headers=None):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(BASE + path, data=data, method=method)
    req.add_header("Accept", "application/json")
    if data is not None:
        req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    for k, v in (headers or {}).items():
        req.add_header(k, v)
    try:
        with urllib.request.urlopen(req, timeout=30) as r:
            raw = r.read().decode()
            return r.status, (json.loads(raw) if raw else None)
    except urllib.error.HTTPError as e:
        raw = e.read().decode()
        try:
            return e.code, json.loads(raw) if raw else None
        except json.JSONDecodeError:
            return e.code, raw


def check(name, cond, detail=""):
    global passed
    if cond:
        passed += 1
        print(f"  PASS  {name}")
    else:
        failures.append(name)
        print(f"  FAIL  {name} {detail}")


def wait_for(fn, timeout=40, interval=1.0):
    end = time.time() + timeout
    while time.time() < end:
        v = fn()
        if v:
            return v
        time.sleep(interval)
    return None


print(f"Smoke test against {BASE}")
s, _ = call("GET", "/")
check("gateway reachable", s == 200)

# --- catalog -----------------------------------------------------------------
s, page = call("GET", "/api/products?pageSize=50")
check("catalog lists >= 30 products", s == 200 and page["totalCount"] >= 30, str(s))
s, res = call("GET", "/api/products?q=Headphones&sort=price_asc")
check("search 'Headphones' returns results", s == 200 and len(res["items"]) > 0)
s, res = call("GET", "/api/products?maxPrice=50&sort=price_desc")
check("price filter respected", s == 200 and all(p["price"] <= 50 for p in res["items"]))
s, cats = call("GET", "/api/categories")
check("categories listed", s == 200 and len(cats) >= 8)

in_stock = None
for p in page["items"]:
    s, stock = call("GET", f"/api/inventory/stock?ids={p['id']}")
    if s == 200 and stock and stock[0]["available"] >= 5:
        in_stock = (p, stock[0]["available"])
        break
check("found an in-stock product", in_stock is not None)
product, available_before = in_stock

# --- authorization -----------------------------------------------------------
s, _ = call("GET", "/api/cart")
check("cart requires auth (401)", s == 401)
s, _ = call("POST", "/api/products", {"sku": "X"})
check("catalog write requires auth (401)", s == 401)

email = f"smoke+{uuid.uuid4().hex[:8]}@example.com"
s, reg = call("POST", "/api/auth/register", {"email": email, "password": "Smoke-Test-1", "fullName": "Smoke Tester"})
check("register customer", s == 201 and reg["user"]["role"] == "Customer", str(s))
token = reg["accessToken"]
s, _ = call("POST", "/api/products", {"sku": "X"}, token)
check("customer cannot write catalog (403)", s == 403, str(s))
s, _ = call("GET", "/api/admin/users", None, token)
check("customer cannot list users (403)", s == 403, str(s))
s, me = call("GET", "/api/profile", None, token)
check("profile readable", s == 200 and me["email"] == email)

# --- cart + order saga --------------------------------------------------------
s, cart = call("POST", "/api/cart/items", {"productId": product["id"], "quantity": 2}, token)
check("add to cart", s == 200 and cart["itemCount"] == 2, str(s))
check("cart price comes from server", abs(cart["items"][0]["unitPrice"] - product["price"]) < 0.001)
shipping = {"fullName": "Smoke Tester", "line1": "1 Test Street", "city": "Cairo", "postalCode": "11511", "country": "Egypt", "phone": "+20100000000"}
key = uuid.uuid4().hex
s, order = call("POST", "/api/orders", {"shipping": shipping, "paymentMethod": "mock_success"}, token, {"Idempotency-Key": key})
check("order accepted (202)", s == 202, str(s))
s2, again = call("POST", "/api/orders", {"shipping": shipping, "paymentMethod": "mock_success"}, token, {"Idempotency-Key": key})
check("duplicate submit is idempotent", s2 == 200 and again["id"] == order["id"], str(s2))
final = wait_for(lambda: (lambda r: r[1] if r[1] and r[1]["status"] == "Confirmed" else None)(call("GET", f"/api/orders/{order['id']}", None, token)))
check("order reaches Confirmed via RabbitMQ saga", final is not None)
s, pay = call("GET", f"/api/payments/order/{order['id']}", None, token)
check("payment authorized and labelled mock", s == 200 and pay["status"] == "Authorized" and pay["isMock"] is True, str(pay))
s, cart_after = call("GET", "/api/cart", None, token)
check("cart emptied after order", s == 200 and cart_after["itemCount"] == 0)
s, stock = call("GET", f"/api/inventory/stock?ids={product['id']}")
check("inventory reduced by order quantity", s == 200 and stock[0]["available"] == available_before - 2, f"{stock} vs {available_before}")

# declined payment -> cancelled -> stock released
call("POST", "/api/cart/items", {"productId": product["id"], "quantity": 1}, token)
s, order2 = call("POST", "/api/orders", {"shipping": shipping, "paymentMethod": "mock_decline"}, token)
cancelled = wait_for(lambda: (lambda r: r[1] if r[1] and r[1]["status"] == "Cancelled" else None)(call("GET", f"/api/orders/{order2['id']}", None, token)))
check("declined payment cancels the order", cancelled is not None)
time.sleep(2)
s, stock2 = call("GET", f"/api/inventory/stock?ids={product['id']}")
check("stock released after failed payment", s == 200 and stock2[0]["available"] == available_before - 2)

# --- reviews -----------------------------------------------------------------
s, rv = call("POST", "/api/reviews", {"productId": product["id"], "rating": 5, "title": "Smoke review", "body": "Works as expected."}, token)
check("verified purchaser can review", s == 201, str(s))
s, rv2 = call("POST", "/api/reviews", {"productId": product["id"], "rating": 4, "title": "Again", "body": "Duplicate attempt."}, token)
check("duplicate review rejected (409)", s == 409, str(s))

# --- chatbot -----------------------------------------------------------------
s, st = call("GET", "/api/assistant/status")
check("assistant status reports its mode honestly", s == 200 and st["mode"] in ("fallback", "llm"))
s, chat = call("POST", "/api/assistant/chat", {"message": "recommend wireless headphones under 250"})
check("chatbot returns catalog products", s == 200 and len(chat["products"]) > 0 and all(p["price"] <= 250 for p in chat["products"]), str(chat)[:200])
s, chat = call("POST", "/api/assistant/chat", {"message": "x" * 1001})
check("chatbot rejects over-long messages", s == 400)

# --- admin (optional) --------------------------------------------------------
if args.admin_email and args.admin_password:
    s, adm = call("POST", "/api/auth/login", {"email": args.admin_email, "password": args.admin_password})
    check("admin login", s == 200 and adm["user"]["role"] == "Admin", str(s))
    at = adm["accessToken"]
    for path in ("/api/admin/users", "/api/admin/orders", "/api/admin/orders/stats", "/api/admin/inventory", "/api/admin/reviews", "/api/admin/monitoring/services"):
        s, _ = call("GET", path, None, at)
        check(f"admin GET {path}", s == 200, str(s))
    s, mon = call("GET", "/api/admin/monitoring/services", None, at)
    check("all services healthy", s == 200 and all(x["healthy"] for x in mon["services"]), str(mon))
else:
    print("  (skipping admin checks: pass --admin-email/--admin-password)")

print(f"\n{passed} passed, {len(failures)} failed")
sys.exit(1 if failures else 0)
