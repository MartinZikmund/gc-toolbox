# Contract: Authoring Surface — `[Tool]` Attribute, Enums & Conventions

The public, stable surface a tool author touches. Designed so discovery can later span multiple referenced assemblies without changing the attribute's shape (FR-017). Decisions referenced as **R#** map to [../research.md](../research.md).

---

## 1. `[Tool]` attribute

```csharp
namespace GcToolkit.Core.Discovery;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ToolAttribute : Attribute
{
    public ToolAttribute(string id, ToolCategory category, string introduced, string updated);

    public string Id { get; }              // unique, PascalCase, resource-key-safe; localization base key (R5)
    public ToolCategory Category { get; }  // the single category that holds the tool (FR-007)
    public string Introduced { get; }      // ISO yyyy-MM-dd (R9)
    public string Updated { get; }         // ISO yyyy-MM-dd
    public string[] Keywords { get; init; } = [];  // optional search aliases
}
```

**Placement contract**: applied to a `class` deriving `ToolViewModelBase`. Anything else is a build error (see [generators.md](./generators.md) `GCTOOL002`).

**Authoring example** (a converted placeholder, R3):

```csharp
[Tool("CoordinateConversion", ToolCategory.Coordinates,
      Introduced = "2026-05-31", Updated = "2026-05-31",
      Keywords = ["wgs84", "utm", "convert", "souřadnice", "gps"])]
public sealed partial class CoordinateConversionViewModel : ToolViewModelBase
{
    public CoordinateConversionViewModel(/* injected services via base */)
        : base("CoordinateConversion") { }
}
```

**Not on the attribute** (by design): the **icon** (resolved by convention from `Id`, R7) and the **group** (a property of the category, not the tool, R6).

---

## 2. Localization convention (R5, FR-005)

The `Id` is the base key; two derived `.resw` resources are **required** in every supported culture (EN + CS today):

| Resource | Example key | Example value (EN) |
|---|---|---|
| Display name | `CoordinateConversion_Name` | `Coordinate conversion` |
| Tooltip | `CoordinateConversion_Tooltip` | `Convert between coordinate formats (WGS84, UTM, …).` |

A missing `<Id>_Name` or `<Id>_Tooltip` in any required culture is a **build error** (R8; `GCTOOL004`). Today's `Tool_*` keys are re-keyed to `<Id>_Name` and new `<Id>_Tooltip` keys added.

---

## 3. `ToolCategory` — required level, convention-only metadata (R6)

```csharp
public enum ToolCategory { Coordinates, Ciphers, Numbers, Field }  // declaration order = display order
```

| Derived | Rule | Example |
|---|---|---|
| `Id` | member name | `Coordinates` |
| `NameKey` | `Category_<Member>` | `Category_Coordinates` |
| `Order` | declaration index | `0` |
| Icon | `Assets/Icons/Categories/<Member>.png` (app head) | runtime-resolved; placeholder fallback (R7) |

**Add a category** = add a member + a `Category_<Member>` resw string (EN+CS) + a bitmap. No other edits.

---

## 4. `ToolGroup` — optional "uber-category", convention-only metadata (R6)

```csharp
public enum ToolGroup { /* e.g. */ Conversion }  // declaration order = display order
```

| Derived | Rule |
|---|---|
| `NameKey` | `Group_<Member>` |
| `Order` | declaration index |
| Icon | none — groups render as text headers |

**Category → group** is the single relationship not derivable from a name, held in a small explicit map beside the enums:

```csharp
public static class ToolGrouping
{
    public static IReadOnlyDictionary<ToolCategory, ToolGroup> CategoryGroups { get; } =
        new Dictionary<ToolCategory, ToolGroup>
        {
            [ToolCategory.Coordinates] = ToolGroup.Conversion,
            [ToolCategory.Numbers]     = ToolGroup.Conversion,
            // Ciphers, Field → stand alone (absent ⇒ no group)
        };
}
```

The initial release defines **one** group over ≥2 categories so US3/SC-003 can be verified; the exact grouping is easily changed here.

---

## 5. Extended catalog records (consumed downstream)

`ToolDescriptor` gains `TooltipKey`, `GroupId?`, `IntroducedDate`, `UpdatedDate`; `IconKey` becomes required (bitmap key). `Category` gains required `IconKey` + `GroupId?`. Full field tables in [../data-model.md](../data-model.md). The records remain immutable; `CatalogService`'s contract (`GetCategories`/`GetTools`/`GetToolsByCategory`/`Search`) is **unchanged** — it just receives richer descriptors from the generated contributor.

---

## 6. Stability / forward-compat guarantees

- Adding an optional named property to `ToolAttribute` is non-breaking.
- The `Id` is the durable identity for favorites/recents; renaming a VM type does **not** change it (R5).
- Discovery is single-assembly (`GcToolkit.Core`) today; the attribute shape and the generated contributor are designed to aggregate additional referenced assemblies later with no authoring change (FR-017).
