---
description: How to write and run tests in this app
---

# Testing

- Tests live in **`GcToolkit.Core.Tests`** (MSTest on **Microsoft.Testing.Platform**, `net10.0`). Keep testable logic in `GcToolkit.Core` so it can be covered without a UI head.
- **TDD:** write a failing test first, watch it fail, then implement until it passes.

## Running
```bash
dotnet test tests/GcToolkit.Core.Tests/GcToolkit.Core.Tests.csproj
```
The runner is MTP, not VSTest — **don't pass VSTest-only flags** like `--nologo` or `--logger`; they error out. Filter with `dotnet test ... --filter "FullyQualifiedName~MyClass"`.

## Conventions
- Name tests `Method_Scenario_ExpectedResult`; structure them Arrange / Act / Assert. Use `[TestMethod]` and `[DataRow]` for parameterized cases.
- **Prefer small hand-written fakes/stubs** for collaborators (clearer and refactor-stable) — see `Fakes/` in the test project.
- Assert with the built-in **MSTest `Assert`** API (`Assert.AreEqual(expected, actual)`); that's what the existing suite uses (no FluentAssertions dependency).
- Core view models touch WinUI *data types* (e.g. `ElementTheme`); add `using Microsoft.UI.Xaml;` in the test when faking such services.
