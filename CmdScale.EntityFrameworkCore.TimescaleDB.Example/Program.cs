using CmdScale.EntityFrameworkCore.TimescaleDB;
using CmdScale.EntityFrameworkCore.TimescaleDB.Example.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

string? connectionString = builder.Configuration.GetConnectionString("Timescale");
builder.Services.AddDbContext<TimescaleContext>(options =>
    options.UseNpgsql(connectionString).UseTimescaleDb());

IHost host = builder.Build();

Console.WriteLine("TimescaleDB EF Core Demo");
Console.WriteLine("------------------------------------");
Console.WriteLine("Run 'dotnet ef migrations add <MigrationName>' to generate a migration.");
Console.WriteLine("Run 'dotnet ef database update' to apply migrations.");

Console.WriteLine("\nApplication is running. Press Ctrl+C to exit.");

await host.RunAsync();