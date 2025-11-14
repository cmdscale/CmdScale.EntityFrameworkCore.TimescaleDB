# Stryker.NET Mutation Testing

This directory contains the configuration for **Stryker.NET** mutation testing, which validates the quality and effectiveness of your test suite.

## What is Mutation Testing?

Mutation testing introduces small changes (mutations) to your source code and checks if your tests catch these changes. It answers the question: **"Do your tests actually catch bugs?"**

## Quick Start

### 1. Install Stryker.NET (if not already installed)

```bash
dotnet tool install -g dotnet-stryker
```

### 2. Run Mutation Testing

From the `CmdScale.EntityFrameworkCore.TimescaleDB.Tests` directory:

```bash
# Full mutation run (can take 30-60 minutes)
dotnet stryker

# Quick run for development (tests only changed files since last commit)
dotnet stryker --since

# Run on specific files only
dotnet stryker --mutate "**/HypertableDiffer.cs"
```

### 3. View Results

After completion, the HTML report will be generated in:
```
StrykerOutput/reports/mutation-report.html
```

Open this file in your browser to see detailed results.

## Configuration Explained

The `stryker-config.json` file is optimized for this project:

### Key Settings

- **Projects Mutated:**
  - `CmdScale.EntityFrameworkCore.TimescaleDB` (main library)
  - `CmdScale.EntityFrameworkCore.TimescaleDB.Design` (design-time library)

- **Test Project:**
  - `CmdScale.EntityFrameworkCore.TimescaleDB.Tests` ✅
  - **Excludes:** `CmdScale.EntityFrameworkCore.TimescaleDB.FunctionalTests` ❌

- **Concurrency:** 8 parallel processes (adjust based on CPU cores)

- **Thresholds:**
  - **High:** 80% (green) - Excellent mutation score
  - **Low:** 60% (orange) - Needs improvement
  - **Break:** 50% (red) - Build fails if below this

- **Coverage Analysis:** `perTest` (most accurate, slower)

- **Test Filter:** `Category!=Integration`
  - Skips slow integration tests for faster feedback
  - Remove this to include integration tests

### Files Excluded from Mutation

The following are automatically excluded:
- `*.Designer.cs` files
- `obj/` and `bin/` directories
- `Migrations/` directories
- `Properties/` directories

### Mutations Ignored

- **String mutations** - Often produce false positives
- **ToString/GetHashCode/Equals** - Usually not critical business logic

## Understanding Results

### Mutation Score

```
Mutation Score = (Killed Mutations / Total Mutations) × 100%
```

- **Killed** ✅ - Test caught the mutation (good!)
- **Survived** ❌ - Mutation not caught (test gap!)
- **Timeout** ⏱️ - Test took too long (possible infinite loop)
- **No Coverage** 🚫 - No test executed the mutated code

### Example Output

```
All files | 87.5% | 350/400 | 350 | 40 | 10 | 0
├── Differs | 92.3% | 120/130 | 120 | 8 | 2 | 0
├── Generators | 85.7% | 180/210 | 180 | 25 | 5 | 0
└── Extractors | 83.3% | 50/60 | 50 | 7 | 3 | 0
```

## Performance Tips

### Speed Up Mutation Testing

1. **Use `--since` flag:**
   ```bash
   dotnet stryker --since
   ```
   Only mutates files changed since last commit.

2. **Increase concurrency:**
   ```bash
   dotnet stryker --concurrency 16
   ```

3. **Use baseline (after first run):**
   ```json
   "baseline": {
     "enabled": true
   }
   ```

4. **Target specific files:**
   ```bash
   dotnet stryker --mutate "**/Differs/**"
   ```

5. **Skip integration tests:**
   Already configured via `test-case-filter: "Category!=Integration"`

## Interpreting Low Scores

If a file has a low mutation score (<60%), it usually means:

1. **Missing test coverage** - Add more tests
2. **Weak assertions** - Use specific assertions (not just `Assert.NotNull()`)
3. **Dead code** - Remove unused code
4. **Complex logic** - Simplify or add targeted tests

## Troubleshooting

### Issue: Tests are too slow

**Solution:** Increase `additional-timeout` in config:
```json
"additional-timeout": 20000
```

### Issue: Too many mutations to process

**Solution:** Use `--since` or target specific files:
```bash
dotnet stryker --since --mutate "**/Differs/**"
```

### Issue: False positives on string mutations

**Solution:** Already configured to ignore string mutations:
```json
"ignore-mutations": ["string"]
```

### Issue: Integration tests timing out

**Solution:** Already excluded via:
```json
"test-case-filter": "Category!=Integration"
```

## Best Practices

1. **Run regularly:** After major feature additions
2. **Use `--since`:** For day-to-day development
3. **Full run weekly:** As part of CI/CD pipeline
4. **Target 80%+:** Aim for high mutation score
5. **Review survivors:** Manually check survived mutations
6. **Keep baseline updated:** Re-run full analysis monthly

## Advanced Configuration

### Enable Dashboard Reporting

To upload results to Stryker Dashboard:

```json
"dashboard": {
  "api-key": "your-api-key",
  "project": "CmdScale.EntityFrameworkCore.TimescaleDB",
  "version": "main"
}
```

### Enable Baseline Mode

After first successful run:

```json
"baseline": {
  "enabled": true,
  "provider": "disk"
}
```

This speeds up subsequent runs by comparing against baseline.

## Resources

- [Stryker.NET Documentation](https://stryker-mutator.io/docs/stryker-net/introduction)
- [Configuration Options](https://stryker-mutator.io/docs/stryker-net/configuration)
- [Mutation Types](https://stryker-mutator.io/docs/mutation-testing-elements/supported-mutators)

## Questions?

Check the [Stryker.NET GitHub Issues](https://github.com/stryker-mutator/stryker-net/issues) or documentation.
