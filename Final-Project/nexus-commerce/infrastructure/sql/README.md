# SQL Server notes

Each service owns its own database on the shared development SQL Server instance:
`nexus_identity`, `nexus_catalog`, `nexus_inventory`, `nexus_cart`, `nexus_orders`, `nexus_payments`,
`nexus_notifications`, `nexus_reviews`, `nexus_assistant`.

Services create their database and schema automatically on first start (`EnsureCreated`, with retries) and seed
development data. No manual SQL is required. `create-databases.sql` is an optional helper for DBA-style setups.
