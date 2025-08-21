using System;
using System.Collections.Generic;
using CmdScale.EntityFrameworkCore.TimescaleDB.Example.DataAccess.DbFirst.Models;
using Microsoft.EntityFrameworkCore;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Example.DataAccess.DbFirst;

public partial class MyTimescaleDbContext : DbContext
{
    public MyTimescaleDbContext()
    {
    }

    public MyTimescaleDbContext(DbContextOptions<MyTimescaleDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<DeviceReading> DeviceReadings { get; set; }

    public virtual DbSet<WeatherDatum> WeatherData { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseNpgsql("Host=localhost;Database=cmdscale-ef-timescaledb;Username=timescale_admin;Password=R#!kro#GP43ra8Ae");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("timescaledb");

        modelBuilder.Entity<DeviceReading>(entity =>
        {
            entity.HasKey(e => new { e.Id, e.Time });

            entity
                .HasAnnotation("TimescaleDB:IsHypertable", true)
                .HasAnnotation("TimescaleDB:TimeColumnName", "Time");

            entity.HasIndex(e => e.Time, "DeviceReadings_Time_idx").IsDescending();
        });

        modelBuilder.Entity<WeatherDatum>(entity =>
        {
            entity.HasKey(e => new { e.Id, e.Time });

            entity
                .HasAnnotation("TimescaleDB:IsHypertable", true)
                .HasAnnotation("TimescaleDB:TimeColumnName", "Time");

            entity.HasIndex(e => e.Time, "WeatherData_Time_idx").IsDescending();
        });
        modelBuilder.HasSequence("chunk_constraint_name", "_timescaledb_catalog");

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
