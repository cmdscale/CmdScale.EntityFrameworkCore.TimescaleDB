# CmdScale.EntityFrameworkCore.TimescaleDB
This is the main runtime library for integrating TimescaleDB with Entity Framework Core.
It provides the core components needed to define and interact with TimescaleDB features in your application's `DbContext`.

# CmdScale.EntityFrameworkCore.TimescaleDB.Design
This package provides the essential design-time logic that enables the `dotnet ef` command-line tools to understand TimescaleDB.
It hooks into the migration and scaffolding processes to automate database-first and code-first workflows.

# Documentation

# Contributing

# Scripts

To run the scripts in this project, you may need to set the execution policy to bypass.
`Set-ExecutionPolicy -ExecutionPolicy Bypass -Scope Process`

Switch to project references (good for development):
`.\SwitchToProjectReferences.ps1`

Switch to package references (good for testing):
`.\SwitchToPackageReferences.ps1`

# Licencing