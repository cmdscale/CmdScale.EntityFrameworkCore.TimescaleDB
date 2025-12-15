# Benchmark Testing 🚀

This project uses **BenchmarkDotNet** to measure the performance of high-throughput data ingestion. The following steps will guide you through setting up the environment and running the benchmarks.

---

## Prerequisites

- .NET 10 SDK or later  
- Docker

## Run the Benchmarks

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
