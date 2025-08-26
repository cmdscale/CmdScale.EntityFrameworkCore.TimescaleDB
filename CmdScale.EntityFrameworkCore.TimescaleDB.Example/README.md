# EF Core Code-First Example with TimescaleDB

This project demonstrates how to use the **Code-First** approach with [TimescaleDB](https://www.timescale.com/) using the `CmdScale.EntityFrameworkCore.TimescaleDB` package.

---

## 🚀 Migrations and Database Management

Use the following commands to manage your EF Core migrations and database updates.

### 📌 Add a New Migration

```bash
dotnet ef migrations add --project CmdScale.EntityFrameworkCore.TimescaleDB.Example.DataAccess --startup-project CmdScale.EntityFrameworkCore.TimescaleDB.Example <MigrationName>
```

### ✅ Apply Migrations to the Database

```bash
dotnet ef database update --project CmdScale.EntityFrameworkCore.TimescaleDB.Example.DataAccess --startup-project CmdScale.EntityFrameworkCore.TimescaleDB.Example
```

### 🧹 Reset All Migrations (Rollback to Initial State)

```bash
dotnet ef database update 0 --project CmdScale.EntityFrameworkCore.TimescaleDB.Example.DataAccess --startup-project CmdScale.EntityFrameworkCore.TimescaleDB.Example
```

---

## 📁 Project Structure

```text
CmdScale.EntityFrameworkCore.TimescaleDB.Example/
│
├── CmdScale.EntityFrameworkCore.TimescaleDB.Example/            # Startup project
├── CmdScale.EntityFrameworkCore.TimescaleDB.Example.DataAccess/ # Contains DbContext and migrations
└── docker-compose.yml                                           # Sets up TimescaleDB container (in Solution Items)
```

---

## 🐳 Docker
- This project assumes you have an existing TimescaleDB-compatible PostgreSQL database.
- A `docker-compose.yml` file is included in the **Solution Items** folder to spin up a TimescaleDB container for local development and testing.

  To start the container, run:

  ```bash
  docker-compose up -d
  ```

- Connection string settings should match the configuration in your `docker-compose.yml`.

---

## 🧠 Notes
- Depending on if you're using project- or package-references you might to (un)comment adding the services in `TimescaleDBDesignTimeService.cs`


## 📚 Resources

- [Entity Framework Core Documentation](https://learn.microsoft.com/en-us/ef/core/)
- [TimescaleDB Documentation](https://docs.timescale.com/)

