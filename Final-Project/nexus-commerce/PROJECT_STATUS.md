# Project status

_Generated: 2026-10-12. Read this before trusting anything else in the repository._

## Honest summary
All source files, configuration, tests and documentation requested for the platform have been **written**
(94 C# files / ~5414 lines, 39 TypeScript files / ~2214 lines, plus Docker, compose, JSON/YAML config, SVG assets, docs).
**None of it has been compiled or executed**, because the generation sandbox had **no .NET SDK, no Docker and no network
access (so no NuGet/npm restore)**. Expect a small number of compile/type errors on the very first build; the code was
written carefully and statically cross-checked, but "it compiles and passes" is **not** claimed.

## Completed (written, not yet run)
* 9 microservices + YARP gateway with separate data ownership: Identity, Catalog, Inventory, Cart, Orders, Payments (mock), Notifications, Reviews, AI Assistant.
* JWT auth, roles, server-side authorization on every admin/protected API, RFC 7807 errors, validation, paging, CORS allow-list, rate limits, health checks, JSON logs, correlation ids.
* RabbitMQ: versioned events, durable queues, confirms, manual acks, retry + dead-letter, outbox + inbox, order saga with compensation.
* 8 categories, 32 products, stock, 65 reviews seeded; local SVG images (no external image URLs).
* React storefront + admin dashboard, English/Arabic (RTL), toasts, loading/empty/error states, accessible forms.
* AI assistant: LLM mode (OpenAI-compatible, Anthropic) with read-only tools and fallback mode that works without any key.
* Docker Compose (SQL Server, RabbitMQ + UI, Redis, 9 services, gateway, frontend), `.env.example`, documentation.
* Tests: xUnit backend (contracts, rules, inventory, payments, authz/validation, assistant), Vitest frontend, Python live-stack smoke test.

## Verification actually performed in the generation environment
| Check | Result |
|---|---|
| All `*.json` files parse (appsettings, package.json, tsconfig, seed data) | ✔ pass |
| `docker-compose.yml` parses as YAML; anchors/merge keys resolve; 14 services present | ✔ pass |
| All `*.csproj`/`.props` are well-formed XML; every `ProjectReference` and every `.sln` entry points to an existing file | ✔ pass |
| Brace/paren/bracket balance of all 94 C# files and all TS/TSX files (heuristic, strings/comments stripped) | ✔ pass |
| Every relative TS import resolves to a file; every `t('…')` i18n key exists; EN/AR dictionaries have identical keys (also asserted by a test) | ✔ pass |
| Python scripts compile (`py_compile`); seed generator ran and produced 32 products + 105 SVG images | ✔ pass |
| Arabic→English keyword tables have equal length | ✔ pass |
| ZIP `Nexus-Commerce-AI.zip` created (~310 KB, 379 entries), `unzip -t` clean, test-extracted, required files present, no `.env`/secrets/`node_modules`/`bin`/`obj` inside | ✔ pass |

## NOT verified (could not be run here)
`dotnet restore/build/test` · `npm install/build/test` / TypeScript type-check · `docker compose config/up` · SQL Server schema creation
· RabbitMQ publish/consume · gateway routing at runtime · authentication flows · cart/order/payment/saga flows · inventory under concurrency ·
LLM provider calls (OpenAI-compatible / Anthropic wire formats written from API knowledge, never called) · chatbot end to end · Swagger · visual/UX review of the frontend.
NuGet versions use floating ranges (`8.0.*`, `2.*`, …) plus `RabbitMQ.Client 6.8.1`, `Yarp.ReverseProxy 2.1.0`, `Swashbuckle.AspNetCore 6.6.*`; npm versions are caret ranges. They have not been resolved against the feeds, and no lock files exist.

## Known limitations
* No EF migrations (`EnsureCreated` at startup; see docs/database.md).
* Cart requires sign-in (guests are redirected to login) – by design, the cart is server-side.
* Wishlist is device-local (localStorage), not synced to the server.
* Arabic covers the storefront; the admin dashboard is English only. Product catalog content is English.
* Payments are mock-only; no refunds/captures, no real gateway implemented. Shipping is a flat fee configured in Orders (display logic duplicated in the checkout UI).
* Consumer retry delay blocks that consumer's channel while waiting (simple design); no replay tool for dead-letter queues.
* Single-instance in-process rate limiting; no refresh tokens or token revocation; no email verification/password reset.
* Admin product images are URL-based (no upload).
* Notifications default to log-only; SMTP path untested.
* Redis is used only for catalog category caching (cache failures degrade gracefully).
* Search is SQL `LIKE`-based (no full-text/relevance ranking).

## Exact next steps (in order)
1. `cp .env.example .env`, fill `MSSQL_SA_PASSWORD`, `JWT_SIGNING_KEY`, `ADMIN_PASSWORD`.
2. On a machine with .NET 8 SDK: `dotnet restore Nexus.Commerce.sln && dotnet build Nexus.Commerce.sln` – fix any compile errors first (most likely spots: package version resolution, nullable warnings-as-errors none, minimal-API lambda inference).
3. `dotnet test tests/backend` – fix failing tests (they run with EF InMemory, no Docker needed).
4. `cd frontend && npm install && npm run build && npm test` – fix TypeScript errors; commit the generated `package-lock.json`.
5. `docker compose up --build`; watch `docker compose ps` until all services are healthy.
6. `python3 tests/integration/smoke_test.py --admin-email <ADMIN_EMAIL> --admin-password <ADMIN_PASSWORD>` – fix failures.
7. Try the chatbot in fallback mode, then configure an AI provider (README §3) and re-test tool calls against the real API.
8. Generate EF migrations (docs/database.md) and replace `EnsureCreated`.
9. Update this file with the real build/test results.
