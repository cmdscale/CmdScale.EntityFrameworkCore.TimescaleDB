# EF Core Database-First Example with TimescaleDB

This project demonstrates how to use the **Database-First** approach with [TimescaleDB](https://www.timescale.com/) using the `CmdScale.EntityFrameworkCore.TimescaleDB` package.

---

## 📦 Required NuGet Packages

Ensure the following package is installed in your project:

- `CmdScale.EntityFrameworkCore.TimescaleDB.Design`

---

## 🛠️ Scaffold DbContext and Models

Use the following command to scaffold the `DbContext` and entity classes from an existing TimescaleDB database:

```bash
dotnet ef dbcontext scaffold
  "Host=localhost;Database=cmdscale-ef-timescaledb;Username=timescale_admin;Password=R#!kro#GP43ra8Ae"
  CmdScale.EntityFrameworkCore.TimescaleDB.Design
  --output-dir Models
  --schema public
  --context-dir .
  --context MyTimescaleDbContext
  --project CmdScale.EntityFrameworkCore.TimescaleDB.Example.DataAccess.DbFirst
```

This command will:

- Generate entity models in the `Models/` directory
- Place the `MyTimescaleDbContext` in the current directory
- Use the specified connection string to connect to the TimescaleDB instance

> **Note**: When scaffolding your `DbContext`, use the `--schema` flag to target only your data schema (usually `public`). This prevents the tool from unintentionally including TimescaleDB's internal management tables from schemas like `_timescaledb_internal`.

---

## 📁 Project Structure

```text
CmdScale.EntityFrameworkCore.TimescaleDB.Example.DataAccess.DbFirst/
│
├── Models/                     # Auto-generated entity models
└── MyTimescaleDbContext.cs     # Auto-generated DbContext
```

---

## 🐳 Docker

- A `docker-compose.yml` file is available in the **Solution Items** to spin up a TimescaleDB container for local development:

  ```bash
  docker-compose up -d
  ```

- Connection string settings should match the configuration in your `docker-compose.yml`.

---

## 📚 Resources

- [Entity Framework Core Documentation](https://learn.microsoft.com/en-us/ef/core/)
- [TimescaleDB Documentation](https://docs.timescale.com/)
