---
paths:
  - "tests/**"
---

# Testing Conventions

## Self-Contained Region Pattern

Each test uses a self-contained region with unique entities, contexts, and table names.
Test-specific data classes must be placed **above** their corresponding test method so reviewers can read top-down:

```csharp
#region Should_Detect_New_Hypertable

// Test-specific types — defined ABOVE the test
private class TestEntity { /* unique per test */ }
private class InitialContext(string conn) : DbContext { /* before state */ }
private class ModifiedContext(string conn) : DbContext { /* after state */ }

[Fact]
public async Task Should_Detect_New_Hypertable() { /* ... */ }

#endregion
```

## Naming

- **Table names:** Unique per test to avoid cross-test interference (e.g., `"detect_new_ht_entity"`)
- **Index names:** Unique per test when testing reorder policies
- **Test methods:** `Should_<ExpectedBehavior>` using PascalCase

## Context Pattern

- `InitialContext` — represents the "before" model state
- `ModifiedContext` — represents the "after" model state with changes

## Test Structure (AAA)

1. **Arrange:** Create initial migration, apply to database
2. **Act:** Diff `InitialContext` vs `ModifiedContext` to produce operations
3. **Assert:** Verify generated operations match expected changes

## Comments

Comments are for navigation, never explanation. If a test needs an explanatory comment, it is not a good test — make the test plainer instead.

- Allowed: XML doc `<summary>` on test classes, bare AAA markers (`// Arrange`, `// Act`, `// Assert`, bare combinations like `// Act & Assert`), thin `// ── Title ──` dividers, `#region`/`#endregion`
- Forbidden: explanatory text on or under AAA markers, comments in test-data setup (model classes, `OnModelCreating`), trailing explanations after code lines (including `// <-- Changed from <value>` delta markers), `//`-comment banners above test classes or methods
- Shared test infrastructure (base classes, fixtures) may carry a comment only for a genuine non-obvious constraint the code cannot express

## Integration Tests

- Use `Testcontainers` for real TimescaleDB instances (requires Docker)
- Each test class inherits from the appropriate base with `IAsyncLifetime`
- Tests are fully isolated — no shared state between tests
