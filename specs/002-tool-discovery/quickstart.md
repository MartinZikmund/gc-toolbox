# Quickstart: Attribute-Based Tool Auto-Discovery & Navigation Tree

How to build/run/test the feature and — the headline — **add a tool with zero edits to any shared file** (SC-001/SC-008). Paths are repository-relative. Decisions referenced as **R#** map to [research.md](./research.md).

## Prerequisites

- .NET 10 SDK (pinned via `global.json`; `Uno.Sdk` 6.7.0-dev.68).
- Uno Platform workloads for the heads you build (Windows/Android/iOS/Desktop/WebAssembly).
- Solution: `src/GcToolkit.slnx`. Central Package Management is on (`src/Directory.Packages.props`); `WarningsAsErrors=True` is repo-wide.

## Build & run

```pwsh
# from repo root
dotnet build src/GcToolkit.slnx                      # builds Core, app, GcToolkit.SourceGenerators, tests
dotnet build src/GcToolkit/GcToolkit.csproj -f net10.0-windows10.0.26100   # Windows head
dotnet build src/GcToolkit/GcToolkit.csproj -f net10.0-desktop             # macOS/Linux via Skia desktop
```

`GcToolkit.SourceGenerators` runs as part of the app-head build (referenced as an analyzer). Generated files appear under `src/GcToolkit/obj/.../generated/` — useful when diagnosing `GCTOOL####` diagnostics.

## Test

```pwsh
dotnet test tests/GcToolkit.Core.Tests/GcToolkit.Core.Tests.csproj                       # catalog, recency classifier, nav-tree shaping
dotnet test tests/GcToolkit.SourceGenerators.Tests/GcToolkit.SourceGenerators.Tests.csproj # generated output snapshots + GCTOOL diagnostics
```

(MSTest on the Microsoft Testing Platform runner, per `global.json`.)

---

## Add a tool (the core workflow — proves zero central edits)

1. **Write the ViewModel** in `src/GcToolkit.Core/ViewModels/Tools/`, deriving `ToolViewModelBase` and decorated with `[Tool]`:

   ```csharp
   using GcToolkit.Core.Discovery;
   using GcToolkit.Core.ViewModels;

   namespace GcToolkit.Core.ViewModels.Tools;

   [Tool("BaseConverter", ToolCategory.Numbers,
         Introduced = "2026-05-31", Updated = "2026-05-31",
         Keywords = ["binary", "hex", "base", "soustava", "číslo"])]
   public sealed partial class BaseConverterViewModel : ToolViewModelBase
   {
       public BaseConverterViewModel() : base("BaseConverter") { }
   }
   ```

2. **Add the two localized strings** to `src/GcToolkit/Strings/en/Resources.resw` **and** `.../cs/Resources.resw`:

   | Key | EN | CS |
   |---|---|---|
   | `BaseConverter_Name` | `Base converter` | `Převodník soustav` |
   | `BaseConverter_Tooltip` | `Convert numbers between bases.` | `Převod čísel mezi soustavami.` |

   Both keys are **required in both cultures** — a missing one is a build error (`GCTOOL004`, R8).

3. **Drop a bitmap** at `src/GcToolkit/Assets/Icons/Tools/BaseConverter.png` (Icons8). Optional in the sense that a missing file falls back to a runtime placeholder (R7) — it never breaks the build.

4. **Rebuild.** The tool now appears in the catalog and under **Numbers** in the navigation pane, opens via the shared tool host, and records into recents — with **no** `IToolContributor`, **no** DI line, and **no** `WindowShell`/`App.xaml.cs` edit (SC-001, SC-008). Removing the `[Tool]` attribute and rebuilding removes it (US1 scenario 2).

If the tool's `Introduced`/`Updated` date is within the recency window (default 30 days), it shows a **"New"**/**"Updated"** badge and appears in Home's **New & Updated** section (R9).

## Add a category

Add a member to `ToolCategory` (`src/GcToolkit.Core/Discovery/ToolCategory.cs`); add a `Category_<Member>` string (EN+CS); drop `Assets/Icons/Categories/<Member>.png`. Convention derives the rest (id, name key, order, icon). No other edits (R6).

## Add / assign a group

Add a member to `ToolGroup`; add a `Group_<Member>` string (EN+CS); map the relevant categories to it in `ToolGrouping.CategoryGroups`. The group renders as a pane section header above its categories; categories not in the map stand alone (R6).

---

## How it fits together (one rebuild)

```
all VMs + [Tool] types + enums (GcToolkit.Core)        Views (GcToolkit app head)
        └──────────────┬───────────────────────────────────────┘
                       │  GcToolkit.SourceGenerators  (one analyzer, referenced by the app head)
                       │   • scans referenced Core metadata for [Tool] VMs
                       │   • scans app-head ViewBase<TVm> views (syntax)
                       │   • validates <Id>_Name/_Tooltip in .resw (AdditionalFiles)
                       ▼  emits into the app head:
GeneratedToolContributor (IToolContributor/ICategoryContributor) ──▶ CatalogService (unchanged, via DI)
GeneratedToolCatalog.NavigationTree ─────────────────────────────▶ WindowShell pane (groups/categories/tools)
AddDiscoveredTools(services) ─────── called once in App.RegisterServices
RegisterDiscoveredViews(nav) ─────── called once in the INavigationService factory
        (every view↔VM pair, plus each tool VM ▶ shared ToolHostView)
```

## Verify (acceptance smoke)

- **US1**: add an attributed VM → appears in the catalog; remove attribute → gone.
- **US2**: switch language (Settings) → name + tooltip change in catalog and pane.
- **US3**: pane shows group headers, expandable+clickable categories, tools; expanding reveals tools without navigating; clicking a category opens its scoped Catalog (which **ignores** the global search box, R12); selecting a tool opens it.
- **US4**: tools dated inside/outside the window classify as New/Updated/neither and surface in Home's New & Updated.
- **US5**: bad target / bad date / missing name|tooltip resource fail the build with a `GCTOOL###` message; a duplicate `Id` fails at startup.
- **SC-006**: with the 10 stub tools in place, delete `PlaceholderToolContributor` → catalog/pane fully populated by discovery; recents/favorites/search tests still pass.
