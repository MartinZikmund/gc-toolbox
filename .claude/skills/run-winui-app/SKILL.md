---
name: run-winui-app
description: >-
  Build, launch, and UI-automate the WinUI (Windows / WinAppSDK) head of this Uno app from the
  command line — no Visual Studio. Use this whenever the user wants to run, start, launch, or
  screenshot the Windows/WinUI/WinAppSDK target, see a change working in the real Windows app,
  verify a fix on Windows, or drive the app's UI (click buttons, inspect the visual tree, set
  text, wait for elements). Trigger even if the user just says "run the app on Windows", "show me
  the Windows app", "take a screenshot of the app", or "click Settings in the app" without naming
  WinUI explicitly. Launches a fully packaged app (with package identity) via `winapp run` and
  automates it via `winapp ui`.
---

# Run & automate the WinUI app

This Uno single-project app has a WinUI (WinAppSDK) head. This skill builds it, launches it
**fully packaged with package identity** straight from the CLI (no Visual Studio, no manual
`Add-AppxPackage`), and drives its UI with the Windows App Development CLI (`winapp`).

Use it when you need to see something working in the real Windows app — a layout change, a new
page, a bug fix — or when the user asks to click around, screenshot, or inspect the running app.

## Why two tools

- **`dotnet build` / `dotnet run`** compiles the head and produces a loose-layout package folder.
- **`winapp`** (Windows App Development CLI, v0.3+) registers that loose layout with a development
  package identity, launches it, and exposes UI Automation (`winapp ui`) so you can inspect and
  interact with the live window. It works against any running Windows app (WinUI 3, WPF, WinForms,
  Win32, Electron), so the same `ui` commands work here.

## Prerequisites (usually already satisfied)

- .NET 10 SDK (`dotnet --version` → 10.x). The repo's `global.json` pins the Uno SDK.
- `winapp` CLI: `winapp --version` should print `0.3.x` or newer. If missing:
  `winget install Microsoft.WinAppCli` (or `npm install -g @microsoft/winappcli`).
- No extra NuGet package is needed for the loose layout — `Uno.Sdk` already emits a packaged
  loose layout with an `AppxManifest.xml` for the Windows head. (The
  `Microsoft.Windows.SDK.BuildTools.WinApp` package referenced for the Windows target additionally
  enables plain `dotnet run` — see `src/GcToolkit/Platforms/Windows/README.md`.)

## The happy path

Run these from PowerShell. `winapp` is a Windows executable, so always pass **Windows-style
paths** (`D:\...`), never bash `/d/...` paths.

### 1. Find the WinUI target framework

It is the `*-windows*` entry in `<TargetFrameworks>` of `src/GcToolkit/GcToolkit.csproj`
(currently `net10.0-windows10.0.26100`, but read it from the csproj in case it has bumped). Call
it `$TFM` below. The output folder is `src/GcToolkit/bin/Debug/$TFM/` and the launchable exe is
`GcToolkit.exe` (it matches the project/assembly name — note `RestartAgent.exe` also sits in
that folder, which is why we disambiguate with `--exe` below).

### 2. Build the head

```powershell
dotnet build src/GcToolkit/GcToolkit.csproj -f net10.0-windows10.0.26100 -c Debug
```

`AppChannel` defaults to `Dev` (see `src/Directory.Build.props`), so the package identity
(`dev.mzikmund.gctoolkit.dev`) and the `DEV` corner badge are set automatically — no extra
flags. A first build takes ~1 min; rebuilds are fast.

### 3. Launch — pick the path that fits

**Path A — automation (recommended for an agent).** `winapp run` with `--detach` registers the
package, launches it, and **returns immediately** with the AUMID and PID, so you stay in control
to automate or screenshot it:

```powershell
$out = Join-Path (Get-Location) "src\GcToolkit\bin\Debug\net10.0-windows10.0.26100"   # from the repo root; swap the TFM if it bumped
winapp run $out --exe GcToolkit.exe --detach --json
# -> { "AUMID": "dev.mzikmund.gctoolkit.dev_...!App", "ProcessId": 470772 }
```

