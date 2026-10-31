# Architecture

## Services and data ownership
| Service | Port | Database | Responsibility | Publishes | Consumes |
|---|---|---|---|---|---|
| Gateway (YARP) | 5100 | – | single entry point, JWT validation, route-level authorization, CORS, rate limits, correlation id, admin health view | – | – |
| Identity | 5101 | nexus_identity | register/login (ASP.NET Core `PasswordHasher`), JWT, roles Admin/Customer, profile, admin user management | – | – |
| Catalog | 5102 | nexus_catalog | products, categories, images, specs, search/filter/sort/paging, admin CRUD (soft delete), Redis-cached categories | – | – |
| Inventory | 5103 | nexus_inventory | stock, reservations, adjustment history, oversell protection (row-version concurrency) | InventoryReserved, InventoryReservationFailed | OrderCreated, OrderConfirmed, OrderCancelled |
| Cart | 5104 | nexus_cart | per-customer cart, server-side price/stock validation against Catalog/Inventory | – | OrderCreated (clears cart) |
| Orders | 5105 | nexus_orders | orders, item snapshots, lifecycle, idempotent creation, saga state, sales stats | OrderCreated, OrderConfirmed, OrderCancelled | InventoryReserved, InventoryReservationFailed, PaymentAuthorized, PaymentFailed |
| Payments | 5106 | nexus_payments | payment intent per order via `IPaymentProvider`; **mock provider only** | PaymentAuthorized, PaymentFailed | InventoryReserved, OrderCancelled |
| Notifications | 5107 | nexus_notifications | order emails via `IEmailSender` (log provider by default, SMTP optional) | – | OrderConfirmed, OrderCancelled |
| Reviews | 5108 | nexus_reviews | ratings/reviews, one per customer per product, purchase verification via Orders, moderation | – | – |
| AI Assistant | 5109 | nexus_assistant | chat API, conversations (signed-in users), read-only tools, LLM clients, rule-based fallback | – | – |

No service reads another service's database. Synchronous REST is used for queries (Cart→Catalog/Inventory, Orders→Cart,
Reviews→Orders, Assistant→Catalog/Inventory/Reviews); RabbitMQ is used for state changes.

## Order workflow (saga by choreography)
```
Customer ─POST /api/orders (Idempotency-Key)─► Orders: Order=Pending + outbox(OrderCreated)   [one local transaction]
  Inventory ◄─ OrderCreated ─  reserve all lines, or fail
      ├─ ok   → InventoryReserved ─► Orders: AwaitingPayment ;  Payments: authorize (mock)
      │              ├─ PaymentAuthorized ─► Orders: Confirmed + outbox(OrderConfirmed)
      │              │        Inventory: commit (OnHand -= qty) ; Notifications: confirmation email
      │              └─ PaymentFailed ─────► Orders: Cancelled + outbox(OrderCancelled) ─► Inventory: release
      └─ fail → InventoryReservationFailed ─► Orders: Cancelled (+ OrderCancelled for the customer email)
Customer cancel (Pending/AwaitingPayment/Confirmed) → OrderCancelled → Inventory releases or restocks, Payments voids.
Cart ◄─ OrderCreated ─ cart emptied.
```
**Consistency model:** there are no distributed transactions. Each service commits its own database change together with its
outbox row; delivery is at-least-once; consumers deduplicate with an inbox table (inbox row commits atomically with the
handler's changes) and idempotent business rules (e.g. one reservation per order, one payment per order, state-machine guards).
Late/duplicate/out-of-order events (e.g. payment authorised after the customer cancelled) are ignored or compensated
(payment voided, stock released). Failure recovery: the outbox dispatcher retries unpublished rows; consumers retry
3× with exponential backoff and then park the message in `<queue>.dead` for inspection/replay.

## Cross-cutting
* **Correlation id:** `X-Correlation-Id` is created at the gateway (or accepted if valid), forwarded over HTTP, stored in
  the outbox, copied into the message envelope and restored in consumers; all logs carry it as a scope.
* **Errors:** RFC 7807 `application/problem+json` everywhere (including 401/403/429) with `traceId` and `correlationId`; 5xx never leak details.
* **Auth:** JWT (HS256) validated by the gateway **and again by every service** (defence in depth); roles enforced server-side.
* **Observability:** `/health` (dependencies) and `/health/live` (process) on every service; JSON console logs; gateway `GET /api/admin/monitoring/services`.
* **Validation:** DataAnnotations on every request DTO + explicit domain checks; paging capped.
