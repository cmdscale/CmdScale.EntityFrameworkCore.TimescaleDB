---
name: coverage-plan
description: Analyze test coverage, identify gaps, and create a testing strategy. Does NOT write tests.
user-invocable: true
---

Analyze test coverage and create a testing strategy by delegating to the `test-coverage-planner` agent.

## Delegation

Use the Task tool to launch the `test-coverage-planner` agent (subagent_type).
Pass the full context of what needs to be done.

## Steps

1. Identify all source files in `src/Eftdb/` and `src/Eftdb.Design/`
2. Map existing tests in `tests/Eftdb.Tests/` to the source files they cover
3. Identify untested code paths and missing scenarios
4. Produce a prioritized testing plan with:
   - Critical gaps (public API surface without tests)
   - Important gaps (complex logic paths)
   - Nice-to-have (edge cases, error paths)

## Output

A structured testing strategy document. This skill does **NOT** write tests — the output is a plan for the `test-writer` agent to implement.

## Rules

- **Read-only** — do not create or edit any files
- Focus on `CmdScale.EntityFrameworkCore.TimescaleDB` and `CmdScale.EntityFrameworkCore.TimescaleDB.Design` packages
- Consider both unit tests and integration tests (Testcontainers)