Useful extra flags: `--unregister-on-exit` (auto-clean when the app closes), `--clean` (wipe the
app's LocalState/settings before deploying), `--debug-output` (stream OutputDebugString + first-
chance exceptions; can't be combined with `--detach`).

**Path B — simple foreground run (for a human watching).** This **blocks the terminal** until the
app closes, so don't use it when you need to run more commands afterward:

```powershell
dotnet run --project src/GcToolkit/GcToolkit.csproj -f net10.0-windows10.0.26100 -c Debug
```

The `-f net10.0-windows10.0.26100` is **required**: `launchSettings.json` lists the WebAssembly
profile first, so a bare `dotnet run` would launch WASM. Passing the Windows TFM makes `dotnet
run` auto-select the compatible `MsixPackage` profile and launch the packaged WinUI app.

> Do not use PowerShell `Start-Job` to background the app: background jobs do **not** survive
> across separate tool calls. `winapp run --detach` launches a real detached OS process that
> persists — that's the correct primitive for an agent.

### 4. Automate the running app with `winapp ui`

Every `ui` command targets the app with `-a "GC Toolkit"`. That value is the **window title**,
which is `GC Toolkit` (the `ApplicationName` resource in `src/GcToolkit/Strings/en/Resources.resw`).
If unsure, discover it first:

```powershell
winapp ui list-windows                   # all windows; find yours by process "GcToolkit"
winapp ui status -a "GC Toolkit"          # confirm connection (process, PID, HWND)
```

#### The core loop: inspect → act → verify

You drive the app through the UI Automation tree, not pixels. Don't guess element names — the
reliable rhythm is:

1. **Inspect** to discover what's on screen and get a selector for the target element.
2. **Act** on that selector (invoke / click / set-value / …).
3. **Verify** the result (get-value, a fresh inspect, wait-for, or a screenshot) before the next
   step — the tree changes after navigation, popups, and toggles.

```powershell
winapp ui inspect -a "GC Toolkit"           # full tree: slugs, types, names, bounds
winapp ui inspect -a "GC Toolkit" -i        # interactive elements only (buttons, inputs, list items)
winapp ui search  "Settings" -a "GC Toolkit"   # find elements whose name matches text
```

#### Selectors: prefer the slug

`inspect`/`search` print each element as a **semantic slug** — the first token, e.g.
`cmb-theme-cd0a`, `itm-dark-e7bf`, `SettingsItem`. You can target an element two ways:

- **By slug (preferred):** precise and unambiguous. Copy it verbatim from `inspect`.
- **By visible text:** convenient (`invoke "Settings"`), but text often matches *several* nodes —
  e.g. a list item *and* its inner text label — and the command then refuses and lists the
  candidates. When that happens, re-issue it with the exact slug.

Slugs carry a short hash derived from the element's UIA RuntimeId, so **a slug can go stale after
the UI changes** (you'll see "RuntimeId hash doesn't match — re-run inspect"). Re-inspect after
any navigation or popup and use the fresh slug.

#### Acting on elements

```powershell
# invoke = the smart "activate it". Tries Invoke → Toggle → SelectionItem → ExpandCollapse
# patterns in order, so it presses buttons, flips toggles/checkboxes, expands combos/expanders,
# and selects list/combo items. This is your default for "press this".
winapp ui invoke "SettingsItem" -a "GC Toolkit"

# click = a real mouse click at the element's coordinates. Use when there is no UIA pattern
# (column headers, custom-drawn items) or you specifically need a mouse gesture.
winapp ui click "<slug>" -a "GC Toolkit"            # add --double or --right as needed

# set-value = type text via the UIA ValuePattern. Works ONLY on *editable* controls
# (TextBox, editable ComboBox, Slider). Selection-only controls reject it — see the combo
# example below.
winapp ui set-value "txt-name-a3" "Hello world" -a "GC Toolkit"

winapp ui focus           "<slug>" -a "GC Toolkit"   # move keyboard focus (e.g. before typing)
winapp ui scroll-into-view "<slug>" -a "GC Toolkit"  # bring an off-screen element into view
```

#### Reading state to verify

```powershell
winapp ui get-value    "<slug>" -a "GC Toolkit"               # current text/value
winapp ui get-property "<slug>" -a "GC Toolkit"               # all UIA props (or --property X)
winapp ui get-focused  -a "GC Toolkit"                        # what currently has focus
winapp ui screenshot   -a "GC Toolkit" --output .screenshots\app.png   # then read the PNG back
```

Always write screenshots into the repo-root **`.screenshots/`** folder (it's git-ignored) — create
it first if missing (`New-Item -ItemType Directory -Force .screenshots`) and give each capture a
descriptive name. After capturing, read the PNG back with the Read tool to actually look at it.

#### Synchronizing with async UI

After an action that triggers loading, navigation, or a dialog, **wait for the result instead of
sleeping a fixed amount** — it's faster and far less flaky:

```powershell
winapp ui wait-for "<slug>" -a "GC Toolkit" --timeout 10000    # until it appears / reaches a value
```

#### Worked example — change the theme (a selection-only ComboBox)

This shows the whole loop, including the common gotcha that `set-value` does **not** work on a
non-editable ComboBox — you expand it and pick the item instead:

```powershell
winapp ui invoke "SettingsItem" -a "GC Toolkit"          # 1. navigate to Settings
winapp ui inspect -a "GC Toolkit" -i                     # 2. discover the "Theme" combo's slug
winapp ui invoke "cmb-theme-cd0a" -a "GC Toolkit"        # 3. expand it (ExpandCollapsePattern)
winapp ui search "Dark" -a "GC Toolkit"                  # 4. find the item; note "Dark" is ambiguous
winapp ui invoke "itm-dark-e7bf" -a "GC Toolkit"         # 5. select it by its exact slug
winapp ui get-value "cmb-theme-cd0a" -a "GC Toolkit"     # 6. verify -> "Dark"
```

> The slugs above (`cmb-theme-cd0a`, `itm-dark-e7bf`) are illustrative — always read the current
> ones from your own `inspect`/`search` output, since the hash suffix is regenerated.

### 5. Clean up

```powershell
Get-Process GcToolkit -ErrorAction SilentlyContinue | Stop-Process -Force
winapp unregister --manifest "$out\AppxManifest.xml"
```

`winapp unregister` only removes development-mode registrations (it's a no-op otherwise, which is
safe). Skip the manual stop if you launched with `--unregister-on-exit` and the app has closed.

## Gotchas worth remembering

- **Paths:** `winapp` is native Windows — give it `D:\...` paths. The Bash tool's working
  directory also persists between calls, so prefer absolute paths over `cd`.
- **`--exe GcToolkit.exe`:** the output folder contains both `GcToolkit.exe` and
  `RestartAgent.exe`; `winapp run` needs the disambiguation.
- **`-a` matches the window title** (`GC Toolkit`), not the manifest DisplayName.
- **`-f` on `dotnet run` is mandatory** — otherwise you get the WebAssembly head.
- **Don't background with `Start-Job`** — use `winapp run --detach`.
- Run `winapp <command> --help` (e.g. `winapp run --help`, `winapp ui --help`) for the full,
  current flag list; the CLI also supports `--cli-schema` for machine-readable command structure.

## Reference

- dotnet run for packaged apps:
  https://devblogs.microsoft.com/ifdef-windows/introducing-dotnet-new-templates-for-winui/
- Windows App Development CLI v0.3 (`run` + `ui`):
  https://devblogs.microsoft.com/ifdef-windows/windows-app-development-cli-v0-3-new-run-and-ui-commands-plus-dotnet-run-support-for-packaged-apps/
