# Implementation Plan: App Shell & Foundation

**Branch**: `001-app-shell-foundation` | **Date**: 2026-05-27 | **Revised**: 2026-05-30 (template integrated) | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/001-app-shell-foundation/spec.md`

## Summary

Build the application shell and cross-cutting foundations for the geocaching toolkit. **The Uno Platform app template has already been integrated into this repository** under `src/` as the `GcToolkit` (app) and `GcToolkit.Core` (shared library) projects, with a skeleton MSTest project at `tests/GcToolkit.Core.Tests`. The integrated template already provides the host/DI bootstrap (`App.xaml.cs`), a `NavigationView`-based `WindowShell` with a view-model-first `NavigationService` (section-based, via a `NavigationSection` enum), MVVM base classes (CommunityToolkit.Mvvm), `.resw` EN/CS localization with a `{markup:Localize}` XAML extension, an `IThemeManager` (light/dark/system with WinAppSDK title-bar button sync and live OS-theme tracking), an `IPreferences`/`IAppPreferences` local store, a custom WinAppSDK title bar (`TitleBarGrid`), Dev/Prod app channels, and a set of supporting services (dialogs, launcher, share, rating, display-request, app-updater).

This feature is therefore purely **additive on top of the integrated template**: add a Home landing screen (favorites + recents + search) replacing the placeholder `MainView`, a browsable categorized tool **catalog** with an extensible registration model, favorites and recents services, English/Czech language selection (applied on restart), a platform-adaptive search surface (into the WinUI `TitleBar` control on Windows; into the `NavigationView` elsewhere), and placeholder catalog entries so the shell is demonstrable end-to-end. All data stays local; no backend.

## Technical Context

**Language/Version**: C# 13 on .NET 10 (`net10.0` + platform heads); `Nullable` enabled; file-scoped namespaces; target-typed `new()`.

**Primary Dependencies** (as pinned in `src/Directory.Packages.props` / `global.json`): Uno Platform (`Uno.Sdk` 6.7.0-dev.64), CommunityToolkit.Mvvm, Microsoft.Extensions.Hosting/DI/Localization/Configuration/Http, CommunityToolkit.WinUI controls (SettingsControls, Converters, Helpers, Primitives), MZikmund.Toolkit.WinUI, Nerdbank.GitVersioning. `sqlite-net-e` + `SourceGear.sqlite3` are referenced by the app but **not used** in SP-1.

**Storage**: Local device storage via the template's `IPreferences` (`Windows.Storage.ApplicationData.Current.LocalSettings`, with typed `Get/Set` and JSON `GetComplex/SetComplex`) wrapped by the typed `IAppPreferences`. Used for settings, favorites, and recents. `sqlite-net-e` is available but **not used** in SP-1 (data volume is tiny).

**Testing**: MSTest via `MSTest.Sdk` 4.2.3 on the Microsoft Testing Platform runner (`global.json` → `"runner": "Microsoft.Testing.Platform"`), in the existing `tests/GcToolkit.Core.Tests` project (currently a skeleton — `MSTestSettings.cs` only). Catalog/search/favorites/recents/preferences logic lives in `GcToolkit.Core` and is unit-tested; UI is verified by per-platform smoke runs.

**Target Platform**: Six heads from the single-project app (`src/GcToolkit/GcToolkit.csproj`): Windows (WinAppSDK, `net10.0-windows10.0.26100`), Android (`net10.0-android`), iOS (`net10.0-ios`), **macOS and Linux via the Skia desktop head** (`net10.0-desktop`), and WebAssembly (`net10.0-browserwasm`).

**Project Type**: Cross-platform desktop + mobile + web application (Uno single-project app + shared Core library + MSTest project).

**Identity/Branding**: Display name **GC Toolkit** (Prod) / **GC Toolkit Dev** (Dev channel), `ApplicationId` `dev.mzikmund.gctoolkit[.dev]`, publisher *Martin Zikmund*. The code identifier / root namespace is `GcToolkit` (and `GcToolkit.Core`), distinct from the display name.

**Performance Goals**: Cold start to interactive catalog ≤ 3 s; search narrows within ≤ 2 s of typing (incremental); theme change applies in ≤ 1 s without restart; language change is applied on the next launch (restart acceptable) (from spec SC-001/002/005/009).

**Constraints**: Fully offline (no network dependency); local-first (no accounts/backend); WCAG 2.2 Level AA; English + Czech (language change applied on restart); responsive 320 px → large desktop; no `x:Uid` (use the `{markup:Localize}` extension).

**Scale/Scope**: SP-1 delivers ~4 screens (Home, Catalog, a tool host/stub, Settings — Settings already exists and is extended), ~5 new Core services, the tool-descriptor/registration model, and placeholder catalog entries (~2–3 per category). It is the foundation the ~60+ later tools and the field/sensor tools plug into.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

> **Note**: `.specify/memory/constitution.md` is still the unedited template (placeholders). Running `/speckit.constitution` to ratify it is recommended. In its absence, this plan is evaluated against the project's established conventions in `CLAUDE.md` and `docs/PRD.md`, treated as de-facto principles. The integrated template already embodies most of them (thin VMs, DI, CommunityToolkit.Mvvm, `{markup:Localize}`, theme resources).

| Principle (de-facto) | Status | How this plan complies |
|---|---|---|
| Thin ViewModels; business logic in DI-injected services | ✅ Pass | Catalog/search/favorites/recents/language logic lives in Core services; ViewModels only orchestrate. Matches the template's existing `MainViewModel`/`SettingsViewModel` style. |
| TDD with MSTest | ✅ Pass | Core services (search matching, recents dedup/cap, favorites toggle, preferences mapping) are unit-tested in `GcToolkit.Core.Tests`; tests written alongside implementation. |
| MVVM via CommunityToolkit.Mvvm | ✅ Pass | Reuses `ViewModelBase : ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`. |
| Localization without `x:Uid`, short keys, EN+CS | ✅ Pass | Uses the existing `{markup:Localize Key=...}` extension and `.resw`; adds runtime switching (see Phase 1 design note). |
| C# style (file-scoped namespaces, nullable, target-typed new, braces) | ✅ Pass | Applied throughout new code; `dotnet format` before staging. |
| Fluent design, theme resources | ✅ Pass | Reuses template theme dictionaries, WinUI built-in styles, and CommunityToolkit `SettingsCard` (as already used in `SettingsView`). |
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

### Source Code (repository root) — current integrated state

The template is **already integrated** under `src/` with the code identifier `GcToolkit` (root namespace `GcToolkit`). Items below are marked **(reuse)** = use as-is, **(extend)** = modify in place, **NEW** = add for this feature.

```text
src/
├── GcToolkit.slnx
├── Directory.Build.props / .targets   # (reuse) common props, UnoFeatures, GitVersioning
├── Directory.Packages.props           # (reuse) central package versions
├── GcToolkit/                         # Uno single-project app (UI heads)
│   ├── App.xaml(.cs)                  # (extend) host builder + RegisterServices: register new VMs/services + RegisterView/sections
│   ├── WindowShell.xaml(.cs)          # (extend) NavigationView shell; replace TitleBarGrid → WinUI TitleBar (Windows) + adaptive search; add Home/Catalog nav items & sections
│   ├── Controls/DevChannelBadge       # (reuse) Dev-channel overlay
│   ├── Converters/                    # (reuse) EnumLocalizationConverter (enum→localized text), NullToVisibility
│   ├── Infrastructure/                # (reuse) AppEnvironment (Dev/Prod), AppUpdater, IWindowShell
│   ├── Markup/LocalizeExtension.cs    # (reuse) one-shot resolve; language applies on restart (no live refresh needed)
│   ├── Models/AppConfig.cs            # (reuse) embedded appsettings config
│   ├── Resources/                     # (reuse + extend) Colors/Converters/Styles/DataTemplates
│   ├── Strings/{en,cs}/Resources.resw # (extend) new shell/Home/Catalog strings + language names
│   ├── Views/
│   │   ├── HomeView.xaml(.cs)             # NEW — Home landing (favorites + recents + search); replaces MainView
│   │   ├── CatalogView.xaml(.cs)          # NEW — categorized browsable catalog
│   │   ├── ToolHostView.xaml(.cs)         # NEW — stub host for placeholder tools (receives tool id as nav parameter)
│   │   ├── SettingsView.xaml(.cs)         # (extend) add a Language SettingsCard alongside the existing Theme card
│   │   ├── MainView.xaml(.cs)             # (remove) "Hello Uno Platform!" placeholder
│   │   └── ViewBase.cs / IViewBase.cs     # (reuse) generic view base wiring DataContext→VM
│   ├── ViewModels/
│   │   ├── HomeViewModel.cs               # NEW — Home (favorites + recents + search); replaces MainViewModel
│   │   ├── CatalogViewModel.cs            # NEW
│   │   ├── ToolHostViewModel.cs           # NEW
│   │   ├── SearchViewModel.cs             # NEW — shared search VM (title bar + nav view)
│   │   ├── SettingsViewModel.cs           # (extend) add language selection
│   │   └── MainViewModel.cs               # (remove) placeholder
│   ├── Services/
│   │   ├── Settings/                  # (reuse + extend) IPreferences/Preferences, IAppPreferences/AppPreferences (+ Language)
│   │   ├── Theming/                   # (reuse) IThemeManager/ThemeManager (light/dark/system + title-bar sync)
│   │   ├── Navigation/                # (reuse) NavigationService, WindowShellProvider, IXamlRootProvider
│   │   ├── Localization/Localizer.cs  # (reuse) IStringLocalizer wrapper
│   │   ├── Dialogs/                   # (reuse) Dialog/Confirmation services — e.g. "Clear recents" confirm
│   │   ├── Rating/ Http/             # (reuse) AppRatingService, DebugHttpHandler
│   │   ├── LauncherService / ShareService / DisplayRequestManager  # (reuse) available to future tools
│   ├── Platforms/                     # (reuse) per-head entry points (Android/iOS/Desktop/WebAssembly/Windows)
│   └── Properties/                    # (reuse) launchSettings, publish profiles
├── GcToolkit.Core/                    # shared, testable library
│   ├── Infrastructure/                # (reuse) IoC, IApplication, IAppUpdater, ApplicationReleaseInfo
│   ├── Navigation/                    # (reuse + extend) NavigationSection enum (+ Home/Catalog/Tool), NavigationInfoAttribute, NavigationTransition
│   ├── Services/INavigationService.cs # (reuse) view-model-first Navigate<TViewModel>(parameter)
│   ├── ViewModels/                    # (reuse) ViewModelBase, WindowShellViewModel
│   ├── Catalog/                       # NEW — ToolDescriptor, Category, ICatalogService, registration
│   ├── Search/                        # NEW — accent/case-insensitive matching
│   ├── Favorites/                     # NEW — IFavoritesService
│   ├── Recents/                       # NEW — IRecentsService (dedup, cap 10, clear)
│   └── Localization/                  # NEW — ILanguageService (available cultures + persist; applied on restart)
└── ...

