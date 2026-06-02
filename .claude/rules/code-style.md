---
description: C# language, naming, and dependency conventions for this Uno app
---

# Code style

These mirror `.editorconfig` and `src/Directory.Build.props` — they are the *why* behind the machine-checked rules.

## Language & compiler
- `LangVersion=preview`, `Nullable=enable`, `ImplicitUsings=enable`. Use modern C# (collection expressions, pattern matching, primary constructors, and the new **extension types** — `extension(T x) { ... }` blocks for extension methods/properties) where it reads more clearly — the `.editorconfig` already *suggests* primary constructors and expression-bodied properties/accessors.
- Keep new code **warning-clean** — no unused usings, nullable mismatches, or obsolete-API hits. Fix the cause rather than suppressing with `#pragma`/`NoWarn` (add project-wide `NoWarn` only for known framework noise, matching the existing entries). (Note: `src/Directory.Build.props` sets `WarningsAsErrors=True`, which is a *warning-code list*, not a global switch — it does **not** currently promote all warnings; treat warning-clean as a discipline, not something the build enforces for you.)
- File-scoped namespaces (enforced as a warning). System usings first; no `this.`/`Me.` qualification.

## Naming
- `_camelCase` for private/internal fields (including `static readonly`) — used consistently across the codebase and enforced (suggestion) in `.editorconfig`.
- PascalCase types & members, `I`-prefixed interfaces, `T`-prefixed type parameters.

## Dependencies — Central Package Management
- Versions live **only** in `src/Directory.Packages.props`. Add/bump a package there with `<PackageVersion Include="..." Version="..." />`; reference it from a `.csproj` with `<PackageReference Include="..." />` and **no** `Version=` attribute.

## Comments
- Comment the *why* in a line or two; let clear names and small methods carry the *what*. No walls of text.
