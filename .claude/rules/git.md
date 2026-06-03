---
description: Branching, commit, and versioning conventions
---

# Git & releases

## Commits — Conventional Commits
- Format: `type: subject`, where type is `feat | fix | chore | docs | refactor | test | ci | build`.
- A **scope is optional** — add one when it sharpens intent (`feat(navigation): ...`), omit it otherwise (`fix: ...`).
- Imperative mood, concise, no trailing period.

## Branches
- Work on `feature/<short-name>` branches off `main`; open a PR back to `main`.
- Releases happen on `release/v{major}.{minor}` branches.

## Versioning
- Versions are computed automatically by **Nerdbank.GitVersioning** from `version.json` and git height. `main` produces `…-dev.{height}`; a `release/v{x.y}` branch produces public releases.
- **Don't hand-edit version numbers** in manifests — the `SetNbgvVersionForUnoWindows` target injects them at build time.
