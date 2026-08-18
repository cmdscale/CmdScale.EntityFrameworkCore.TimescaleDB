# CLAUDE.md

## Project Overview

CmdScale.EntityFrameworkCore.TimescaleDB is an EF Core provider extension integrating TimescaleDB features (hypertables, compression, continuous aggregates, policies) into EF Core's migration and scaffolding system. Extends Npgsql.EntityFrameworkCore.PostgreSQL.

Detailed reference: `.claude/reference/architecture.md` (structure, file-location formula, priority table, scaffolding pipeline) and `.claude/reference/patterns.md` (patterns with code examples).

## Build and Test

```bash
dotnet build
dotnet test                     # requires Docker (Testcontainers)
dotnet test --filter "FullyQualifiedName~TestName"

# Coverage (reports land in tests/Eftdb.Tests/TestResults/)
dotnet test tests/Eftdb.Tests --settings tests/Eftdb.Tests/coverlet.runsettings --collect:"XPlat Code Coverage"
reportgenerator -reports:"tests/Eftdb.Tests/TestResults/**/coverage.cobertura.xml" -targetdir:"tests/Eftdb.Tests/TestResults/CoverageReport" -reporttypes:Html -sourcedirs:"src/"

docker-compose up -d            # local TimescaleDB
docker-compose down -v          # reset database (destructive)
```

Switch between project and NuGet package references:

```powershell
.\Scripts\Switch-References.ps1 -Mode Project   # development
.\Scripts\Switch-References.ps1 -Mode Package   # test as NuGet consumer
```

## Coding Standards

- **Explicit types** with target-typed `new()`: `StoreObjectIdentifier id = new();` — never `var`
- **Collection expressions**: `List<string> items = ["a", "b"];` and spreads `[.. xs, .. ys]`
- **Async**: `async/await` with `ConfigureAwait(false)` in library code
- **Primary constructors** for classes that only assign parameters to fields
- **Static** methods when no instance state is used
- **Comments**: XML docs on public members, neutral voice, no pronouns. Explain "why", not "what" — code should be self-explaining. Never extract trivial 1–3 line guards into helpers.
- **DRY**: constants in `DefaultValues.cs`, SQL via `SqlBuilderHelper`, shared logic via the helpers listed in architecture.md
- **SoC**: extractors read metadata, differs compare models, generators produce SQL/C# — never mix

## Documentation Policy

Feature documentation lives in `docs/` (owned by `eftdb-docs-writer`). The root README is a deliberately compressed overview **without a feature list** — never add feature sections or usage examples to any README. Only correct a README when a change breaks instructions it already contains.

## Agents

New-feature flow: `eftdb-feature-initializer` → `eftdb-feature-implementer` → `eftdb-scaffold-support` → `test-writer` → `example-feature-generator` → `/prepare-commit`

| Agent | Purpose | Writes to | Skill |
|-------|---------|-----------|-------|
| `eftdb-feature-initializer` | Operations, fluent API, attributes, conventions | `src/Eftdb/` | |
| `eftdb-feature-implementer` | Differ, extractor, SQL + C# generators | `src/Eftdb/`, `src/Eftdb.Design/` | |
| `eftdb-scaffold-support` | Scaffolding extractor, applier, renderer | `src/Eftdb.Design/` only | |
| `eftdb-bug-fixer` | Bug fixes in runtime/design-time code | `src/` (tests read-only) | |
| `test-writer` | Unit and integration tests | `tests/` only | |
| `test-coverage-planner` | Coverage gap analysis (plan only) | read-only | `/coverage-plan` |
| `example-feature-generator` | Usage examples | `samples/` only | |
| `git-committer` | Format, test, commit message (never stages/commits) | — | `/prepare-commit` |
| `code-detective` | Bug investigation, history tracing | read-only | |
| `pr-code-reviewer` | PR review against patterns | read-only | `/review` |
| `eftdb-docs-writer` | Feature documentation | `docs/` only | |
