# Implementation Plan: App Shell & Foundation

**Branch**: `001-app-shell-foundation` | **Date**: 2026-05-27 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/001-app-shell-foundation/spec.md`

## Summary

Build the application shell and cross-cutting foundations for the geocaching toolkit on top of the existing Uno Platform app template at `D:\Personal\uno-app-template`. The template already provides the host/DI, a `NavigationView`-based `WindowShell` with a view-model-first `NavigationService`, MVVM base classes (CommunityToolkit.Mvvm), `.resw` EN/CS localization with a `{markup:Localize}` XAML extension, an `IThemeManager` (light/dark/system + title-bar sync), and an `IPreferences` local store. This feature is therefore largely **additive**: bring the template into the `gc-toolbox` repository, then add a Home landing screen (favorites + recents + search), a browsable categorized tool **catalog** with an extensible registration model, favorites and recents services, runtime language switching, a platform-adaptive title bar/search (WinUI `TitleBar` on Windows; `NavigationView` `AutoSuggestBox` elsewhere), and placeholder catalog entries so the shell is demonstrable end-to-end. All data stays local; no backend.

## Technical Context

**Language/Version**: C# 13 on .NET 10 (`net10.0` + platform heads); `Nullable` enabled; file-scoped namespaces; target-typed `new()`.

**Primary Dependencies**: Uno Platform (`Uno.Sdk` 6.7.0-dev.64), CommunityToolkit.Mvvm, Microsoft.Extensions.Hosting/DI/Localization/Configuration, CommunityToolkit.WinUI controls (incl. SettingsControls), MZikmund.Toolkit.WinUI. All inherited from the `uno-app-template`.

**Storage**: Local device storage via the template's `IPreferences` (`Windows.Storage.ApplicationData.Current.LocalSettings` + JSON `GetComplex/SetComplex`) for settings, favorites, and recents. `sqlite-net-e` is available in the template but **not used** in SP-1 (data volume is tiny).

**Testing**: MSTest v4 (Microsoft Testing Platform) in the `*.Core.Tests` project. Catalog/search/favorites/recents/preferences logic lives in the shared Core library and is unit-tested; UI is verified by per-platform smoke runs.

**Target Platform**: Windows (WinAppSDK, `net10.0-windows10.0.26100`), Android (`net10.0-android`), iOS (`net10.0-ios`), **macOS and Linux via the Skia desktop head** (`net10.0-desktop`), and WebAssembly (`net10.0-browserwasm`) — six platforms from the single-project app.

**Project Type**: Cross-platform desktop + mobile + web application (Uno single-project app + shared Core library + test project).

**Performance Goals**: Cold start to interactive catalog ≤ 3 s; search narrows within ≤ 2 s of typing (incremental); theme/language change applies in ≤ 1 s without restart (from spec SC-001/002/005/009).

**Constraints**: Fully offline (no network dependency); local-first (no accounts/backend); WCAG 2.2 Level AA; English + Czech with runtime switching; responsive 320 px → large desktop; no `x:Uid` (use the `{markup:Localize}` extension).

**Scale/Scope**: SP-1 delivers ~4 screens (Home, Catalog, a tool host/stub, Settings), ~5 new Core services, the tool-descriptor/registration model, and placeholder catalog entries (~2–3 per category). It is the foundation the ~60+ later tools and the field/sensor tools plug into.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

> **Note**: `.specify/memory/constitution.md` is still the unedited template (placeholders). Running `/speckit.constitution` to ratify it is recommended. In its absence, this plan is evaluated against the project's established conventions in `CLAUDE.md` and `docs/PRD.md`, treated as de-facto principles.

| Principle (de-facto) | Status | How this plan complies |
|---|---|---|
| Thin ViewModels; business logic in DI-injected services | ✅ Pass | Catalog/search/favorites/recents/language logic lives in Core services; ViewModels only orchestrate. |
| TDD with MSTest | ✅ Pass | Core services (search matching, recents dedup/cap, favorites toggle, preferences mapping) are unit-tested; tests written alongside implementation. |
| MVVM via CommunityToolkit.Mvvm | ✅ Pass | Reuses `ViewModelBase : ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`. |
| Localization without `x:Uid`, short keys, EN+CS | ✅ Pass | Uses the existing `{markup:Localize Key=...}` extension and `.resw`; adds runtime switching. |
| C# style (file-scoped namespaces, nullable, target-typed new, braces) | ✅ Pass | Applied throughout new code; `dotnet format` before staging. |
| Fluent design, theme resources | ✅ Pass | Reuses template theme dictionaries and WinUI built-in styles. |
| Local-first / offline / no backend | ✅ Pass | All state via `IPreferences`; no network. |
| Accessibility | ✅ Pass | WCAG 2.2 AA targeted (FR-014, SC-011). |

**Result**: PASS (no violations). Complexity Tracking is empty.

## Project Structure

### Documentation (this feature)

```text
specs/001-app-shell-foundation/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   └── services.md      # Service & tool-registration contracts
├── checklists/
│   └── requirements.md  # Spec quality checklist (from /speckit-specify)
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

