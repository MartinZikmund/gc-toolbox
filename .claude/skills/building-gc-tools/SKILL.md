---
name: building-gc-tools
description: >-
  Use when adding a new tool to the GC Toolbox / GcToolkit tool gallery — a converter, cipher,
  calculator, encoder, or any geocaching utility. Trigger when the user says "add a tool", "new
  tool", "build a tool", "add X to the gallery", "implement the <something> tool", references a
  tool issue, or asks to create a [Tool]-decorated ViewModel, its View, the localized strings, a
  tool icon, or a new tool category. Covers the attribute-based discovery system: where each file
  goes, the Core/head split, crossing the boundary with services, stubs vs real tools, icons,
  feature parity, and how to build/verify all heads.
---

# Building tools for GC Toolbox

A **tool** is one entry in the gallery (Morse, Roman numerals, Caesar, …). Tools are discovered at
**compile time** by a Roslyn source generator (`GcToolkit.SourceGenerators`) — so adding one needs
**no edit to any shared/central file**: no DI line, no `App.xaml.cs`, no `WindowShell`, no nav table.
You add a ViewModel, two strings, an icon, and (for real UI) a View, then rebuild.

The general C#/XAML conventions in `AGENTS.md` and `.claude/rules/` still apply — read those first if
you haven't. This skill is the tool-authoring workflow specifically.

## How discovery works (30 seconds)

- **All ViewModels live in `GcToolkit.Core`. All Views live in the `GcToolkit` app head.**
- The generator scans Core for `[Tool]`-decorated ViewModels, scans the head for `ViewBase<TVm>`
  views, validates the `.resw` strings, and emits the catalog, the navigation tree, and **all** view
  registrations.
- A tool with a matching `ViewBase<TVm>` view is routed to that view and marked **not a placeholder**.
  A tool with **no** view still appears in the gallery but opens the shared `ToolHostView` with a
  "placeholder" notice — handy for stubbing a tool before its UI exists.

## The shape of a tool

Every tool is the same spine. Confirmed identical across Morse, Roman numerals, and Caesar:

| Piece | Location | Required? |
|---|---|---|
| Domain logic (a pure, testable service) | `src/GcToolkit.Core/<Feature>/<Thing>.cs` | Yes (keep VMs thin) |
| Unit tests for that logic | `tests/GcToolkit.Core.Tests/<Feature>/<Thing>Tests.cs` | Yes (TDD) |
| `[Tool]` ViewModel deriving `ToolViewModelBase` | `src/GcToolkit.Core/ViewModels/Tools/<Tool>ViewModel.cs` | Yes |
| `<Id>_Name` + `<Id>_Tooltip` strings, EN **and** CS | `src/GcToolkit/Strings/{en,cs}/Resources.resw` | Yes (build error if missing) |
| Tool icon | `src/GcToolkit/Assets/Icons/Tools/<Id>.png` | Optional (degrades gracefully) |
| View pair (`<Tool>ViewBase` + `<Tool>View`) | `src/GcToolkit/Views/<Tool>View.xaml(.cs)` | Optional (omit = placeholder stub) |
| New category | `Discovery/ToolCategory.cs` + `Category_<Member>` string + category icon | Only if no existing category fits |

## Checklist

### 1. Logic in a Core service, with tests first (TDD)

Domain logic must be unit-testable and free of UI dependencies, so it lives in `GcToolkit.Core` and
is covered in `GcToolkit.Core.Tests`. Write the failing test first, watch it fail, then implement.

- Service: `src/GcToolkit.Core/<Feature>/<Thing>.cs` (e.g. a codec / cipher / converter).
- Tests: `[TestClass]`/`[TestMethod]`, named `Method_Scenario_ExpectedResult`, MSTest `Assert`.
- Run: `dotnet test tests/GcToolkit.Core.Tests/GcToolkit.Core.Tests.csproj` (MTP runner — **never**
  pass `--nologo`; it runs zero tests).

### 2. The ViewModel

Derive `ToolViewModelBase`, decorate with `[Tool]`. The base already provides `ToolName`, `Tooltip`,
`IsFavorite`, `ToggleFavoriteCommand`, `IsPlaceholder`, and records the open in recents — keep your VM
thin (wire the service to bindings; no UI manipulation).

```csharp
[Tool("Sample", ToolCategory.Numbers,
      Introduced = "2026-06-03", Updated = "2026-06-03",
      Keywords = ["sample", "convert", "vzorek"])]   // include CS aliases for search
public sealed partial class SampleViewModel : ToolViewModelBase
{
    private readonly SampleCodec _codec = new();        // your Core service
    private readonly IClipboardService _clipboard;

    public SampleViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard)                    // tool-specific services come after the base 4
        : base("Sample", catalog, recents, favorites, localizer)
        => _clipboard = clipboard;

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    partial void OnInputTextChanged(string value) => OutputText = _codec.Convert(value);
}
```

`[Tool]` rules:
- **`Id`** (first arg, also the `base(...)` arg) — unique, resource-key-safe **PascalCase**. It is the
  localization base key (`<Id>_Name` / `<Id>_Tooltip`) **and** the favorites/recents key. **Never
  rename it later** — that orphans saved favorites/recents.
