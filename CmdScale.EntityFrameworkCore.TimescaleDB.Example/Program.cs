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

#if false
// --- Code to run Database.EnsureCreatedAsync() ---
// NOTE: Set the #if to false to disable this block or to true to enable it.
using (IServiceScope scope = host.Services.CreateScope())
{
    IServiceProvider services = scope.ServiceProvider;
    try
    {
        TimescaleContext context = services.GetRequiredService<TimescaleContext>();
        Console.WriteLine("Applying Database.EnsureCreatedAsync()...");
        await context.Database.EnsureCreatedAsync();
        Console.WriteLine("Database setup complete.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"An error occurred while creating the database: {ex.Message}");
    }
}
#endif

Console.WriteLine("TimescaleDB EF Core Demo");
Console.WriteLine("------------------------------------");
Console.WriteLine("Run 'dotnet ef migrations add <MigrationName>' to generate a migration.");
Console.WriteLine("Run 'dotnet ef database update' to apply migrations.");

Console.WriteLine("\nApplication is running. Press Ctrl+C to exit.");

await host.RunAsync();