# EF Core Database-First Example with TimescaleDB

This project demonstrates how to use the **Database-First** approach with [TimescaleDB](https://www.timescale.com/) using the `CmdScale.EntityFrameworkCore.TimescaleDB` package.

For command usage see [dotnet ef tools](../../docs/01-dotnet-tools.md); for the normalization contract and known limitations see [Scaffolding Behavior and Limitations](../../docs/03-scaffolding.md).

---

## Quick Start

Install the design-time package, then run:

```bash
dotnet ef dbcontext scaffold "Host=localhost;Database=cmdscale-ef-timescaledb;Username=timescale_admin;Password=R#!kro#GP43ra8Ae" CmdScale.EntityFrameworkCore.TimescaleDB.Design --output-dir Models --context-dir . --context MyTimescaleDbContext --project samples/Eftdb.Samples.DatabaseFirst --schema public
```

Add `--data-annotations` to generate attributes instead of Fluent API calls.

> **Note**: Use `--schema public` to prevent the tool from including TimescaleDB's internal management schemas (`_timescaledb_internal`, etc.).

---

## Project Structure

```text
samples/Eftdb.Samples.DatabaseFirst/
|
+-- Models/                     # Auto-generated entity models
+-- MyTimescaleDbContext.cs     # Auto-generated DbContext
```

---

## Docker

A `docker-compose.yml` file is available at the repository root to spin up a TimescaleDB container for local development:

```bash
docker-compose up -d
```

Connection string settings should match the configuration in your `docker-compose.yml`.

---

## Resources

- [Scaffolding Behavior and Limitations](../../docs/03-scaffolding.md)
- [Entity Framework Core Documentation](https://learn.microsoft.com/en-us/ef/core/)
- [TimescaleDB Documentation](https://docs.timescale.com/)
