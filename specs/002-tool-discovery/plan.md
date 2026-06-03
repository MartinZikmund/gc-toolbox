# Implementation Plan: Attribute-Based Tool Auto-Discovery & Navigation Tree

**Branch**: `002-tool-discovery` | **Date**: 2026-05-31 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/002-tool-discovery/spec.md`

## Summary

Replace hand-written tool/category registration (feature 001's `IToolContributor`/`PlaceholderToolContributor` + DI lines) with **attribute-based auto-discovery driven by a single C# source generator** (`GcToolkit.SourceGenerators`), and surface the discovered category hierarchy in the navigation pane. A tool author writes a single ViewModel in `GcToolkit.Core`, decorates it with a `[Tool(...)]` attribute (carrying an `Id`, a `ToolCategory`, an introduced/last-updated date), adds two localized resource strings, and drops a bitmap icon — and the tool appears in the catalog **and** the navigation pane after one rebuild, with **zero edits to any shared/central file**.

The engineering is modeled closely on the **WinUI Gallery source generator** (`D:\Personal\WinUI-Gallery\WinUIGallery.SourceGenerator`): an incremental generator (`IIncrementalGenerator`, `RegisterSourceOutput`), `StringBuilder` code generation with an auto-generated header, `DiagnosticDescriptor`-based diagnostics, a netstandard2.0 analyzer project, the `NavigationPageMapperGenerator` → `PageDictionary` view-registration pattern, and the `IsNew`/`IsUpdated`/`BadgeString` + "New & Updated" presentation model. Like WinUI Gallery, the **one generator runs against the app head** (`GcToolkit`) — where it sees both the tool ViewModels (via the referenced `GcToolkit.Core` assembly's metadata) and the `View` types — and emits everything (catalog + navigation tree + all view registrations) into the app head. The **one deliberate divergence** from WinUI Gallery: the source of truth is the **`[Tool]` attribute on each ViewModel**, not a hand-maintained `ControlInfoData.json` — mandated by GitHub issue #71 ("auto-discover individual tools based on attribute"). Because the attributed VMs live in Core (a referenced assembly, not the app's own source), discovery uses **referenced-assembly metadata scanning** rather than `ForAttributeWithMetadataName` (which only sees the current compilation's source); the accepted trade-off is location-less diagnostics for tool-attribute mistakes (research R2/R13).

This feature is **additive on top of feature 001**: it reuses the existing `CatalogService`, `INavigationService`, `IRecentsService`, `IFavoriteToolsService`, localization, theming, and the Catalog page UI. It does **not** rebuild the catalog page, recents, favorites, or search. It does: (1) add the `[Tool]` attribute + closed `ToolCategory`/`ToolGroup` enums; (2) extend `ToolDescriptor`/`Category` with a tooltip key, introduced/updated dates, and a bitmap icon reference; (3) add **one generator** (`GcToolkit.SourceGenerators`, referenced by the app head) that discovers the `[Tool]` VMs in Core + the app-head views and emits a generated `IToolContributor`/`ICategoryContributor`, a UI-agnostic group→category→tool navigation tree, **and** all view↔ViewModel registrations; (4) convert the 10 placeholder tools into attributed **stub ViewModels** in Core and remove `PlaceholderToolContributor`; (5) move the page ViewModels into Core (so all VMs live in Core, Views in the app head); and (6) render the discovered hierarchy in `WindowShell`'s `NavigationView` with expandable-and-clickable category nodes that open a category-scoped Catalog view.

## Technical Context

**Language/Version**: C# (`LangVersion=preview`) on .NET 10 (`net10.0` + platform heads) for app/Core; the single generator project `GcToolkit.SourceGenerators` targets **`netstandard2.0`** (Roslyn analyzer requirement). `Nullable` enabled; file-scoped namespaces; target-typed `new()`; **`<WarningsAsErrors>True</WarningsAsErrors>` is set repo-wide** (`src/Directory.Build.props`).

**Primary Dependencies** (pinned centrally in `src/Directory.Packages.props`; CPM is on — `ManagePackageVersionsCentrally=true`): Uno Platform (`Uno.Sdk` 6.7.0-dev.68), CommunityToolkit.Mvvm, Microsoft.Extensions.Hosting/DI/Localization, MZikmund.Toolkit.WinUI, Nerdbank.GitVersioning. **NEW**: `Microsoft.CodeAnalysis.CSharp` (4.x) + `Microsoft.CodeAnalysis.Analyzers` for `GcToolkit.SourceGenerators`; `Microsoft.CodeAnalysis.CSharp.SourceGenerators.Testing.MSTest` (or Verify) for generator unit tests. New `PackageVersion` entries go in `Directory.Packages.props`.

**Storage**: N/A new. Discovery is **build-time only** (no runtime reflection, no new persistence). Recents/favorites continue via the existing `IPreferences` store unchanged.

**Testing**: MSTest on the Microsoft Testing Platform runner (`global.json`). Existing `tests/GcToolkit.Core.Tests` gains catalog/badge/nav-tree tests. **NEW** `tests/GcToolkit.SourceGenerators.Tests` uses the Roslyn source-generator testing harness (MSTest variant) to assert generated output and diagnostics (structural error / missing-resource error / happy path), feeding the attributed VMs in as a referenced compilation so metadata discovery is exercised end-to-end.

**Target Platform**: Unchanged — six heads from `src/GcToolkit/GcToolkit.csproj` (Windows WinAppSDK, Android, iOS, Skia desktop = macOS/Linux, WebAssembly). The generator runs at build for every head; emitted code must compile under all TFMs.

**Project Type**: Cross-platform Uno single-project app + shared Core library + one Roslyn source-generator project + test projects.

**Performance Goals**: Discovery and tree-building add **no per-tool runtime cost** (SC-007): everything is generated at compile time; app startup does not grow as tool count rises. Catalog/pane ordering is deterministic.

**Constraints**: No runtime reflection-based assembly scanning (FR-012) — note this is *build-time* metadata scanning inside the generator, not runtime reflection. Tools live in `GcToolkit.Core` now; the generator already scans referenced-assembly metadata, so extending discovery to additional referenced tool assemblies later needs no attribute change (FR-017). Localization without `x:Uid`; EN + CS. Generator output must be **deterministic** (no `DateTime.Now`/timestamps in generated code) so incremental-generator caching and reproducible builds hold.

**Scale/Scope**: ~10 attributed stub tool VMs (converted from today's placeholders) across 4 categories with ≥1 group; 1 generator (`GcToolkit.SourceGenerators`); 1 attribute; 2 enums; extended descriptor/category; page-VM relocation to Core; `WindowShell` pane rendering + a category-scoped Catalog entry mode; a date-driven "New & Updated" badge/section.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

> **Note**: `.specify/memory/constitution.md` is still the unedited template (placeholders), so there are no ratified principles to gate against. As in feature 001, this plan is evaluated against the project's de-facto principles in `CLAUDE.md`.

| Principle (de-facto) | Status | How this plan complies |
|---|---|---|
| Thin ViewModels; logic in DI-injected services | ✅ Pass | Discovery/registry/tree/badge logic lives in Core (generated registry + a recency classifier service); VMs orchestrate. Stub tool VMs derive a thin `ToolViewModelBase`. |
| TDD with MSTest | ✅ Pass | Generator output + diagnostics are unit-tested via the Roslyn testing harness; the recency classifier and nav-tree shaping are unit-tested in `GcToolkit.Core.Tests`. Tests authored alongside. |
| MVVM via CommunityToolkit.Mvvm | ✅ Pass | Reuses `ViewModelBase`, `[ObservableProperty]`, `[RelayCommand]`. |
| Localization without `x:Uid`, short keys, EN+CS | ✅ Pass | `<Id>_Name` / `<Id>_Tooltip` + `Category_<Name>` / `Group_<Name>` resw keys via the existing `{markup:Localize}` / `IStringLocalizer`. |
| C# style (file-scoped ns, nullable, target-typed new, braces, `dotnet format`) | ✅ Pass | Applied to new Core/app code. Generated code carries an `<auto-generated/>` header and is exempt from style review. |
| Fluent design, theme resources | ✅ Pass | Pane uses built-in `NavigationView`/`NavigationViewItem(Header)` + theme resources; badges use Fluent styles. |
| Local-first / offline / no backend | ✅ Pass | Build-time discovery; no network, no new persistence. |
| Accessibility | ✅ Pass | Pane nodes, badges, and category-scoped entry carry `AutomationProperties`; expand vs. invoke are independently operable. |

**Result**: PASS. The one added generator project is a justified structural cost (see Complexity Tracking) rather than a violation.

## Project Structure

### Documentation (this feature)

```text
specs/002-tool-discovery/
├── plan.md                       # This file
├── research.md                   # Phase 0 — decisions & rationale
├── data-model.md                 # Phase 1 — entities & metadata model
├── quickstart.md                 # Phase 1 — build/run/test + "how to add a tool"
├── contracts/                    # Phase 1
│   ├── attribute-and-metadata.md # [Tool] attribute, ToolCategory/ToolGroup, conventions, extended descriptor/category
│   └── generators.md             # GcToolkit.SourceGenerators I/O contract + diagnostics + nav hosting
├── checklists/
│   └── requirements.md           # Spec quality checklist (from /speckit-specify)
└── tasks.md                      # Phase 2 (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

