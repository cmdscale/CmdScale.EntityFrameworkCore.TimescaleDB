# EF Core Code-First Example with TimescaleDB

This project demonstrates how to use the **Code-First** approach with [TimescaleDB](https://www.timescale.com/) using the `CmdScale.EntityFrameworkCore.TimescaleDB` package.

---

## Migrations and Database Management

Use the following commands to manage your EF Core migrations and database updates.

> **Note:** Run all commands from the repository root directory.

### Add a new migration

```bash
dotnet ef migrations add <MigrationName> --project samples/Eftdb.Samples.Shared --startup-project samples/Eftdb.Samples.CodeFirst
```

### Apply migrations to the database

```bash
dotnet ef database update --project samples/Eftdb.Samples.Shared --startup-project samples/Eftdb.Samples.CodeFirst
```

### Remove last migration (if not applied to the database yet)

```bash
dotnet ef migrations remove --project samples/Eftdb.Samples.Shared --startup-project samples/Eftdb.Samples.CodeFirst
```

### Reset all migrations (rollback to initial state)

```bash
dotnet ef database update 0 --project samples/Eftdb.Samples.Shared --startup-project samples/Eftdb.Samples.CodeFirst
```

---

## Project Structure

```text
samples/
├── Eftdb.Samples.CodeFirst/   # Startup project (contains Program.cs and design-time services)
├── Eftdb.Samples.Shared/      # Contains DbContext, entities, and migrations
└── Eftdb.Samples.DatabaseFirst/ # Database-first scaffolding example
```

---

## Docker

This project assumes you have an existing TimescaleDB-compatible PostgreSQL database. A `docker-compose.yml` file is included in the repository root to spin up a TimescaleDB container for local development and testing.

To start the container, run from the repository root:

```bash
docker-compose up -d
```

To stop and reset the database:

```bash
docker-compose down -v
```

Connection string settings should match the configuration in your `docker-compose.yml`.

---

## Notes

- Depending on if you're using project- or package-references you might need to (un)comment adding the services in `TimescaleDBDesignTimeService.cs`
- The `Eftdb.Samples.Shared` project contains the `TimescaleContext` and entity models shared between samples

## Resources

- [Entity Framework Core Documentation](https://learn.microsoft.com/en-us/ef/core/)
- [TimescaleDB Documentation](https://docs.timescale.com/)

