# Phase 0 Research: App Shell & Foundation

All decisions are grounded in the existing `uno-app-template` capabilities (see plan Summary) and the spec's constraints. No `NEEDS CLARIFICATION` remain.

## R1 — Local persistence approach

- **Decision**: Use the template's `IPreferences` (`ApplicationData.Current.LocalSettings` + `GetComplex/SetComplex` JSON) for settings (theme, language), favorites (set of tool IDs), and recents (ordered list, capped). Surface app-level keys through the existing `IAppPreferences` wrapper.
- **Rationale**: Data is tiny (a language code, a theme enum, ≤ a few dozen IDs, ≤ 10 recents). `IPreferences` already exists, is DI-registered as a singleton, works across all heads (Uno maps `ApplicationData` to per-platform stores, including IndexedDB on WebAssembly), and supports JSON for complex values. Keeps SP-1 free of a database.
- **Alternatives considered**: `sqlite-net-e` (present in the template) — rejected as overkill for SP-1; revisit when the solver workspace / large data arrive. Hand-rolled JSON files — rejected; reinvents `IPreferences` and its cross-platform path handling.

## R2 — Tool catalog & extensibility model

- **Decision**: Represent each tool as an immutable `ToolDescriptor` (stable `Id`, localization key for the name, `CategoryId`, search keywords, icon, and a navigation target = ViewModel type). Tools are contributed to the catalog through DI (an `IEnumerable<ToolDescriptor>` aggregated from registered contributors), and `ICatalogService` groups them by category and runs search. Navigation reuses the template's view-model-first `NavigationService` (`RegisterView<TView,TViewModel>` + `Navigate<TViewModel>()`).
- **Rationale**: DI registration keeps the shell fully decoupled from individual tools (FR-015): later phases add a tool by registering a descriptor + view, with zero shell changes. A descriptor is machine-readable metadata, which is exactly the seam the future AI assistant needs to enumerate and invoke tools. For SP-1, register placeholder descriptors pointing at a stub `ToolHostView`.
- **Alternatives considered**: A hardcoded catalog list — rejected (not extensible). A full plugin/MEF system — rejected (overkill; DI registration is enough and simpler to test).

## R3 — Accent- and case-insensitive search

- **Decision**: Match queries with `CompareInfo.IndexOf(candidate, query, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace)` (invariant culture) over each tool's localized name and keywords; a non-negative index means a match. Expose this as a small, unit-tested `IToolSearch`/matcher in Core.
- **Rationale**: `IgnoreNonSpace` makes matching diacritic-insensitive in both directions (e.g., a query "reseni" matches "řešení" and vice versa), satisfying FR-003 for Czech without manual normalization code. Pure function → trivially unit-testable.
- **Alternatives considered**: Manual Unicode `FormD` decomposition + stripping combining marks — works but is more code and easy to get wrong for edge scripts. Naive `ToLowerInvariant().Contains()` — rejected; fails diacritics.

## R4 — Language switching (restart-based)

- **Decision**: Add an `ILanguageService` that persists the chosen culture (`en`/`cs`) and, **at startup**, applies it via `ApplicationLanguages.PrimaryLanguageOverride` + `CultureInfo.CurrentUICulture/CurrentCulture` so the selection takes effect on the next launch. A language change **may require an app restart** (user-accepted, 2026-05-30), so the stock one-shot `LocalizeExtension` is reused unchanged and the Settings UI shows a "restart to apply" notice. On first run, default to the system language when it is `en` or `cs`, otherwise `en` (FR-009).
- **Rationale**: Meets FR-008 (persisted + applied) with far less complexity; allowing a restart avoids fragile live-refresh of an already-rendered visual tree. Centralizing culture resolution + persistence in one service keeps it unit-testable.
- **Follow-up**: A future enhancement could make switching instant (dynamic `LocalizeExtension` or re-navigation), but it is out of scope now that a restart is acceptable.
- **Alternatives considered**: Live refresh via a dynamic markup extension — deferred (added complexity not needed once a restart is acceptable). Rebuilding the whole visual tree on change — rejected (heavy, loses state).