tests/
└── GcToolkit.Core.Tests/             # (extend) MSTest skeleton → add search, recents, favorites, catalog, preferences-mapping tests
```

**Structure Decision**: Build on the already-integrated two-project layout (single-project Uno **app** `GcToolkit` + shared **Core** library `GcToolkit.Core`) plus the existing `GcToolkit.Core.Tests` project. New, testable domain logic (catalog, search, favorites, recents, language) goes in `GcToolkit.Core`; UI (Home/Catalog/ToolHost views + view models, shell, adaptive search) goes in the `GcToolkit` app. Existing template services (navigation, theming, preferences, localization, dialogs, launcher, share) are **reused and extended**, not replaced.

### Key integration points (concrete extension surfaces)

- **Navigation / catalog wiring**: The shell is section-driven. To add screens, extend the `NavigationSection` enum (`Main`, `Settings` → add `Home`/`Catalog`/`Tool`), add `NavigationView.MenuItems` with matching `Tag`s in `WindowShell.xaml`, register view↔VM pairs in `App.RegisterServices` via `NavigationService.RegisterView(...)`, and add cases to `WindowShell.NavigateToSection`. Individual tools do **not** each get a section — `CatalogView` navigates to `ToolHostViewModel` using the existing `Navigate<TViewModel>(object? parameter)` overload, passing the tool id/descriptor.
- **Adaptive search (decided)**: On the Windows head, **replace** the template's hand-rolled `TitleBarGrid` with the WinUI `TitleBar` control (`Microsoft.UI.Xaml.Controls.TitleBar`) and host the search `AutoSuggestBox` in its content region (FR-018). On heads where that control is not yet available, keep a standard title bar and host the `AutoSuggestBox` in the `NavigationView` (pane/header). A single bindable search VM sits behind both so behavior is identical (FR-003). The template's existing `HasCustomTitleBar` gating (`AppWindowTitleBar.IsCustomizationSupported()`, `#if !HAS_UNO`) is the seam where the Windows branch plugs in.
- **Language switching (restart-based — decided)**: A restart to apply a language change is acceptable, which removes the live-refresh problem. `LocalizeExtension` stays one-shot; `IStringLocalizer` is wired via `.UseLocalization()`. Add an `ILanguageService` (Core) that enumerates available cultures (`en`/`cs`), persists the choice, and applies `CultureInfo`/`PrimaryLanguageOverride` **at startup** so the selection takes effect on the next launch; the Settings UI shows a "restart to apply" notice on change. First-run default: system language if `en`/`cs`, else `en` (FR-009). No dynamic markup-extension refresh is needed.
- **Settings UI**: `SettingsView` already uses CommunityToolkit `SettingsCard` + a `ComboBox` bound to `ElementTheme` options with `EnumLocalizationConverter`. Add the language selector as a sibling `SettingsCard` following the identical pattern.

