# Feature Specification: App Shell & Foundation

**Feature Branch**: `001-app-shell-foundation`

**Created**: 2026-05-27

**Status**: Draft

**Input**: User description: "https://github.com/MartinZikmund/gc-toolbox/issues/63 . We will use uno-app-template D:\Personal\uno-app-template as the basis on which we will start building up"

**Source**: Phase SP-1 (epic [#63](https://github.com/MartinZikmund/gc-toolbox/issues/63)) of the product defined in `docs/PRD.md`.

## Overview

This feature establishes the application shell and the cross-cutting foundations that every later phase plugs into. It delivers a navigable, searchable catalog of tools, the ability to favorite tools and revisit recent ones, appearance and language settings, local persistence of all of the above, a responsive layout that works from phone to desktop, and full English + Czech localization. No individual tools are built here — the shell must work with whatever tools are registered, starting with a small set of placeholder catalog entries so it can be demonstrated and tested before real tools land in later phases.

## Clarifications

### Session 2026-05-27

- Q: How should the shell's landing screen and navigation be structured? → A: A Home landing screen surfacing Favorites + Recents + a prominent search entry, with a separate browsable categorized catalog reachable from primary navigation.
- Q: What should populate the catalog in this SP-1 phase? → A: Placeholder/sample entries (~2-3 per category) with stub pages and no real tool logic, removed as real tools arrive in later phases.
- Q: Which settings should SP-1 actually ship? → A: Theme + language only; the preferences service is built extensible so global options (units, default coordinate format) are added later with the tools that consume them.
- Decision (user-provided): On the Windows/WinUI target the shell uses the WinUI `TitleBar` control; other Uno targets do not (not yet supported there) and use a standard title bar.
- Decision (user-provided): Search placement is platform-adaptive — within the title bar on WinUI; within the navigation view's integrated search field (AutoSuggestBox) on other targets. Search behavior is identical either way.
- Q: How should the Recents list behave? → A: Deduplicated (one entry per tool, bumped to the top on re-open), capped at the 10 most recent, and user-clearable.
- Q: What accessibility conformance bar should the shell target? → A: WCAG 2.2 Level AA.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Browse and open tools from a categorized catalog (Priority: P1)

A geocacher opens the app and is presented with a catalog of tools grouped into clear categories (for example: Coordinates, Ciphers, Numbers, Field). They scan the categories, pick a tool, and open it; they can return to the catalog and pick another.

**Why this priority**: This is the spine of the entire product. Without a way to browse and open tools, nothing else has a home. It is the minimum viable shell and must exist before any tool can be added.

**Independent Test**: Launch the app with a set of registered (placeholder) catalog entries; verify categories and tools are listed, a tool opens when selected, and back navigation returns to the catalog — delivering a usable navigation experience on its own.

**Acceptance Scenarios**:

1. **Given** the app is launched with one or more tools registered, **When** the catalog appears, **Then** tools are shown grouped by their category with their localized names.
2. **Given** the catalog is shown, **When** the user selects a tool, **Then** that tool's page opens.
3. **Given** a tool's page is open, **When** the user navigates back, **Then** the catalog is shown again in its previous state.
4. **Given** no tools are registered yet, **When** the catalog appears, **Then** a clear empty state is shown rather than a blank screen.

---

### User Story 2 - Find a tool by searching (Priority: P2)

With dozens of tools planned, a geocacher types part of a tool's name or a related keyword and the catalog narrows to matching tools so they can jump straight to the one they need.

**Why this priority**: Search dramatically reduces time-to-tool once the catalog is large, but the app is still usable by browsing alone, so it ranks just below the core catalog.

**Independent Test**: With several tools registered, type a query and verify the list narrows to matching tools (by name or keyword), that matching is case- and accent-insensitive, and that a no-results state appears for non-matching queries.

**Acceptance Scenarios**:

1. **Given** several tools are registered, **When** the user types part of a tool name, **Then** the list updates to show only tools whose name or keywords match.
2. **Given** a query that matches nothing, **When** the search is run, **Then** a clear "no results" state is shown.
3. **Given** a Czech query typed without diacritics, **When** the search runs, **Then** tools whose names contain the accented form still match.
4. **Given** an active search, **When** the user clears the query, **Then** the full catalog is shown again.
5. **Given** the Windows (WinUI) target, **When** the app is shown, **Then** the search box appears in the title bar; **and given** any other target, **Then** the search box appears in the navigation view's search field — with identical search results in both.

---

### User Story 3 - Save favorites and revisit recents (Priority: P2)

A geocacher marks the tools they use most as favorites and, separately, sees the tools they opened most recently, so they can return to either without searching.

**Why this priority**: A strong convenience that drives retention, but the app delivers value without it; it depends on the catalog from US1.

**Independent Test**: Mark a tool as a favorite and confirm it appears in a favorites area; open a few tools and confirm they appear in a recents area, most-recent-first; unmark a favorite and confirm it is removed.

**Acceptance Scenarios**:

1. **Given** a tool in the catalog, **When** the user marks it as a favorite, **Then** it appears in the favorites area and its favorite state is reflected wherever the tool is shown.
2. **Given** a favorited tool, **When** the user unmarks it, **Then** it is removed from favorites.
3. **Given** the user opens several tools, **When** they view recents, **Then** the tools appear ordered from most to least recently opened, limited to the 10 most recent, with each tool appearing once.
4. **Given** no favorites or no recents exist, **When** the user views those areas, **Then** an appropriate empty state is shown.
5. **Given** a tool already in recents, **When** the user re-opens it, **Then** it moves to the top without creating a duplicate; **and when** the user clears recents, **Then** the recents list becomes empty.

---

### User Story 4 - Personalize appearance and language (Priority: P2)

A geocacher opens settings and chooses a theme (light, dark, or follow the system) and an interface language (English or Czech). The change applies immediately to the whole app.

**Why this priority**: Important for comfort and for the Czech audience, and it exercises the localization and theming foundations every later phase relies on — but the catalog is usable at default settings first.

**Independent Test**: Change the theme and confirm the whole UI updates immediately; switch the language and confirm all visible text changes; set theme to "system" and confirm it follows the OS appearance.

**Acceptance Scenarios**:

1. **Given** the settings screen, **When** the user selects a different theme, **Then** the entire app reflects the new theme immediately without a restart.
2. **Given** theme is set to "system", **When** the OS appearance changes, **Then** the app follows it.
3. **Given** the settings screen, **When** the user switches the language between English and Czech, **Then** all visible text updates immediately to the chosen language.
4. **Given** the device's system language is neither English nor Czech, **When** the app is first launched, **Then** it defaults to English.

---

### User Story 5 - Use the app comfortably on any screen size (Priority: P3)

A geocacher uses the app on a phone in the field and on a larger tablet or desktop window at home; the layout adapts so navigation and content remain comfortable on each.

**Why this priority**: Cross-device polish matters for the all-platform goal, but a single adaptive layout is acceptable initially, so it ranks last among the foundational stories.

**Independent Test**: Resize the window / run on different form factors and confirm the navigation pattern and content adapt (e.g., compact navigation on narrow screens, expanded on wide) without truncation or overlap.

**Acceptance Scenarios**:

1. **Given** a narrow (phone-width) screen, **When** the catalog is shown, **Then** navigation uses a compact pattern suited to small screens.
2. **Given** a wide (desktop-width) window, **When** the catalog is shown, **Then** navigation expands to use the available space.
3. **Given** any supported screen width, **When** content is displayed, **Then** no text is truncated or overlapping and all controls remain reachable.

---

### Edge Cases

- **Empty catalog**: When no tools are registered, the catalog shows a clear empty state, and search/favorites/recents behave sensibly (no crashes, appropriate empty states).
- **No stored data on first run**: On a fresh install with no saved favorites/recents/settings, the app starts with sensible defaults (theme = system, language = system-or-English).
- **Corrupt or unreadable saved data**: If stored preferences cannot be read, the app falls back to defaults rather than failing to launch.
- **System language unsupported**: Defaults to English while keeping the option to switch.
- **Runtime OS theme change** while theme = system: the app updates to match.
- **Very long tool/category names** and **large numbers of categories**: the layout remains usable and scrollable.
- **Diacritics in search**: Czech accented names are found by un-accented queries and vice versa.
- **Offline**: The shell and all of its functions work with no network connection.
- **Rapid navigation / back-stack**: Repeated open/back actions keep the catalog state consistent.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST present a catalog of tools grouped by category, showing each tool's localized display name.
- **FR-002**: Users MUST be able to open a tool from the catalog and return to the catalog via back navigation, with the catalog's prior state preserved.
- **FR-003**: The system MUST provide incremental search over the catalog that matches on tool name and associated keywords, and MUST be case-insensitive and accent-insensitive (so Czech names match with or without diacritics).
- **FR-004**: The system MUST show a clear, localized empty state for: an empty catalog, no search results, no favorites, and no recents.
- **FR-005**: Users MUST be able to mark and unmark any tool as a favorite, and favorited tools MUST be presented in a dedicated favorites area.
- **FR-006**: The system MUST record recently opened tools deduplicated (one entry per tool, moved to the top when re-opened), present them most-recent-first, limit the list to the 10 most recent tools, and allow the user to clear the recents list.
- **FR-007**: Users MUST be able to choose a theme of light, dark, or follow-system, and the choice MUST apply to the entire app immediately without a restart; follow-system MUST track runtime OS appearance changes.
- **FR-008**: Users MUST be able to choose the interface language between English and Czech, and the choice MUST apply to all visible text immediately without a restart.
- **FR-009**: On first launch, the system MUST default the language to the device's system language when it is English or Czech, otherwise to English; and the theme to follow-system.
- **FR-010**: The system MUST persist favorites, recents, theme, and language locally on the device and restore them on subsequent launches, with no account or sign-in required.
- **FR-011**: The system MUST function fully without any network connection.
- **FR-012**: All user-visible text MUST be localized for English and Czech with no missing or placeholder strings, and the localization approach MUST allow additional languages to be added later without code changes to consuming screens.
- **FR-013**: The layout MUST adapt responsively across phone, tablet, and desktop sizes, using a navigation pattern appropriate to the available width, without truncation or overlapping content.
- **FR-014**: The interface MUST conform to WCAG 2.2 Level AA: every interactive element MUST expose an accessible name/label, the app MUST be operable via keyboard and assistive focus navigation with a visible focus indicator, MUST honor the OS text-scaling setting, MUST meet AA contrast ratios in both light and dark themes, and MUST meet minimum interactive target sizes.
- **FR-015**: The tool catalog MUST be extensible so that later phases can register new tools and categories without modifying the shell.
- **FR-016**: The system MUST run on Windows, Android, iOS, macOS, Linux, and the web from a single shared implementation.
- **FR-017**: The system MUST present a Home landing screen that surfaces the user's favorites, recent tools, and a search entry point; the full categorized catalog MUST be a distinct screen reachable from primary navigation.
- **FR-018**: On the Windows (WinUI) target, the shell MUST use the platform title-bar control (`TitleBar`) and present the catalog search within the title bar. On all other targets — where that title-bar control is not yet supported — the shell MUST use a standard title bar and present search via the navigation view's integrated search field. The search behavior defined in FR-003 MUST be identical regardless of placement.
- **FR-019**: Settings exposed to the user in this phase MUST be limited to theme and language; the underlying preferences mechanism MUST be extensible so additional global preferences can be added later without rework.

### Key Entities *(include if feature involves data)*

- **Tool (catalog entry)**: Metadata describing a tool the shell can list and open — stable identifier, localized display name, owning category, and search keywords/aliases. (The tool's actual functionality and page are provided by later phases.)
- **Category**: A grouping for tools — stable identifier, localized display name, and display order.
- **Favorite**: A user's mark that a given tool is a favorite, with the time it was added.
- **Recent entry**: A record that a given tool was opened, with the time it was last opened; deduplicated to one entry per tool and capped at the 10 most recent; the collection can be cleared by the user.
- **Preferences**: The user's persisted settings — selected theme (light/dark/system) and selected language (English/Czech), extensible for future settings.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: From a cold start, a user can reach and open a specific tool in 3 or fewer interactions (taps/clicks).
- **SC-002**: When searching, the catalog narrows and surfaces a known matching tool within 2 seconds of typing on a typical mid-range device, updating as the user types.
- **SC-003**: In usability testing, at least 90% of users locate a specific named tool (via browse or search) on their first attempt.
- **SC-004**: Favorites, recents, theme, and language persist across 100% of normal app restarts.
- **SC-005**: Changing theme or language updates the entire visible interface within 1 second, without an app restart.
- **SC-006**: All shell functions (browse, search, favorites, recents, settings) work with no network connection.
- **SC-007**: The interface renders without truncation or overlap across screen widths from a small phone (≈320 px wide) to a large desktop window.
- **SC-008**: With either language selected, 100% of visible text appears in that language, with no missing or placeholder strings.
- **SC-009**: The app launches to an interactive catalog within 3 seconds on a mid-range device.
- **SC-010**: The app builds and runs on all six target platforms from the shared codebase.
- **SC-011**: The shell passes a WCAG 2.2 Level AA audit of its primary screens (Home, catalog, tool page, settings) with no Level A or AA violations.

## Assumptions

- The application is built on the existing Uno Platform app template located at `D:\Personal\uno-app-template` (the `GcToolkit` / `GcToolkit.Core` projects) as its technical starting point; that template supplies the multi-target project structure, dependency-injection/host setup, theming, and localization plumbing this feature builds upon.
- Target platforms are Windows, Android, iOS, macOS, Linux, and WebAssembly, consistent with `docs/PRD.md`.
- Launch languages are English and Czech; the localization mechanism is designed so more can be added later.
- The product is local-first: no user accounts, no backend, and no cloud sync in this feature (consistent with the PRD).
- The catalog ships with a small set of placeholder/sample entries (roughly 2-3 per category) backed by stub pages, used solely to exercise browse, search, favorites, recents, and navigation. They contain no real tool logic and are removed as real tools arrive in SP-4/SP-5.
- The recents list is capped (assumed at 10 most-recent tools) unless changed during design.
- The product's working name is **GC Toolkit** (a placeholder pending the final trademark-safe brand decision before store submission); used in the UI until a final name is chosen.
- The Windows/WinUI target uses the WinUI `TitleBar` control; this control is not yet supported on the other Uno Platform targets, which use a standard title bar and place search in the navigation view's integrated search field. When Uno adds support, the title-bar treatment can be unified.

## Out of Scope

- The shared tool contract and reusable tool UI components (delivered in SP-2).
- Any individual tool's functionality or page content (delivered in SP-4 / SP-5).
- Maps, sensors, and field tools (SP-5).
- Cloud sync, accounts, analytics/telemetry, and the AI assistant (post-MVP, per `ideas.md`).
- Settings beyond theme and language (e.g. measurement units, default coordinate format) — added later with the tools that consume them.
- Use of the WinUI `TitleBar` control on non-Windows targets — deferred until Uno Platform supports it on those targets.

## Dependencies

- The `uno-app-template` repository at `D:\Personal\uno-app-template` as the base codebase.
- The product requirements in `docs/PRD.md` (platforms, localization, local-first, theming, accessibility).
- Downstream phases SP-2 through SP-6 depend on this shell; this feature has no upstream dependency on other phases.