The `uno-app-template` is brought into this repo under `src/`, with the `GcToolkit*` projects renamed to `GCToolkit*` (root namespace `GCToolkit`; the user-facing brand name remains TBD and trademark-safe, decoupled from the code identifier).

```text
src/
├── GCToolkit.slnx
├── Directory.Build.props          # (from template) UnoFeatures, common props
├── Directory.Packages.props       # (from template) central package versions
├── GCToolkit/                     # Uno single-project app (UI heads)
│   ├── App.xaml(.cs)              # host builder + RegisterServices (extend)
│   ├── WindowShell.xaml(.cs)      # NavigationView shell (extend: TitleBar + search)
│   ├── Markup/LocalizeExtension.cs# (extend: dynamic refresh on language change)
│   ├── Strings/{en,cs}/Resources.resw  # (extend: new shell strings)
│   ├── Resources/                 # Colors/Styles/DataTemplates (reuse + add)
│   ├── Views/
│   │   ├── HomeView.xaml(.cs)         # NEW — favorites + recents + search
│   │   ├── CatalogView.xaml(.cs)      # NEW — categorized browsable catalog
│   │   ├── ToolHostView.xaml(.cs)     # NEW — stub host for placeholder tools
│   │   └── SettingsView.xaml(.cs)     # (extend: add language selector)
│   ├── ViewModels/                # HomeViewModel, CatalogViewModel, ToolHostViewModel, SettingsViewModel(extend)
│   ├── Services/                  # platform/UI services (Theming, Navigation, Localization, Settings — reuse)
│   └── Platforms/                 # per-head entry points (reuse)
├── GCToolkit.Core/                # shared, testable library
│   ├── Catalog/                   # NEW — ToolDescriptor, Category, ICatalogService, registration
│   ├── Search/                    # NEW — accent/case-insensitive matching
│   ├── Favorites/                 # NEW — IFavoritesService
│   ├── Recents/                   # NEW — IRecentsService (dedup, cap 10, clear)
│   ├── Localization/              # NEW — ILanguageService (runtime switch)
│   ├── Navigation/                # (reuse) view-model-first navigation
│   ├── Services/                  # (reuse) Preferences/AppPreferences
│   └── ViewModels/                # (reuse) ViewModelBase
└── ...

tests/
└── GCToolkit.Core.Tests/          # MSTest — search, recents, favorites, catalog, preferences mapping
```

**Structure Decision**: Reuse the template's two-project layout (single-project Uno **app** + shared **Core** library) plus the MSTest project, renamed to `GCToolkit*`. New, testable domain logic (catalog, search, favorites, recents, language) goes in `GCToolkit.Core`; UI (Home/Catalog/ToolHost/Settings views + view models, shell, title bar/search) goes in the `GCToolkit` app. Existing template services (navigation, theming, preferences, localization) are reused and extended rather than replaced.

## Complexity Tracking

> No Constitution Check violations. Section intentionally empty.

## Phase 0 — Research

See [research.md](./research.md). Ten decisions resolved: local persistence approach, tool catalog & extensibility model, accent/case-insensitive search, runtime language switching, platform-adaptive WinUI `TitleBar` + search, theming reuse, responsive navigation, macOS/Linux coverage via the desktop head, WCAG 2.2 AA approach, and template bring-in/renaming. No unresolved `NEEDS CLARIFICATION` remain.

## Phase 1 — Design & Contracts

- **Data model**: [data-model.md](./data-model.md) — `ToolDescriptor`, `Category`, `FavoriteEntry`, `RecentEntry`, `AppPreferences` (Theme, Language), with validation and persistence mapping.
- **Contracts**: [contracts/services.md](./contracts/services.md) — `ICatalogService`, tool-registration contract, `IFavoritesService`, `IRecentsService`, `ILanguageService`, and the reused `IThemeManager`/`INavigationService`/`IPreferences`.
- **Quickstart**: [quickstart.md](./quickstart.md) — prerequisites, bring-in steps, build/run per head, tests, and "how to add a tool".
- **Agent context**: `CLAUDE.md` SPECKIT block updated to point to this plan.

**Post-design Constitution re-check**: PASS — the design keeps logic in testable Core services, reuses template conventions, and introduces no new principle violations.
