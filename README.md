# CmdScale.EntityFrameworkCore.TimescaleDB

![CmdScale Project](https://github.com/cmdscale/.github/raw/main/profile/assets/CmdShield.svg)
[![Test Workflow](https://github.com/cmdscale/CmdScale.EntityFrameworkCore.TimescaleDB/actions/workflows/run-tests.yml/badge.svg)](https://github.com/cmdscale/CmdScale.EntityFrameworkCore.TimescaleDB/actions/workflows/run-tests.yml)
[![NuGet downloads](https://img.shields.io/nuget/dt/CmdScale.EntityFrameworkCore.TimescaleDB?logo=nuget&label=Downloads)](https://www.nuget.org/packages/CmdScale.EntityFrameworkCore.TimescaleDB)
[![codecov](https://codecov.io/gh/cmdscale/CmdScale.EntityFrameworkCore.TimescaleDB/graph/badge.svg?token=YP3YCJLQ41)](https://codecov.io/gh/cmdscale/CmdScale.EntityFrameworkCore.TimescaleDB)
[![GitHub release (latest by date)](https://img.shields.io/github/v/tag/cmdscale/CmdScale.EntityFrameworkCore.TimescaleDB)](https://github.com/cmdscale/CmdScale.EntityFrameworkCore.TimescaleDB/tags)
[![GitHub issues](https://img.shields.io/github/issues/cmdscale/CmdScale.EntityFrameworkCore.TimescaleDB)](https://github.com/cmdscale/CmdScale.EntityFrameworkCore.TimescaleDB/issues)
[![GitHub license](https://img.shields.io/github/license/cmdscale/CmdScale.EntityFrameworkCore.TimescaleDB)](https://github.com/cmdscale/CmdScale.EntityFrameworkCore.TimescaleDB/blob/main/LICENSE)
![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg)

This repository provides the essential libraries and tooling to seamlessly integrate [TimescaleDB](https://www.timescale.com/), the leading open-source time-series database, with Entity Framework Core. It is designed to give you the full power of TimescaleDB's features, like hypertables and compression, directly within the familiar EF Core environment.

- **CmdScale.EntityFrameworkCore.TimescaleDB**: The core runtime library. You include this in your project to enable TimescaleDB-specific features when configuring your `DbContext`.
- **CmdScale.EntityFrameworkCore.TimescaleDB.Design**: Provides crucial design-time extensions. This package enhances the EF Core CLI tools (`dotnet ef`) to understand TimescaleDB concepts, enabling correct schema generation for migrations and scaffolding.

> [!Tip]
> Learn more about **Eftdb** in the [documentation](https://eftdb.cmdscale.com/docs/).

---

## ✨ Features

This package extends **Entity Framework Core** with powerful, first-class support for **TimescaleDB's** core features, allowing you to build high-performance time-series applications in .NET.

### Hypertable Creation and Configuration

Seamlessly define and manage **TimescaleDB hypertables** using standard EF Core conventions, including both data attributes and a rich **Fluent API**. This allows you to control partitioning and other optimizations directly from your `DbContext`.

- **Time Partitioning**: Easily specify the primary time column and set the `chunk_time_interval`.
- **Space Partitioning**: Add additional dimensions for hash or range partitioning to further optimize queries.
- **Chunk Time Interval**: Configure chunk intervals to balance performance and storage efficiency.
- **Data Migration**: Control whether existing data should be migrated when converting a regular table to a hypertable using `migrate_data`.
- **Chunk Skipping**: Enable chunk skipping to improve query performance on specific columns.
- **Compression Segment By**: Define columns to group compressed data by, allowing efficient access to specific segments without decompressing entire chunks.
- **Compression Order By**: Specify the sort order within compressed segments, with support for ascending/descending direction and NULLS FIRST/LAST positioning.

### Reorder Policies

Take full control over how your hypertable data is organized on disk with **TimescaleDB's** reorder policies. By defining a reorder policy, you can automatically re-sort chunks of data by a specified index, significantly improving the performance of queries that scan large time ranges or specific index values.

### Continuous Aggregates

Create and manage **TimescaleDB continuous aggregates** — automatically refreshed materialized views that pre-compute aggregate data for faster queries. Define time-bucketed aggregations using a type-safe Fluent API or Data Annotations.

- **Time Bucketing**: Automatically group data into time intervals (e.g., `1 hour`, `1 day`).
- **Aggregate Functions**: Support for `Avg`, `Sum`, `Min`, `Max`, `Count`, `First`, and `Last`.
- **Group By Columns**: Add additional grouping dimensions beyond time.
- **Filtering**: Apply WHERE clauses to filter source data.
- **Refresh Policies**: Configure automatic refresh with customizable time windows, schedule intervals, and batching options.

---

## 📦 NuGet Packages

To get started, install the necessary packages from NuGet. For a typical project, you will need both.

| Package                                           | Description                               |
| ------------------------------------------------- | ----------------------------------------- |
| `CmdScale.EntityFrameworkCore.TimescaleDB`        | Runtime support for EF Core + TimescaleDB |
| `CmdScale.EntityFrameworkCore.TimescaleDB.Design` | Design-time support for EF Core tooling   |

---

## 🧰 Setup

To enable TimescaleDB in your project, chain the `.UseTimescaleDb()` method after `.UseNpgsql()` when configuring your DbContext. This call registers all the necessary components to make EF Core aware of TimescaleDB's unique features.
Note that you do **NOT** have to install `Npgsql.EntityFrameworkCore.PostgreSQL` as it is referenced transitively via `CmdScale.EntityFrameworkCore.TimescaleDB`.

In `Program.cs` or your dependency injection container:

```csharp
string? connectionString = builder.Configuration.GetConnectionString("Timescale");

builder.Services.AddDbContext<TimescaleContext>(options =>
    options.UseNpgsql(connectionString).UseTimescaleDb());
```

---

## 🔧 Fluent API Example

The Fluent API provides a powerful, type-safe way to configure your entities. Use the `.IsHypertable()` extension method on an entity builder to designate it as a hypertable and configure its properties.

### Model

A standard POCO class representing our time-series data.

```csharp
public class WeatherData
{
    public Guid Id { get; set; }
    public DateTime Time { get; set; }
    public double Temperature { get; set; }
    public double Humidity { get; set; }
}
```

### Configuration

In a separate configuration class, you can define the hypertable settings.

```csharp
public class WeatherDataConfiguration : IEntityTypeConfiguration<WeatherData>
{
    public void Configure(EntityTypeBuilder<WeatherData> builder)
    {
        // Define a composite primary key, common for time-series data.
        builder.HasKey(x => new { x.Id, x.Time });

        // Convert the table to a hypertable partitioned on the 'Time' column.
        builder.IsHypertable(x => x.Time)
               // Optional: Enable chunk skipping for faster queries on this column.
               .WithChunkSkipping(x => x.Time)
               // Optional: Set the chunk interval. Can be a string ("7 days") or long (microseconds).
               .WithChunkTimeInterval("86400000")
               // Optional: Migrate existing data when converting to hypertable (defaults to false).
               .WithMigrateData(true);
    }
}
```

---

## 🏷️ Data Annotations Example

For simpler configurations, you can use the [Hypertable] attribute directly on your model class.

```csharp
[Hypertable(nameof(Time),
    ChunkSkipColumns = new[] { "Time" },
    ChunkTimeInterval = "86400000",
    MigrateData = true)]
[PrimaryKey(nameof(Id), nameof(Time))]
public class DeviceReading
{
    public Guid Id { get; set; }
    public DateTime Time { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public double Voltage { get; set; }
    public double Power { get; set; }
}
```

---

## 🐳 Docker Support

For convenient local development, a `docker-compose.yml` file is included in the **Solution Items**. This allows you to spin up a pre-configured TimescaleDB instance with a single command.

### Start TimescaleDB container

From the solution root, run:

```bash
docker-compose up -d
```

### Resetting the Database Environment

If you need to start with a completely fresh, empty database, you can stop the running container and permanently delete all of its data.

> **Warning**: This command is destructive and will erase all tables and data stored in your local TimescaleDB instance.

```bash
docker-compose down -v
```

---

## 🧪 Testing

This project uses a two-tier testing strategy to ensure code quality and correctness.

> Checkout the test coverage on [Codecov](https://app.codecov.io/gh/cmdscale/CmdScale.EntityFrameworkCore.TimescaleDB)

### Test Projects

| Project                                                    | Purpose                                                                                                                                                          |
| ---------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `CmdScale.EntityFrameworkCore.TimescaleDB.Tests`           | Unit tests using xUnit and Moq. Fast, isolated tests for differs, extractors, generators, and conventions. Also includes integration tests using Testcontainers. |
| `CmdScale.EntityFrameworkCore.TimescaleDB.FunctionalTests` | EF Core specification tests validating end-to-end behavior against a real TimescaleDB instance.                                                                  |

### Running Tests

```bash
# Run all tests
dotnet test

# Run a specific test by name
dotnet test --filter "FullyQualifiedName~HypertableDifferTests"
```

### Test Coverage

Generate an HTML coverage report using [ReportGenerator](https://github.com/danielpalme/ReportGenerator):

```bash
# Install ReportGenerator (once)
dotnet tool install -g dotnet-reportgenerator-globaltool

# Run tests with coverage collection
dotnet test tests/Eftdb.Tests --settings tests/Eftdb.Tests/coverlet.runsettings --collect:"XPlat Code Coverage"

# Generate HTML report from coverage files
reportgenerator -reports:"tests/Eftdb.Tests/TestResults/**/coverage.cobertura.xml" -targetdir:"tests/Eftdb.Tests/TestResults/CoverageReport" -reporttypes:Html
```

The HTML report will be generated at `tests/Eftdb.Tests/TestResults/CoverageReport/index.html`.

### Mutation Testing

Use [Stryker.NET](https://stryker-mutator.io/docs/stryker-net/introduction) to validate test effectiveness by introducing mutations and checking if tests catch them:

```bash
# Install Stryker (once)
dotnet tool install -g dotnet-stryker

# Run from the test directory
cd tests/Eftdb.Tests
dotnet stryker

# Quick run (test only changed files)
dotnet stryker --since
```

Results are generated in `StrykerOutput/reports/mutation-report.html`. See `STRYKER_README.md` in the `CmdScale.EntityFrameworkCore.TimescaleDB.Tests` project for detailed configuration.

---

## 🛠️ Scripts

This repository includes PowerShell scripts to streamline the development workflow, particularly for switching between local project development and package-based testing.

### Allow PowerShell Scripts to Run

To run these scripts, you may first need to change the execution policy for the current process:

```powershell
Set-ExecutionPolicy -ExecutionPolicy Bypass -Scope Process
```

### Switch Project/Package References

These script modify your `.csproj` files to switch between referencing the core libraries as direct project or as local NuGet packages.

Switch to **project references** (ideal for active development):

```powershell
.\scripts\Switch-References.ps1 -Mode Project
```

Switch to **NuGet package references** (to simulate a real-world consumer):

```powershell
.\scripts\Switch-References.ps1 -Mode Package
```

---

## 📦 Publish Local NuGet Package

To build and publish the core libraries to a local NuGet feed for testing, use the central publishing script. Note that this also done automatically by the `.\SwitchToPackageReferences.ps1` script.

```powershell
# Publish the design-time package
.\scripts\Publish-Local.ps1 -ProjectName "Eftdb.Design"

# Publish the runtime package
.\scripts\Publish-Local.ps1 -ProjectName "Eftdb"
```

> To change this path, edit the `$LocalNuGetRepo` variable inside the `Publish-Local.ps1` script.

---

## 🔗 Add Local NuGet Source (Optional)

To use the locally published NuGet packages in other projects, you need to tell NuGet where to find them.

Add the local folder as a NuGet source using the .NET CLI:

```bash
dotnet nuget add source "path\NuGet-Packages" --name LocalCmdScale
```

Or, configure it in Visual Studio:

1. Go to `Tools` → `NuGet Package Manager` → `Package Manager Settings`.
2. Navigate to the `Package Sources` section.
3. Click the '+' icon to add a new source, give it a name (e.g., "LocalCmdScale"), and set the path to your local feed folder.

## 🔖 Release strategy

Eftdb targets the latest .NET LTS release. Support follows a rolling two-version model:

| Support Level    | Scope                      |
| ---------------- | -------------------------- |
| **Current LTS**  | New features and bug fixes |
| **Previous LTS** | Critical bug fixes only    |

**Example:** When .NET 12 (LTS) releases, it becomes the development target. .NET 10 receives only critical fixes, and .NET 8 support ends.

This policy balances maintainability with ensuring the most widely-used .NET versions receive support.

## 📚 Resources

- [TimescaleDB Documentation](https://docs.timescale.com/)
- [Entity Framework Core Documentation](https://learn.microsoft.com/en-us/ef/core/)

---

## Contributing 🤝

We welcome contributions to help improve this package and make it even more powerful for the .NET and TimescaleDB communities!

Whether you're fixing bugs, adding new features, improving documentation, or sharing examples — every bit helps. 🙌

### How to Contribute

1. **Fork the Repository**

   Create a personal fork of the repository on GitHub and clone it to your local machine.

2. **Create a Branch**

   Use a descriptive branch name based on the feature or fix you're working on using [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/):

   ```bash
   git checkout -b feature/improve-bulk-copy
   git checkout -b fix/bulk-copy-complex-type-bug
   ```

3. **Make Your Changes**

   - Follow the existing code style and patterns.
   - Write meaningful tests for any new logic. Check out the [Wiki](https://github.com/cmdscale/CmdScale.EntityFrameworkCore.TimescaleDB/wiki) to gain knowledge about writing tests.

4. **Run Tests**

   Make sure all tests pass before submitting a pull request:

   ```bash
   dotnet test
   ```

5. **Submit a Pull Request**

   Push your branch and open a pull request (PR) and include a clear description of what you changed and why.

### Guidelines

- Keep pull requests focused and minimal.
- Reference any related issues using keywords (e.g. `Fixes #42`).
- Be respectful in code reviews and discussions.
- Use [BenchmarkDotNet](https://benchmarkdotnet.org/) where performance-related changes are involved.

### AI Assistants

Contributors are welcome to use AI assistants such as Claude Code, GitHub Copilot, or similar tools. However, AI-generated code must not be submitted blindly. Contributors are responsible for every line of code in their pull requests. Code quality is very important and AI-assisted contributions are held to the same standard as any other.

Before submitting AI-assisted contributions, make sure to:

- **Review all generated code** for correctness, readability, and security.
- **Verify that tests pass** and add new tests where appropriate and effective.
- **Follow the project's coding style and conventions** — don't let your AI assistant overuse comments; code should be self-explanatory, and comments should explain *why*, not *what*.

This repository ships with a [Claude Code](https://claude.ai/code) setup in the `.claude/` directory, including specialized agents, coding rules, reusable skills, and architecture reference docs. Personal settings go in `.claude/settings.local.json` (gitignored).


### Questions or Ideas?

If you have questions, ideas, or need help getting started, feel free to [open an issue](https://github.com/cmdscale/CmdScale.EntityFrameworkCore.TimescaleDB/issues). We’re happy to help and discuss!

Thank you for contributing! 💜

---

## 📄 License

```txt
MIT License
Copyright (c) 2025 CmdScale GmbH

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```