## R5 — Platform-adaptive title bar & search (FR-018)

- **Decision**: On the Windows head, use the WinUI `TitleBar` control (`Microsoft.UI.Xaml.Controls.TitleBar`, WinAppSDK) and host an `AutoSuggestBox` in its content region for catalog search. On every other head — where that control is not yet available in Uno — use a standard title bar and place the search `AutoSuggestBox` in the `NavigationView` (as its `AutoSuggestBox`/pane header). Select the variant with platform-conditional code in `WindowShell` (feature check / conditional compilation), keeping a single shared search ViewModel so behavior is identical (FR-003).
- **Rationale**: Directly implements the user-mandated FR-018. Isolating the platform branch in the shell keeps the rest of the app platform-agnostic. The template already performs Windows-only title-bar customization (`AppWindowTitleBar.IsCustomizationSupported()`), so a guarded branch fits the existing pattern.
- **Alternatives considered**: One custom title bar on all heads (the template's current approach) — rejected for Windows because the user explicitly wants the native `TitleBar` control there. No title-bar search — rejected (fails FR-018).

## R6 — Theming

- **Decision**: Reuse the template `IThemeManager` (light/dark/system, persists `Theme` to `IAppPreferences`, follows OS changes, recolors the title bar). Extend it to also drive the WinUI `TitleBar` control colors on Windows.
- **Rationale**: Already implements FR-007 fully; only the new `TitleBar` integration is additive.
- **Alternatives considered**: A new theming service — rejected (duplication).

## R7 — Responsive navigation

- **Decision**: Use `NavigationView` adaptive `PaneDisplayMode` driven by `AdaptiveTrigger` visual states — `LeftMinimal` (hamburger) on phone-width, `LeftCompact`/`Left` on wider screens — and reflow Home/Catalog content with adaptive layouts. Validate from 320 px to large desktop (SC-007).
- **Rationale**: Built-in, battle-tested, meets FR-013/SC-007 with minimal custom code.
- **Alternatives considered**: A bespoke responsive shell — rejected (reinvents `NavigationView`).

## R8 — macOS and Linux coverage

- **Decision**: Serve macOS and Linux through the existing `net10.0-desktop` Skia head; no separate Catalyst/GTK heads are added. Verify the desktop head launches on macOS and Linux during this phase (SC-010).
- **Rationale**: Uno's Skia desktop head is cross-OS (Windows/macOS/Linux), so the six PRD platforms are covered by the five template heads (Windows also has its native WinAppSDK head).
- **Alternatives considered**: Dedicated macOS Catalyst/Linux heads — rejected (unnecessary; more build/QA surface).

## R9 — Accessibility (WCAG 2.2 AA)

- **Decision**: Apply `AutomationProperties.Name`/`LabeledBy` to interactive elements, ensure a visible focus indicator and full keyboard operability, honor OS text scaling, meet AA contrast via theme brushes, and respect 2.2's minimum target-size guidance (≥ 24×24, larger touch targets in the field UI later). Verify with Accessibility Insights on Windows plus manual screen-reader passes; encode SC-011 as an audit of the primary screens.
- **Rationale**: Turns FR-014 into an auditable bar.
- **Alternatives considered**: Best-effort without a standard — rejected per the clarification (WCAG 2.2 AA chosen).

## R10 — Template bring-in & renaming

- **Decision**: Copy the `uno-app-template` `src/` and `tests/` into the `gc-toolbox` repo; rename `GcToolkit` → `GCToolkit` (app), `GcToolkit.Core` → `GCToolkit.Core`, `GcToolkit.Core.Tests` → `GCToolkit.Core.Tests`; set the root namespace to `GCToolkit`. The user-facing display name stays a placeholder until the trademark-safe brand is chosen (decoupled from the code identifier).
- **Rationale**: Gives the repo a working, buildable foundation that matches its name (`gc-toolbox`) without baking the "Geocaching" trademark into identifiers. Keeping display name separate lets branding be finalized later without code churn.
- **Alternatives considered**: Keep `GcToolkit` names — rejected (confusing, not the product). Pick the final brand now — blocked (name is deliberately TBD).
