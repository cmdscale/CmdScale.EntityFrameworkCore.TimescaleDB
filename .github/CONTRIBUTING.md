# Contributing

We welcome contributions to help improve this package and make it even more powerful for the .NET and TimescaleDB communities!

Whether you're fixing bugs, adding new features, improving documentation, or sharing examples — every bit helps. 🙌

> [!NOTE]
> While AI tools like Copilot or Claude are permitted, vibe-coded submissions will be rejected. All code must be manually verified and subject to the standard code review process.


## How to Contribute

1. **Fork the Repository**

   Create a personal fork of the repository on GitHub and clone it to your local machine.

2. **Create a Branch**

   Use a descriptive branch name prefixed with the type of change you're working on (`feature/`, `fix/`, `docs/`, ...):

   ```bash
   git checkout -b feature/improve-bulk-copy
   git checkout -b fix/bulk-copy-complex-type-bug
   ```

3. **Make Your Changes**
   - Follow the existing code style and patterns.
   - Write meaningful tests for any new logic. Check out the [Wiki](https://github.com/cmdscale/CmdScale.EntityFrameworkCore.TimescaleDB/wiki) to gain knowledge about writing tests.

4. **Run Tests**

   Make sure all tests pass before submitting a pull request:

   ```bash
   dotnet test
   ```

5. **Submit a Pull Request**

   Push your branch and open a pull request (PR) and include a clear description of what you changed and why.

### Commit Messages

Commit messages follow [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/). This is not just style: the release notes are generated automatically from commit prefixes, so your commit message ends up in the changelog.

| Prefix                                                     | Changelog section   |
| ---------------------------------------------------------- | ------------------- |
| `feat:`                                                    | ✨ New Features      |
| `fix:`                                                     | 🐛 Fixes            |
| `docs:`, `refactor:`, `perf:`, `test:`, `chore:`, `style:` | 🔧 Miscellaneous    |

Since the message is user-facing, describe the value for library users, not the implementation:

```
feat: add complex type support for all column-referencing APIs   ✔
fix: resolve review comments from PR #30                         ✘
```

### Guidelines

- Keep pull requests focused and minimal
- Reference any related issues using keywords (e.g. `Fixes #42`)
- Be respectful in code reviews and discussions
- Use [BenchmarkDotNet](https://benchmarkdotnet.org/) where performance-related changes are involved
- Feature PRs should include documentation in `docs/`

### AI Assistants

Contributors are allowed to use AI assistants such as Claude Code, GitHub Copilot, or similar tools. However, AI-generated code must not be submitted blindly. Contributors are responsible for every line of code in their pull requests. Code quality is very important and AI-assisted contributions are held to the same standard as any other.

Before submitting AI-assisted contributions, make sure to:

- **Review all generated code** for correctness, readability, and security.
- **Verify that tests pass** and add new tests where appropriate and effective.
- **Follow the project's coding style and conventions** — don't let your AI assistant overuse comments; code should be self-explanatory, and comments should explain _why_, not _what_.

This repository ships with a [Claude Code](https://claude.ai/code) setup in the `.claude/` directory, including specialized agents, coding rules, reusable skills, and architecture reference docs. Personal settings go in `.claude/settings.local.json` (gitignored).


## Tips for local development

This section informs you about Docker, testing, available scripts and some other things that might be useful for local development. 

### 🐳 Docker

For convenient local development, a `docker-compose.yml` file is included in the root directory. This allows you to spin up a pre-configured TimescaleDB instance with a single command.

Also, some tests use `Testcontainers` and need you to have Docker installed. Just keep that in mind.

### 🧪 Testing

This project uses a two-tier testing strategy to ensure code quality and correctness.

> Checkout the test coverage on [Codecov](https://app.codecov.io/gh/cmdscale/CmdScale.EntityFrameworkCore.TimescaleDB)

#### Test Projects

| Project                                                    | Purpose                                                                                                                                                          |
| ---------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `CmdScale.EntityFrameworkCore.TimescaleDB.Tests`           | Unit tests using xUnit and Moq. Fast, isolated tests for differs, extractors, generators, and conventions. Also includes integration tests using Testcontainers. |
| `CmdScale.EntityFrameworkCore.TimescaleDB.FunctionalTests` | EF Core specification tests validating end-to-end behavior against a real TimescaleDB instance.                                                                  |

#### Running Tests

```bash
# Run all tests
dotnet test

# Run a specific test by name
dotnet test --filter "FullyQualifiedName~HypertableDifferTests"
```

#### Test Coverage

Generate an HTML coverage report using [ReportGenerator](https://github.com/danielpalme/ReportGenerator):

```bash
# Install ReportGenerator (once)
dotnet tool install -g dotnet-reportgenerator-globaltool

# Run tests with coverage collection
dotnet test tests/Eftdb.Tests --settings tests/Eftdb.Tests/coverlet.runsettings --collect:"XPlat Code Coverage"

# Generate HTML report from coverage files
reportgenerator -reports:"tests/Eftdb.Tests/TestResults/**/coverage.cobertura.xml" -targetdir:"tests/Eftdb.Tests/TestResults/CoverageReport" -reporttypes:Html
```

The HTML report will be generated at `tests/Eftdb.Tests/TestResults/CoverageReport/index.html`.

#### Mutation Testing

Use [Stryker.NET](https://stryker-mutator.io/docs/stryker-net/introduction) to validate test effectiveness by introducing mutations and checking if tests catch them:

```bash
# Install Stryker (once)
dotnet tool install -g dotnet-stryker

# Run from the test directory
cd tests/Eftdb.Tests
dotnet stryker

# Quick run (test only changed files)
dotnet stryker --since
```

Results are generated in `StrykerOutput/reports/mutation-report.html`. See `STRYKER_README.md` in the `CmdScale.EntityFrameworkCore.TimescaleDB.Tests` project for detailed configuration.

### 🛠️ Scripts

The folder `./Scripts` includes scripts to streamline the development workflow, particularly for switching between local project development and package-based testing.

#### Allow PowerShell Scripts to Run

To run these scripts, you may first need to change the execution policy for the current process:

```powershell
Set-ExecutionPolicy -ExecutionPolicy Bypass -Scope Process
```

#### Switch Project/Package References

This script modifies your `.csproj` files to switch between referencing the core libraries as direct projects or as local NuGet packages.

Switch to **project references** (ideal for active development):

```powershell
.\Scripts\Switch-References.ps1 -Mode Project
```

Switch to **NuGet package references** (to simulate a real-world consumer):

```powershell
.\Scripts\Switch-References.ps1 -Mode Package
```

### 📦 Publish Local NuGet Package

To build and publish the core libraries to a local NuGet feed for testing, use the central publishing script. Note that this is also done automatically by `.\Scripts\Switch-References.ps1 -Mode Package`.

```powershell
# Publish the design-time package
.\Scripts\Publish-Local.ps1 -ProjectName "Eftdb.Design"

# Publish the runtime package
.\Scripts\Publish-Local.ps1 -ProjectName "Eftdb"
```

> To change this path, edit the `$LocalNuGetRepo` variable inside the `Publish-Local.ps1` script.

#### 🔗 Add Local NuGet Source (Optional)

To use the locally published NuGet packages in other projects, you need to tell NuGet where to find them.

Add the local feed folder (the `$LocalNuGetRepo` path configured in `Publish-Local.ps1`) as a NuGet source using the .NET CLI:

```bash
dotnet nuget add source "C:\path\to\NuGet-Packages" --name LocalCmdScale
```

Or, configure it in Visual Studio:

1. Go to `Tools` → `NuGet Package Manager` → `Package Manager Settings`.
2. Navigate to the `Package Sources` section.
3. Click the '+' icon to add a new source, give it a name (e.g., "LocalCmdScale"), and set the path to your local feed folder.