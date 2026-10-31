# Database setup, migrations and seed data

* One logical database per service on a shared development SQL Server instance (`nexus_<service>`); in production use separate servers/credentials.
* **Initialisation:** `DbExtensions.InitializeDatabaseAsync` runs `Database.EnsureCreated()` with retry (up to ~2 minutes) at startup, then seeds only when the tables are empty. This is repeatable and needs no manual steps.
* **Why no migrations are included:** the generation environment had no .NET SDK, and hand-written migration snapshots would be unverified guesses. `EnsureCreated` cannot evolve an existing schema.
* **Moving to migrations (recommended before any real deployment):**
  ```bash
  dotnet tool install --global dotnet-ef
  dotnet ef migrations add Initial --project services/catalog --context CatalogDb   # repeat per service/context
  ```
  then replace `EnsureCreated` in `DbExtensions` with `Database.MigrateAsync()`. Add `Microsoft.EntityFrameworkCore.Design` to each service.
* **Seed data:** `services/catalog/Data/seed-products.json` (8 categories, 32 products) is the single source; `scripts/generate_seed_assets.py` regenerates it and the SVG placeholders. Deterministic ids (`Contracts/SeedIds.cs`) keep catalog, inventory and review seeds consistent across independent databases.
* **Reset everything:** `docker compose down -v`.
