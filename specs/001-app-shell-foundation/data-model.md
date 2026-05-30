# Phase 1 Data Model: App Shell & Foundation

All types live in `GCToolkit.Core` and are persisted locally via `IPreferences` (no database in SP-1). Identifiers are stable strings so favorites/recents survive renames and re-ordering.

## ToolDescriptor

Immutable metadata describing a catalog entry. Provided by tool contributors via DI; the shell never hardcodes tools.

| Field | Type | Notes |
|---|---|---|
| `Id` | `string` | Stable, unique, invariant (e.g. `coordinates.conversion`). Primary key for favorites/recents. |
| `NameKey` | `string` | Localization key resolved via `{markup:Localize}` / `IStringLocalizer`. |
| `CategoryId` | `string` | References `Category.Id`. |
| `Keywords` | `IReadOnlyList<string>` | Search aliases (localized + invariant); searched alongside the name. |
| `IconKey` | `string?` | Optional icon/glyph identifier. |
| `ViewModelType` | `Type` | Navigation target (view-model-first). For SP-1 placeholders this is the stub `ToolHostViewModel`. |
| `IsPlaceholder` | `bool` | `true` for SP-1 sample entries; removed as real tools land. |

**Validation**: `Id` unique across the catalog (duplicate registration is a startup error); `CategoryId` must match a registered `Category`; `NameKey` must resolve in EN and CS.

## Category

| Field | Type | Notes |
|---|---|---|
| `Id` | `string` | Stable, unique, invariant (e.g. `coordinates`). |
| `NameKey` | `string` | Localization key. |
| `Order` | `int` | Display order in the catalog. |
| `IconKey` | `string?` | Optional. |

**Validation**: `Id` unique; `Order` defines deterministic sort (ties broken by localized name).

**Relationship**: one `Category` has many `ToolDescriptor` (`ToolDescriptor.CategoryId → Category.Id`).

## FavoriteEntry

| Field | Type | Notes |
|---|---|---|
| `ToolId` | `string` | References `ToolDescriptor.Id`. |
| `AddedUtc` | `DateTimeOffset` | When favorited (for stable ordering). |

**Validation / rules**: at most one entry per `ToolId` (toggling removes it). A favorite whose `ToolId` no longer exists in the catalog is ignored when rendering (and may be pruned).

**Persistence**: stored as a JSON list under a single preferences key (`favorites`).

## RecentEntry

| Field | Type | Notes |
|---|---|---|
| `ToolId` | `string` | References `ToolDescriptor.Id`. |
| `LastOpenedUtc` | `DateTimeOffset` | Updated on each open. |

**Validation / rules**:
- **Deduplicated**: one entry per `ToolId`; re-opening updates `LastOpenedUtc` and moves it to the top.
- **Capped**: keep only the 10 most recent; the oldest is evicted beyond that.
- **Ordering**: most-recent-first.
- **Clearable**: the user can clear the whole list (becomes empty).
- Entries referencing tools no longer in the catalog are ignored when rendering.

**Persistence**: stored as a JSON list under a single preferences key (`recents`), already in most-recent-first order.

## AppPreferences (extends the template wrapper)

| Field | Type | Notes |
|---|---|---|
| `Theme` | `ElementTheme` (`Light`/`Dark`/`Default`=system) | Already in the template; reused. |
| `Language` | `string` (`en` / `cs`) | **New.** Default: system language if `en`/`cs`, else `en` (FR-009). |
| `Favorites` | `List<FavoriteEntry>` | **New.** Backed by `IPreferences` complex JSON. |
| `Recents` | `List<RecentEntry>` | **New.** Backed by `IPreferences` complex JSON. |

**State transitions**:
- **Theme**: `System ⇄ Light ⇄ Dark`; applied immediately; persisted.
- **Language**: `en ⇄ cs`; persisted; applied on next launch (restart acceptable, per 2026-05-30 decision).
- **Favorite**: absent ⇄ present (toggle).
- **Recent**: opened → inserted/bumped to top → evicted when it falls past index 10 → cleared (all removed).

## Notes

- These are in-memory domain types with thin JSON persistence; no migrations needed for SP-1. The template's `IAppUpdater` hook remains available for future schema versioning.
- Tool *functionality* and tool *pages* are out of scope; `ViewModelType` simply names where navigation lands (the stub host for placeholders).
