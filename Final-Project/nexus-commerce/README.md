# Nexus Commerce AI

A complete e-commerce **microservices** platform with an **AI shopping assistant**:

* **Backend:** C# / ASP.NET Core 8 (minimal APIs), EF Core, JWT + roles, YARP API gateway
* **Messaging & data:** SQL Server (one database per service), RabbitMQ (outbox/inbox + saga), Redis (cache)
* **Frontend:** React 18 + TypeScript + Vite + Tailwind, React Router, Zustand, English/Arabic (RTL)
* **AI:** provider-independent assistant (OpenAI-compatible or Anthropic) with a deterministic fallback that works with no API key
* **Ops:** Docker Compose, health checks, structured JSON logs, correlation ids

> **Read [`PROJECT_STATUS.md`](PROJECT_STATUS.md) first.** It states exactly what has and has not been built, run and
> verified. This project was generated in an environment **without** the .NET SDK, Docker or network access, so it
> has **not been compiled or started yet**. The first `docker compose up --build` (or `dotnet build`) is its first real compile.

---

## 1. Quick start (Docker)

### Prerequisites
* Docker Desktop (or Docker Engine + Compose v2), **≥ 8 GB RAM** available to Docker (SQL Server needs ~2 GB; 9 .NET services + RabbitMQ + Redis need the rest).
* An x86-64 / amd64 CPU, or Apple Silicon with Docker Desktop's *"Use Rosetta for x86_64/amd64 emulation"* enabled (the SQL Server image is amd64-only).
* Ports free: 3000, 5100–5109, 1433, 5672, 15672, 6379.
* Accepting the SQL Server licence: Compose sets `ACCEPT_EULA=Y` (Developer edition, non-production use).

### Steps
```bash
unzip Nexus-Commerce-AI.zip && cd nexus-commerce
cp .env.example .env
#   edit .env:  MSSQL_SA_PASSWORD (complex), JWT_SIGNING_KEY (>=32 chars: `openssl rand -base64 48`),
#               ADMIN_PASSWORD (for the development admin account)
docker compose up --build
```
The first build downloads images and restores NuGet/npm packages (several minutes). Services wait for SQL Server,
RabbitMQ and Redis to become healthy, create their databases/tables and seed development data automatically.

| What | URL |
|---|---|
| **Storefront** | http://localhost:3000 |
| **API gateway** (all APIs) | http://localhost:5100 |
| Swagger per service (Development) | identity :5101, catalog :5102, inventory :5103, cart :5104, orders :5105, payments :5106, notifications :5107, reviews :5108, assistant :5109 → `http://localhost:<port>/swagger` |
| **RabbitMQ management** | http://localhost:15672 (user/password from `.env`) |
| SQL Server | `localhost,1433`, user `sa`, password from `.env` |

Stop / restart:
```bash
docker compose stop            # stop, keep data
docker compose start           # start again
docker compose down            # remove containers, keep data volumes
docker compose down -v         # remove containers AND all data (databases, RabbitMQ, Redis)
docker compose up --build -d   # rebuild after code changes
```

## 2. Development administrator

There are **no hard-coded credentials**. On startup the Identity service creates (or promotes) an administrator **only if**
`ADMIN_EMAIL` and `ADMIN_PASSWORD` are set in `.env` **and** `ASPNETCORE_ENVIRONMENT=Development`
(outside Development it additionally requires `Admin__AllowOutsideDevelopment=true`, and logs a warning).
Sign in at http://localhost:3000/login, then open **Admin** in the header. Customers register through the UI.
Every admin API enforces the `Admin` role on the server; the UI role check is only a convenience.

## 3. AI assistant configuration

The chat button (bottom corner) is available on every storefront page.

* **No key configured (default):** the **rule-based fallback** runs. It understands English and Arabic budgets
  ("under 200", "أقل من 200"), categories, availability/price questions, follow-ups and **product comparisons**, using live
  catalog, inventory and rating data. The UI labels it *"Basic assistant – no AI model configured"*. It is **not** an LLM.
* **LLM mode:** set in `.env` (never in React; keys stay server-side):

  ```
  AI_PROVIDER=openai        # OpenAI-compatible Chat Completions API (set AI_BASE_URL for compatible gateways)
  # or
  AI_PROVIDER=anthropic     # Anthropic Messages API
  AI_API_KEY=...            # your key
  AI_MODEL=...              # a model id your account can use
  ```
  then `docker compose up -d --force-recreate assistant`. Check `GET http://localhost:5100/api/assistant/status`.
* **How it is kept safe:** the model can call only four **read-only** tools (`search_products`, `get_product_details`,
  `compare_products`, `list_categories`). Arguments are validated server-side; there is no cart/order/payment/SQL/shell/HTTP tool.
  Product text is sanitised and labelled untrusted; the product cards shown to the user are built from tool results (not model text);
  amounts in the reply that do not match retrieved prices trigger a visible caution. If the provider fails or times out the
  service falls back to the rule-based assistant and tells the user. Messages are length-limited and rate-limited (20/min per user or IP).
  Any future cart/order tool must use the caller's JWT and explicit user confirmation (see `docs/security.md`).

