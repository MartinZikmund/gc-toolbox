# Quickstart: App Shell & Foundation

How to stand up and work on the shell, built on the `uno-app-template`.

## Prerequisites

- **.NET 10 SDK** matching `global.json` (the template pins `Uno.Sdk` 6.7.0-dev.64, `MSTest.Sdk` 4.2.1).
- **Uno Platform prerequisites** — run `dotnet tool install -g uno.check` then `uno-check` to install required workloads (Android/iOS/WebAssembly/Skia desktop).
- An IDE: Visual Studio 2022 (Windows, for the WinAppSDK head), Rider, or VS Code with the C#/Uno extensions.

## One-time bring-in (Phase setup task)

1. Copy the template `src/` and `tests/` from `D:\Personal\uno-app-template` into `D:\Personal\gc-toolbox\` (repo currently has no `src/`).
2. Rename projects and root namespace: `GcToolkit` → `GCToolkit`, `GcToolkit.Core` → `GCToolkit.Core`, `GcToolkit.Core.Tests` → `GCToolkit.Core.Tests`; rename `GcToolkit.slnx` → `GCToolkit.slnx`. Update namespaces and `GlobalUsings`.
3. Keep the user-facing display name as a placeholder (brand TBD); only the code identifiers change.
4. Confirm a clean build before adding features.

## Build & run per platform head

From `src/` (paths/heads as in the template):

```bash
# Desktop (Skia) — also how macOS/Linux are served
dotnet run --project GCToolkit/GCToolkit.csproj -f net10.0-desktop

# WebAssembly
dotnet run --project GCToolkit/GCToolkit.csproj -f net10.0-browserwasm

# Windows (WinAppSDK) — build/run from Visual Studio (or dotnet) on Windows
dotnet build GCToolkit/GCToolkit.csproj -f net10.0-windows10.0.26100
```

Android (`net10.0-android`) and iOS (`net10.0-ios`) are launched from the IDE against an emulator/simulator or device.

## Run the tests

```bash
dotnet test tests/GCToolkit.Core.Tests/GCToolkit.Core.Tests.csproj
```

Core logic with unit tests in SP-1: tool search matching (case/accent-insensitive), recents dedup/cap/clear, favorites toggle, language default resolution, and preferences (de)serialization.

## Definition-of-done smoke check (maps to spec SCs)

1. App launches to the **Home** screen (favorites + recents + search) on the desktop head within ~3 s (SC-009).
2. Open the **Catalog**, see placeholder tools grouped by category, open one, navigate back (US1).
3. Search a placeholder by name and by an un-accented Czech query; verify it matches and that a no-match query shows the empty state (US2, SC-002).
4. Favorite/unfavorite a tool; open a few tools and confirm recents (dedup, max 10, clearable) (US3).
5. In **Settings**, switch the theme and confirm the UI updates within ~1 s without restart; switch the language, restart the app, and confirm the UI is in the new language (US4, SC-005).
6. Restart the app; favorites, recents, theme, and language persist (SC-004).
7. Resize from ~320 px to a wide desktop window; navigation adapts, nothing truncates (SC-007).
8. On the **Windows** head, search is in the WinUI `TitleBar`; on other heads it's in the `NavigationView` search field (FR-018).
9. Run an accessibility pass (Accessibility Insights + screen reader) against Home/Catalog/Tool/Settings (SC-011).

## How to add a tool (for later phases)

1. Create a `ToolDescriptor` (Id, NameKey, CategoryId, keywords, ViewModelType) and add EN/CS strings to `Strings/{en,cs}/Resources.resw`.
2. Implement the tool's `View` + `ViewModel`; register the view with the `NavigationService`.
3. Register an `IToolContributor` that yields the descriptor in `RegisterServices`. The catalog, search, favorites, and recents pick it up automatically — no shell changes.
