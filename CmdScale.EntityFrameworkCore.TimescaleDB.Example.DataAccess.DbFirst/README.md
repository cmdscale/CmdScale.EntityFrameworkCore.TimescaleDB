# Database First Approach Example
This project shows an example of how to work with a database first approach using the 
CmdScale.EntityFrameworkCore.TimescaleDB package. 

## Scaffold DbContext and Models
To scaffold the DbContext and Models for an existing Timescale database, you need to
have the following NuGet-Packages installed:
- `CmdScale.EntityFrameworkCore.TimescaleDB.Design`

Then you need to run the following command:

``dotnet ef dbcontext scaffold "Host=localhost;Database=cmdscale-ef-timescaledb;Username=timescale_admin;Password=R#!kro#GP43ra8Ae" CmdScale.EntityFrameworkCore.TimescaleDB.Design --output-dir Models --context-dir . --context MyTimescaleDbContext``