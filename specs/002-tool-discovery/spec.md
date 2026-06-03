# Feature Specification: Attribute-Based Tool Auto-Discovery & Navigation Tree

**Feature Branch**: `002-tool-discovery`

**Created**: 2026-05-30

**Status**: Draft

**Input**: User description: "Auto-discover individual tools based on attribute (GitHub issue #71). Each tool should have an attribute by which it will be auto-discovered, including a localization key (display name + tooltip), a tool category, the date the tool was introduced/last updated (for the recents section), and a tool icon. Use a C# source generator to auto-register the tools based on the attribute (manual registration is the fallback, auto is preferred). The category hierarchy should also be shown in the navigation view pane, built by a C# source generator that runs against `GcToolkit.Core` to find all tools and build the tree (similar in spirit to `D:\Work\uno\src\SamplesApp\SamplesApp.UITests.Generator`). Inspiration also drawn from the Windows Community Toolkit (source-generator sample registration via `[ToolkitSample]`) and the WinUI Gallery (catalog metadata model with New/Updated badges)."

## Context: How this builds on the existing app shell (feature 001)

The app shell foundation (feature `001-app-shell-foundation`) already ships a working catalog, navigation shell, and the services that consume them. This feature changes **how tools get into the catalog**, **what metadata they carry**, and **adds the discovered category hierarchy to the navigation pane** — it does not rebuild the catalog page UI, recents, favorites, or search.

What already exists and is reused:

- **`ToolDescriptor`** (`Id`, `NameKey`, `CategoryId`, `Keywords`, `IconKey`, `ViewModelType`, `IsPlaceholder`) and **`Category`** (`Id`, `NameKey`, `Order`, `IconKey`) records.
- **`ICatalogService` / `CatalogService`** — aggregates contributed tools/categories, validates them (duplicate id, unknown category) **at startup**, orders deterministically, and runs search.
- **`IToolContributor` / `ICategoryContributor`** — the current registration mechanism (DI-resolved). `PlaceholderToolContributor` supplies SP-1 sample tools and is meant to be removed as real tools land.
- **`WindowShell` + `NavigationView`** — the app shell (in the `GcToolkit` head). Today the pane has two static items: **Home** and **Catalog** (plus the system Settings item). Selection maps a `Tag` to a `NavigationSection` and navigates. There are **no** per-category or per-tool items in the pane yet.
- **View-model-first navigation** — tools open via `INavigationService.Navigate<TViewModel>(parameter)`; descriptors carry a `ViewModelType`; the navigation service maps a ViewModel type to its view.
- **`IRecentsService` / `RecentsService`** — recently-*used* tracking (deduped, capped at 10, persisted via `IPreferences`). **Already complete.**
- **`IFavoriteToolsService`** — favorite tools. Already complete.
- **Localization** — `NameKey` resolved through `IStringLocalizer` / the `{markup:Localize}` extension.
- **ViewModel locations (today)** — `ViewModelBase` and `WindowShellViewModel` live in `GcToolkit.Core`; the page/tool ViewModels (`CatalogViewModel`, `HomeViewModel`, `SearchViewModel`, `SettingsViewModel`, `ToolHostViewModel`) currently live in the `GcToolkit` app head.

What this feature changes or adds:

1. **Auto-discovery via attribute + source generator** replaces hand-written `IToolContributor` classes and DI registration lines for first-party tools.
2. **Richer tool metadata** on the descriptor: a **tooltip** (today only a display name exists), an **introduced date** and a **last-updated date** (for a "New & Updated" surface — distinct from the existing recently-*used* list).
3. **Closed enumerations for the grouping hierarchy**: a required **category** (which directly contains tools) and an optional **group ("uber-category")** that aggregates related categories. (Today categories are free strings contributed at runtime; no group level exists.)
4. **A bitmap icon** sourced from Icons8, **required for every tool and every category** (today `IconKey` is an optional glyph string on both `ToolDescriptor` and `Category`).
5. **Build-time validation** of tool metadata (missing required values, invalid attribute target, value outside the grouping enums). Duplicate-`Id` detection intentionally stays at app startup, where `CatalogService` already throws today.
6. **The discovered category hierarchy is shown in the navigation pane.** A source generator that runs **against `GcToolkit.Core`** finds all attributed tools and emits a UI-agnostic group→category→tool tree; the existing `WindowShell` renders it into `NavigationView` (groups as section headings, categories holding their tools, selecting a tool opens it). **Each category node is both expandable and clickable**: expanding (its chevron) reveals the tools inline, while clicking the category itself opens a Catalog view scoped to that category's tools.
7. **Tool ViewModels live in `GcToolkit.Core`** so the generator (running against Core) can see them; per the project owner, the page ViewModels are also consolidated into Core.

