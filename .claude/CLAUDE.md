# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

CmdScale.EntityFrameworkCore.TimescaleDB is an Entity Framework Core provider extension that integrates TimescaleDB features (hypertables, compression, continuous aggregates, reorder policies, etc.) into EF Core's migration and scaffolding system. The library extends Npgsql.EntityFrameworkCore.PostgreSQL.

**Detailed documentation:** See `.claude/reference/` for architecture, patterns, and file organization.

## Build and Test Commands

```bash
dotnet build                    # Build the solution
dotnet test                     # Run all tests (requires Docker for Testcontainers)
dotnet test --filter "FullyQualifiedName~TestName"  # Run single test
dotnet restore                  # Restore dependencies
```

### Test Coverage

Coverage reports are always generated under `tests/Eftdb.Tests/TestResults/`.

```bash
# Run tests with coverage
dotnet test tests/Eftdb.Tests --settings tests/Eftdb.Tests/coverlet.runsettings --collect:"XPlat Code Coverage"

# Generate HTML report (use -sourcedirs to resolve source locally instead of via Source Link)
reportgenerator -reports:"tests/Eftdb.Tests/TestResults/**/coverage.cobertura.xml" -targetdir:"tests/Eftdb.Tests/TestResults/CoverageReport" -reporttypes:Html -sourcedirs:"src/"
```

The HTML report will be at `tests/Eftdb.Tests/TestResults/CoverageReport/index.html`.

### Local Development

```bash
docker-compose up -d            # Start TimescaleDB container
docker-compose down -v          # Reset database (destructive)
```

### Testing Project/Package References

```powershell
Set-ExecutionPolicy -ExecutionPolicy Bypass -Scope Process
.\Scripts\Switch-References.ps1 -Mode Project   # For development
.\Scripts\Switch-References.ps1 -Mode Package   # To test as NuGet consumer
```

## Coding Standards

### C# Style Guidelines

**Type Declarations:** Use explicit types with `new()` target-typed initializer:
```csharp
StoreObjectIdentifier storeIdentifier = new();  // Correct
var storeIdentifier = new StoreObjectIdentifier();  // Incorrect
```

**Collection Expressions:** Use `[]` syntax and spread operator:
```csharp
List<string> items = ["item1", "item2"];  // Correct
List<string> allItems = [.. existingItems, .. newItems];  // Correct
```

**Async Programming:** Use `async/await` with `ConfigureAwait(false)` in library code.

**Primary Constructors:** Use for classes that only assign parameters to fields:
```csharp
private class TestContext(string connectionString) : DbContext { }
```

**Static Methods:** Make methods static when they don't depend on instance state.

**Comments:** XML docs on public members; neutral voice; no pronouns or enumerations. Do not overuse comments to explain "what" code does - prefer clear code. Use comments to explain "why" or complex logic. The code should be self-explaining in most cases.

### Architectural Principles

**DRY:** Centralize constants in `DefaultValues.cs`, use `SqlBuilderHelper`, share patterns via extractors.

**SoC:** Keep classes focused - extractors read metadata, differs compare models, generators produce SQL/C#.

## Key Patterns (Quick Reference)

| Pattern | Description | See |
|---------|-------------|-----|
| Service Registration | `UseTimescaleDb()` configures all services | `reference/patterns.md` |
| Convention System | `IEntityTypeAddedConvention` processes attributes | `reference/patterns.md` |
| Dual Configuration | Annotations + Fluent API → same annotations | `reference/patterns.md` |
| IFeatureDiffer | Per-feature differ with model extractor | `reference/patterns.md` |
| Runtime vs Design-Time | `isDesignTime` parameter changes quote escaping | `reference/patterns.md` |
| Column Name Resolution | Always use `StoreObjectIdentifier` + `GetColumnName()` | `reference/patterns.md` |

## Agent Workflow

```
New Feature → [1] eftdb-feature-initializer
            → [2] eftdb-feature-implementer
            → [3] eftdb-scaffold-support
            → [4] test-writer
            → [5] example-feature-generator
            → [6] git-committer (/prepare-commit)
```

| Agent | Purpose | Skill |
|-------|---------|-------|
| `eftdb-feature-initializer` | Creates Operations, FluentAPI, Attributes, Conventions | |
| `eftdb-feature-implementer` | Creates Differ, Extractor, Generator | |
| `eftdb-scaffold-support` | Creates Scaffolding Extractor, Applier (Design-time) | |
| `eftdb-bug-fixer` | Fixes bugs in runtime/design-time code | |
| `test-writer` | Creates unit and integration tests | |
| `test-coverage-planner` | Analyzes test coverage gaps | `/coverage-plan` |
| `example-feature-generator` | Creates usage examples | |
| `git-committer` | Formats, tests, stages, generates commit message | `/prepare-commit` |
| `code-detective` | Investigates bugs, traces history | |
| `pr-code-reviewer` | Reviews PR changes against patterns | `/review` |
| `eftdb-docs-writer` | Writes and updates documentation | |
| `changelog-generator` | Generates changelog entries for the documentation page | |

### Agent Boundaries

| Agent | Allowed | Forbidden |
|-------|---------|-----------|
| `eftdb-feature-initializer` | `src/Eftdb/` (Operations, Configuration) | Design, Tests, Samples |
| `eftdb-feature-implementer` | `src/Eftdb/` + `src/Eftdb.Design/` | Tests, Samples |
| `eftdb-scaffold-support` | `src/Eftdb.Design/` only | All others |
| `eftdb-bug-fixer` | `src/Eftdb/`, `src/Eftdb.Design/` | Tests (read-only), Samples |
| `test-writer` | `tests/` only | src/, Samples |
| `example-feature-generator` | `samples/` only | src/, Tests |

## Reference Documentation

- `.claude/reference/architecture.md` - Project structure, library organization
- `.claude/reference/patterns.md` - Key patterns with code examples
- `.claude/reference/file-organization.md` - File location quick reference
- `.claude/agents/` - Detailed agent prompts
