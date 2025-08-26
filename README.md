# CmdScale.EntityFrameworkCore.TimescaleDB

This repository provides the essential libraries and tooling to seamlessly integrate [TimescaleDB](https://www.timescale.com/), the leading open-source time-series database, with Entity Framework Core. It is designed to give you the full power of TimescaleDB's features, like hypertables and compression, directly within the familiar EF Core environment.

- **CmdScale.EntityFrameworkCore.TimescaleDB**: The core runtime library. You include this in your project to enable TimescaleDB-specific features when configuring your `DbContext`.
- **CmdScale.EntityFrameworkCore.TimescaleDB.Design**: Provides crucial design-time extensions. This package enhances the EF Core CLI tools (`dotnet ef`) to understand TimescaleDB concepts, enabling correct schema generation for migrations and scaffolding.

---

## 📦 NuGet Packages
To get started, install the necessary packages from NuGet. For a typical project, you will need both.

| Package | Description |
|--------|-------------|
| `CmdScale.EntityFrameworkCore.TimescaleDB` | Runtime support for EF Core + TimescaleDB |
| `CmdScale.EntityFrameworkCore.TimescaleDB.Design` | Design-time support for EF Core tooling |

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
               .WithChunkTimeInterval("86400000");
    }
}
```

---

## 🏷️ Data Annotations Example
For simpler configurations, you can use the [Hypertable] attribute directly on your model class.

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

---

## 🐳 Docker Support

For convenient local development, a `docker-compose.yml` file is included in the **Solution Items**. This allows you to spin up a pre-configured TimescaleDB instance with a single command.

### Start TimescaleDB container
From the solution root, run:
```bash
docker-compose up -d
```

---

## 🧪 Scripts
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
.\SwitchToProjectReferences.ps1
```

Switch to **NuGet package references** (to simulate a real-world consumer):

```powershell
.\SwitchToPackageReferences.ps1
```

---

## 📦 Publish Local NuGet Package
To build and publish the core libraries to a local NuGet feed for testing, use the central publishing script. Note that this also done automatically by the `.\SwitchToPackageReferences.ps1` script.

```powershell
# Publish the design-time package
./Publish-Local.ps1 -ProjectName "CmdScale.EntityFrameworkCore.TimescaleDB.Design"

# Publish the runtime package
./Publish-Local.ps1 -ProjectName "CmdScale.EntityFrameworkCore.TimescaleDB"
```

By default, this script outputs the `.nupkg` files to:

```
C:\_Dev\NuGet-Packages
```

> To change this path, edit the `$LocalNuGetRepo` variable inside the `Publish-Local.ps1` script.

---

## 🔗 Add Local NuGet Source (Optional)

To use the locally published NuGet packages in other projects, you need to tell NuGet where to find them.

Add the local folder as a NuGet source using the .NET CLI:

```bash
dotnet nuget add source "C:\_Dev\NuGet-Packages" --name LocalCmdScale
```

Or, configure it in Visual Studio:

1. Go to `Tools` → `NuGet Package Manager` → `Package Manager Settings`.
2. Navigate to the `Package Sources` section.
3. Click the '+' icon to add a new source, give it a name (e.g., "LocalCmdScale"), and set the path to your local feed folder.

---

## 📚 Resources

- [TimescaleDB Documentation](https://docs.timescale.com/)
- [Entity Framework Core Documentation](https://learn.microsoft.com/en-us/ef/core/)