- **`Introduced` / `Updated`** — ISO `yyyy-MM-dd`. A bad or missing date is a **build error**
  (`GCTOOL001` / `GCTOOL003`). A recent date earns the tool a **New/Updated** badge and a slot in
  Home's "New & Updated".
- **`Keywords`** — optional search aliases, merged with the localized name. Add Czech aliases too.

### 3. Cross the Core→head boundary with services

The VM is in Core, so it may only depend on **Core** interfaces. Any platform capability is a Core
interface implemented in the head and registered in `App.RegisterServices`. Reusable ones already
exist — inject them, don't reinvent:

| Interface (in `GcToolkit.Core/Services`) | Use for |
|---|---|
| `IClipboardService` | copy the result |
| `IShareService` | `ShareTextAsync(...)` to the OS share sheet |
| `INavigationService` | `Navigate<TViewModel>()` |
| `IMorseAudioService` | tone playback (Morse) — has `IsSupported` |
| `IDisplayRequestManager` | keep the screen awake during playback |

Add a new one the same way: interface in `GcToolkit.Core/Services`, implementation in
`GcToolkit/Services`, registered in `App.RegisterServices` (`App.xaml.cs`) with the right lifetime —
`AddScoped` for per-window services (clipboard, share, audio, navigation all are), `AddSingleton` for
app-wide state (`IDisplayRequestManager` is). Head-specific code uses `#if !HAS_UNO`
(WinAppSDK / Windows) vs `#else` (Uno heads). When a capability only works on some heads, expose an
`Is…Supported` flag on the interface and **hide its UI** where unsupported — every head must compile.

### 4. The two required strings (+ UI keys), EN and CS

Add to **both** `src/GcToolkit/Strings/en/Resources.resw` **and** `.../cs/Resources.resw`:

| Key | Required |
|---|---|
| `<Id>_Name` | Yes — missing in any culture is build error `GCTOOL004` |
| `<Id>_Tooltip` | Yes — same |
| Your UI keys (button labels, headers, …) | As needed |

Keys are short and descriptive (`SamplePlay`, `SampleSwap`) — **no `x:Uid`, no GUIDs**. In XAML use the
markup extension: `Text="{markup:Localize Key=SamplePlay}"`. Missing **UI** keys render as blank text at
runtime (not a build error), so keep EN and CS in lockstep.

### 5. Tool icon

Drop `src/GcToolkit/Assets/Icons/Tools/<Id>.png`. Assets are auto-globbed (no csproj edit); a missing
icon simply renders nothing. Use **Icons8**, **Fluency** style, **colorful** — pick a clear metaphor
(Morse → "Radio Waves"; Roman → a numeral glyph). Use the **Icons8 MCP** (`mcp__icons8mcp__search_icons`
→ `get_icon_png_url`) or the URL form `https://img.icons8.com/?id=<iconId>&format=png&size=200`
(~200 px is plenty; rendered at 20–32 px).

### 6. The View (omit it to ship a stub)

For a real tool with custom UI, author the view as a **non-generic base + sealed view** pair so XAML can
close the generic. Put `[NavigationInfo(NavigationSection.Tool)]` on the base so pane selection works:

```csharp
[NavigationInfo(NavigationSection.Tool)]
public partial class SampleViewBase : ViewBase<SampleViewModel> { }

public sealed partial class SampleView : SampleViewBase
{
    public SampleView() => this.InitializeComponent();
}
```

The XAML root element is the intermediate base: `<local:SampleViewBase x:Class="…SampleView" …>`.
`ViewBase<TVm>` resolves the VM from the per-window scope and sets `DataContext` — **never** `new` a VM
or set `DataContext` by hand. Build accessible, responsive XAML: Fluent styles + theme resources,
`AutomationProperties.Name` on icon-only controls, an `AdaptiveTrigger` for padding, a `MaxWidth` for
readability. Run `/winui-design` for any layout/styling work.

**Root layout — cap width with `HorizontalAlignment="Center"`, never `Stretch`.** The page root is a
`ScrollViewer` whose single child is the content panel, capped with a `MaxWidth` (≈760 for forms) and
**centered**:

```xml
<ScrollViewer Padding="24">
    <StackPanel MaxWidth="760" HorizontalAlignment="Center" Spacing="16">
        …
    </StackPanel>
</ScrollViewer>
```

Do **not** put `HorizontalAlignment="Stretch"` on a `MaxWidth`-capped panel that is a `ScrollViewer`'s
*direct* child. WinUI's old `ScrollViewer` (DirectManipulation) then centers the panel by its *desired*
width but renders it at `MaxWidth`, so it sits off-centre and visibly **"jumps"** whenever the desired
width changes — e.g. the moment you type the first character into a `TextBox`. This is the deliberately
won't-fix WinUI bug [microsoft-ui-xaml#4619](https://github.com/microsoft/microsoft-ui-xaml/issues/4619)
(does not repro in the newer `ScrollView` control). An outer `<Grid MaxWidth=… HorizontalAlignment="Center">`
wrapper around the `ScrollViewer` is an equivalent older fix some views still use; new tools should prefer
the single-attribute `HorizontalAlignment="Center"` form above.