Items below are **(reuse)** = use as-is, **(extend)** = modify in place, **NEW** = add.

```text
src/
├── GcToolkit.slnx                          # (extend) add GcToolkit.SourceGenerators + its test project
├── Directory.Packages.props                # (extend) add Microsoft.CodeAnalysis.* + generator-testing PackageVersion
├── Directory.Build.props/.targets          # (reuse) — note: WarningsAsErrors=True drives diagnostics-as-errors
│
├── GcToolkit.Core/                         # shared, testable library — all VMs + [Tool] types live here
│   ├── Catalog/
│   │   ├── ToolDescriptor.cs               # (extend) + TooltipKey, IntroducedDate, UpdatedDate; IconKey → bitmap key
│   │   ├── Category.cs                     # (extend) IconKey required (bitmap key); + GroupId
│   │   ├── ICatalogService.cs              # (reuse) unchanged contract
│   │   ├── CatalogService.cs               # (reuse) unchanged — still aggregates contributors
│   │   ├── IToolContributor.cs             # (reuse) generated registry implements these (FR-018 manual fallback kept)
│   │   └── PlaceholderToolContributor.cs   # (remove) once stub tool VMs land (SC-006)
│   ├── Discovery/                          # NEW
│   │   ├── ToolAttribute.cs                #   the [Tool] attribute (decorates a ToolViewModelBase)
│   │   ├── ToolCategory.cs                 #   closed enum (Coordinates, Ciphers, Numbers, Field) — convention metadata
│   │   ├── ToolGroup.cs                    #   closed enum (optional "uber-category"); category→group map
│   │   └── ToolRecencyClassifier.cs        #   dates + recency window → New/Updated/None (BadgeString)
│   ├── Navigation/
│   │   ├── NavigationTree.cs               # NEW — UI-agnostic group→category→tool model types (instances generated in app head)
│   │   └── NavigationInfoAttribute.cs …    # (reuse) shell-section attribute — unrelated to [Tool]
│   ├── ViewModels/                         # (extend) page VMs RELOCATED here (see research R4)
│   │   ├── ToolViewModelBase.cs            # NEW — base for stub/real tool VMs (name/tooltip/recents/favorite)
│   │   ├── Tools/*ViewModel.cs             # NEW — ~10 attributed stub tool VMs (replace placeholders)
│   │   ├── CatalogViewModel.cs             # (move+extend) + category-scoped entry mode
│   │   ├── HomeViewModel.cs                # (move+extend) + "New & Updated" collection
│   │   ├── SearchViewModel.cs              # (move)
│   │   ├── SettingsViewModel.cs            # (move)
│   │   ├── ToolHostViewModel.cs            # (move) — shared host behavior folds into ToolViewModelBase
│   │   ├── ToolListItem.cs                 # (move) + BadgeString; Localizer.Instance dependency removed
│   │   └── CatalogGroup.cs                 # (move)
│   └── Localization/                       # (extend) a Core-resolvable localizer seam (replaces app-head Localizer.Instance)
│
├── GcToolkit.SourceGenerators/             # NEW — netstandard2.0 analyzer (referenced by the APP HEAD)
│   ├── GcToolkit.SourceGenerators.csproj   #   IsRoslynComponent; CodeAnalysis.CSharp; IncludeBuildOutput=false
│   ├── ToolDiscoveryGenerator.cs           #   one IIncrementalGenerator: scans referenced Core metadata for [Tool] + app-head ViewBase<TVm> views → emits contributor + nav tree + ALL view registrations
│   └── StringBuilderExtensions.cs          #   AppendAutoGeneratedHeader (static, no DateTime.Now)
│
├── GcToolkit/                              # Uno app head — UI; the generator runs HERE
│   ├── App.xaml.cs                         # (extend) call generated AddDiscoveredTools() + RegisterDiscoveredViews() once; drop placeholder DI + the hand-written RegisterView block
│   ├── WindowShell.xaml(.cs)               # (extend) render generated nav tree: group headers, expandable+clickable category nodes, tool items
│   ├── Resources/DataTemplates.xaml        # (extend) tool card icon FontIcon → bitmap (BitmapIcon/ImageIcon) + "New/Updated" badge
│   ├── Strings/{en,cs}/Resources.resw      # (extend) <Id>_Name/<Id>_Tooltip, Category_*/Group_* (re-key from Tool_*); read by the generator via AdditionalFiles
│   ├── Assets/Icons/Tools/<Id>.png         # NEW — per-tool Icons8 bitmaps (convention path)
│   ├── Assets/Icons/Categories/<Cat>.png   # NEW — per-category Icons8 bitmaps (convention path)
│   └── Views/                              # (reuse) ViewBase<T>, CatalogView/HomeView/etc.; discovered by the generator (syntax)
│
tests/
├── GcToolkit.Core.Tests/                   # (extend) recency classifier, nav-tree shaping
└── GcToolkit.SourceGenerators.Tests/       # NEW — Roslyn testing harness: generated output snapshots + diagnostic assertions
```