## Complexity Tracking

> No Constitution Check violations. Section intentionally empty.

## Phase 0 — Research

See [research.md](./research.md). The template **bring-in/rename decision has been executed** — the template is integrated under `src/` as `GcToolkit`/`GcToolkit.Core` with the display name *GC Toolkit*, so that item is now historical rather than pending. The remaining decisions stand: local persistence approach, tool catalog & extensibility model, accent/case-insensitive search, restart-based language switching (see the decided approach above), platform-adaptive search via the WinUI `TitleBar` control on Windows (and the `NavigationView` elsewhere), theming reuse, responsive navigation, macOS/Linux coverage via the desktop head, and the WCAG 2.2 AA approach. No unresolved `NEEDS CLARIFICATION` remain.

## Phase 1 — Design & Contracts

- **Data model**: [data-model.md](./data-model.md) — `ToolDescriptor`, `Category`, `FavoriteEntry`, `RecentEntry`, `AppPreferences` (Theme exists; **add** Language), with validation and persistence mapping (`GetComplex/SetComplex`).
- **Contracts**: [contracts/services.md](./contracts/services.md) — `ICatalogService`, tool-registration contract, `IFavoritesService`, `IRecentsService`, `ILanguageService`, and the reused `IThemeManager`/`INavigationService`/`IPreferences`/`IAppPreferences`.
- **Quickstart**: [quickstart.md](./quickstart.md) — prerequisites, build/run per head (`src/GcToolkit.slnx`), tests (MTP runner), and "how to add a tool" against the section/registration pattern above.
- **Agent context**: `CLAUDE.md` SPECKIT block updated to point to this plan.

**Post-design Constitution re-check**: PASS — the design keeps logic in testable Core services, reuses the integrated template's conventions and services, and introduces no new principle violations.
