# Phase 0 Research: Attribute-Based Tool Auto-Discovery & Navigation Tree

This document records the decisions that resolve the spec's open "planning tasks" and the two tensions its success criteria create. Each decision was settled with the project owner during planning (`/speckit-plan`, 2026-05-31). Format: **Decision → Rationale → Alternatives considered**.

Reference implementation studied: **`D:\Personal\WinUI-Gallery\WinUIGallery.SourceGenerator`** (`NavigationPageMapperGenerator`, `StringBuilderExtensions`, the `ControlInfoData` model with `IsNew`/`IsUpdated`/`BadgeString`, and `ControlInfoDataSource`). The owner asked to model the feature "very closely" on it; the patterns below adopt its engineering while keeping attribute-based discovery.

---

## R1 — Source of truth: `[Tool]` attribute, modeled on WinUI Gallery's generator engineering

**Decision**: Discovery is driven by a `[Tool]` attribute on each tool ViewModel, found by the generator scanning the referenced `GcToolkit.Core` assembly's **metadata** (see R2 for why metadata, not `ForAttributeWithMetadataName`). We mirror WinUI Gallery's generator *patterns* (a single incremental generator running against the app, `RegisterSourceOutput`, `StringBuilder` codegen + auto-generated header, `DiagnosticDescriptor` diagnostics, the netstandard2.0 analyzer csproj, the `NavigationPageMapperGenerator`→`PageDictionary` view-registration pattern, and the `IsNew`/`IsUpdated`/`BadgeString` model) — but **not** its JSON source of truth.

**Rationale**: GitHub issue #71 explicitly requires attribute-based discovery, and the owner confirmed four attribute-based decisions (explicit `Id` in the attribute, attribute on the VM, convert placeholders to attributed VMs, attribute carries category/dates). WinUI Gallery's `ControlInfoData.json` is a hand-maintained catalog — the opposite of "no central file edit per tool" (SC-001/SC-002). The attribute keeps tool metadata co-located with the tool's own code.

**Alternatives**: (a) **Full JSON-file-driven** like WinUI Gallery — rejected: contradicts #71 and SC-001 (every tool edits a shared JSON). (b) Runtime reflection scan — rejected by FR-012 (no runtime scanning; startup must not grow).

---

## R2 — One source generator (`GcToolkit.SourceGenerators`) running against the app head

**Decision**: Ship a **single** incremental generator, project `GcToolkit.SourceGenerators` (`netstandard2.0` analyzer), **referenced by the app head `GcToolkit`** — like WinUI Gallery, whose one generator runs against the app. Running there, it sees **both**:
- the `[Tool]` ViewModels (which live in `GcToolkit.Core`) by scanning the **referenced Core assembly's metadata** (`CompilationProvider` → walk `IAssemblySymbol`s for `ToolAttribute`), and
- the app-head `ViewBase<TVm>` `View` types by **syntax**.

