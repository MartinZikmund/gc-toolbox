# Phase 1 Data Model: Attribute-Based Tool Auto-Discovery & Navigation Tree

New and extended types. Unless noted, everything lives in **`GcToolkit.Core`** (so it's reachable from the referenced-assembly metadata the generator scans, and unit-testable). Authoring metadata is build-time; nothing new is persisted. Decisions referenced as **R#** map to [research.md](./research.md).

---

## ToolAttribute *(new — authoring surface, `GcToolkit.Core/Discovery`)*

Decorates a tool's ViewModel (a class deriving `ToolViewModelBase`). Discovered by `GcToolkit.SourceGenerators` scanning the referenced `GcToolkit.Core` assembly's metadata (R2). Attribute args are compile-time constants, so dates are ISO strings (R9).

| Member | Type | Notes |
|---|---|---|
| `Id` | `string` (ctor) | **Required.** Explicit, unique, resource-key-safe **PascalCase** (e.g. `CoordinateConversion`). Doubles as the localization base key and the favorites/recents key (R5, FR-005). |
| `Category` | `ToolCategory` (ctor) | **Required.** The single category that directly contains the tool (FR-007). |
| `Introduced` | `string` (ctor or named) | **Required.** ISO `yyyy-MM-dd`. Invalid format → build error (R8). |
| `Updated` | `string` (ctor or named) | **Required.** ISO `yyyy-MM-dd`. (FR-011) |
| `Keywords` | `string[]` (named, optional) | Search aliases merged with the localized name (reuses today's `ToolDescriptor.Keywords`). |

Notes:
- **No `Group` here** — group membership is a property of the *category*, not the tool (R6).
- **No icon argument** — the icon is resolved by convention from `Id` (R7).
- `[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]`.

**Validation (generator, all build errors — R8)**: target derives `ToolViewModelBase`; `Id` present and PascalCase/resource-safe; `Introduced`/`Updated` parse as `yyyy-MM-dd`; `<Id>_Name` and `<Id>_Tooltip` exist in every required culture (`.resw` read via `AdditionalFiles`). Duplicate `Id` is **not** a build error — caught at startup by `CatalogService` (FR-004).

---

## ToolCategory *(new enum — `GcToolkit.Core/Discovery`)*

Closed set; the **required** level that directly holds tools. **Convention-only** metadata (R6).

```
enum ToolCategory { Coordinates, Ciphers, Numbers, Field }   // order = display order
```

| Derived value | Convention |
|---|---|
| `Id` | member name (`Coordinates`) |
| `NameKey` | `Category_<MemberName>` → `Category_Coordinates` (matches existing resw) |
| `Order` | declaration index (`Coordinates`=0 … `Field`=3) |
| Icon key | member name → `Assets/Icons/Categories/<MemberName>.png` (R7) |

Initial set mirrors today's categories. Adding a category = adding an enum member (+ its resw `Category_*` string + a bitmap).

---

## ToolGroup *(new enum — `GcToolkit.Core/Discovery`)*

Closed set; the **optional** "uber-category" that aggregates categories and renders as a pane **section header** (text, no icon). **Convention-only** metadata.

| Derived value | Convention |
|---|---|
| `NameKey` | `Group_<MemberName>` |
| `Order` | declaration index |

**Category → group membership** (the one non-convention relationship, R6) is a small explicit map kept beside the enums:

```
// GcToolkit.Core/Discovery — the minimal exception to "convention only"
static IReadOnlyDictionary<ToolCategory, ToolGroup> CategoryGroups { get; }
//   e.g. { Coordinates → Conversion, Numbers → Conversion }   (Ciphers, Field stand alone)
```

The initial release defines **one** group spanning ≥2 categories so US3/SC-003 (a group heading above its categories) is exercised; the rest stand alone. A category absent from the map has no group and renders without a heading.

---

## ToolDescriptor *(existing record — extended, `GcToolkit.Core/Catalog`)*

The catalog's per-tool record, now produced by the generated contributor. Existing fields kept; additions in **bold**.

| Field | Type | Notes |
|---|---|---|
| `Id` | `string` | Unchanged role; now PascalCase (R5). Primary key for favorites/recents. |
| `NameKey` | `string` | Now derived: `<Id>_Name` (R5). |
| **`TooltipKey`** | `string` | **New.** `<Id>_Tooltip` (FR-005). |
| `CategoryId` | `string` | `ToolCategory.Id`. |
| **`GroupId`** | `string?` | **New.** `ToolGroup` of the tool's category, or null. |
| `Keywords` | `IReadOnlyList<string>` | Unchanged. |
| `IconKey` | `string` | **Now required**, semantics changed: a bitmap **key** (= `Id`) the app head resolves to `ms-appx:///Assets/Icons/Tools/<Id>.png` (R7). |
| **`IntroducedDate`** | `DateOnly` | **New.** Parsed from the attribute (FR-011). |
| **`UpdatedDate`** | `DateOnly` | **New.** Parsed from the attribute. |
| `ViewModelType` | `Type` | The tool's own VM (a `ToolViewModelBase`), per view-model-first navigation (R10, FR-009). |
| `IsPlaceholder` | `bool` | Stub tools set this `true` until real functionality lands (R3). |

**Validation**: `Id` unique (startup, `CatalogService`); `CategoryId` matches a `ToolCategory`; `NameKey`/`TooltipKey` resolve (generator, build error). `UpdatedDate < IntroducedDate` is a tolerated inconsistency (classifier treats as introduced-only; R9).

---

## Category *(existing record — extended, `GcToolkit.Core/Catalog`)*

| Field | Type | Notes |
|---|---|---|
| `Id` | `string` | From `ToolCategory.Id`. |
| `NameKey` | `string` | `Category_<Name>`. |
| `Order` | `int` | Enum declaration order. |
| `IconKey` | `string` | **Now required** (was `string?`): a bitmap key resolved to `Assets/Icons/Categories/<Name>.png` (FR-008a, R7). |
| **`GroupId`** | `string?` | **New.** From the category→group map, or null. |

**Relationship**: `ToolGroup` 1—* `ToolCategory` 1—* `ToolDescriptor`. The generated `NavigationTree` materializes this hierarchy; `CatalogService` continues to expose the flat ordered lists it does today.

---

## ToolViewModelBase *(new — `GcToolkit.Core/ViewModels`)*

Base for stub and (future) real tool VMs; absorbs today's `ToolHostViewModel` host behavior so one shared view can host any tool VM (R10).

| Member | Type | Notes |
|---|---|---|
| `ToolId` | `string` | The tool's `Id` (from its descriptor). |
| `ToolName` | `string` | Localized `<Id>_Name`. |
| `Tooltip` | `string` | Localized `<Id>_Tooltip`. |
| `IsPlaceholder` | `bool` | From the descriptor. |
| `IsFavorite` | `bool` | Via `IFavoriteToolsService`. |
| `ToggleFavoriteCommand` | `IRelayCommand` | Reuses favorites. |
| (lifecycle) | — | On activation: resolve descriptor by `ToolId`, set name/tooltip/`PageTitle`, **record open in `IRecentsService`** (FR-020), set favorite state. |

Each stub tool VM is a small subclass: `[Tool("CoordinateConversion", ToolCategory.Coordinates, Introduced="…", Updated="…")] public sealed partial class CoordinateConversionViewModel : ToolViewModelBase`. It supplies its `Id` to the base (constant), so the base resolves everything else from the catalog.

---

## NavigationTree *(new — type defined in `GcToolkit.Core/Navigation`; instance generated in the app head)*

UI-agnostic group→category→tool model the generator emits (as a `GeneratedToolCatalog.NavigationTree` instance in the app head) and `WindowShell` renders (FR-013). No XAML/controls — pure data (mirrors WinUI Gallery emitting data the app renders).

```
NavigationTree
└─ IReadOnlyList<NavGroupNode> Roots            // groups + group-less categories, in deterministic order
   NavGroupNode    { string? GroupId; string? NameKey; int Order; IReadOnlyList<NavCategoryNode> Categories }
   NavCategoryNode { string CategoryId; string NameKey; string IconKey; int Order; IReadOnlyList<NavToolNode> Tools }
   NavToolNode     { string ToolId; string NameKey; string TooltipKey; string IconKey; Type ViewModelType }
```

Rules baked in by the generator: deterministic order (group order → category order → tool `Id` ordinal, consistent with `CatalogService`); **empty categories/groups omitted** (FR-024); a group-less category appears as a top-level node with no heading (US3 scenario 3).

---

## Generated artifacts *(emitted by `GcToolkit.SourceGenerators` into the app head; not hand-written)*

One generator (running against the app head; discovering Core's `[Tool]` VMs via referenced-assembly metadata + the app-head views via syntax — R2) emits all of the following into `GcToolkit`:

| Artifact | Shape | Role |
|---|---|---|
| `GeneratedToolContributor` | `IToolContributor, ICategoryContributor` | Feeds discovered `ToolDescriptor`s + `Category`s into the **unchanged** `CatalogService` via DI (FR-010, FR-018 — coexists with manual contributors). |
| `GeneratedToolCatalog` | static | `IReadOnlyList<Type> ToolViewModelTypes`; `NavigationTree NavigationTree`. Consumed by `WindowShell` + the registration helpers. |
| `AddDiscoveredTools(IServiceCollection)` | extension method | Registers the generated contributor + each tool VM (transient). Called **once** in `App.RegisterServices`. |
| `RegisterDiscoveredViews(NavigationService)` | extension method | Emits `RegisterView(view, vm)` for every `ViewBase<TVm>` view, **plus** each discovered tool VM → `ToolHostView` (R10). Replaces the hand-written `RegisterView` block; called **once** in the `INavigationService` factory. |

---

## ToolRecencyClassifier + BadgeString *(new — `GcToolkit.Core/Discovery`)*

Date-driven New/Updated, presented WinUI-Gallery-style (R9).

```
enum ToolRecency { None, New, Updated }
ToolRecency Classify(DateOnly introduced, DateOnly updated, DateOnly today, int windowDays = 30)
//  introduced within window         → New
//  introduced older, updated within → Updated
//  both older / either in the future→ None
```

- **`ToolListItem.BadgeString`** *(moved to Core, extended)*: `"New"` / `"Updated"` / `""` from the classifier — same shape as WinUI Gallery's `ControlInfoDataSource` switch; rendered as a small badge on the tool card.
- **`HomeViewModel`** *(moved to Core, extended)*: a **`NewAndUpdatedTools`** collection = tools whose recency `!= None` — the analog of WinUI Gallery's `RecentlyAddedOrUpdatedSamplesList`. Distinct from the existing recently-*used* list.
- Window default = 30 days, a single configurable constant (R9).

---

## ToolListItem & CatalogGroup *(existing — moved to Core, `GcToolkit.Core/ViewModels`)*

Relocated from the app head (R4). Changes: the static `Localizer.Instance` dependency is replaced by an injected/Core localization seam; `ToolListItem` gains `BadgeString` (+ a `HasBadge` convenience). Public shape (`ToolId`, `Name`, `IconKey`, `IsFavorite`, commands) is otherwise preserved so existing Catalog/Home bindings and tests keep working.

## Notes

- **No new persistence / migrations.** Recents/favorites stores are unchanged; the new dates and badges are derived, not stored.
- **Spec deltas** (see research R7/R8): icon-missing is a runtime placeholder (not a build error); missing name/tooltip resources are build **errors** (not warnings). `data-model` reflects the agreed behavior; the spec FRs should be updated to match.