## Clarifications

### Session 2026-05-30

- Q: What should this feature deliver, given the catalog already exists? → A: The **discovery mechanism and metadata contract** that feeds the existing `CatalogService` — i.e., the attribute, the build-time source generator that auto-registers tools, the additional descriptor metadata, **and the generated category tree shown in the navigation pane.** The catalog page UI, recents, favorites, and search already exist and are reused, not rebuilt.
- Q: What does the date-driven "recents" represent? → A: **Both** — but recently-*used* already exists (`RecentsService`); the **new** part is a "New & Updated" surface driven by the authored introduced/last-updated dates plus per-tool "New"/"Updated" badges.
- Q: How is the grouping modeled? → A: **Predefined closed sets (enumerations).** A required **category** (the level that directly contains tools) and an optional **group / "uber-category"** that aggregates categories. *(This supersedes the earlier "subcategory" framing: the optional level is now a parent heading above categories, not a child nested below a category — matching how it should read in the navigation pane.)*
- Q: How is a tool's icon specified, and is one required? → A: **Bitmap asset only** (curated from Icons8) for now; font-glyph icons are out of scope. An icon is **required for every tool**.
- Q: Do categories have icons? → A: **Yes — every category MUST have an icon too** (same Icons8 bitmap approach). Category icons are shown next to category nodes in the navigation pane (and wherever categories are presented). This makes the existing optional `Category.IconKey` a required field for first-party categories. (Groups/"uber-categories" render as text headings; a group icon is not required.)
- Q: How does the single localization key map to name + tooltip? → A: **One base key (the tool `Id`) → two derived resources by convention** (`<Id>_Name` and `<Id>_Tooltip`).
- Q: What does the attribute decorate / what is the entry point? → A: The tool's **ViewModel** (matching the existing view-model-first navigation); the generated registration captures that ViewModel type as the descriptor's `ViewModelType`.
- Q: How wide must discovery reach? → A: **Single project (`GcToolkit.Core`) now**, designed so it can later aggregate tools across multiple referenced assemblies without changing the attribute.
- Q: What is the tool's identity field? → A: A single **`Id`** that also serves as the localization base key (reusing the existing `ToolDescriptor.Id`).
- Q: How should invalid metadata be handled? → A: **Structural problems are build-breaking errors; content gaps are warnings** (and degrade gracefully at runtime). **Exception:** duplicate `Id` is **not** required to be a build error — the existing `CatalogService` startup check already throws on duplicates, and that runtime failure is sufficient.
- Q: Where does the navigation-tree generator run, and where do tool ViewModels live? → A: The generator runs **against `GcToolkit.Core`**; **all (tool) ViewModels live in `GcToolkit.Core`** so the generator can see them.
- Q: What should the generator emit for the pane? → A: A **data-only, UI-agnostic tree** (group → category → tool). The existing `WindowShell` builds the `NavigationView` items from it at runtime (mirrors the Uno `SamplesListGenerator`, which emits data the app renders).
- Q: How should tools appear in the pane? → A: **Categories with their tools inside.** A higher-level "uber-category" (group), when present, renders as a **section heading** (`NavigationViewItemHeader`) above its categories.
- Q: What happens when a category itself is interacted with? → A: **Each category is both expandable and clickable.** Expanding the category (its chevron) reveals its tools inline in the pane; clicking the category body opens a **Catalog view scoped to that category** (the existing Catalog page filtered to that category's tools). Both affordances coexist on the same category node.
- Q: Separate spec or same? → A: **Same spec (002)** — the nav tree shares the attribute, enums, and generator with tool discovery.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Add a tool with a single attribute, no registration (Priority: P1)

A tool developer creates a new tool's ViewModel in `GcToolkit.Core` and decorates it with the discovery attribute (supplying an `Id`, a category, an icon, and the introduced/updated dates). After building, the tool appears in the existing catalog — with **no** hand-written `IToolContributor`, **no** DI registration line, and **no** edit to any shared/central file.

**Why this priority**: This is the core value of issue #71 and the whole point of the feature. It is the minimum viable product: it removes the manual contributor step that exists today and is what every later capability builds on.

**Independent Test**: Add one attributed tool ViewModel, build, and confirm the catalog (via `ICatalogService.GetTools()`) returns that tool with its metadata, that it can be opened, and that no contributor class or DI line was added.

**Acceptance Scenarios**:

1. **Given** a tool ViewModel decorated with the discovery attribute carrying valid metadata, **When** the app is built and started, **Then** the tool is present in the catalog and opening it navigates to that ViewModel.
2. **Given** an attributed tool, **When** the developer removes the attribute and rebuilds, **Then** the tool no longer appears in the catalog.
3. **Given** the placeholder sample tools are replaced by real attributed tools, **When** the app is built, **Then** the catalog is populated entirely from auto-discovered tools with no `PlaceholderToolContributor` required.

---

### User Story 2 - Localized name and tooltip from one key (Priority: P1)

Each tool's display name and tooltip are resolved from localization resources derived from its `Id` by convention (`<Id>_Name`, `<Id>_Tooltip`), so the catalog and navigation pane show fully localized names and tooltips in every supported language (currently English and Czech).

**Why this priority**: Names are required for the catalog and pane to be usable at all, and the project is localized from day one. Tooltips are a new, required part of the issue's metadata. This rounds out the minimum viable descriptor.

**Independent Test**: Provide `<Id>_Name` and `<Id>_Tooltip` resources for a tool in EN and CS, switch language, and confirm the resolved name and tooltip change accordingly.

**Acceptance Scenarios**:

1. **Given** a tool whose `<Id>_Name` and `<Id>_Tooltip` resources exist, **When** the catalog or pane is shown, **Then** the localized name and tooltip are displayed for the current language.
2. **Given** the user switches the app language, **When** the catalog or pane is shown again, **Then** the name and tooltip reflect the newly selected language.

---

### User Story 3 - Browse the category hierarchy in the navigation pane (Priority: P1)

A user opens the app and sees the tool hierarchy directly in the navigation pane: each **category** lists its **tools** as selectable items; where a higher-level **group ("uber-category")** exists, it appears as a non-selectable section heading above its categories. A category node is **both expandable and clickable** — expanding it reveals its tools inline, clicking it opens a Catalog view scoped to that category. Selecting a tool opens it; the existing Home and Catalog entries remain.

**Why this priority**: Showing the hierarchy in the pane is the headline of this request and the primary way users discover and reach tools. It depends on discovery (US1) and grouping metadata, and is the user-visible payoff of the generator.

**Independent Test**: Register attributed tools across two categories (one of them under a group), build, and confirm the pane shows the group heading, the categories, and the tools nested under each — that expanding a category reveals its tools, that clicking a category opens its scoped catalog, and that selecting a tool navigates to it.

**Acceptance Scenarios**:

1. **Given** attributed tools spanning multiple categories, **When** the shell loads, **Then** the navigation pane shows each category with its tools listed inside, in a deterministic order, beneath the existing Home and Catalog items.
2. **Given** a category that belongs to a group, **When** the pane is shown, **Then** the group appears as a section heading above that category.
3. **Given** a category with no group, **When** the pane is shown, **Then** its tools appear without any heading above them.
4. **Given** a tool item in the pane, **When** the user selects it, **Then** the app navigates to that tool and the pane selection reflects the active tool.
5. **Given** a category node in the pane, **When** the user expands it (its chevron), **Then** the category's tools are revealed inline without navigating away.
6. **Given** a category node in the pane, **When** the user clicks the category itself, **Then** the app opens a Catalog view scoped to that category's tools.
7. **Given** a new attributed tool is added and the app rebuilt, **When** the shell loads, **Then** the tool appears in the pane under its category with no edit to `WindowShell` or any pane definition.

---

### User Story 4 - Discover what's new and recently updated (Priority: P2)

Users can see which tools were recently introduced or updated, via a "New & Updated" surface and per-tool "New"/"Updated" badges, based on the authored introduced/last-updated dates. (This is separate from the existing recently-*used* list, which continues to work as today.)

**Why this priority**: Surfacing new and improved tools drives discovery as the toolbox grows. It depends on the new date metadata from US1 but is additive to the catalog.

**Independent Test**: Register tools with introduced/updated dates inside and outside the recency window and confirm the catalog exposes the dates such that the shell can mark and group "New" vs "Updated" vs neither.

**Acceptance Scenarios**:

1. **Given** a tool whose introduced date is within the recency window, **When** the catalog is shown, **Then** the tool is identifiable as "New".
2. **Given** a tool whose last-updated date is within the recency window but whose introduced date is older, **When** the catalog is shown, **Then** the tool is identifiable as "Updated" (not "New").
3. **Given** a tool whose introduced and updated dates are both older than the recency window, **When** the catalog is shown, **Then** the tool is marked as neither.

---

### User Story 5 - Catch metadata mistakes at build time (Priority: P2)

A developer who makes a metadata mistake (missing required value, attribute on an invalid target, a category/group outside the enum) gets a **build-breaking error** that pinpoints the problem; a non-fatal content gap (e.g., a missing localized name/tooltip resource) produces a **warning** but still builds. A duplicate `Id` is not required to be caught at build time — it continues to fail at app startup via the existing `CatalogService` check.

**Why this priority**: Fast, compile-time feedback protects catalog/pane integrity and developer productivity, and is what makes auto-discovery trustworthy as the toolbox scales.

**Independent Test**: Introduce each error/warning condition and confirm the build fails (errors) or succeeds with a diagnostic (warnings) as specified.

**Acceptance Scenarios**:

1. **Given** a tool missing a required metadata value (e.g., no category, no icon, no introduced date), **When** the project is built, **Then** the build fails with an error naming the missing value and tool.
2. **Given** a category missing its required icon, **When** the project is built, **Then** the build fails with an error naming the category.
3. **Given** the attribute placed on a type that is not a valid tool entry point (e.g., not a ViewModel the shell can navigate to), **When** the project is built, **Then** the build fails with an explanatory error.
4. **Given** a category or group value outside the predefined enum, **When** the project is built, **Then** the build fails with an explanatory error.
5. **Given** a tool whose `<Id>_Name` or `<Id>_Tooltip` resource is missing, **When** the project is built, **Then** the build succeeds with a warning and the name falls back to a sensible default at runtime.
6. **Given** two tools declaring the same `Id`, **When** the app starts, **Then** startup fails with an error identifying the duplicate (build-time detection is not required).

---

### Edge Cases

- **Duplicate `Id`** (within the project, or across assemblies in the future) → startup `InvalidOperationException` via the existing `CatalogService` check (build-time detection not required).
- **Category required but absent**, or a category/group value outside the closed enum → build-breaking error.
- **Missing `<Id>_Name` / `<Id>_Tooltip` resource** → **build-breaking error** (`GCTOOL004`), reconciled with research R8 — every tool's name and tooltip must exist in every supported culture.
- **`Id` not resource-key-safe** (the `Id` is the localization base key) → build-breaking error (`GCTOOL001`); the existing dotted placeholder ids (e.g., `coordinates.conversion`) were reconciled to PascalCase (`CoordinateConversion`).
- **Tool or category icon bitmap missing** → **not** a build error (reconciled with research R7); the icon key resolves by convention and a missing asset falls back to a runtime placeholder.
- **Introduced/updated date in the future** → the tool is not surfaced as New/Updated until that date is reached.
- **Updated date earlier than introduced date** → treated as a data inconsistency (`GCTOOL010`, Info severity); the tool is still discoverable.
- **A discovered tool that is opened** feeds the existing recently-used list unchanged; a recents entry for a tool no longer discovered is ignored (existing behavior).
- **Empty category** (a category enum value with no tools) → not shown in the navigation pane (no empty nodes/headings).
- **Group with no populated categories** → its heading is not shown.
- **Tool opened from the pane vs. from the Catalog page** → both reach the same tool via the same view-model-first navigation and record recents identically.
- **Category expanded vs. category clicked** → expanding (the chevron) only reveals/hides the tools inline and does not navigate; clicking the category body opens its scoped Catalog view. The two must not interfere.
- **Category with a single tool** → still expandable to that one tool and still clickable to its scoped catalog (no special-casing required; planning may choose to collapse trivial cases).
- **Scoped Catalog view + active search query** → opening a category's catalog scopes to that category; how it interacts with any in-progress global search query is a planning/design detail (e.g., scope overrides or composes with the query).
- **No attributed tools yet** → the catalog is empty and the pane shows only Home/Catalog rather than failing; the placeholder contributor may bridge until real tools exist.
- **Large catalog** → discovery and tree-building add no per-tool runtime reflection cost; pane ordering stays deterministic.

## Requirements *(mandatory)*

### Functional Requirements

#### Discovery & metadata

- **FR-001**: The system MUST automatically discover every tool whose ViewModel (in `GcToolkit.Core`) is annotated with the tool-discovery attribute and register it into the existing catalog, with no hand-written contributor class and no DI registration line per tool.
- **FR-002**: Adding a newly attributed tool MUST make it appear in both the catalog and the navigation pane without modifying any shared or central file (only the tool's own ViewModel and its resource strings change).
- **FR-003**: The discovery attribute MUST capture, for each tool: a unique identifier (`Id`), a category, an icon, an introduced date, and a last-updated date. The category MAY belong to an optional group ("uber-category").
- **FR-004**: Each tool's `Id` MUST be unique across all discovered tools. A duplicate `Id` MUST cause an app-startup failure (the existing `CatalogService` check is sufficient); build-time detection of duplicates is NOT required.
- **FR-005**: The `Id` MUST serve as the localization base key; the display name and tooltip MUST resolve from resources derived from the `Id` by a fixed convention (`<Id>_Name`, `<Id>_Tooltip`).
- **FR-006**: Category and group values MUST come from predefined, closed enumerations; free-form values MUST NOT be accepted.
- **FR-007**: Each tool MUST declare exactly one category; the category's membership in a group is optional (a category either belongs to one group or stands alone).
- **FR-008**: Each tool SHOULD declare an icon as a bundled bitmap asset (curated from Icons8); the system MUST expose the icon reference (the tool `Id`, resolved by convention to `Assets/Icons/Tools/<Id>.png`) for the catalog and pane to display. **(Reconciled with research R7)** A missing icon bitmap is **not** a build-breaking error; it falls back to a runtime placeholder so the icon set can be curated incrementally.
- **FR-008a**: Each category SHOULD declare an icon as a bundled bitmap asset (curated from Icons8); the system MUST expose it (the category member name, resolved to `Assets/Icons/Categories/<Member>.png`) so the category is shown with its icon in the navigation pane. **(Reconciled with research R7)** A missing icon bitmap is **not** a build-breaking error; it falls back to a runtime placeholder. (A group/"uber-category" icon is not required; groups render as text headings.)
- **FR-009**: The system MUST expose each discovered tool's entry point (its ViewModel type) so the existing navigation can open the tool, consistent with the current view-model-first navigation.
- **FR-010**: Discovered tools MUST flow into the existing `ICatalogService` so that categories, grouping, ordering, and search on the Catalog page continue to work unchanged.
- **FR-011**: The system MUST expose each tool's introduced and last-updated dates so the shell can mark tools "New"/"Updated" and present a "New & Updated" grouping based on a recency window.

#### Source generator

- **FR-012**: Tool discovery and tree-building MUST happen at build/compile time via a C# source generator that runs against `GcToolkit.Core` (no runtime reflection-based assembly scanning), so app startup time does not grow as tools are added.
- **FR-013**: The generator MUST emit a UI-agnostic representation of both (a) the tool registry consumed by `ICatalogService` and (b) the navigation hierarchy (group → category → tool). It MUST NOT emit UI controls or XAML; rendering is the shell's responsibility.
- **FR-014**: Structural metadata problems — missing required values, an attribute on an invalid target, or a category/group outside the enum — MUST be reported as build-breaking errors with actionable messages. (Duplicate `Id` is excluded; it is handled at startup per FR-004.)
- **FR-015**: **(Reconciled with research R8)** A missing localized name (`<Id>_Name`) or tooltip (`<Id>_Tooltip`) resource in any supported culture MUST be reported as a **build-breaking error** (`GCTOOL004`) with a message naming the tool, the missing key, and the culture — the project favours fail-hard feedback over silent runtime fallback for required content. (A missing *icon bitmap* remains non-fatal per FR-008/008a.)
- **FR-016**: The generated registration and tree MUST refresh on the next build with no additional manual steps when tool metadata changes.
- **FR-017**: The attribute, generated registration, and generated tree MUST be designed so discovery can extend from the single `GcToolkit.Core` project today to multiple referenced assemblies later, without changing the attribute's shape.
- **FR-018**: Manual registration (a hand-written contributor) MUST remain possible as a fallback for special cases, but auto-discovery MUST be the default path; the existing placeholder contributor MUST be removable once real attributed tools exist.

#### Navigation pane

- **FR-019**: The navigation pane MUST present the discovered hierarchy: each populated category lists its tools as selectable items; an optional group renders as a non-selectable section heading above its categories. The existing Home and Catalog items (and the system Settings item) MUST remain.
- **FR-020**: Selecting a tool in the pane MUST navigate to that tool via the existing view-model-first navigation and MUST record the open in the existing recently-used tracking, identically to opening it from the Catalog page.
- **FR-021**: Each category node MUST be **expandable** — expanding it reveals that category's tools inline in the pane — **and clickable** — clicking the category itself MUST open a Catalog view scoped to that category's tools. The two affordances MUST be independently operable on the same node (expanding does not navigate; clicking the category does).
- **FR-022**: The category-scoped Catalog view MUST show exactly that category's tools, reusing the existing Catalog page (grouping, ordering, search, favorites) rather than a new screen.
- **FR-023**: The pane MUST present categories, groups, and tools in a deterministic, stable order derived from the metadata (consistent with the existing catalog ordering).
- **FR-024**: Empty categories and groups (no discovered tools) MUST NOT appear in the pane.
- **FR-025**: The pane MUST reflect the current selection (highlighting the active tool or the active category's scoped catalog) and MUST localize category, group, and tool labels via the existing localization mechanism.

### Key Entities *(include if data involved)*

- **Tool Discovery Attribute** *(new)*: Declared on a tool's ViewModel in `GcToolkit.Core`. Carries `Id` (unique, also localization base key), Category, Icon (bitmap asset), Introduced date, and Last-updated date.
- **`ToolCategory` enumeration** *(new, required level)*: The predefined closed set of categories that directly contain tools (initial set mirrors today's: Coordinates, Ciphers, Numbers, Field). Each value maps to a stable id, a localization key, an order, and a **required icon** (Icons8 bitmap) shown next to the category in the pane.
- **`ToolGroup` enumeration** *(new, optional "uber-category" level)*: The predefined closed set of higher-level groupings that aggregate categories and render as pane section headings. A category optionally belongs to one group. Each value maps to a stable id, a localization key, and an order.
- **`ToolDescriptor`** *(existing, extended)*: The catalog's per-tool record produced by the generator; extended to carry a tooltip key, introduced/last-updated dates, and a bitmap icon reference.
- **Generated tool registration** *(new)*: Build-time output that feeds discovered tools (and their categories/groups) into `ICatalogService` — conceptually a generated contributor/registry — replacing hand-written contributors.
- **Generated navigation tree** *(new)*: Build-time, UI-agnostic group→category→tool model the `WindowShell` consumes to build `NavigationView` items (groups as headings, categories as expandable + clickable nodes, tools as selectable items).
- **Category-scoped Catalog view** *(existing page, new entry mode)*: The existing Catalog page opened filtered to a single category, reached by clicking a category node in the pane. Reuses the current grouping/ordering/search/favorites; requires a way to pass the target category to it (planning task).
- **Recently-used list** *(existing)*: `RecentsService` continues to track opened tools; this feature feeds it from both the pane and the Catalog page.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer can add a fully working catalog **and** navigation-pane entry for a new tool by changing only that tool's own ViewModel and its resource strings — **zero** edits to any shared registration/DI file or to `WindowShell` — verified across at least three separate tool additions.
- **SC-002**: 100% of discovered tools present a localized display name, a localized tooltip, and a category in both the catalog and the pane (the localized name/tooltip are build-enforced per FR-015); each tool and category shows its bitmap icon when the asset is present and a placeholder otherwise (FR-008/008a, R7).
- **SC-003**: The navigation pane renders the discovered hierarchy correctly — groups as headings, categories holding their tools, deterministic order, no empty nodes — and: selecting any tool opens it, expanding a category reveals its tools inline without navigating, and clicking a category opens a Catalog view containing exactly that category's tools. Verified for a sample spanning at least two categories and one group.
- **SC-004**: For a set of tools with known introduced/updated dates, the system classifies each as "New", "Updated", or neither, matching the recency window in 100% of cases.
- **SC-005**: Each build-time error condition fails the build with a message naming the offending tool or culture: missing required tool value / non-resource-safe `Id` (`GCTOOL001`), attribute on an invalid target (`GCTOOL002`), invalid ISO date (`GCTOOL003`), and a missing `<Id>_Name`/`<Id>_Tooltip` resource (`GCTOOL004`, reconciled per R8). A duplicate `Id` fails at app startup. A missing icon bitmap is **not** an error (R7) — verified for every listed condition.
- **SC-006**: The catalog and pane can be populated entirely by auto-discovered tools with the placeholder contributor removed, and existing recents, favorites, and search continue to pass their tests against the discovered catalog.
- **SC-007**: Measured app startup time does not increase as the number of tools grows (no per-tool runtime scanning), staying within normal launch expectations as the catalog scales from a handful to dozens of tools.
- **SC-008**: A newly added, correctly attributed tool appears in both the catalog and the navigation pane after a single rebuild, with no other build or configuration steps.

## Assumptions

- **Builds on feature 001**: This feature targets the existing `GcToolkit.Core` catalog layer and the `GcToolkit` app head (`WindowShell`/`NavigationView`). It supplies the discovery mechanism, metadata, and generated navigation tree; it reuses the existing catalog page UI, navigation service, localization, recents, favorites, search, and `IPreferences` persistence.
- **Chosen mechanism**: Per issue #71, auto-registration is implemented with a **compile-time C# source generator** (an incremental generator using attribute-based discovery — e.g. `ForAttributeWithMetadataName`, `Collect`, `RegisterSourceOutput` — modeled on the Uno `SamplesApp.UITests.Generator`'s `SamplesListGenerator`, which emits typed data the app renders). It emits the registration consumed by `ICatalogService` and the navigation tree consumed by `WindowShell`. Manual contributors remain a supported fallback. The functional requirements are written to be verifiable regardless of mechanism.
- **Generator placement & ViewModel location**: The generator runs **against `GcToolkit.Core`**. **All (tool) ViewModels live in `GcToolkit.Core`** so the generator can see them; per the project owner, the existing page ViewModels (`CatalogViewModel`, `HomeViewModel`, `SearchViewModel`, `SettingsViewModel`, `ToolHostViewModel`) are also consolidated into Core. Moving those (and their current app-head dependencies such as `Localizer.Instance` / `ToolListItem`) into Core is a planning task. The generator project itself targets `netstandard2.0` (analyzer requirement) and is referenced by `GcToolkit.Core` as an analyzer.
- **Attribute target**: The attribute decorates each tool's **ViewModel**; the generated descriptor's `ViewModelType` is that ViewModel, matching the current view-model-first navigation (`Navigate<TViewModel>` → registered view).
- **Localization convention**: The `Id` is the localization base key; name and tooltip resolve from `<Id>_Name` and `<Id>_Tooltip` resources (`.resw`, EN + CS) via the existing `IStringLocalizer`/`{markup:Localize}`. Because `.resw` keys cannot use the dotted form of today's placeholder ids, ids are assumed to be authored in (or normalized to) a resource-key-safe form; reconciling the existing dotted placeholder ids is a planning task.
- **Grouping model**: Closed `ToolCategory` (required, holds tools) and `ToolGroup` (optional, "uber-category" heading) enumerations supersede the current free-string `Category`/`ICategoryContributor` model for first-party tools. This **replaces the earlier "subcategory" notion**: the optional level is a *parent* heading above categories, not a child below a category. Each enum value maps to a stable id, localization key, and order so existing catalog ordering/grouping is preserved.
- **Icons**: Icons are bundled bitmap assets curated from Icons8 (one asset per tool/category assumed sufficient for light/dark for now). Font-glyph icons are out of scope. This redefines the current optional glyph `IconKey` on **both** `ToolDescriptor` and `Category` as a **required** bitmap reference — every tool and every category must declare one; a group/"uber-category" renders as a text heading and does not require an icon.
- **Pane rendering**: Categories render as nodes that both hold their tools (expandable) and are clickable to open a scoped catalog; groups render as `NavigationViewItemHeader`-style section headings; tools render as selectable items. This is the WinUI-Gallery-style pattern of an expandable-yet-invokable `NavigationViewItem`. The exact control mapping (chevron expansion + `SelectsOnInvoked`/`ItemInvoked` to navigate) is a planning/design decision; the requirement is that the hierarchy and the expand-vs-click behaviors above hold.
- **Category-scoped Catalog reuse**: Clicking a category opens the **existing** Catalog page filtered to that category, not a new page. Today `CatalogViewModel` always builds groups for *all* categories and is navigated to without a parameter; adding a category-filter entry point (e.g., a navigation parameter that scopes `Rebuild()` to one category) is a planning task.
- **New & Updated window**: The threshold that defines "New"/"Updated" (e.g., 30 days) is a presentation concern owned by the shell; this feature exposes the raw authored dates. Dates are authored in a fixed, sortable format (e.g., ISO 8601 `yyyy-MM-dd`).
- **Recently-used is existing**: `RecentsService` already implements usage-based recents; this feature does not change it beyond feeding discovered tools into it (from both the pane and the Catalog page).
- **`NavigationInfoAttribute` is unrelated**: the existing `NavigationInfoAttribute` marks shell sections (Home/Catalog/Tool/Settings) for navigation chrome and is distinct from the new tool-discovery attribute.
- **Single project now**: All tools live in `GcToolkit.Core` initially; the contract is designed to scale to multiple assemblies later without change.

## Dependencies

- **Feature 001 (app shell foundation)** — provides `ICatalogService`/`CatalogService`, `IToolContributor`/`ICategoryContributor`, `ToolDescriptor`, `Category`, `WindowShell`/`NavigationView`, `INavigationService`, `IRecentsService`, `IFavoriteToolsService`, localization, theming, and `IPreferences`. This feature integrates with all of these.
- **Localization resources** — `.resw` entries for tools (`<Id>_Name` / `<Id>_Tooltip`) and for category/group headings, in EN and CS.
- **Icons8-sourced bitmap assets** — bundled with the app and referenced by each tool's and each category's metadata.

## Out of Scope

- The Catalog page UI, search, recents UI, favorites, theming, and language selection — all owned by feature 001 and reused unchanged.
- Recently-*used* tracking and persistence (already implemented by `RecentsService`).
- Favorite tools (already implemented).
- Font-glyph icons and per-theme icon variants.
- Authoring the individual tools themselves (their actual functionality and pages) — this feature provides the discovery/metadata/navigation contract, not the tools.
- Runtime/plugin-style discovery of tools shipped outside the app build.
- Reordering, pinning, or hiding pane items by the end user; collapsible-state persistence of pane sections.