It emits **into the app head**: `GeneratedToolContributor` (`IToolContributor`/`ICategoryContributor`), `GeneratedToolCatalog` (tool VM types + `NavigationTree`), `AddDiscoveredTools(IServiceCollection)`, and `RegisterDiscoveredViews(NavigationService)` — every view↔VM pair **plus** each discovered tool VM → the shared `ToolHostView` (the analog of WinUI Gallery's `NavigationPageMapperGenerator` → `PageDictionary`).

**Why metadata, not `ForAttributeWithMetadataName`**: `ForAttributeWithMetadataName` only finds attributes in the **current compilation's source**. The generator runs in the app head but the attributed VMs are in a *referenced* assembly (Core), so it must read them from metadata via `AttributeData`. This also naturally aggregates tools from any *additional* referenced assemblies later (FR-017) with no attribute change.

**Rationale**: The owner asked to "have the generator go directly against the main app" and confirmed "tool VMs stay in Core" — so one generator in the app head, metadata discovery. This is the closest match to WinUI Gallery (one generator, against the app), eliminates the two-generator split and the cross-assembly handoff, and lets the generator name app-head `View` types directly (which a Core-side generator could not).

**Accepted trade-off**: metadata-discovered tool symbols have no syntax, so tool-attribute diagnostics (`GCTOOL001/002/003`) are **location-less** (they identify the type by name + `Id`); incrementality is coarser (keyed on `CompilationProvider`). Both are acceptable at this catalog size. The `.resw` lives in the app head, so resource-gap diagnostics (`GCTOOL004`) *can* point at the file.

**Alternatives**: (a) Two generators (Core catalog generator with syntax discovery + precise locations, plus an app-head view generator) — better diagnostics, but the owner chose one generator. (b) Move tool VMs into the app head for pure syntax discovery — rejected: the owner kept tool VMs in Core (testability; logic in Core services per the "thin VM" rule). (c) **Optional mitigation** kept in reserve: also reference `GcToolkit.SourceGenerators` from Core *analyzer-only* as a small `DiagnosticAnalyzer` to recover precise locations — one DLL, two consumers; deferred unless needed.

---

## R3 — Convert the 10 placeholder tools into attributed stub ViewModels; remove `PlaceholderToolContributor`

**Decision**: Replace `PlaceholderToolContributor` with ~10 attributed **stub** tool ViewModels in `GcToolkit.Core` (one per current placeholder: the three Coordinates tools, three Ciphers, two Numbers, two Field). Each derives `ToolViewModelBase` and carries `[Tool(...)]`. They have no real tool functionality (authoring tools is out of scope) but are fully discovered, navigable, badge-able, and pane-rendered. `PlaceholderToolContributor` and its DI lines are deleted (SC-006).

**Rationale**: SC-006 demands the catalog be populated entirely by auto-discovered tools with the placeholder removed; US1 requires adding an attributed VM and seeing it appear. Reusing the existing 10 placeholders as the first attributed tools gives an immediate, realistic discovery surface spanning all four categories without inventing tool functionality.

**Alternatives**: (a) Keep the placeholder contributor + a couple of samples — rejected: fails SC-006's "placeholder removed." (b) Generator-side synthesis of fake tools — rejected: tools must be real types the navigation can open.

---

## R4 — Move the page ViewModels (and their app-head dependencies) into `GcToolkit.Core`

**Decision**: Relocate `CatalogViewModel`, `HomeViewModel`, `SearchViewModel`, `SettingsViewModel`, `ToolHostViewModel` and their supporting types (`ToolListItem`, `CatalogGroup`) into `GcToolkit.Core`. The static app-head `Localizer.Instance` dependency in `ToolListItem` is replaced by a Core-resolvable localization seam (constructor-injected `IStringLocalizer`, or a Core localizer facade). `ToolHostViewModel`'s host behavior folds into the new `ToolViewModelBase`.

**Rationale**: The project owner chose to consolidate all VMs into Core. With the generator now running against the app head (R2), this is no longer *required* by the generator, but the owner's preference stands and is consistent with tool VMs living in Core — the result is a clean split: **all ViewModels in Core, all Views in the app head**. It also removes the app-head static `Localizer.Instance` coupling that would otherwise block the move and keeps tool/page logic unit-testable in `GcToolkit.Core.Tests`.

**Alternatives**: (a) Move only tool VMs (the strict generator requirement) — the owner declined; full consolidation was chosen. (b) Move tool VMs + `ToolHostViewModel` only — superseded by the same choice. *Risk captured in plan Complexity/Structure: the `Localizer.Instance` and `ToolListItem`/`CatalogGroup` relocation is the main refactor cost and must preserve existing Catalog/Home behavior and tests.*

---

## R5 — Tool `Id`: explicit PascalCase in the attribute; resw keys `<Id>_Name` / `<Id>_Tooltip`

**Decision**: The `[Tool]` attribute carries an **explicit, resource-key-safe PascalCase `Id`** (e.g., `CoordinateConversion`), independent of the VM type name. Display name and tooltip resolve from `<Id>_Name` and `<Id>_Tooltip` resw keys. Today's dotted placeholder ids (`coordinates.conversion`) are re-authored to PascalCase; the `Tool_*` resw keys are re-keyed to `<Id>_Name` (+ new `<Id>_Tooltip`).

**Rationale**: `.resw` keys are not reliably dotted-key-safe, and the `Id` doubles as the localization base key (FR-005). An explicit `Id` decouples the persisted favorites/recents key from VM renames. The placeholder ids are being replaced anyway (R3), so the id-shape migration is free.

**Alternatives**: (a) Derive `Id` from the VM type name by convention — consistent with the convention-only enums, but the owner chose explicit ids for stability/clarity. (b) Keep dotted ids + dotted resw keys — rejected: dotted `.resw` keys risk WinUI resource-targeting semantics.

---

## R6 — `ToolCategory` / `ToolGroup` are closed enums with **convention-only** metadata

**Decision**: Two closed enums. Per-member metadata is derived by convention, no per-member attributes or tables:
- **`ToolCategory`** members: `Coordinates`, `Ciphers`, `Numbers`, `Field`. Conventions: `Id` = member name; `NameKey` = `Category_<MemberName>` (matches existing resw keys); `Order` = declaration order; icon = `Assets/Icons/Categories/<MemberName>.png`.
- **`ToolGroup`** members (optional "uber-category"): `NameKey` = `Group_<MemberName>`; `Order` = declaration order; groups render as text headers (no icon).
- **Category→group membership** is the one relationship a member name can't express; it is held in a small explicit `ToolCategory → ToolGroup?` map next to the enums (the deliberate, minimal exception to "convention only"). Initial release defines at least one group covering ≥2 categories so US3/SC-003 (a group heading over its categories) can be exercised; remaining categories stand alone.

**Rationale**: The owner chose "convention only" for enum metadata — least authoring, no second source to sync. Member name → key/icon/order is fully deterministic. Group aggregation is inherently a relationship, not a property of one member, so a tiny map is unavoidable; keeping it beside the enum preserves the "one place" spirit.

**Alternatives**: (a) `[CategoryInfo(...)]`/`[GroupInfo(...)]` attributes on members — rejected (owner chose convention). (b) A full static metadata table — rejected for the same reason; only the irreducible group map remains.

---

## R7 — Icons: convention-path bitmaps, runtime placeholder fallback, **not** build-validated

**Decision**: Tool and category icons are bitmap assets resolved **by convention** from the `Id`/enum member: `Assets/Icons/Tools/<Id>.png` and `Assets/Icons/Categories/<Category>.png` (in the app head, where the rendering `DataTemplate` lives). The `ToolDescriptor`/`Category` carry an icon **key** (the `Id`/category name); the app head resolves it to `ms-appx:///Assets/Icons/Tools/<key>.png` at render time. A missing bitmap falls back to a runtime placeholder. **Icon existence is NOT validated at build time and never breaks the build.**

**Rationale**: The owner explicitly said "we don't have to break the build due to icons," with the convention path above. Keeping the icon as a logical key keeps Core UI-agnostic (no asset coupling), and runtime fallback is graceful as the icon set is curated.

**Alternatives**: (a) Generator validates asset existence via `AdditionalFiles` and errors — rejected by the owner. (b) Explicit icon path in the attribute — rejected: convention from `Id`/enum is enough. (c) Font glyphs — out of scope per spec.

**Spec impact**: This **relaxes FR-008/FR-008a** ("a tool/category with no icon MUST be a build-breaking error") to a runtime placeholder fallback, and adjusts SC-002/SC-005 accordingly. Flagged for a spec update.

---

## R8 — Diagnostics honor `WarningsAsErrors=True`: structural problems **and** missing localization resources are build **errors**

**Decision**: `src/Directory.Build.props` sets `<WarningsAsErrors>True</WarningsAsErrors>` repo-wide, so any plain compiler warning already fails the build. We lean into that rather than fight it:
- **Build-breaking errors**: `[Tool]` on an invalid target (not a `ToolViewModelBase`-derived class the shell can host); missing required attribute value; an `Introduced`/`Updated` value that isn't a valid ISO `yyyy-MM-dd` date; **and a missing `<Id>_Name` or `<Id>_Tooltip` resource** (the generator reads the `.resw` files via `AdditionalFiles` and errors if a key is absent in any required culture).
- **Not a diagnostic**: a missing icon bitmap (R7) — silent runtime placeholder.
- **Startup error (unchanged)**: duplicate `Id` — the existing `CatalogService` ctor check (FR-004).
- Enum range "violations" are impossible: `ToolCategory`/`ToolGroup` are real enums, so an out-of-range value won't compile.

**Rationale**: The owner chose "let them be errors (honor `WarningsAsErrors`)." With `WarningsAsErrors=True`, emitting these as Roslyn *warnings* would break the build anyway; making them explicit *errors* with actionable messages is clearer than fragile Info-severity gymnastics. Fast, fail-hard feedback protects catalog/pane integrity.

**Alternatives**: (a) Info/suggestion severity (never fails) — rejected by the owner. (b) Real warnings + a `WarningsNotAsErrors` allowlist — rejected; more config, weaker guarantee.

**Spec impact**: This **overrides FR-015 / SC-005** ("non-fatal content gaps → warnings that still build"): missing name/tooltip resources are now **errors**, not warnings. Combined with R7, the spec's structural-error-vs-content-warning split collapses to "everything detectable is an error, icons excepted." Flagged for a spec update.

---

## R9 — "New & Updated": date-driven classification, WinUI-Gallery-style badge + Home section

**Decision**: The `[Tool]` attribute carries `Introduced` and `Updated` ISO dates (string literals; attributes can't hold `DateOnly`). The generator parses/validates them (R8) and emits `IntroducedDate`/`UpdatedDate` on the descriptor. A Core `ToolRecencyClassifier` computes a state from the dates vs. a **recency window** (a configurable constant; default **30 days**, evaluated against the current date):
- introduced within window → **New**
- introduced older but updated within window → **Updated**
- otherwise → **None**

Presentation mirrors WinUI Gallery: `ToolListItem.BadgeString` (`"New"`/`"Updated"`/empty, like their `ControlInfoDataSource` switch) renders as a small badge on the tool card; `HomeViewModel` exposes a **"New & Updated"** collection (`tools where state != None`), the analog of WinUI Gallery's `RecentlyAddedOrUpdatedSamplesList`.

**Rationale**: The owner asked to build this "in a similar fashion to WinUI Gallery." WinUI Gallery uses hand-set `IsNew`/`IsUpdated` booleans; our only difference is deriving those flags from authored dates against a window (the spec's date-driven requirement, FR-011, SC-004). Edge cases follow the spec: future dates are not surfaced until reached; `Updated` earlier than `Introduced` is a data inconsistency (the classifier treats it as introduced-only; flagged, not fatal).

**Alternatives**: (a) Data-only (expose dates, no classifier/badge) — rejected; SC-004 needs classification and the owner wants the surface. (b) A full standalone "What's New" page — heavier than the WinUI-Gallery Home-section pattern and overlaps app-head UI the spec calls "reused"; deferred.

---

## R10 — Navigation hosting: stub tools share `ToolHostView`, which hosts the concrete tool VM resolved at navigation

**Decision**: Each stub tool VM derives `ToolViewModelBase` (carrying the host behavior `ToolHostViewModel` has today: resolve its descriptor by `Id`, set name/tooltip/title, record recents, expose favorite toggle, `IsPlaceholder`). Navigation stays view-model-first: opening a tool is `Navigate<TToolViewModel>()`. The generator (R2) maps every discovered tool VM → the single shared `ToolHostView`. `ToolHostView` is adapted from `ViewBase<ToolHostViewModel>` (which hard-binds one VM type) into a **host that resolves and binds the concrete navigated tool VM type** (the VM type is conveyed as the navigation target/parameter and resolved from DI), so one shared view can host any tool VM.

**Rationale**: Authoring per-tool pages is out of scope, yet the spec requires the attribute on each tool's own VM and view-model-first navigation to that VM (FR-009). `ViewBase<T>` resolves a fixed `T` from DI, so a shared view can't host different VMs unaltered — this is the trickiest design point and is called out as such. WinUI Gallery sidesteps it by giving every sample its own `Page`; we can't (no per-tool pages), so we invert it: one host view, many VM types.

**Alternatives**: (a) One generated trivial `Page` per tool deriving `ViewBase<ThatVM>` — rejected: the generator must not emit XAML/UI (FR-013), and it's per-tool view authoring by another name. (b) Keep navigating to a single `ToolHostViewModel` + `toolId` (today's model) — rejected: then the `[Tool]` attribute can't decorate a per-tool VM as the spec requires. (c) A marker-interface runtime fallback in `NavigationService` — overlaps the rejected R2(b).

---

## R11 — Category pane node: expandable **and** clickable `NavigationViewItem`

**Decision**: Each category renders as a `NavigationViewItem` that (a) holds its tools as child `MenuItems` so the **chevron expands/collapses** them inline, and (b) is itself **invocable** (`SelectsOnInvoked`/`ItemInvoked`) so a body click opens the category-scoped Catalog. Expand and invoke are independent: toggling the chevron does not navigate; clicking the body does not require expanding. `ToolGroup` renders as a non-selectable `NavigationViewItemHeader` above its categories. Empty categories/groups are omitted.

**Rationale**: This is the WinUI-Gallery-style expandable-yet-invokable nav item the spec describes (FR-019/FR-021, US3). `NavigationView` natively supports hierarchical items + `ItemInvoked`, so this needs no custom control — only careful wiring so the chevron-toggle path and the invoke path don't interfere.

**Alternatives**: (a) Category as a pure expander (not clickable) with a separate "open category" affordance — rejected: spec wants both on the same node. (b) A bespoke templated control — unnecessary; built-in `NavigationView` suffices.

---

## R12 — Category-scoped Catalog **ignores** the global search

**Decision**: Clicking a category navigates `Navigate<CatalogViewModel>(categoryId)`. `CatalogViewModel.OnNavigatedTo(categoryId)` enters a **scoped mode**: it shows exactly that category's tools and **does not subscribe to / apply `SearchViewModel.Query`**. The unscoped Catalog and Home entry points keep their existing live-search behavior.

**Rationale**: The owner chose "independent — scoped view ignores global search." A category-scoped view is a deliberate drill-in; mixing a stale global query into it is surprising. Keeping scope and search independent is the simplest, most predictable behavior and avoids reconciling two filters.

**Alternatives**: (a) Compose (search filters within the scoped category) — rejected by the owner. (b) Override (a query broadens back to all tools) — rejected; muddies the drill-in.

---

## R13 — Deterministic generated code; WinUI-Gallery csproj patterns

**Decision**: The single generator project `GcToolkit.SourceGenerators` targets `netstandard2.0`, `IsRoslynComponent=true`, `EnforceExtendedAnalyzerRules=true`, `IncludeBuildOutput=false`, `Nullable=enable`, referencing `Microsoft.CodeAnalysis.CSharp` (4.x) + `Microsoft.CodeAnalysis.Analyzers` (PrivateAssets=all) — mirroring `WinUIGallery.SourceGenerator.csproj`. We adopt their `StringBuilder` + `AppendAutoGeneratedHeader` pattern **but emit a static `<auto-generated/>` header with no timestamp** (their `AppendFullHeader` embeds `DateTime.Now`). No `System.Text.Json`-as-analyzer trick is needed (attribute-based, not JSON). Discovery is via referenced-assembly metadata (R2), so the discovery transform is keyed on `CompilationProvider` (coarser incrementality, fine at this scale). Generator unit tests use the Roslyn source-generator testing harness (MSTest variant), feeding the attributed VMs as a **referenced compilation**, to snapshot generated output and assert each diagnostic.

**Rationale**: A timestamp in generated output defeats incremental-generator caching and breaks reproducible builds — a concrete improvement over the reference. The rest of the csproj recipe is proven by WinUI Gallery. `WarningsAsErrors=True` makes generator-test coverage of diagnostics valuable (a stray warning anywhere fails CI).

**Alternatives**: (a) Copy WinUI Gallery's timestamped header verbatim — rejected (non-deterministic). (b) `ForAttributeWithMetadataName` syntax discovery — not usable here because the attributed VMs are in a referenced assembly, not the app's source (R2); metadata scanning is the deliberate consequence of running one generator against the app head.

---

## Open assumptions (low-risk defaults, override if desired)

- **Recency window** default = **30 days**, as a single configurable constant owned by the shell/Core classifier (R9).
- **Icon assets physically live in the app head** (`GcToolkit/Assets/Icons/...`) beside the rendering `DataTemplate`, even though tool VMs are in Core (R7) — Core carries only the logical key.
- **Initial `ToolGroup`** defines one real group spanning ≥2 of the four categories purely to exercise US3/SC-003; the exact grouping is a small, easily-changed map (R6).
- **Spec deltas to record** (R7, R8): FR-008/FR-008a (icon not build-breaking) and FR-014/FR-015 + SC-002/SC-005 (missing resources are errors, not warnings). Recommend running `/speckit-clarify` or editing the spec so artifacts stay consistent.
