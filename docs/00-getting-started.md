---
slug: /
---

# Getting started

Follow these simple steps to integrate **TimescaleDB** with your Entity Framework Core application using the `CmdScale.EntityFrameworkCore.TimescaleDB` NuGet packages.

You may also want to checkout the eample projects in the [repository](https://github.com/cmdscale/CmdScale.EntityFrameworkCore.TimescaleDB).

---

## Getting Started Steps

### Step 1: Install NuGet Packages

**Description:** Add the required packages to your project. These packages provide the core runtime integration and design-time support for EF Core tooling and migrations.

**Code:**

```bash
dotnet add package CmdScale.EntityFrameworkCore.TimescaleDB
dotnet add package CmdScale.EntityFrameworkCore.TimescaleDB.Design
```

---

### Step 2: Configure DbContext

**Description:** Enable TimescaleDB support in your `DbContext` configuration with a single line of code by chaining the `.UseTimescaleDb()` method onto your PostgreSQL provider configuration.

**Code:**

```csharp
string? connectionString = builder.Configuration.GetConnectionString("Timescale");

builder.Services.AddDbContext<TimescaleContext>(options =>
    options.UseNpgsql(connectionString).UseTimescaleDb());
```

---

### Step 3: Define Your Models

**Description:** Use familiar EF Core patterns to define **hypertables** using [Data Annotations](/category/data-annotations/) or the [Fluent API](/category/fluent-api/). The example below uses the `[Hypertable]` and `[PrimaryKey]` attributes.

**Code:**

```csharp
[Hypertable(nameof(Time), ChunkSkipColumns = new[] { "Time" }, ChunkTimeInterval = "86400000")]
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