**Stub form** — to land a tool before its UI exists, give it just the `[Tool]` VM (a one-liner via a
primary constructor) and **no view**. It shows in the gallery and opens the placeholder host:

```csharp
[Tool("Sample", ToolCategory.Numbers, Introduced = "2026-06-03", Updated = "2026-06-03",
      Keywords = ["sample"])]
public sealed partial class SampleViewModel(
    ICatalogService catalog, IRecentsService recents,
    IFavoriteToolsService favorites, IStringLocalizer localizer)
    : ToolViewModelBase("Sample", catalog, recents, favorites, localizer);
```

### 7. New category — only if none fits

Categories are a closed enum, metadata-by-convention. To add one:
1. **Append** a member to `ToolCategory` (`src/GcToolkit.Core/Discovery/ToolCategory.cs`) — append so
   existing display order (declaration index) is preserved.
2. Add a `Category_<Member>` string (EN + CS).
3. Drop `src/GcToolkit/Assets/Icons/Categories/<Member>.png`.
4. *(Optional)* map it to a `ToolGroup` in `ToolGrouping.CategoryGroups`
   (`Discovery/ToolGroup.cs`) to render it under a pane section header, adding a `Group_<Member>` string
   if you also add a new group. A category absent from the map renders with no heading.

### 8. Build & verify

A tool touches the generator, so also run its test project. All heads must compile (gate any
head-specific capability behind `Is…Supported`).

```pwsh
dotnet test tests/GcToolkit.Core.Tests/GcToolkit.Core.Tests.csproj
dotnet test tests/GcToolkit.SourceGenerators.Tests/GcToolkit.SourceGenerators.Tests.csproj
dotnet build src/GcToolkit/GcToolkit.csproj -f net10.0-windows10.0.26100   # WinAppSDK head
dotnet build src/GcToolkit/GcToolkit.csproj -f net10.0-desktop             # Skia desktop
dotnet xstyler -c Settings.XamlStyler -r -d ./src                          # XAML format (CI-enforced)
dotnet format src/GcToolkit.slnx                                           # before staging
```

To see the tool actually render and drive its UI, use the **`/run-winui-app` skill** (build → launch
packaged → inspect/click/screenshot → clean up) plus the **uno-app MCP** for runtime inspection. Don't
report a tool done before you've seen it open in the gallery.

## Feature parity — ask first, then beat it

For every tool, aim for parity-or-better with a reference implementation. **Ask the user which parity
source to compare against** (a specific site, app, or competing tool) before building — don't assume.
Also sanity-check the canonical source (e.g. Wikipedia's table) so coverage is complete.

- **Match conventions** so data interoperates with the reference (e.g. Morse: dot `.`, dash `-`, single
  space = letter gap, two+ spaces = word gap, `#` for unknown).
- **Then do better** where it's cheap — the existing tools each exceed parity (Morse adds full
  punctuation/accents + audio + visual flash; Caesar adds a "show all shifts" brute-force view; Roman
  adds the vinculum range + lenient decoding with warnings). Capture the comparison in the PR.
- Aim to cover all features the parity source has, and then exceed it with additional features (especially when the device capabilities like sensors would provide even better experiences than the reference).

## Common mistakes

| Mistake | Fix |
|---|---|
| Putting the VM or logic in the head | VMs and domain logic go in `GcToolkit.Core` so they're testable; only impls live in the head |
| Renaming `Id` after release | Don't — it's the favorites/recents/localization key. Orphans saved state |
| String added to EN only | Add `<Id>_Name`/`<Id>_Tooltip` to **both** cultures or the build fails (`GCTOOL004`) |
| Forgetting `[NavigationInfo(NavigationSection.Tool)]` | Pane selection breaks; put it on the `ViewBase` |
| VM reaching into controls | VMs interact only through bindings + service interfaces (head-independent) |
| Editing `App.xaml.cs`/nav tables to register the tool | Unnecessary — discovery is automatic. Only register a **new service** in DI |
| A head-specific API with no guard | Gate behind `Is…Supported`, hide its UI elsewhere, or the other heads won't compile |
| Hand-editing version numbers | Versions come from Nerdbank.GitVersioning — never touch manifests |

## Anchor types (don't guess these)

- `ToolViewModelBase` (`GcToolkit.Core/ViewModels/`) — base ctor is
  `(string toolId, ICatalogService, IRecentsService, IFavoriteToolsService, IStringLocalizer)`.
- `[Tool]` (`GcToolkit.Core/Discovery/ToolAttribute.cs`) — `(string id, ToolCategory category)` +
  `Introduced`, `Updated`, `Keywords`.
- `ToolCategory` / `ToolGroup` / `ToolGrouping` (`GcToolkit.Core/Discovery/`).
- `ViewBase<TVm>`, `[NavigationInfo]` (head) — the view contract the generator routes to.
