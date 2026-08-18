# CmdScale.EntityFrameworkCore.TimescaleDB

[![Test Workflow](https://github.com/cmdscale/CmdScale.EntityFrameworkCore.TimescaleDB/actions/workflows/run-tests.yml/badge.svg)](https://github.com/cmdscale/CmdScale.EntityFrameworkCore.TimescaleDB/actions/workflows/run-tests.yml)
[![NuGet downloads](https://img.shields.io/nuget/dt/CmdScale.EntityFrameworkCore.TimescaleDB?logo=nuget&label=Downloads)](https://www.nuget.org/packages/CmdScale.EntityFrameworkCore.TimescaleDB)
[![codecov](https://codecov.io/gh/cmdscale/CmdScale.EntityFrameworkCore.TimescaleDB/graph/badge.svg?token=YP3YCJLQ41)](https://codecov.io/gh/cmdscale/CmdScale.EntityFrameworkCore.TimescaleDB)
[![GitHub release (latest by date)](https://img.shields.io/github/v/tag/cmdscale/CmdScale.EntityFrameworkCore.TimescaleDB)](https://github.com/cmdscale/CmdScale.EntityFrameworkCore.TimescaleDB/tags)
[![GitHub issues](https://img.shields.io/github/issues/cmdscale/CmdScale.EntityFrameworkCore.TimescaleDB)](https://github.com/cmdscale/CmdScale.EntityFrameworkCore.TimescaleDB/issues)
[![GitHub license](https://img.shields.io/github/license/cmdscale/CmdScale.EntityFrameworkCore.TimescaleDB)](https://github.com/cmdscale/CmdScale.EntityFrameworkCore.TimescaleDB/blob/main/LICENSE)

`CmdScale.EntityFrameworkCore.TimescaleDB` (aka `Eftdb`) is an EntityFrameworkCore provider for [TimescaleDB](https://www.timescale.com/). It lets you interact with TimescaleDB in a type-safe way with rich IntelliSense support, so you don't have to write SQL in magic strings like you did with plain `Npgsql` - all without losing a single feature of `Npgsql`.

> [!TIP]
> Learn more about **Eftdb** in the [documentation](https://eftdb.cmdscale.com/docs/).

## 📦 Installation

For a typical project, install both packages:

```bash
dotnet add package CmdScale.EntityFrameworkCore.TimescaleDB
dotnet add package CmdScale.EntityFrameworkCore.TimescaleDB.Design
```

| Package                                           | Description                                                                      |
| ------------------------------------------------- | -------------------------------------------------------------------------------- |
| `CmdScale.EntityFrameworkCore.TimescaleDB`        | Runtime support for EF Core + TimescaleDB                                        |
| `CmdScale.EntityFrameworkCore.TimescaleDB.Design` | Design-time support for EF Core tooling (`dotnet ef` migrations and scaffolding) |

> [!TIP]
> You do **NOT** have to install `Npgsql.EntityFrameworkCore.PostgreSQL` — it is referenced transitively via `CmdScale.EntityFrameworkCore.TimescaleDB`.

## ⏩ Quick Start

### 1. Enable TimescaleDB

Chain `.UseTimescaleDb()` after `.UseNpgsql()` when configuring your DbContext. This registers all components that make EF Core aware of TimescaleDB's features.

```csharp
string? connectionString = builder.Configuration.GetConnectionString("Timescale");

builder.Services.AddDbContext<TimescaleContext>(options =>
    options.UseNpgsql(connectionString).UseTimescaleDb());
```

### 2. Define a Hypertable

You can either use the Fluent API or Data Annotations.

**Fluent API**

```csharp
public class WeatherData
{
    public Guid Id { get; set; }
    public DateTime Time { get; set; }
    public double Temperature { get; set; }
    public double Humidity { get; set; }
}

public class WeatherDataConfiguration : IEntityTypeConfiguration<WeatherData>
{
    public void Configure(EntityTypeBuilder<WeatherData> builder)
    {
        builder.HasKey(x => new { x.Id, x.Time });

        builder.IsHypertable(x => x.Time)
               .WithChunkTimeInterval("7 days");
    }
}
```

**Data Annotations**

```csharp
[Hypertable(nameof(Time), ChunkTimeInterval = "7 days")]
[PrimaryKey(nameof(Id), nameof(Time))]
public class WeatherData
{
    public Guid Id { get; set; }
    public DateTime Time { get; set; }
    public double Temperature { get; set; }
    public double Humidity { get; set; }
}
```

### 3. Create and Apply a Migration

With the Design package installed, you can generate the migration with the default `dotnet ef` tools, just like you're used to.

```bash
dotnet ef migrations add "AddWeatherData"
dotnet ef database update
```

## 🔖 Release strategy

Eftdb targets the latest .NET LTS release. Support follows a rolling two-version model:

| Support Level    | Scope                      |
| ---------------- | -------------------------- |
| **Current LTS**  | New features and bug fixes |
| **Previous LTS** | Critical bug fixes only    |

**Example:** When .NET 12 (LTS) releases, it becomes the development target. .NET 10 receives only critical fixes, and .NET 8 support ends.

This policy balances maintainability with ensuring the most widely-used .NET versions receive support.


## Questions or Ideas?

If you have questions, ideas, or need help getting started, feel free to [open an issue](https://github.com/cmdscale/CmdScale.EntityFrameworkCore.TimescaleDB/issues). We’re happy to help and discuss!

Thank you for contributing! 💜
