# Integration events (contracts v1)

Defined in `contracts/Events/*.cs`, marked with `[EventContract(name, version)]`. Routing key = `<name>.v<version>`,
exchange `nexus.events` (topic, durable). Every message is an `EventEnvelope`:
`{ messageId, type, version, correlationId, occurredAtUtc, payload(json) }`, published persistent with publisher confirms.

| Routing key | Record | Producer | Consumers |
|---|---|---|---|
| `order.created.v1` | `OrderCreated(OrderId, UserId, UserEmail, Items[], Total, Currency, PaymentMethod)` | Orders | Inventory, Cart |
| `inventory.reserved.v1` | `InventoryReserved(OrderId, UserId, Total, Currency, PaymentMethod)` | Inventory | Orders, Payments |
| `inventory.reservation-failed.v1` | `InventoryReservationFailed(OrderId, Reason)` | Inventory | Orders |
| `payment.authorized.v1` | `PaymentAuthorized(OrderId, PaymentId, Amount, Currency, Provider)` | Payments | Orders |
| `payment.failed.v1` | `PaymentFailed(OrderId, Reason)` | Payments | Orders |
| `order.confirmed.v1` | `OrderConfirmed(OrderId, UserId, UserEmail, Total, Currency, Items[])` | Orders | Inventory, Notifications |
| `order.cancelled.v1` | `OrderCancelled(OrderId, UserId, UserEmail, Reason)` | Orders | Inventory, Payments, Notifications |

Queues (durable): `inventory.q`, `cart.q`, `orders.q`, `payments.q`, `notifications.q`, each with `<name>.dead`.
Versioning rule: never change a published record; add a new type with a new version and run both during migration.
`InventoryReserved` carries the amount/payment method so Payments does not need to query Orders (documented coupling).
