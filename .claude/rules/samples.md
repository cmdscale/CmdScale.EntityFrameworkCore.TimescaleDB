---
paths:
  - "samples/**"
---

# Sample Project Conventions

## Domain Models

Use real-world domain models that demonstrate practical TimescaleDB use cases:
- IoT sensor data, financial time-series, metrics, events
- Meaningful property names and types (not `Foo`/`Bar`)

## Configuration Approaches

Show both configuration styles for each feature:
- **Data Annotations:** Attributes on entity classes
- **Fluent API:** Configuration in `OnModelCreating` or `IEntityTypeConfiguration<T>`

## Entity Naming

- Entity classes use singular PascalCase (e.g., `SensorReading`, `StockTrade`)
- Table names use snake_case via `.ToTable("sensor_readings")`

## Project Structure

- `Eftdb.Samples.Shared` — Shared entities and contexts used by migration samples
- `Eftdb.Samples.DatabaseFirst` — Scaffolded (database-first) example
- Generated migrations in `Eftdb.Samples.Shared/Migrations/` are gitignored
- Scaffolded files in `Eftdb.Samples.DatabaseFirst/**/*.cs` are gitignored
