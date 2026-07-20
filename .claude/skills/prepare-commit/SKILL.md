---
name: prepare-commit
description: Prepare changes for commit. Formats code, runs tests, updates READMEs, and generates a commit message for review. Does not stage files.
user-invocable: true
---

Prepare the current working tree changes for commit by delegating to the `git-committer` agent.

## Delegation

Use the Task tool to launch the `git-committer` agent (subagent_type).
Pass the full context of what needs to be done.

## Steps

1. Run `dotnet format` on changed files
2. Run `dotnet build` to verify compilation
3. Run `dotnet test` to verify all tests pass
4. If files were added/removed/renamed in `src/`, update `.claude/reference/file-organization.md` and `.claude/reference/architecture.md`
5. Update relevant README.md files if features or APIs changed
6. Generate a conventional commit message based on the working tree changes

## Rules

- **NEVER** execute `git commit` — the user reviews and commits manually
- **NEVER** stage changes — do not run `git add` in any form; the user stages files themselves so the working tree stays easy to review
- **NEVER** push to remote
- Skip inspecting files that likely contain secrets (`.env`, credentials)
- Follow the repository's existing commit message style (check `git log`)
- Use conventional commits if you can infer the type (feat, fix, docs, etc.) from the changes and you think it would be helpful for the user to see that in the message. Note that conventional commits will be added to the release notes by the generate-changelog.yml workflow, so they should be used when the commit represents a meaningful change that should be highlighted in the changelog. However, if the changes are minor or don't fit well into a conventional commit type, it's better to write a clear, descriptive message without forcing a conventional format.