## 4. Architecture overview

```
 Browser ── nginx (frontend:3000) ──► API Gateway (YARP, JWT, CORS, rate limits) ──► services
                                                        identity ─ catalog ─ inventory ─ cart ─ orders ─ payments
                                                        notifications ─ reviews ─ ai-assistant
 Services ◄──── RabbitMQ topic exchange "nexus.events" (durable queues, outbox/inbox, retries, DLQ) ────►
 Each service owns its database (nexus_<service>) on the shared SQL Server; Redis caches catalog categories.
```
Order saga (choreography, eventual consistency): `OrderCreated → InventoryReserved | InventoryReservationFailed →
PaymentAuthorized | PaymentFailed → OrderConfirmed | OrderCancelled` (stock committed on confirm, released/restocked on cancel,
payment voided on cancel). Details: [`docs/architecture.md`](docs/architecture.md), [`docs/events.md`](docs/events.md).

## 5. Repository layout
```
Nexus.Commerce.sln        solution (open in VS Code with C# Dev Kit, or Visual Studio)
contracts/                versioned event contracts + seed ids (Nexus.Contracts)
building-blocks/          shared auth, error handling, validation, paging, outbox/inbox, RabbitMQ, caching
gateway/                  YARP API gateway + admin monitoring endpoint
services/identity|catalog|inventory|cart|orders|payments|notifications|reviews|ai-assistant
frontend/                 React storefront + admin dashboard (+ nginx.conf, Dockerfile)
tests/backend             xUnit unit + in-process API integration tests
tests/frontend            pointer to the Vitest tests in frontend/src/test
tests/integration         smoke_test.py – end-to-end checks against the running stack
infrastructure/docker     generic .NET Dockerfile;   infrastructure/sql  optional SQL helpers
scripts/                  generate_seed_assets.py (regenerates seed JSON + placeholder SVG images)
docs/                     architecture, events, database, security
```

## 6. Database, migrations and seed data
Each service calls `EnsureCreated` at startup (retrying while SQL Server boots) and seeds idempotently: 8 categories,
**32 products** with specs, stock levels (one out of stock, one low), and 65 sample reviews. **No EF migrations are shipped**
(they could not be generated without the SDK) – see [`docs/database.md`](docs/database.md) for the migration path.
Product images are local SVG placeholders in `frontend/public/images` (no external URLs).

## 7. Running without Docker (optional)
Requires .NET 8 SDK, Node 20+, and reachable SQL Server, RabbitMQ (and optionally Redis).
```bash
export Jwt__Key="$(openssl rand -base64 48)"        # same value for every service + gateway
export ConnectionStrings__Default="Server=localhost,1433;Database=nexus_<svc>;User Id=sa;Password=...;TrustServerCertificate=True;Encrypt=False"
dotnet run --project services/identity      # repeat per service (ports 5101-5109), then:
dotnet run --project gateway                # :5100
cd frontend && npm install && npm run dev   # :5173, proxies /api to the gateway
```
(Each service needs its own `ConnectionStrings__Default`; set it per terminal.)

## 8. Tests
```bash
dotnet test tests/backend                         # unit + in-process integration (EF InMemory, RabbitMQ disabled)
cd frontend && npm install && npm test            # Vitest
cd frontend && npm run build                      # type-check + production build
python3 tests/integration/smoke_test.py --admin-email admin@nexus.local --admin-password '<your ADMIN_PASSWORD>'
```
The smoke test needs the stack running and verifies gateway routing, auth (401/403), catalog search/filter, cart, the
RabbitMQ order saga (confirm + decline), inventory consistency, mock payment, reviews, chatbot fallback and admin endpoints.

## 9. Troubleshooting
| Symptom | Fix |
|---|---|
| `JWT_SIGNING_KEY` / `MSSQL_SA_PASSWORD` error from compose | Fill them in `.env` (compose refuses to start without them). |
| SQL Server container exits | Password too weak (needs upper/lower/digit/symbol, 8+), or < 2 GB RAM for Docker, or arm64 without emulation. |
| Services restart repeatedly at first start | They wait for SQL Server (up to ~2 min). Check `docker compose logs <service>`. |
| Orders stay *Pending* | RabbitMQ or Inventory/Payment consumers not running; check `docker compose logs inventory payments orders` and the queues in the RabbitMQ UI (`*.dead` queues hold failed messages). |
| 401 after a while | Access tokens last 60 minutes (no refresh token); sign in again. |
| Port already in use | Change the host port mappings in `docker-compose.yml`. |
| Chatbot says "basic assistant" | No `AI_API_KEY`; this is expected (see §3). |

## 10. Security notes and limitations
See [`docs/security.md`](docs/security.md). Highlights: JWT in `sessionStorage` (XSS-exposed by design of SPAs; no refresh
tokens/revocation), no email verification or password reset, in-process rate limiting (per instance), the payment provider is
a **mock** (no card data is collected anywhere), development SQL/RabbitMQ ports are published to the host, and the
whole stack is a reference implementation – not hardened for production.