**Structure Decision**: Keep the 001 two-project layout and add **one analyzer project**, `GcToolkit.SourceGenerators`, referenced by the **app head** — exactly like WinUI Gallery, whose single generator runs against the app. Running there lets the one generator see *both* the `[Tool]` VMs (via the referenced `GcToolkit.Core` assembly's metadata) *and* the app-head `View` types (`ViewBase<TVm>`, incl. the shared `ToolHostView`) that Core cannot reference — so it emits the catalog, the navigation tree, **and** all view registrations from one place. All ViewModels (page + tool) live in `GcToolkit.Core`; only `View`s live in the app head. The generated tool registry implements the existing `IToolContributor`/`ICategoryContributor`, so `CatalogService` is reused unchanged and **manual contributors remain a supported fallback** (FR-018).

### Key integration points (concrete extension surfaces)

- **Generated tool registry → CatalogService (unchanged)**: The generator emits (into the app head) a `GeneratedToolContributor : IToolContributor, ICategoryContributor`, a `GeneratedToolCatalog` static (tool VM types + navigation-tree instance), and an `AddDiscoveredTools(IServiceCollection)` extension called **once** in `App.RegisterServices`, replacing the placeholder registration. `CatalogService` (in Core) consumes the contributor via DI like any other. Adding a new tool later regenerates these — **no `App.xaml.cs` edit** (SC-001).
- **Generated view registrations**: The same generator emits `RegisterDiscoveredViews(NavigationService)` (mirroring WinUI Gallery's `PageDictionary`) covering every `ViewBase<TVm>` view **and** every discovered tool VM → the shared `ToolHostView`. The current hand-written `RegisterView(...)` block in `App.RegisterServices` is replaced by this one call.
- **Navigation hosting for stub tools (key design — see research R10)**: Stub tool VMs derive `ToolViewModelBase` (which carries the host behavior `ToolHostViewModel` has today). The shared `ToolHostView` is adapted to host the concrete tool VM resolved by navigation, so `Navigate<TToolViewModel>()` opens the right tool through one shared view without authoring per-tool pages (out of scope).
- **Pane rendering**: `WindowShell` builds `NavigationView` items from `GeneratedToolCatalog.NavigationTree` at runtime: `ToolGroup` → `NavigationViewItemHeader`; `ToolCategory` → an **expandable + clickable** `NavigationViewItem` (chevron reveals tools; body click opens the category-scoped Catalog); tools → selectable `NavigationViewItem`s. Empty categories/groups are omitted. Home/Catalog/Settings items remain.
- **Category-scoped Catalog**: `CatalogViewModel` gains an `OnNavigatedTo(categoryId)` scope. Per decision, the scoped view **ignores global search** (does not subscribe to `SearchViewModel`) and lists exactly that category's tools, reusing the Catalog page's grouping/ordering/favorites.
- **New & Updated**: `ToolRecencyClassifier` computes New/Updated/None from `IntroducedDate`/`UpdatedDate` against a configurable recency window (default 30 days). `ToolListItem` gains a `BadgeString`; `HomeViewModel` exposes a "New & Updated" collection (`tools where New or Updated`), mirroring WinUI Gallery's `RecentlyAddedOrUpdatedSamplesList`.

## Complexity Tracking

| Addition | Why needed | Simpler alternative rejected because |
|---|---|---|
| Discovery via referenced-assembly **metadata** scan (not `ForAttributeWithMetadataName`) | The one generator runs in the app head but the `[Tool]` VMs live in Core (a referenced assembly); `ForAttributeWithMetadataName` only sees the current compilation's source. | A second Core-side generator would give syntax discovery + precise locations, but the owner chose a single generator in the app head. Accepted trade-off: location-less diagnostics for tool-attribute mistakes (research R2/R13). |
| `netstandard2.0` analyzer project alongside the `net10.0` app | Roslyn analyzers/source generators must target `netstandard2.0`. | No alternative — the compiler will not load a `net10.0` analyzer. |
| `ToolViewModelBase` + shared-host navigation tweak | Lets ~10 distinct attributed tool VMs share one `ToolHostView` without authoring per-tool pages (out of scope) while keeping view-model-first navigation. | Per-tool views are out of scope; a single `ToolHostViewModel` target (today's model) can't carry per-tool `[Tool]` attributes that the spec requires on each tool's own VM. |

## Phase 0 — Research

See [research.md](./research.md). All spec "planning tasks" and the two success-criteria tensions (SC-001 zero-edit vs. central view registration; SC-006 placeholder removal vs. tools-are-out-of-scope) are resolved there. No unresolved `NEEDS CLARIFICATION` remain — the open spec items were settled with the project owner (see research R1–R13).

## Phase 1 — Design & Contracts

- **Data model**: [data-model.md](./data-model.md) — `ToolAttribute`, `ToolCategory`/`ToolGroup` (+ conventions and the category→group map), extended `ToolDescriptor`/`Category`, `ToolViewModelBase`, the generated registry + `NavigationTree`, and the `BadgeString`/recency classification.
- **Contracts**: [contracts/attribute-and-metadata.md](./contracts/attribute-and-metadata.md) and [contracts/generators.md](./contracts/generators.md) — the authoring surface, the generator's inputs/outputs, the diagnostic catalog (IDs + severities), and the navigation-hosting contract.
- **Quickstart**: [quickstart.md](./quickstart.md) — build/run/test per head, and the headline "add a tool / add a category" developer workflow proving the zero-central-edit promise.
- **Agent context**: `CLAUDE.md` SPECKIT block updated to point at this plan.

**Post-design Constitution re-check**: PASS — logic stays in testable Core (+ the generator with its own tests), the existing catalog/navigation/localization contracts are reused, and the only structural addition (one analyzer) is justified above.
