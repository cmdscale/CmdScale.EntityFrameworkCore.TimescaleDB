---
name: review
description: Review a pull request for coding standards, architectural patterns, and completeness.
user-invocable: true
argument-hint: <pr-number>
---

Review a pull request by delegating to the `pr-code-reviewer` agent.

## Delegation

Use the Task tool to launch the `pr-code-reviewer` agent (subagent_type).
Pass the full context of what needs to be done, including the PR number.

## Input

`$ARGUMENTS` is the PR number (e.g., `42`).

## Steps

1. Fetch PR details: `gh pr view $ARGUMENTS --repo cmdscale/CmdScale.EntityFrameworkCore.TimescaleDB`
2. Check for linked issues and fetch their context for better review
3. Get the full diff: `gh pr diff $ARGUMENTS`
4. Review all changes against:
   - Coding standards from CLAUDE.md (explicit types, collection expressions, async patterns)
   - Architectural patterns from `.claude/reference/patterns.md`
   - Agent boundaries (files modified match the expected scope)
   - Test coverage (new features should include tests)
5. Provide structured feedback with specific file:line references

## Rules

- **Read-only** — do not edit any files
- Review ALL commits in the PR, not just the latest
- Flag security concerns (SQL injection, missing input validation)
- Check for missing test coverage on new/changed code paths
