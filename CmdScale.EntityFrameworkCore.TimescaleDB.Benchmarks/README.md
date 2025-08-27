# Benchmark Testing 🚀

This project uses **BenchmarkDotNet** to measure the performance of high-throughput data ingestion. The following steps will guide you through setting up the environment and running the benchmarks.

---

## Prerequisites

- .NET 8 SDK or later  
- Docker and Docker Compose

---

## Step 1: Start a Clean Database

The benchmarks require a running TimescaleDB instance. A `docker-compose.yml` file is provided in the project root to simplify this process.

### 🔄 Stop and Reset (if needed)

To ensure you start with a clean slate, run this command to stop any running containers and permanently delete all existing data.

```bash
docker compose down -v
```

### ▶️ Start the Database

Launch a new TimescaleDB instance in the background.

```bash
docker compose up -d
```

### ✅ Verify Connection String

Ensure the connection string in `BulkCopyToAsyncBenchmarks.cs` matches the settings in your `docker-compose.yml` file (the default should work).

---

## Step 2: Apply Migrations

Next, create the necessary tables in the new database using EF Core migrations.

### ➕ Add a Migration

Create a new migration. Run this from the root of the solution.

```bash
dotnet ef migrations add <YourMigrationName> --project CmdScale.EntityFrameworkCore.TimescaleDB.Example.DataAccess --startup-project CmdScale.EntityFrameworkCore.TimescaleDB.Example
```

### ⬆️ Update the Database

Apply the migrations to create the hypertable schema.

```bash
dotnet ef database update --project CmdScale.EntityFrameworkCore.TimescaleDB.Example.DataAccess --startup-project CmdScale.EntityFrameworkCore.TimescaleDB.Example
```

---

## Step 3: Run the Benchmarks

Once the database is set up, you can run the performance tests.

### ⚙️ Set Build Configuration

Change your solution's build configuration to `Release` in order to run the tests.

### ▶️ Run the Project

You can run the benchmark project in one of two ways:

#### Via Visual Studio

Right-click on the `CmdScale.EntityFrameworkCore.TimescaleDB.Benchmarks` project and select:

```
Debug > Start Without Debugging
```

Or press `Ctrl + F5`.

#### Via Command Line

```bash
dotnet run --project CmdScale.EntityFrameworkCore.TimescaleDB.Benchmarks -c Release
```

---

## 📊 Benchmark Results

The benchmark will run through various combinations of worker counts and batch sizes, and the results will be saved in the `BenchmarkDotNet.Artifacts` folder.
