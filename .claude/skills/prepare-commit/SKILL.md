---
name: prepare-commit
description: Prepare changes for commit. Formats code, runs tests, and generates a commit message for review. Does not stage files.
user-invocable: true
---

Launch the `git-committer` agent (via the Task tool, subagent_type `git-committer`) and pass it the full context of the current changes. The complete workflow — format, test, reference-doc and README checks, commit message generation — is defined in that agent.

Non-negotiable rules (enforced by the agent, repeated here for the caller):

- **NEVER** run `git commit`, `git add`, or `git push` — the user stages and commits manually
- Skip inspecting files that likely contain secrets (`.env`, credentials)
- Follow the repository's commit style (`git log`); use conventional commits when the change is meaningful for the changelog (generate-changelog.yml picks them up), otherwise a plain descriptive message is fine
