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

**Adding a tool to the gallery?** The full step-by-step workflow lives in the **`building-gc-tools`
skill** — read [`.claude/skills/building-gc-tools/SKILL.md`](.claude/skills/building-gc-tools/SKILL.md)
before you start (agents auto-load it on any "add a tool" request).

The 30-second version: tools are discovered at **compile time** by `GcToolkit.SourceGenerators`, so
adding one needs **no edit to any shared/central file** (no DI line, no `App.xaml.cs`, no nav table).
You add a `[Tool]`-decorated ViewModel in `GcToolkit.Core`, its `<Id>_Name`/`<Id>_Tooltip` strings
(EN **and** CS), an icon, and — for real UI — a `ViewBase<TVm>` View pair in the app head, then
rebuild. Omit the View to ship a placeholder stub. The skill covers the Core/head split, crossing the
boundary with services, icons, feature parity, new categories, and building/verifying every head; the
discovery system itself is specified in [`specs/002-tool-discovery/plan.md`](specs/002-tool-discovery/plan.md).
