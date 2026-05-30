# Tasks: App Shell & Foundation

**Input**: Design documents from `/specs/001-app-shell-foundation/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/services.md, quickstart.md

**Tests**: Test tasks **are included**. The project mandates TDD with MSTest (global `CLAUDE.md`; plan Constitution Check). Core logic (catalog, search, favorites, recents, language) is unit-tested in `tests/GcToolkit.Core.Tests`; UI is verified by per-head smoke runs. Write each test task before/alongside its implementation and confirm it fails first.

**Organization**: Tasks are grouped by user story (US1–US5) for independent implementation and testing.

## Reconciliation note (integrated template)

The `uno-app-template` is **already integrated** under `src/` as `GcToolkit` (app) + `GcToolkit.Core` (library) with `tests/GcToolkit.Core.Tests`; display name **GC Toolkit**. Therefore:

- All paths/namespaces below use the **actual** identifier `GcToolkit` / `GcToolkit.Core` (the design docs' `GCToolkit` and the research R10 "copy + rename" decision are **obsolete** — no bring-in/rename tasks are generated).
- Existing template assets are **reused/extended**: `WindowShell`, `NavigationService`, `NavigationSection`, `IThemeManager`, `IPreferences`/`IAppPreferences`, `LocalizeExtension`/`Localizer`, `SettingsView` (Theme card already present), dialogs/launcher/share services.
- Per FR-018, the Windows head **adopts the WinUI `TitleBar` control**, replacing the template's hand-rolled `TitleBarGrid`; other heads keep a standard title bar with search in the `NavigationView`.
- The placeholder `MainView`/`MainViewModel` (“Hello Uno Platform!”) is **replaced** by a new `HomeView`/`HomeViewModel`.
- A language change **may require an app restart** to fully apply (per user decision); the choice is persisted and applied on next launch. Theme still applies instantly. No live `LocalizeExtension` refresh is built.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: US1–US5 (omitted for Setup / Foundational / Polish)
- All paths are repository-relative.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Confirm the integrated baseline and scaffold the new feature areas.

- [X] T001 Verify the integrated solution builds and the existing test project runs green as a baseline — build `src/GcToolkit.slnx` on the desktop head (`dotnet build src/GcToolkit/GcToolkit.csproj -f net10.0-desktop`) and run `dotnet test tests/GcToolkit.Core.Tests/GcToolkit.Core.Tests.csproj`.
- [X] T002 [P] Create the new Core feature folders `Catalog/`, `Search/`, `Favorites/`, `Recents/`, `Localization/` under `src/GcToolkit.Core/` (add a temporary `.gitkeep` if needed).
- [X] T003 [P] Mirror those folders in tests: create `Catalog/`, `Search/`, `Favorites/`, `Recents/`, `Localization/`, and `Fakes/` under `tests/GcToolkit.Core.Tests/`.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared persistence, catalog domain, placeholder data, and navigation scaffolding that every user story depends on.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T004 Relocate the `IPreferences` abstraction into Core at `src/GcToolkit.Core/Storage/IPreferences.cs` (namespace `GcToolkit.Core.Storage`) so Core services can persist; keep the `Preferences` implementation in `src/GcToolkit/Services/Settings/Preferences.cs`; update usings in `AppPreferences.cs` and the DI registration in `src/GcToolkit/App.xaml.cs`.
- [X] T005 [P] Create the immutable `ToolDescriptor` record (Id, NameKey, CategoryId, Keywords, IconKey, ViewModelType, IsPlaceholder) in `src/GcToolkit.Core/Catalog/ToolDescriptor.cs`.
- [X] T006 [P] Create the `Category` record (Id, NameKey, Order, IconKey) in `src/GcToolkit.Core/Catalog/Category.cs`.
- [X] T007 [P] Create the `IToolContributor` contract (`IEnumerable<ToolDescriptor> GetTools()`) and a `ICategoryContributor` (or category list) in `src/GcToolkit.Core/Catalog/IToolContributor.cs`.
- [X] T008 Create `ICatalogService` + `CatalogService` in `src/GcToolkit.Core/Catalog/` — aggregate contributed tools, expose `GetCategories()`/`GetTools()`/`GetToolsByCategory(id)`, and validate (duplicate `Id` and unknown `CategoryId` are startup errors) (depends on T005–T007).
- [X] T009 Create `PlaceholderToolContributor` yielding ~2–3 placeholder tools per category (Coordinates, Ciphers, Numbers, Field), all targeting the stub `ToolHostViewModel` with `IsPlaceholder = true`, in `src/GcToolkit.Core/Catalog/PlaceholderToolContributor.cs` (depends on T005–T007).
- [X] T010 [P] Add EN + CS localization strings for category names, placeholder tool names, and shell chrome (Home, Catalog, empty-state messages) to `src/GcToolkit/Strings/en/Resources.resw` and `src/GcToolkit/Strings/cs/Resources.resw`.
- [X] T011 Extend the `NavigationSection` enum to `Home`, `Catalog`, `Tool`, `Settings` (replacing the placeholder `Main`) in `src/GcToolkit.Core/Navigation/NavigationSection.cs`.
- [X] T012 Register foundational services in `src/GcToolkit/App.xaml.cs` `RegisterServices`: `ICatalogService`, `IToolContributor` → `PlaceholderToolContributor`, and placeholders for the new view↔ViewModel `RegisterView` calls (depends on T008, T009, T011).
- [X] T013 [P] Add an in-memory `IPreferences` test double at `tests/GcToolkit.Core.Tests/Fakes/InMemoryPreferences.cs` for Core service tests (depends on T004).

**Checkpoint**: Catalog domain, placeholder data, persistence abstraction, and navigation enum are ready — user stories can begin.

---

## Phase 3: User Story 1 - Browse and open tools from a categorized catalog (Priority: P1) 🎯 MVP

**Goal**: A navigable Home landing and a categorized Catalog where placeholder tools open into a stub host and back navigation returns to the catalog in its prior state.

**Independent Test**: Launch with the placeholder contributor registered; verify tools appear grouped by category with localized names, a tool opens when selected, back returns to the catalog, and an empty catalog shows a clear empty state.

### Tests for User Story 1

- [X] T014 [P] [US1] `CatalogService` tests — grouping by category + deterministic order, `GetToolsByCategory`, empty-catalog result, and validation (duplicate `Id`, unknown `CategoryId`) in `tests/GcToolkit.Core.Tests/Catalog/CatalogServiceTests.cs`.

### Implementation for User Story 1

- [X] T015 [P] [US1] Create `HomeView` + `HomeViewModel` as the landing screen (entry point to Catalog + a search slot and empty favorites/recents sections to be enriched in US2/US3) in `src/GcToolkit/Views/HomeView.xaml(.cs)` and `src/GcToolkit/ViewModels/HomeViewModel.cs`; delete the placeholder `MainView`/`MainViewModel`.
- [X] T016 [P] [US1] Create `CatalogView` + `CatalogViewModel` — tools grouped by category (localized names, ordered), tool-open command, and a localized empty state — in `src/GcToolkit/Views/CatalogView.xaml(.cs)` and `src/GcToolkit/ViewModels/CatalogViewModel.cs`.
- [X] T017 [P] [US1] Create the `ToolHostView` + `ToolHostViewModel` stub that accepts a tool id/descriptor as the navigation parameter, resolves it via `ICatalogService`, shows the localized tool name, and supports back navigation, in `src/GcToolkit/Views/ToolHostView.xaml(.cs)` and `src/GcToolkit/ViewModels/ToolHostViewModel.cs`.
- [X] T018 [US1] Wire the shell: add Home + Catalog `NavigationView.MenuItems` with section tags, extend `NavigateToSection`, and `RegisterView` the Home/Catalog/ToolHost VM↔View pairs in `src/GcToolkit/WindowShell.xaml`, `src/GcToolkit/WindowShell.xaml.cs`, and `src/GcToolkit/App.xaml.cs` (depends on T015–T017).
- [X] T019 [US1] Implement Catalog→ToolHost open via `INavigationService.Navigate<ToolHostViewModel>(toolId)`, preserve catalog state across back navigation, and add `AutomationProperties.Name` to catalog items and the open affordance (depends on T016–T018).

**Checkpoint**: US1 is independently functional — browse, open, back. This is the MVP.

---

## Phase 4: User Story 2 - Find a tool by searching (Priority: P2)

**Goal**: Incremental, case- and accent-insensitive search over name + keywords, surfaced in the WinUI `TitleBar` on Windows and the `NavigationView` search field elsewhere, with a no-results state and clear-restores-all behavior.

**Independent Test**: With several tools registered, type a query and verify the list narrows by name/keyword, matching is case- and accent-insensitive (un-accented Czech query matches accented names), a no-results state appears for non-matches, and clearing restores the full catalog.

### Tests for User Story 2

- [X] T020 [P] [US2] `ToolMatcher` tests — case-insensitive, accent-insensitive in both directions (e.g. `reseni` ⇄ `řešení`), keyword matches, and empty/whitespace query → all tools, in `tests/GcToolkit.Core.Tests/Search/ToolMatcherTests.cs`.
- [X] T021 [P] [US2] `CatalogService.Search` tests — narrowing to matches, no-results (empty list), and empty query returns the full catalog, in `tests/GcToolkit.Core.Tests/Catalog/CatalogSearchTests.cs`.

### Implementation for User Story 2

- [X] T022 [P] [US2] Implement `IToolMatcher` + `ToolMatcher` using `CompareInfo.IndexOf(..., IgnoreCase | IgnoreNonSpace)` over name + keywords in `src/GcToolkit.Core/Search/IToolMatcher.cs` and `ToolMatcher.cs`.
- [X] T023 [US2] Add `Search(string query)` to `ICatalogService`/`CatalogService` delegating to `IToolMatcher` (empty query → full catalog) in `src/GcToolkit.Core/Catalog/CatalogService.cs` (depends on T022).
- [X] T024 [P] [US2] Create a shared `SearchViewModel` (incremental query → results, navigates to the filtered Catalog) in `src/GcToolkit/ViewModels/SearchViewModel.cs` (depends on T023).
- [X] T025 [US2] Implement platform-adaptive search placement in `src/GcToolkit/WindowShell.xaml(.cs)`: on the Windows head use the WinUI `TitleBar` control hosting an `AutoSuggestBox` (replacing the custom `TitleBarGrid`); on all other heads put the `AutoSuggestBox` in the `NavigationView` (pane/header) — both bound to the single `SearchViewModel` (depends on T024).
- [X] T026 [US2] Wire the Home search entry and the Catalog results UI with a localized no-results empty state, and ensure clearing the query restores the full catalog, in `HomeView`/`HomeViewModel` and `CatalogView`/`CatalogViewModel` (depends on T024, T023).

**Checkpoint**: US1 + US2 both work independently.

---

## Phase 5: User Story 3 - Save favorites and revisit recents (Priority: P2)

**Goal**: Toggle favorites and view a deduplicated, capped-at-10, most-recent-first, clearable recents list — surfaced on Home and reflected wherever a tool is shown; persisted locally.

**Independent Test**: Favorite a tool and confirm it appears in the favorites area and its state is reflected in the catalog; open several tools and confirm recents are most-recent-first, deduped, capped at 10; re-open bumps to top; clear empties the list; unfavorite removes it.

### Tests for User Story 3

- [X] T027 [P] [US3] `FavoritesService` tests — toggle add/remove, `IsFavorite`, stable order by `AddedUtc`, persistence round-trip via `InMemoryPreferences`, and pruning ids missing from the catalog, in `tests/GcToolkit.Core.Tests/Favorites/FavoritesServiceTests.cs`.
- [X] T028 [P] [US3] `RecentsService` tests — dedup + bump-to-top, cap at 10 (oldest evicted), most-recent-first ordering, clear, persistence round-trip, and pruning missing ids, in `tests/GcToolkit.Core.Tests/Recents/RecentsServiceTests.cs`.

### Implementation for User Story 3

- [X] T029 [P] [US3] Implement `FavoriteEntry` + `IFavoritesService` + `FavoritesService` (persist JSON list under preferences key `favorites`, raise `FavoritesChanged`) in `src/GcToolkit.Core/Favorites/`.
- [X] T030 [P] [US3] Implement `RecentEntry` + `IRecentsService` + `RecentsService` (key `recents`, dedup/bump, cap 10, clear, raise `RecentsChanged`) in `src/GcToolkit.Core/Recents/`.
- [X] T031 [US3] Register `IFavoritesService` and `IRecentsService` in `src/GcToolkit/App.xaml.cs` `RegisterServices` (depends on T029, T030).
- [X] T032 [US3] Add favorite-toggle affordance reflecting state in `CatalogView`/`CatalogViewModel` and `ToolHostView`/`ToolHostViewModel` (depends on T029, T031).
- [X] T033 [US3] Record opens via `IRecentsService.RecordOpenedAsync(toolId)` when `ToolHostViewModel` is navigated to, in `src/GcToolkit/ViewModels/ToolHostViewModel.cs` (depends on T030, T031).
- [X] T034 [US3] Surface favorites + recents areas with localized empty states on `HomeView`/`HomeViewModel`, subscribing to `FavoritesChanged`/`RecentsChanged` (depends on T029–T031).
- [X] T035 [US3] Add a "Clear recents" action in `SettingsView`/`SettingsViewModel` using `IConfirmationDialogService`, calling `IRecentsService.ClearAsync()` (depends on T030, T031).

**Checkpoint**: US1 + US2 + US3 all work independently.

---

## Phase 6: User Story 4 - Personalize appearance and language (Priority: P2)

**Goal**: Theme (light/dark/system, already present) applies instantly; English↔Czech language selection is persisted and applied on the next launch (a restart to apply the language is acceptable), with the correct first-run default.

**Independent Test**: Change theme and confirm the whole UI updates immediately; set theme to system and confirm it follows the OS; switch the language, restart, and confirm all visible text is in the chosen language; on a non-EN/CS system, confirm first-run defaults to English.

### Tests for User Story 4

- [X] T036 [P] [US4] `LanguageService` tests — first-run default resolution (`en`/`cs` system → that; otherwise `en`, FR-009), `SetAsync` persists the selected code, and `Available` = { en, cs }, in `tests/GcToolkit.Core.Tests/Localization/LanguageServiceTests.cs`.

### Implementation for User Story 4

- [X] T037 [P] [US4] Implement `AppLanguage` record + `ILanguageService` + `LanguageService` — persist the selected code via Core `IPreferences` and resolve the first-run default (FR-009) — in `src/GcToolkit.Core/Localization/`.
- [X] T038 [US4] Apply the persisted/resolved language at startup before the shell builds (set `ApplicationLanguages.PrimaryLanguageOverride` + `CultureInfo.CurrentUICulture/CurrentCulture`) so the selection takes effect on the next launch, in `src/GcToolkit/App.xaml.cs` (and `WindowShell.xaml.cs` init). `LocalizeExtension` stays one-shot — no live refresh (depends on T037).
- [X] T039 [US4] Add a Language selector `SettingsCard` (bound to `ILanguageService.Available`/`Current`) next to the existing Theme card; on change, persist via `SetAsync`. Confirm the Theme card's light/dark/system + follow-system behavior. In `src/GcToolkit/Views/SettingsView.xaml` and `src/GcToolkit/ViewModels/SettingsViewModel.cs` (depends on T037).
- [X] T040 [US4] Add EN/CS "restart to apply the new language" notice strings and surface the notice (e.g. an `InfoBar`/dialog) when the language is changed, in `src/GcToolkit/Strings/{en,cs}/Resources.resw` and `SettingsView`/`SettingsViewModel` (depends on T039).
- [X] T041 [US4] Register `ILanguageService` in `src/GcToolkit/App.xaml.cs` `RegisterServices` (depends on T037).

**Checkpoint**: US1–US4 all work independently.

---

## Phase 7: User Story 5 - Use the app comfortably on any screen size (Priority: P3)

**Goal**: A responsive shell and content that adapt from ~320 px phone width to large desktop without truncation or overlap.

**Independent Test**: Resize / run on different form factors and confirm the navigation pattern (compact ↔ expanded) and Home/Catalog content adapt with no truncation or overlapping content and all controls reachable.

### Implementation for User Story 5

- [X] T042 [US5] Drive `NavigationView.PaneDisplayMode` via `AdaptiveTrigger` visual states (`LeftMinimal` on phone width → `LeftCompact`/`Left` on wider) in `src/GcToolkit/WindowShell.xaml`.
- [X] T043 [P] [US5] Make the Home layout reflow (favorites/recents/search) responsively in `src/GcToolkit/Views/HomeView.xaml`.
- [X] T044 [P] [US5] Make the Catalog layout reflow (category/tool grid) responsively without truncation in `src/GcToolkit/Views/CatalogView.xaml`.
- [ ] T045 [US5] Verify and tune from ~320 px to a wide desktop window (no truncation/overlap, controls reachable) across Home/Catalog/Settings (depends on T042–T044).

**Checkpoint**: All user stories independently functional across screen sizes.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Quality bars spanning all stories.

- [X] T046 [P] Localization completeness pass — confirm EN/CS parity with no missing or placeholder strings across `src/GcToolkit/Strings/{en,cs}/Resources.resw` (FR-012, SC-008).
- [ ] T047 [P] Accessibility pass to WCAG 2.2 AA on Home/Catalog/Tool/Settings — accessible names on all interactive elements, full keyboard operability, visible focus indicator, OS text-scaling honored, AA contrast in light + dark, and ≥24×24 target sizes (FR-014, SC-011).
- [X] T048 Harden against corrupt/unreadable saved data — fall back to defaults on deserialization failure in `FavoritesService`, `RecentsService`, `LanguageService`, and `AppPreferences` (spec edge case).
- [ ] T049 [P] Per-head build/run smoke: desktop, WebAssembly, Windows (WinAppSDK), Android, iOS (SC-010, SC-016 platforms).
- [ ] T050 Performance validation — cold start to interactive catalog ≤ 3 s (SC-009), search narrows ≤ 2 s (SC-002), theme/language apply ≤ 1 s (SC-005).
- [ ] T051 Run the `quickstart.md` definition-of-done smoke checklist end-to-end and record results (SC validation).

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately.
- **Foundational (Phase 2)**: Depends on Setup — **BLOCKS all user stories**. T004 (IPreferences → Core) blocks all Core services; T005–T009 build the catalog domain; T011–T012 the navigation/DI scaffolding.
- **User Stories (Phase 3–7)**: All depend on Foundational. US1 is the MVP; US2/US3/US4 build on the US1 screens but are independently testable; US5 polishes layout across them.
- **Polish (Phase 8)**: Depends on the desired user stories being complete.

### User Story Dependencies

- **US1 (P1)**: After Foundational — no dependency on other stories.
- **US2 (P2)**: After Foundational — extends the Home/Catalog screens from US1 (search slot/results); matcher + `Search` are independent and testable on their own.
- **US3 (P2)**: After Foundational — adds favorite/recent affordances to the US1 screens; services are independently unit-testable.
- **US4 (P2)**: After Foundational — touches Settings + localization; largely independent of US1–US3.
- **US5 (P3)**: After Foundational — adapts whatever screens exist; best run once US1–US4 screens are in place.

### Within Each User Story

- Tests are written first and must fail before implementation.
- Core models → Core services → DI registration → ViewModels → Views/shell wiring.

### Parallel Opportunities

- Setup: T002, T003 in parallel.
- Foundational: T005, T006, T007 in parallel; T010, T013 in parallel with each other and with the model tasks.
- US1: T014 (test) ∥ T015 ∥ T016 ∥ T017 (different files); then T018 → T019.
- US2: T020 ∥ T021 (tests) ∥ T022; then T023 → T024 → T025/T026.
- US3: T027 ∥ T028 (tests); T029 ∥ T030 (services); then T031 → T032/T033/T034/T035.
- US4: T036 (test) ∥ T037; then T038/T039/T040/T041.
- Once Foundational is done, US1–US4 can be staffed in parallel by different developers.

---

## Parallel Example: User Story 1

```text
# Tests + independent view scaffolds together:
Task: "T014 [US1] CatalogService tests in tests/GcToolkit.Core.Tests/Catalog/CatalogServiceTests.cs"
Task: "T015 [US1] HomeView + HomeViewModel (replace MainView placeholder)"
Task: "T016 [US1] CatalogView + CatalogViewModel"
Task: "T017 [US1] ToolHostView + ToolHostViewModel stub"
# Then sequentially:
Task: "T018 [US1] Wire shell navigation (menu items, sections, RegisterView)"
Task: "T019 [US1] Catalog→ToolHost open + back-stack + accessible names"
```

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Phase 1: Setup → 2. Phase 2: Foundational (CRITICAL) → 3. Phase 3: US1 → **STOP & VALIDATE** (browse, open, back on the desktop head) → demo.

### Incremental Delivery

1. Setup + Foundational → foundation ready.
2. US1 → test independently → demo (MVP).
3. US2 (search) → US3 (favorites/recents) → US4 (theme/language) → each tested independently.
4. US5 (responsive) → Polish (localization, a11y, per-head, performance, quickstart).

### Parallel Team Strategy

After Foundational: Dev A → US1, Dev B → US2, Dev C → US3, Dev D → US4; integrate independently; US5 + Polish last.

---

## Notes

- `[P]` = different files, no incomplete dependencies.
- Naming reflects the **integrated** template: `GcToolkit` / `GcToolkit.Core` (not `GCToolkit`); no bring-in/rename tasks.
- Core services depend on the relocated Core `IPreferences` (T004) — do that before US3/US4 services.
- Per `CLAUDE.md`: run `dotnet format` before staging; stage only (never auto-commit).
- Verify each test fails before implementing; stop at any checkpoint to validate a story independently.

---

## Implementation status (speckit-implement, 2026-05-30)

**Done & verified**: Phases 1–6 (T001–T041), US2 adaptive search incl. the Windows `TitleBar` control (T025), US5 layout (T042–T044), localization parity (T046), and corrupt-data hardening (T048). Core logic is covered by **46 passing MSTest tests** (`tests/GcToolkit.Core.Tests`, MTP runner). The **desktop** and **Windows (WinAppSDK)** heads build clean; the desktop app launches without startup/DI errors.

**FR-018 (T025)**: On the **Windows** head the search box is hosted in the WinUI `Microsoft.UI.Xaml.Controls.TitleBar` control (via Uno `win:` conditional XAML, wired with `ExtendsContentIntoTitleBar` + `SetTitleBar`), replacing the hand-rolled `TitleBarGrid`. On **all other heads** (`not_win:`) it lives in `NavigationView.AutoSuggestBox`. Both bind to the single shared `SearchViewModel`. The Windows path is compile-verified; interactive rendering on Windows still warrants a manual look.

**Open items (left unchecked):**

- **T045 — Responsive sweep (320 px → desktop)**: Layouts reflow via `NavigationView` `PaneDisplayMode=Auto` + `UniformGridLayout`/`ScrollViewer`; a manual resize sweep across heads is still required.
- **T047 — WCAG 2.2 AA audit**: Accessible names are set on tool cards, the open affordance, and favorite toggles; the full audit (contrast, focus order, screen-reader pass, target sizes) is a manual task.
- **T049 — Per-head smoke**: desktop ✅ build+launch, Windows ✅ build. WebAssembly/Android/iOS not built/run in this environment.
- **T050 — Performance validation** and **T051 — quickstart definition-of-done**: require running the app per head (manual/runtime).

> Note: per `CLAUDE.md`, run `dotnet format` before staging. Changes were left **unstaged** (no auto-commit).
