# AGENTS.md

Guidance for AI coding agents working in this repository. Start here for general conventions
and workflow; to **add a tool** to the gallery, jump to **[Building tools for GC Toolbox](#building-tools-for-gc-toolbox)** below.

## Conventions

Detailed, auto-loaded conventions live in **`.claude/rules/`** — read them before writing code:
`code-style.md` (language, naming, `WarningsAsErrors`, Central Package Management),
`architecture.md` (Core/head split, MVVM, DI, navigation, localization, "how to add a page"),
`testing.md` (MSTest/MTP, run command, fakes + FluentAssertions), and `git.md` (commits, branches, versioning).

## Skills & external resources

Lean on installed skills and skill collections instead of reinventing — reach for them proactively:

- **Windows / WinUI** — use the [`win-dev-skills`](https://github.com/microsoft/win-dev-skills) (`winui:*`) skills *extensively*. Uno's API mirrors the WinUI API, so WinUI guidance applies almost verbatim to this app.
- **UI & design** — **`/winui-design` is the primary resource** for any layout, styling, theming, or Fluent-design work.
- **.NET** — use the [dotnet skills](https://github.com/dotnet/skills/) (`dotnet-*`, `dotnet-test:*`, `dotnet-msbuild:*`, `dotnet-upgrade:*`, …) for building, testing, performance, diagnostics, and migrations.
- **Browser automation** — when you need to load a page, screenshot, or verify web/WASM output, drive a browser with the **Playwright MCP** or the **Chrome integration** (either or both).
- **MCP docs & runtime** — use the **Microsoft Learn MCP** (`microsoft_docs_search`/`_fetch`) for authoritative .NET/Windows API docs, and the **Uno docs MCP** (`uno_platform_search`/`_fetch`) for Uno specifics — but **not** for design/UX recommendations (use `/winui-design` for those). To drive and inspect the running app at runtime, pick by target: the **Uno-app MCP** (`uno_app_select_solution` → `uno_discover_tools`/`uno_execute_tool`) for **Uno targets** (Desktop/WASM/mobile) only — it does **not** drive the WinUI/WinAppSDK head; for the **WinUI (Windows) target**, use the **`/winui-ui-testing`** skill (and `/run-winui-app` to build/launch it).

## Tech choices to avoid

This app is plain WinUI/XAML + CommunityToolkit.Mvvm by design. **Do not introduce Uno.Extensions Navigation, C# Markup, or MVUX.** More generally, prefer a WinUI / CommunityToolkit alternative over **Uno.Extensions**, **Uno Toolkit**, or **Uno Themes** whenever one exists. (The hosting/configuration/localization/serialization features already wired in `UnoFeatures` are fine to keep — this is about not adding *new* dependencies on those stacks.)

## Running & automating the WinUI (Windows) app

The app is an Uno single-project app; the WinUI (WinAppSDK) head can be built, launched **fully
packaged with package identity**, and UI-automated entirely from the command line — no Visual
Studio. Use this to see a change working on Windows, screenshot the app, or drive its UI.

Full happy path, gotchas, and the complete `winapp ui` command set live in
**`.claude/skills/run-winui-app/SKILL.md`** — read it before running the Windows head. Quick
reference (PowerShell; the WinUI TFM is the `*-windows*` entry in
`src/GcToolkit/GcToolkit.csproj`):

```powershell
# 1. Build the Windows head
dotnet build src/GcToolkit/GcToolkit.csproj -f net10.0-windows10.0.26100 -c Debug

# 2. Launch packaged + detached (returns AUMID + PID; stays non-blocking so you can automate)
$out = Join-Path (Get-Location) "src\GcToolkit\bin\Debug\net10.0-windows10.0.26100"   # run from the repo root
winapp run $out --exe GcToolkit.exe --detach --json

# 3. Automate the live window (-a is the window TITLE, "GC Toolkit")
#    Workflow: inspect (find a slug) -> act (invoke/click/set-value) -> verify (get-value/wait-for).
winapp ui inspect    -a "GC Toolkit"                                      # discover element slugs
winapp ui invoke     "SettingsItem" -a "GC Toolkit"                       # press by slug or text
winapp ui screenshot -a "GC Toolkit" --output .screenshots\app.png       # -> repo-root .screenshots/ (git-ignored)

# 4. Clean up
Get-Process GcToolkit -ErrorAction SilentlyContinue | Stop-Process -Force
winapp unregister --manifest "$out\AppxManifest.xml"
```

Key gotchas: `winapp` is native Windows — pass `D:\...` paths, not `/d/...`; `--exe
GcToolkit.exe` disambiguates from the co-located `RestartAgent.exe`; `-f <winui-tfm>` is
mandatory on `dotnet run` (without it you get the WebAssembly head); don't background the app with
PowerShell `Start-Job` (it doesn't survive across tool calls) — `winapp run --detach` is the right
primitive. Requires the `winapp` CLI (`winget install Microsoft.WinAppCli`).

## Testing new features in the running app

To verify a new feature end-to-end in the live app, use the **`/run-winui-app` skill** (build →
launch packaged → inspect/act/screenshot → clean up) together with the **`uno-app` MCP**
(`uno_app_select_solution`, then `uno_discover_tools` / `uno_execute_tool`) to drive and inspect the
running app at runtime. Reach for these instead of guessing whether UI changes work — see the change
actually render before reporting it done.

## Build & test

```bash
dotnet tool restore                                                  # once after cloning (XAML Styler)

# Build a head (TFM is per-platform; WinUI TFM is the *-windows* entry in the csproj)
dotnet build src/GcToolkit/GcToolkit.csproj -f net10.0-desktop
dotnet build src/GcToolkit/GcToolkit.csproj -f net10.0-windows10.0.26100

# Run the unit tests (MSTest on Microsoft.Testing.Platform, net10.0)
dotnet test tests/GcToolkit.Core.Tests/GcToolkit.Core.Tests.csproj
```

Logic worth testing lives in **`GcToolkit.Core`** (view models, services, navigation) and belongs
under **`GcToolkit.Core.Tests`** — keep testable code there so it stays head-independent. Tests run
on the **Microsoft Testing Platform**, so **do not pass `--nologo` to `dotnet test`** (MTP rejects it
and runs zero tests).

## Working style

- **TDD.** Write a failing test in `GcToolkit.Core.Tests` first, watch it fail, then implement until
  it passes. Prefer pushing logic into `GcToolkit.Core` so it's unit-testable without a UI head.
- **Use modern C#.** Reach for the latest C# language features (collection expressions, primary
  constructors, pattern matching, etc.) where they make the code clearer — not for their own sake.
- **Keep comments lean.** No walls of text. Comment the *why* in a line or two; let clear names and
  small methods carry the *what*.
- **XAML formatting is CI-enforced.** Run `dotnet xstyler -c Settings.XamlStyler -r -d ./src` before
  committing XAML, or the **XAML Style Check** workflow fails the PR.
- **Report Uno divergences upstream.** WinUI is the reference behavior. If during development you find
  something that's buggy or behaves differently on an Uno target (e.g. `net10.0-desktop`) than on
  WinUI, point it out and offer to file an issue at [`unoplatform/uno`](https://github.com/unoplatform/uno)
  (with a minimal repro and the affected target). Don't open it silently — confirm with the user first.

---

# Building tools for GC Toolbox

This half of the guide is for anyone (human or AI) adding a **tool** to the gallery. It assumes the
attribute-based tool-discovery system (feature `002-tool-discovery`,
[`specs/002-tool-discovery/plan.md`](specs/002-tool-discovery/plan.md)). Read it before adding a tool;
the C#/XAML conventions above (and in [`CLAUDE.md`](CLAUDE.md) / `.claude/rules/`) still apply.

## How discovery works (the 30-second version)

- **All ViewModels live in `GcToolkit.Core`**; **all Views live in the `GcToolkit` app head.**
- A single Roslyn source generator (`GcToolkit.SourceGenerators`, runs against the app head) scans Core
  for `[Tool]`-decorated ViewModels, scans the app head for `ViewBase<TVm>` views, validates the `.resw`
  strings, and emits the catalog, the navigation tree, and **all** view registrations.
- Result: **adding a tool needs no edit to any shared/central file** (no DI line, no `App.xaml.cs`, no
  `WindowShell`). You add a ViewModel, two resources, an icon, and (for real UI) a View — then rebuild.

## Add a tool — checklist

Worked example: the **Morse** tool (issue #53). Files it touches are listed at the end.

1. **Put the logic in a testable Core service, and write MSTest tests first/alongside (TDD).**
   Tools' domain logic must be unit-testable and live in `GcToolkit.Core` (it has no UI dependency).
   - Service: `src/GcToolkit.Core/<Feature>/<Thing>.cs` (e.g. `Alphabets/MorseCodec.cs`).
   - Tests: `tests/GcToolkit.Core.Tests/<Feature>/<Thing>Tests.cs`, `[TestClass]`/`[TestMethod]`,
     named `Method_Scenario_ExpectedResult`. Run with
     `dotnet test tests/GcToolkit.Core.Tests/GcToolkit.Core.Tests.csproj`.
   - Keep the ViewModel thin — it wires services to the UI.

2. **Write the ViewModel** in `src/GcToolkit.Core/ViewModels/Tools/<Tool>ViewModel.cs`, derive
   `ToolViewModelBase`, decorate with `[Tool]`:
   ```csharp
   [Tool("MorseCode", ToolCategory.Alphabets,
         Introduced = "2026-05-31", Updated = "2026-05-31",
         Keywords = ["morse", "code", "kód", "abeceda"])]
   public sealed partial class MorseCodeViewModel : ToolViewModelBase { /* ... */ }
   ```
   - `Id` is a unique, resource-key-safe PascalCase string. It is the localization base key and the
     favorites/recents key — don't rename it later.
   - `Introduced`/`Updated` are ISO `yyyy-MM-dd` (a bad/missing date is a build error). A recent date
     gives the tool a **New/Updated** badge and a slot in Home's "New & Updated".
   - Use `[ObservableProperty]`/`[RelayCommand]` (CommunityToolkit.Mvvm). The base provides
     `ToolName`, `Tooltip`, `IsFavorite`, `ToggleFavoriteCommand`, recents recording.

3. **Crossing the Core→head boundary:** because the VM is in Core, it may only depend on **Core**
   interfaces. Any platform capability must be a Core interface implemented in the head and registered in
   `App.RegisterServices`. Reusable ones already exist:
   `IClipboardService`, `IShareService` (has `ShareTextAsync`), `IMorseAudioService`,
   `INavigationService`. Add new ones the same way (interface in `GcToolkit.Core/Services`, impl in
   `GcToolkit/Services`, `AddScoped` in `App.xaml.cs`). Platform-specific code uses `#if !HAS_UNO`
   (WinAppSDK / Windows) vs `#else` (the Uno heads) — see `MorseAudioService.cs`. When a capability only
   works on some heads, expose an `Is…Supported` flag and hide its UI elsewhere.

4. **Add the two required localized strings** to **both** `src/GcToolkit/Strings/en/Resources.resw`
   **and** `.../cs/Resources.resw` (EN + CS), plus any UI strings your view needs:
   | Key | Required |
   |---|---|
   | `<Id>_Name` | yes — missing in any culture is build error `GCTOOL004` |
   | `<Id>_Tooltip` | yes — same |
   | your UI keys (`Morse*`, …) | as needed; short, descriptive keys, **no `x:Uid`** |
   Use the markup extension in XAML: `Text="{markup:Localize Key=MorsePlay}"`. (UI keys missing at
   runtime render as blank text, not a build error — keep EN and CS in lockstep.)

5. **Drop a tool icon** at `src/GcToolkit/Assets/Icons/Tools/<Id>.png` (see *Icons* below). Optional —
   a missing icon degrades gracefully (it never breaks the build); assets are auto-globbed (no csproj
   edit).

6. **Author the View** (for a real tool with custom UI) in `src/GcToolkit/Views/<Tool>View.xaml` + `.cs`
   using the base+sealed pattern, with `[NavigationInfo(NavigationSection.Tool)]` so pane selection is
   correct:
   ```csharp
   [NavigationInfo(NavigationSection.Tool)]
   public partial class MorseCodeViewBase : ViewBase<MorseCodeViewModel> { }
   public sealed partial class MorseCodeView : MorseCodeViewBase
   {
       public MorseCodeView() => this.InitializeComponent();
   }
   ```
   The generator detects the `ViewBase<TVm>` view and **routes the tool to it** (instead of the shared
   `ToolHostView`) and marks the tool **not a placeholder**. **Omit the View** and the tool still appears,
   but opens the shared host with the "placeholder" notice — handy while a tool is still a stub.
   - Build accessible, responsive XAML: Fluent styles + theme resources, `AutomationProperties.Name` on
     icon-only controls, `AdaptiveTrigger` for padding, a `MaxWidth` for readability.

7. **Add a new category (only if needed):** add a member to `ToolCategory`
   (`src/GcToolkit.Core/Discovery/ToolCategory.cs`) — **append** it so existing order is preserved — add a
   `Category_<Member>` string (EN+CS), and drop `Assets/Icons/Categories/<Member>.png`. Optionally map it
   to a `ToolGroup` in `ToolGrouping.CategoryGroups` to render it under a pane section header.

8. **Build & verify** (Windows-first dev). On top of the general [Build & test](#build--test) commands,
   a tool touches the generator, so also run its test project:
   ```pwsh
   dotnet test tests/GcToolkit.Core.Tests/GcToolkit.Core.Tests.csproj
   dotnet test tests/GcToolkit.SourceGenerators.Tests/GcToolkit.SourceGenerators.Tests.csproj
   dotnet build src/GcToolkit/GcToolkit.csproj -f net10.0-windows10.0.26100   # WinAppSDK head
   dotnet build src/GcToolkit/GcToolkit.csproj -f net10.0-desktop             # Skia desktop
   dotnet format src/GcToolkit.slnx                                            # before staging
   ```
   `WarningsAsErrors=True` repo-wide — new code must be warning-clean. All heads must compile; gate any
   head-specific capability (e.g. audio) behind an `Is…Supported` flag and hide its UI where unsupported.

## Icons (Icons8, Fluency, colorful)

- Use **Icons8** with the **Fluency** style, **colorful** (`platform: fluent`, `isColor: true`). Pick a
  clear metaphor (Morse → "Radio Waves"; the Alphabets category → "Text").
- Convention paths (auto-globbed, no csproj edit):
  - tool: `src/GcToolkit/Assets/Icons/Tools/<Id>.png`
  - category: `src/GcToolkit/Assets/Icons/Categories/<CategoryMember>.png`
- Download a PNG (~200 px is plenty; rendered at 20–32 px in cards/pane):
  `https://img.icons8.com/?id=<iconId>&format=png&size=200`. Resolution at runtime is by convention
  (`Helpers/ToolIcons.cs`); a missing file simply renders nothing.

## Feature parity: compare against a reference tool

For **every** tool we build, aim for parity-or-better with an existing reference implementation.
**Ask the user which parity source to compare against** (a specific site, app, or competing tool) before
building — don't assume one. Once you have it, compare behavior and **match or exceed** it, and capture
the comparison in the spec/PR description.

- Match the reference's conventions so data interoperates (e.g. for Morse: dot `.`, dash `-`, single
  space = letter gap, two-or-more spaces = word gap, `#` for unknown — our decoder also accepts `/`).
- Then do **better** where it's cheap (e.g. Morse adds full ITU punctuation, accented Latin letters,
  audio playback with adjustable WPM, on-screen flash, and copy).
- Also sanity-check the canonical reference (e.g. Wikipedia's ITU Morse table) so coverage is complete.

## Worked example — Morse (issue #53)

- Domain (Core, tested): `Alphabets/MorseCodec.cs`, `MorseTimeline.cs`, `MorseTiming.cs`,
  `MorseSignal.cs`, `MorseAudioRenderer.cs` (+ tests under `tests/GcToolkit.Core.Tests/Alphabets/`).
- ViewModel: `ViewModels/Tools/MorseCodeViewModel.cs` (category `Alphabets`).
- Core service interfaces: `Services/IClipboardService.cs`, `IMorseAudioService.cs`, `IShareService.cs`.
- Head: `Views/MorseCodeView.xaml(.cs)`, `Services/ClipboardService.cs`, `MorseAudioService.cs`
  (WinAppSDK audio via `MediaPlayer`), `ShareService.cs` (`ShareTextAsync`), DI in `App.xaml.cs`.
- Category: `Discovery/ToolCategory.cs` (`Alphabets`); strings `Category_Alphabets` + `Morse*` (EN+CS);
  icons `Assets/Icons/Tools/MorseCode.png`, `Assets/Icons/Categories/Alphabets.png`.
- Generator: `GcToolkit.SourceGenerators` now routes a tool to its dedicated `ViewBase<TVm>` view when one
  exists (and marks it non-placeholder), falling back to the shared `ToolHostView` otherwise.
