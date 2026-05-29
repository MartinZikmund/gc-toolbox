# Versioning Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Invert the versioning model so `release/v{minor}` branches are the source of stable, store-published builds while `main` produces sideload-safe Dev-channel artifacts that install side-by-side with the production app on every platform.

**Architecture:** Single MSBuild property (`AppChannel = Dev|Prod`) drives per-platform identity (Android `applicationId`, iOS Bundle ID, Windows Identity Name via signing cert + Uno SDK manifest synthesis), display name, icon, and an in-app DEV banner. NBGV publishes stable versions only on `release/v*`; everywhere else produces `0.X.0-dev.{height}`. Workflows are gated by a public/private repository check so private forks don't burn paid Actions minutes on auto-builds.

**Tech Stack:** .NET 10 Uno Platform (`Uno.Sdk`), Nerdbank.GitVersioning, MSBuild conditional property groups, GitHub Actions, MSTest (for the existing GcToolkit.Core.Tests project — no new test project added; the changes are build-time configuration).

**Reference design:** `docs/superpowers/specs/2026-05-28-versioning-redesign-design.md` (sections cited inline as §N).

**User preference:** Stage changes only (`git add`); do NOT commit. Stage at the end of each task.

**Project conventions:** `.editorconfig` mandates spaces; run `dotnet format whitespace src/GcToolkit.slnx` before staging. Tests use Microsoft.Testing.Platform — never pass `--nologo`. Fast build target: `dotnet build src/GcToolkit/GcToolkit.csproj -f net10.0-desktop`.

---

## File Structure

### Created

- `src/GcToolkit/Infrastructure/AppEnvironment.cs` — static class exposing `IsDevChannel`, `ChannelLabel` constants for the in-app banner.
- `src/GcToolkit/Controls/DevChannelBadge.xaml` (+ `.cs`) — small overlay control that renders the DEV badge when `AppEnvironment.IsDevChannel` is true.
- `src/GcToolkit/Assets/Icons/icon_dev.svg` — Dev variant of the main app icon (with DEV ribbon overlay).
- `src/GcToolkit/Assets/Icons/icon_transparent_dev.svg` — Dev variant of the transparent icon.
- `src/GcToolkit/appsettings.Dev.json` — Dev-channel appsettings overlay (empty placeholder for now; documents the pattern).
- `.github/workflows/tag-release.yml` — pushes annotated `v{version}` tag after all three packaging jobs succeed on a `release/v*` push.
- `.github/workflows/prepare-release.yml` — `workflow_dispatch` action that runs `nbgv prepare-release`, pushes `release/v{current}`, opens a PR for main's `version.json` bump.
- `docs/versioning.md` — full versioning reference (replaces whatever PR #7 added).
- `docs/versioning-migration.md` — migration guide for existing apps (stopwatch, daily-dozen).

### Modified

- `version.json` — `publicReleaseRefSpec` from main to `release/v\d+\.\d+`; `firstUnstableTag` from `beta` to `dev`.
- `src/Directory.Build.props` — add `AppChannel` default + `APP_CHANNEL_DEV` compile constant.
- `src/GcToolkit/GcToolkit.csproj` — per-channel `ApplicationId`/`ApplicationTitle`/`UnoIconBackgroundFile` conditionals.
- `src/GcToolkit/WindowShell.xaml` — host the `DevChannelBadge` overlay in the root grid.
- `.github/workflows/package-windows.yml` — add `release/v*` trigger, public/private guard, channel-selection step, switch signing cert per channel, remove "main is public-release branch" comment.
- `.github/workflows/package-android.yml` — same triggers/guard/channel; gate the Play upload step to `release/v*` only; conditional `packageName` per channel.
- `.github/workflows/package-ios.yml` — same triggers/guard/channel; gate the TestFlight upload step to `release/v*` only; main-channel build is verify-only (no `.ipa` to avoid the dev-provisioning-profile setup overhead in the template).
- `.github/workflows/validate-manifest-version.yml` — no behavioral change; add a comment clarifying it guards the template Identity-Version placeholder.
- `README.md` — add a one-paragraph "Versioning" section pointing at `docs/versioning.md`.

### Not changed

- `Package.appxmanifest` — stays minimal; Uno SDK synthesizes the full manifest from csproj properties. Publisher CN comes from the signing cert at signing time.
- `ci.yml` — unchanged (smoke test stays free of all gates).

---

## Phase 1 — Versioning configuration

### Task 1: Switch `version.json` to the new release scheme

**Files:**
- Modify: `version.json`

- [ ] **Step 1: Edit `version.json`**

Replace the existing content with:

```jsonc
{
    "$schema": "https://raw.githubusercontent.com/dotnet/Nerdbank.GitVersioning/main/src/NerdBank.GitVersioning/version.schema.json",
    "version": "0.1",
    "nuGetPackageVersion": {
        "semVer": 2.0
    },
    "publicReleaseRefSpec": [
        "^refs/heads/release/v\\d+\\.\\d+$"
    ],
    "cloudBuild": {
        "buildNumber": {
            "enabled": true
        }
    },
    "release": {
        "branchName": "release/v{version}",
        "firstUnstableTag": "dev"
    }
}
```

Two changes from current state: `publicReleaseRefSpec` matches `release/v{minor}` only (not main); `firstUnstableTag` is `dev` (not `beta`). `version` value stays at `0.1` per spec §2.3 (template isn't a shipped app).

- [ ] **Step 2: Verify NBGV reads the new config**

Run: `dotnet tool install -g nbgv` (if not already installed), then `nbgv get-version`

Expected: version string ends with `-dev.{N}` (because current branch `feature/issue-7-versioning` does not match `release/v*`). Confirms `publicReleaseRefSpec` change took effect.

- [ ] **Step 3: Run smoke test**

Run: `dotnet build src/GcToolkit/GcToolkit.csproj -f net10.0-desktop`

Expected: build succeeds. NBGV's stamped version (visible in build output around `GenerateNBGVThisAssemblyInfo`) should reflect the `-dev` prerelease tag.

- [ ] **Step 4: Stage**

```pwsh
git add version.json
```

---

## Phase 2 — AppChannel mechanism

### Task 2: Add `AppChannel` property + compile constant to `Directory.Build.props`

**Files:**
- Modify: `src/Directory.Build.props`

- [ ] **Step 1: Add the `AppChannel` PropertyGroup**

Append a new `<PropertyGroup>` inside `<Project>`, after the existing one:

```xml
    <!-- AppChannel: Dev (default, local + main) or Prod (release/v* CI builds).
         Drives per-platform identity, display name, icon, and the in-app DEV banner.
         See docs/versioning.md for the full model. -->
    <PropertyGroup>
        <AppChannel Condition="'$(AppChannel)' == ''">Dev</AppChannel>
        <DefineConstants Condition="'$(AppChannel)' == 'Dev'">$(DefineConstants);APP_CHANNEL_DEV</DefineConstants>
    </PropertyGroup>
```

- [ ] **Step 2: Verify property is observable**

Run: `dotnet build src/GcToolkit/GcToolkit.csproj -f net10.0-desktop -p:AppChannel=Dev -v:minimal 2>&1 | Select-String -Pattern "AppChannel"`

Expected: build succeeds (the property itself is silent — it's consumed by later tasks).

- [ ] **Step 3: Stage**

```pwsh
git add src/Directory.Build.props
```

### Task 3: Add per-channel identity to `GcToolkit.csproj`

**Files:**
- Modify: `src/GcToolkit/GcToolkit.csproj`

- [ ] **Step 1: Replace the static `ApplicationTitle`/`ApplicationId` lines with conditional groups**

Find this block in `src/GcToolkit/GcToolkit.csproj` (lines 14–18):

```xml
        <!-- Application metadata -->
        <ApplicationTitle>GC Toolkit</ApplicationTitle>
        <ApplicationId>dev.mzikmund.gctoolkit</ApplicationId>
        <ApplicationPublisher>Martin Zikmund</ApplicationPublisher>
        <Description>GcToolkit by Martin Zikmund</Description>
```

Replace with:

```xml
        <!-- Application metadata -->
        <ApplicationPublisher>Martin Zikmund</ApplicationPublisher>
        <Description>GcToolkit by Martin Zikmund</Description>
    </PropertyGroup>

    <!-- Channel-specific identity. See docs/versioning.md §4. -->
    <PropertyGroup Condition="'$(AppChannel)' == 'Prod'">
        <ApplicationTitle>GC Toolkit</ApplicationTitle>
        <ApplicationId>dev.mzikmund.gctoolkit</ApplicationId>
    </PropertyGroup>

    <PropertyGroup Condition="'$(AppChannel)' == 'Dev'">
        <ApplicationTitle>GC Toolkit Dev</ApplicationTitle>
        <ApplicationId>dev.mzikmund.gctoolkit.dev</ApplicationId>
    </PropertyGroup>

    <PropertyGroup>
```

The trick: close the existing `PropertyGroup` early, insert the two conditional groups, then re-open a fresh `PropertyGroup` to continue with the icon properties that follow (the existing `UnoIconBackgroundFile`, etc.).

- [ ] **Step 2: Confirm Dev build picks the Dev identity**

Run: `dotnet build src/GcToolkit/GcToolkit.csproj -f net10.0-desktop`

Then inspect: `Select-String -Path src/GcToolkit/obj/**/*.AssemblyInfo.cs -Pattern "AssemblyTitle" | Select-Object -First 5`

Expected: `AssemblyTitle("GC Toolkit Dev")` somewhere in the output (Uno's `ApplicationTitle` feeds the assembly metadata).

- [ ] **Step 3: Confirm Prod build picks the Prod identity**

Run: `dotnet build src/GcToolkit/GcToolkit.csproj -f net10.0-desktop -p:AppChannel=Prod`

Then inspect: `Select-String -Path src/GcToolkit/obj/**/*.AssemblyInfo.cs -Pattern "AssemblyTitle" | Select-Object -First 5`

Expected: `AssemblyTitle("GC Toolkit")` (no "Dev" suffix).

- [ ] **Step 4: Run `dotnet format` and stage**

```pwsh
dotnet format whitespace src/GcToolkit.slnx
git add src/GcToolkit/GcToolkit.csproj
```

### Task 4: Add Dev icon variants and channel-conditional icon selection

**Files:**
- Create: `src/GcToolkit/Assets/Icons/icon_dev.svg`
- Create: `src/GcToolkit/Assets/Icons/icon_transparent_dev.svg`
- Modify: `src/GcToolkit/GcToolkit.csproj`

- [ ] **Step 1: Copy the existing icons as Dev variants**

```pwsh
Copy-Item src/GcToolkit/Assets/Icons/icon.svg src/GcToolkit/Assets/Icons/icon_dev.svg
Copy-Item src/GcToolkit/Assets/Icons/icon_transparent.svg src/GcToolkit/Assets/Icons/icon_transparent_dev.svg
```

(Cosmetic differentiation — adding a DEV ribbon overlay — is a follow-up. The plan ships identical pixels under different filenames so the build-system wiring is in place; the actual ribbon SVG edit is left as a quick manual touch-up after this task lands.)

- [ ] **Step 2: Make `UnoIconBackgroundFile` channel-conditional**

Find the icon block in `GcToolkit.csproj` (currently around lines 21–25):

```xml
        <UnoIconBackgroundFile>Assets/Icons/icon_transparent.svg</UnoIconBackgroundFile>
        <UnoIconBackgroundFile Condition="'$(IsIOS)' == 'true' or '$(IsAndroid)' == 'true'">Assets/Icons/icon.svg</UnoIconBackgroundFile>
        <UnoIconForegroundScale>1</UnoIconForegroundScale>
        <UnoIconForegroundScale Condition="'$(IsAndroid)' == 'true'">0.6</UnoIconForegroundScale>
        <UnoSplashScreenScale>0.85</UnoSplashScreenScale>
```

Replace with channel-aware variants:

```xml
        <!-- Default (Prod) icons -->
        <UnoIconBackgroundFile>Assets/Icons/icon_transparent.svg</UnoIconBackgroundFile>
        <UnoIconBackgroundFile Condition="'$(IsIOS)' == 'true' or '$(IsAndroid)' == 'true'">Assets/Icons/icon.svg</UnoIconBackgroundFile>

        <!-- Dev channel overrides -->
        <UnoIconBackgroundFile Condition="'$(AppChannel)' == 'Dev'">Assets/Icons/icon_transparent_dev.svg</UnoIconBackgroundFile>
        <UnoIconBackgroundFile Condition="'$(AppChannel)' == 'Dev' and ('$(IsIOS)' == 'true' or '$(IsAndroid)' == 'true')">Assets/Icons/icon_dev.svg</UnoIconBackgroundFile>

        <UnoIconForegroundScale>1</UnoIconForegroundScale>
        <UnoIconForegroundScale Condition="'$(IsAndroid)' == 'true'">0.6</UnoIconForegroundScale>
        <UnoSplashScreenScale>0.85</UnoSplashScreenScale>
```

The order matters: defaults first, then the Dev override later (last-write-wins on the same MSBuild property).

- [ ] **Step 3: Verify the Dev icon path resolves at build time**

Run: `dotnet build src/GcToolkit/GcToolkit.csproj -f net10.0-desktop -p:AppChannel=Dev -v:detailed 2>&1 | Select-String -Pattern "UnoIconBackgroundFile|icon.*\.svg" | Select-Object -First 10`

Expected: at least one line mentions `icon_transparent_dev.svg`. If only `icon_transparent.svg` appears, the Dev override condition didn't take — re-check the property ordering.

- [ ] **Step 4: Verify the Prod build still uses the base icons**

Run: `dotnet build src/GcToolkit/GcToolkit.csproj -f net10.0-desktop -p:AppChannel=Prod -v:detailed 2>&1 | Select-String -Pattern "icon.*\.svg" | Select-Object -First 5`

Expected: lines reference `icon_transparent.svg` (no `.dev.svg`).

- [ ] **Step 5: Stage**

```pwsh
git add src/GcToolkit/Assets/Icons/icon_dev.svg src/GcToolkit/Assets/Icons/icon_transparent_dev.svg src/GcToolkit/GcToolkit.csproj
```

---

## Phase 3 — In-app channel visibility

### Task 5: Add `AppEnvironment` static class

**Files:**
- Create: `src/GcToolkit/Infrastructure/AppEnvironment.cs`

- [ ] **Step 1: Write the class**

```csharp
namespace GcToolkit.Infrastructure;

public static class AppEnvironment
{
#if APP_CHANNEL_DEV
    public const bool IsDevChannel = true;
    public const string ChannelLabel = "DEV";
#else
    public const bool IsDevChannel = false;
    public const string ChannelLabel = "";
#endif
}
```

- [ ] **Step 2: Verify it compiles under both channels**

Run: `dotnet build src/GcToolkit/GcToolkit.csproj -f net10.0-desktop -p:AppChannel=Dev`
Then: `dotnet build src/GcToolkit/GcToolkit.csproj -f net10.0-desktop -p:AppChannel=Prod`

Expected: both succeed.

- [ ] **Step 3: Stage**

```pwsh
git add src/GcToolkit/Infrastructure/AppEnvironment.cs
```

### Task 6: Add the DEV badge overlay control

**Files:**
- Create: `src/GcToolkit/Controls/DevChannelBadge.xaml`
- Create: `src/GcToolkit/Controls/DevChannelBadge.xaml.cs`
- Modify: `src/GcToolkit/WindowShell.xaml`

- [ ] **Step 1: Write `DevChannelBadge.xaml`**

```xml
<UserControl
    x:Class="GcToolkit.Controls.DevChannelBadge"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:infra="using:GcToolkit.Infrastructure"
    HorizontalAlignment="Right"
    VerticalAlignment="Top"
    Margin="0,8,8,0"
    IsHitTestVisible="False"
    Visibility="{x:Bind infra:AppEnvironment.IsDevChannel, Mode=OneTime, Converter={StaticResource BoolToVisibility}}">
    <Border Background="{ThemeResource SystemFillColorCautionBrush}"
            CornerRadius="4"
            Padding="6,2">
        <TextBlock Text="{x:Bind infra:AppEnvironment.ChannelLabel, Mode=OneTime}"
                   Foreground="{ThemeResource TextOnAccentFillColorPrimaryBrush}"
                   FontWeight="SemiBold"
                   FontSize="10" />
    </Border>
</UserControl>
```

- [ ] **Step 2: Write `DevChannelBadge.xaml.cs`**

```csharp
using Microsoft.UI.Xaml.Controls;

namespace GcToolkit.Controls;

public sealed partial class DevChannelBadge : UserControl
{
    public DevChannelBadge() => InitializeComponent();
}
```

- [ ] **Step 3: Verify the `BoolToVisibility` converter exists in app resources**

Run: `Select-String -Path src/GcToolkit/Resources/Converters.xaml -Pattern "BoolToVisibility"`

Expected: one or more matches (a converter key named `BoolToVisibility`). If absent, fall back to a `x:Bind` boolean→Visibility code-behind getter — but the existing repo follows the Converters.xaml pattern, so the converter should be there. If it isn't, add this minimal entry to `Resources/Converters.xaml`:

```xml
<converters:BoolToVisibilityConverter x:Key="BoolToVisibility" />
```

…using whichever namespace alias the file already imports for converters.

- [ ] **Step 4: Host the badge in `WindowShell.xaml`**

Open `src/GcToolkit/WindowShell.xaml`. Wrap whatever the root content is in a `Grid` (if it isn't already one) and add the badge as the last child so it renders on top:

```xml
<Grid>
    <!-- existing content -->
    <controls:DevChannelBadge xmlns:controls="using:GcToolkit.Controls" />
</Grid>
```

If `WindowShell.xaml`'s root is already a `Grid`, just add the `DevChannelBadge` line as the last child of that grid. Keep the existing `xmlns` declarations at the page root rather than duplicating; the inline `xmlns:controls` above is only needed if the namespace isn't already declared at the root.

- [ ] **Step 5: Run the desktop head and confirm the badge renders**

Run: `dotnet run --project src/GcToolkit/GcToolkit.csproj -f net10.0-desktop`

Expected: a small orange "DEV" pill in the top-right corner of the window.

- [ ] **Step 6: Confirm it disappears in Prod**

Run: `dotnet run --project src/GcToolkit/GcToolkit.csproj -f net10.0-desktop -p:AppChannel=Prod`

Expected: no badge.

- [ ] **Step 7: Stage**

```pwsh
dotnet format whitespace src/GcToolkit.slnx
git add src/GcToolkit/Controls/DevChannelBadge.xaml src/GcToolkit/Controls/DevChannelBadge.xaml.cs src/GcToolkit/WindowShell.xaml
# (and src/GcToolkit/Resources/Converters.xaml if it had to be edited)
```

### Task 7: Add `appsettings.Dev.json` overlay placeholder

**Files:**
- Create: `src/GcToolkit/appsettings.Dev.json`

- [ ] **Step 1: Write a minimal overlay file**

```json
{
    "//": "Dev-channel overrides. Place sandbox RevenueCat keys, dev analytics endpoints, etc. here.",
    "//": "Loaded after appsettings.json and (if present) appsettings.development.json. See docs/versioning.md."
}
```

(`//` is not valid JSON for most readers — strip it if the loader complains; the comment is purely for humans inspecting the file. Adjust to an empty `{}` if the configuration loader rejects duplicate keys.)

- [ ] **Step 2: Verify the file loads in the existing configuration pipeline**

Run: `dotnet build src/GcToolkit/GcToolkit.csproj -f net10.0-desktop -p:AppChannel=Dev`

Expected: build succeeds. (Wiring `appsettings.Dev.json` into the host configuration pipeline is intentionally deferred — the file is shipped as a placeholder and documented; apps that need real Dev secrets will register the overlay when they migrate. The template doesn't have RevenueCat keys to swap, so no wiring is needed yet.)

- [ ] **Step 3: Stage**

```pwsh
git add src/GcToolkit/appsettings.Dev.json
```

---

## Phase 4 — Workflows

### Task 8: Update `package-windows.yml` triggers, guard, and channel selection

**Files:**
- Modify: `.github/workflows/package-windows.yml`

- [ ] **Step 1: Replace the trigger block + add the public/private guard**

Find the existing top-of-file block (lines 1–11):

```yaml
name: Build Windows Packages

on:
  # main is a public-release branch in version.json, so Nerdbank.GitVersioning
  # stamps pushes to main with a stable version (no -beta suffix). Each push to
  # main therefore builds a store-ready, signed package and uploads it.
  push:
    branches: [ "main" ]
  # Manual runs for ad-hoc package builds from any branch.
  workflow_dispatch:
```

Replace with:

```yaml
name: Build Windows Packages

on:
  # Push to main: Dev-channel package (self-signed, sideload-safe artifact).
  # Push to release/v*: Prod-channel package (Store-signed .msixupload artifact for manual Partner Center submission).
  # See docs/versioning.md for the full model.
  push:
    branches:
      - "main"
      - "release/v**"
  workflow_dispatch:
```

- [ ] **Step 2: Add the channel-selection step + repo-private guard to the `create_package` job**

After the `concurrency:` block, modify the `create_package` job definition. Find:

```yaml
  create_package:

    strategy:
      matrix:
        configuration: [Release]
        platform: [x86,x64,ARM64]

    runs-on: windows-latest
```

Add the `if:` and a channel-output step. The result:

```yaml
  create_package:
    if: github.event.repository.private == false || github.event_name == 'workflow_dispatch'

    strategy:
      matrix:
        configuration: [Release]
        platform: [x86,x64,ARM64]

    runs-on: windows-latest

    outputs:
      channel: ${{ steps.channel.outputs.value }}
```

Then add as the FIRST step (before Checkout):

```yaml
    steps:
    - name: Compute app channel
      id: channel
      shell: pwsh
      run: |
        if ($env:GITHUB_REF -like 'refs/heads/release/v*') {
          "value=Prod" >> $env:GITHUB_OUTPUT
        } else {
          "value=Dev" >> $env:GITHUB_OUTPUT
        }
```

- [ ] **Step 3: Switch the signing cert per channel**

Find the existing "Decode the pfx" step:

```yaml
    - name: Decode the pfx
      run: |
        $pfx_cert_byte = [System.Convert]::FromBase64String("${{ secrets.BASE64_ENCODED_WINDOWS_PFX }}")
        $certificatePath = "GitHubActionsWorkflow.pfx"
        [IO.File]::WriteAllBytes("$certificatePath", $pfx_cert_byte)
```

Replace with channel-aware version:

```yaml
    - name: Decode the pfx
      shell: pwsh
      run: |
        if ('${{ steps.channel.outputs.value }}' -eq 'Prod') {
          $pfxBase64 = '${{ secrets.BASE64_ENCODED_WINDOWS_PFX }}'
        } else {
          $pfxBase64 = '${{ secrets.BASE64_ENCODED_WINDOWS_PFX_DEV }}'
        }
        $pfx_cert_byte = [System.Convert]::FromBase64String($pfxBase64)
        $certificatePath = "GitHubActionsWorkflow.pfx"
        [IO.File]::WriteAllBytes("$certificatePath", $pfx_cert_byte)
```

- [ ] **Step 4: Pass `/p:AppChannel` to the package build**

Find the "Create the app package" step:

```yaml
    - name: Create the app package
      run: msbuild $env:SOLUTION_FILE /p:TargetFramework=$env:WINDOWS_TARGET_FRAMEWORK /p:Configuration=$env:Configuration /p:Platform=$env:Platform /p:UapAppxPackageBuildMode=$env:Appx_Package_Build_Mode /p:AppxBundle=$env:Appx_Bundle /p:AppxPackageDir="$env:Appx_Package_Dir" /p:GenerateAppxPackageOnBuild=true
```

Append `/p:AppChannel=${{ steps.channel.outputs.value }}` to the msbuild command line:

```yaml
    - name: Create the app package
      run: msbuild $env:SOLUTION_FILE /p:TargetFramework=$env:WINDOWS_TARGET_FRAMEWORK /p:Configuration=$env:Configuration /p:Platform=$env:Platform /p:UapAppxPackageBuildMode=$env:Appx_Package_Build_Mode /p:AppxBundle=$env:Appx_Bundle /p:AppxPackageDir="$env:Appx_Package_Dir" /p:GenerateAppxPackageOnBuild=true /p:AppChannel=${{ steps.channel.outputs.value }}
```

Also append the same flag to the "Restore the application" step (so RID-graph generation sees the right ApplicationId):

```yaml
    - name: Restore the application
      run: msbuild $env:SOLUTION_FILE /t:Restore /p:Configuration=$env:Configuration /p:TargetFramework=$env:WINDOWS_TARGET_FRAMEWORK /p:PublishReadyToRun=true /p:AppChannel=${{ steps.channel.outputs.value }}
```

- [ ] **Step 5: Apply the same `if:` guard to the `merge` job**

The `merge` job depends on `create_package`. If `create_package` is skipped (private repo, push trigger), `merge` should skip too. Add:

```yaml
  merge:
    if: needs.create_package.result == 'success'
    runs-on: windows-latest
    needs: create_package
```

This is more precise than re-checking the repo-private flag — if `create_package` was skipped, its `result` will be `skipped`, not `success`.

- [ ] **Step 6: Verify the YAML parses**

Run: `gh workflow view "Build Windows Packages" --repo MartinZikmund/uno-app-template 2>&1 | Select-Object -First 5`

(Alternatively, just inspect the file by eye for indentation correctness — GitHub will reject malformed YAML on push regardless.)

- [ ] **Step 7: Stage**

```pwsh
git add .github/workflows/package-windows.yml
```

### Task 9: Update `package-android.yml` — triggers, guard, channel, gated Play upload

**Files:**
- Modify: `.github/workflows/package-android.yml`

- [ ] **Step 1: Replace the trigger block**

Find lines 1–11:

```yaml
name: Build Android Packages

on:
  # main is a public-release branch in version.json, so Nerdbank.GitVersioning
  # stamps pushes to main with a stable version (no -beta suffix). Each push to
  # main therefore builds a store-ready, signed package and uploads it.
  push:
    branches: [ "main" ]
  # Manual runs for ad-hoc package builds from any branch.
  workflow_dispatch:
```

Replace with:

```yaml
name: Build Android Packages

on:
  # Push to main: Dev-channel AAB (Dev applicationId, sideload-safe; no Play upload).
  # Push to release/v*: Prod-channel AAB uploaded to Google Play Production track.
  # See docs/versioning.md for the full model.
  push:
    branches:
      - "main"
      - "release/v**"
  workflow_dispatch:
```

- [ ] **Step 2: Add the private-repo guard + channel step to `build_android`**

Modify the `build_android` job header:

```yaml
jobs:
  build_android:
    if: github.event.repository.private == false || github.event_name == 'workflow_dispatch'
    runs-on: ubuntu-latest
```

After the existing `steps:` line, insert the channel-compute step as the first step (before Checkout):

```yaml
    steps:
      - name: Compute app channel
        id: channel
        shell: bash
        run: |
          if [[ "$GITHUB_REF" == refs/heads/release/v* ]]; then
            echo "value=Prod" >> "$GITHUB_OUTPUT"
            echo "package_name=dev.mzikmund.gctoolkit" >> "$GITHUB_OUTPUT"
            echo "play_track=production" >> "$GITHUB_OUTPUT"
          else
            echo "value=Dev" >> "$GITHUB_OUTPUT"
            echo "package_name=dev.mzikmund.gctoolkit.dev" >> "$GITHUB_OUTPUT"
            echo "play_track=" >> "$GITHUB_OUTPUT"
          fi
```

- [ ] **Step 3: Pass `/p:AppChannel` to the publish step**

Find the "Publish AAB (signed with Upload Key)" step. Append `-p:AppChannel=${{ steps.channel.outputs.value }}` to the dotnet publish command:

```yaml
      - name: Publish AAB (signed with Upload Key)
        run: |
          dotnet publish "${{ env.PROJECT_FILE }}" -c Release -f ${{ env.ANDROID_TARGET_FRAMEWORK }} \
            -p:AppChannel=${{ steps.channel.outputs.value }} \
            -p:AndroidKeyStore=true \
            -p:AndroidSigningKeyStore="$GITHUB_WORKSPACE/android-upload.keystore" \
            -p:AndroidSigningKeyAlias='${{ secrets.ANDROID_KEY_ALIAS }}' \
            -p:AndroidSigningKeyPass='${{ secrets.ANDROID_KEY_PASSWORD }}' \
            -p:AndroidSigningStorePass='${{ secrets.ANDROID_STORE_PASSWORD }}' \
            -p:AndroidPackageFormat=aab -p:PackageFormat=aab
```

- [ ] **Step 4: Gate the Play upload to release/v* only and use Production track**

Find the "Upload to Google Play Console" step. Add an `if:`, parameterize `packageName` and `track`, and drop the `GOOGLE_PLAY_TRACK_STATUS` env var entry (no longer needed since release builds always go to `production` with `completed` status):

```yaml
      - name: Upload to Google Play Console
        if: steps.channel.outputs.value == 'Prod'
        uses: r0adkll/upload-google-play@v1
        with:
          serviceAccountJsonPlainText: ${{ secrets.GOOGLE_PLAY_SERVICE_ACCOUNT_JSON }}
          packageName: ${{ steps.channel.outputs.package_name }}
          releaseFiles: ${{ env.PROJECT_PATH }}/bin/Release/${{ env.ANDROID_TARGET_FRAMEWORK }}/publish/*-Signed.aab
          track: ${{ steps.channel.outputs.play_track }}
          status: completed
```

Remove the `GOOGLE_PLAY_TRACK_STATUS:` env var entry from the workflow's top-level `env:` block — it's no longer referenced.

- [ ] **Step 5: Stage**

```pwsh
git add .github/workflows/package-android.yml
```

### Task 10: Update `package-ios.yml` — triggers, guard, channel, gated TestFlight upload

**Files:**
- Modify: `.github/workflows/package-ios.yml`

- [ ] **Step 1: Replace the trigger block**

Replace lines 1–11 with:

```yaml
name: Build iOS Packages

on:
  # Push to main: Dev-channel build (verify-only; no signed .ipa to avoid Apple dev-provisioning overhead in the template).
  # Push to release/v*: Prod-channel signed .ipa uploaded to TestFlight (manual "Submit for Review" gate).
  # See docs/versioning.md for the full model.
  push:
    branches:
      - "main"
      - "release/v**"
  workflow_dispatch:
```

- [ ] **Step 2: Add private-repo guard + channel step**

Modify the `build_ios` job header to add `if:`. Add a channel-compute step before Checkout:

```yaml
jobs:
  build_ios:
    if: github.event.repository.private == false || github.event_name == 'workflow_dispatch'
    runs-on: macos-26

    env:
      ...

    steps:
      - name: Compute app channel
        id: channel
        shell: bash
        run: |
          if [[ "$GITHUB_REF" == refs/heads/release/v* ]]; then
            echo "value=Prod" >> "$GITHUB_OUTPUT"
          else
            echo "value=Dev" >> "$GITHUB_OUTPUT"
          fi
```

- [ ] **Step 3: Gate signing setup + .ipa publish + TestFlight upload to Prod**

The existing "Import signing certificate", "Install provisioning profile", "Publish iOS (signed .ipa)", "Upload .ipa", and "Upload to TestFlight" steps are all Prod-only. Add `if: steps.channel.outputs.value == 'Prod'` to each:

```yaml
      - name: Import signing certificate
        if: steps.channel.outputs.value == 'Prod'
        uses: apple-actions/import-codesign-certs@v1
        with:
          p12-file-base64: ${{ secrets.APPLE_DISTRIBUTION_P12_BASE64 }}
          p12-password: ${{ secrets.APPLE_P12_PASSWORD }}

      - name: Install provisioning profile
        if: steps.channel.outputs.value == 'Prod'
        shell: bash
        run: |
          mkdir -p "$HOME/Library/MobileDevice/Provisioning Profiles"
          echo "${{ secrets.APPLE_PROVISIONING_PROFILE_BASE64 }}" | base64 --decode > "$HOME/Library/MobileDevice/Provisioning Profiles/AppStore.mobileprovision"
          ls -l "$HOME/Library/MobileDevice/Provisioning Profiles"

      - name: Restore solution
        run: dotnet restore "${{ env.SOLUTION_FILE }}"

      - name: Publish iOS (signed .ipa, Prod)
        if: steps.channel.outputs.value == 'Prod'
        run: |
          dotnet publish "${{ env.PROJECT_FILE }}" -c Release -f ${{ env.IOS_TARGET_FRAMEWORK }} -r ios-arm64 \
            -p:AppChannel=Prod \
            -p:ArchiveOnBuild=true \
            -p:BuildIpa=true \
            -p:CodesignKey="${{ secrets.APPLE_CODESIGN_KEY }}" \
            -p:CodesignTeamId="${{ secrets.APPLE_TEAM_ID }}" \
            -p:CodesignProvision="${{ secrets.APPLE_PROVISIONING_PROFILE_UUID }}"

      - name: Build iOS (verify-only, Dev)
        if: steps.channel.outputs.value == 'Dev'
        run: |
          dotnet build "${{ env.PROJECT_FILE }}" -c Release -f ${{ env.IOS_TARGET_FRAMEWORK }} \
            -p:AppChannel=Dev

      - name: Upload .ipa
        if: steps.channel.outputs.value == 'Prod'
        uses: actions/upload-artifact@v4
        with:
          name: iOS_IPA
          path: |
            ${{ env.PROJECT_PATH }}/bin/Release/${{ env.IOS_TARGET_FRAMEWORK }}/ios-arm64/publish/*.ipa

      - name: Upload to TestFlight
        if: steps.channel.outputs.value == 'Prod'
        uses: apple-actions/upload-testflight-build@v3
        with:
          app-path: "${{ env.PROJECT_PATH }}/bin/Release/${{ env.IOS_TARGET_FRAMEWORK }}/ios-arm64/publish/${{ env.APP_NAME }}.ipa"
          issuer-id: ${{ secrets.APPSTORE_ISSUER_ID }}
          api-key-id: ${{ secrets.APPSTORE_API_KEY_ID }}
          api-private-key: ${{ secrets.APPSTORE_API_PRIVATE_KEY }}
```

Note: this design intentionally drops the iOS Dev `.ipa` artifact (spec §5.1 deferred this — Apple dev-provisioning is heavyweight to wire up for a template). Local F5 from a Mac is the supported iOS-Dev path. Apps that migrate can add a Dev-cert/Dev-profile path themselves.

- [ ] **Step 4: Stage**

```pwsh
git add .github/workflows/package-ios.yml
```

### Task 11: Create `tag-release.yml`

**Files:**
- Create: `.github/workflows/tag-release.yml`

- [ ] **Step 1: Write the workflow**

```yaml
name: Tag Release

# Pushes an annotated `v{NBGV-version}` tag at the commit that just built a successful Prod release.
# Runs only on release/v* pushes, only after all three packaging workflows succeed.

on:
  workflow_run:
    workflows:
      - "Build Windows Packages"
      - "Build Android Packages"
      - "Build iOS Packages"
    types: [completed]
    branches:
      - "release/v**"

jobs:
  tag:
    if: >
      github.event.workflow_run.conclusion == 'success' &&
      github.event.repository.private == false
    runs-on: ubuntu-latest
    permissions:
      contents: write
    steps:
      - name: Checkout the built commit
        uses: actions/checkout@v4
        with:
          fetch-depth: 0
          ref: ${{ github.event.workflow_run.head_sha }}

      - name: Setup .NET SDK
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x

      - name: Install nbgv
        run: dotnet tool install -g nbgv

      - name: Check all three packaging workflows succeeded on this commit
        id: gate
        env:
          HEAD_SHA: ${{ github.event.workflow_run.head_sha }}
          GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
        run: |
          for wf in "Build Windows Packages" "Build Android Packages" "Build iOS Packages"; do
            ok=$(gh run list --workflow "$wf" --commit "$HEAD_SHA" --json conclusion --jq '.[0].conclusion // "none"')
            if [[ "$ok" != "success" ]]; then
              echo "Workflow '$wf' on $HEAD_SHA is '$ok' — not all three are green yet; skipping tag."
              echo "skip=true" >> "$GITHUB_OUTPUT"
              exit 0
            fi
          done
          echo "skip=false" >> "$GITHUB_OUTPUT"

      - name: Tag
        if: steps.gate.outputs.skip != 'true'
        run: |
          git config user.name "github-actions[bot]"
          git config user.email "41898282+github-actions[bot]@users.noreply.github.com"
          nbgv tag
          git push origin --tags
```

The `workflow_run` trigger fires once per packaging workflow completion; the `gate` step checks all three have succeeded on this commit before tagging. This avoids needing inter-workflow `needs:` (which `workflow_run` doesn't support) while still producing exactly one tag per successful release commit.

- [ ] **Step 2: Stage**

```pwsh
git add .github/workflows/tag-release.yml
```

### Task 12: Create `prepare-release.yml`

**Files:**
- Create: `.github/workflows/prepare-release.yml`

- [ ] **Step 1: Write the workflow**

```yaml
name: Prepare Release

# Cuts a new release branch using `nbgv prepare-release` and opens a PR for main's version.json bump.
# Manual trigger only — there's no auto-cadence here.

on:
  workflow_dispatch:

jobs:
  prepare:
    runs-on: ubuntu-latest
    permissions:
      contents: write
      pull-requests: write
    steps:
      - name: Checkout main
        uses: actions/checkout@v4
        with:
          fetch-depth: 0
          ref: main

      - name: Setup .NET SDK
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x

      - name: Install nbgv
        run: dotnet tool install -g nbgv

      - name: Configure git identity
        run: |
          git config user.name "github-actions[bot]"
          git config user.email "41898282+github-actions[bot]@users.noreply.github.com"

      - name: Capture current version
        id: version
        run: |
          current=$(nbgv get-version -v Version | cut -d. -f1,2)
          echo "current=$current" >> "$GITHUB_OUTPUT"
          echo "release_branch=release/v$current" >> "$GITHUB_OUTPUT"

      - name: Run nbgv prepare-release
        run: nbgv prepare-release

      - name: Push the new release branch
        run: git push origin "${{ steps.version.outputs.release_branch }}"

      - name: Push the main bump as a PR-source branch
        run: |
          bump_branch="chore/bump-version-after-${{ steps.version.outputs.current }}"
          git checkout -b "$bump_branch" main
          git push origin "$bump_branch"
          echo "BUMP_BRANCH=$bump_branch" >> "$GITHUB_ENV"

      - name: Open PR for main's version.json bump
        env:
          GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
        run: |
          gh pr create \
            --base main \
            --head "$BUMP_BRANCH" \
            --title "chore: bump version after cutting ${{ steps.version.outputs.release_branch }}" \
            --body "Cut by \`Prepare Release\` workflow. \`${{ steps.version.outputs.release_branch }}\` is now the source of stable builds for the previous minor; main moves to the next planned version."
```

`nbgv prepare-release` leaves both the new release branch and a bump commit on main in the local clone. We push the release branch directly (it's new — branch protection doesn't apply) and push the main bump under a new branch so we can open a PR (main is protected and refuses direct pushes — confirmed in build-test-merge-gotchas memory).

- [ ] **Step 2: Stage**

```pwsh
git add .github/workflows/prepare-release.yml
```

### Task 13: Update `validate-manifest-version.yml` comment

**Files:**
- Modify: `.github/workflows/validate-manifest-version.yml`

- [ ] **Step 1: Update the error message comment to reference the new docs**

Find the error message line and the workflow header. Add a leading comment under `name:`:

```yaml
name: Validate Manifest Version

# Guards that Package.appxmanifest's Identity Version stays "0.0.0.0" so
# Nerdbank.GitVersioning can stamp the real version at build time. The version
# itself is governed by version.json + branch (see docs/versioning.md).

on:
```

No behavioral change; the existing `grep -q 'Version="0.0.0.0"'` already does the right thing under the new model.

- [ ] **Step 2: Stage**

```pwsh
git add .github/workflows/validate-manifest-version.yml
```

---

## Phase 5 — Documentation

### Task 14: Write `docs/versioning.md`

**Files:**
- Create: `docs/versioning.md`

- [ ] **Step 1: Write the full reference doc**

```markdown
# Versioning

> `main` is where the next release is brewing; `release/v{minor}` branches are where releases ship.
> Every build carries an `AppChannel` (Dev for local + main, Prod for release/v*) baked into its package identity so Dev and Prod sit side-by-side on every device.

## Branch model

| Branch | `version.json` | NBGV-computed | Manifest version | Channel |
|---|---|---|---|---|
| `main` | next planned minor (e.g. `0.2`) | `0.2.0-dev.{height}` | `0.2.0.{height}` | Dev |
| `release/v0.1` | `0.1` | `0.1.0`, `0.1.1`, … | `0.1.0.0`, `0.1.1.0`, … | Prod |
| `feature/*` | inherited from main | `0.2.0-dev.{height}+{sha}` | (not packaged) | Dev |

`publicReleaseRefSpec` matches `release/v{minor}` only, so main can never produce a stable version by accident. `firstUnstableTag` is `dev`.

## AppChannel

Single MSBuild property: `AppChannel = Dev | Prod`. Defaults to `Dev`. Every CI packaging workflow sets the value from the branch (release/v* → Prod, else Dev) and passes `/p:AppChannel=...` to the build.

What changes per channel:
- **Identity:** Android `applicationId`, iOS Bundle ID — `…gctoolkit` vs `…gctoolkit.dev`. Windows Identity Name is set by the signing certificate's Publisher CN (Prod cert vs Dev self-signed cert).
- **Display name:** `GC Toolkit` vs `GC Toolkit Dev`.
- **App icon:** `Assets/Icons/icon.svg` vs `Assets/Icons/icon_dev.svg`.
- **In-app banner:** a `DEV` corner badge appears when `AppEnvironment.IsDevChannel` is true.
- **Compile constant:** `APP_CHANNEL_DEV` defined on Dev builds.
- **Appsettings:** `appsettings.Dev.json` (overlay placeholder; wire up to your configuration host if you need separate sandbox keys).

## Workflows

| Workflow | Trigger | Channel | Publishes? |
|---|---|---|---|
| `ci.yml` | push: main, PR: main + release/** | n/a (Debug smoke test) | n/a |
| `validate-manifest-version.yml` | push: main, PR: main + release/** | n/a | n/a |
| `package-windows.yml` | push: main + release/v* + workflow_dispatch | Dev on main (self-signed), Prod on release/v* (Store-signed) | No — manual Partner Center upload from artifact |
| `package-android.yml` | push: main + release/v* + workflow_dispatch | Dev applicationId on main, Prod on release/v* | release/v* → Play Production track via `r0adkll/upload-google-play` |
| `package-ios.yml` | push: main + release/v* + workflow_dispatch | Dev Bundle ID on main, Prod on release/v* | release/v* → TestFlight via `apple-actions/upload-testflight-build` (manual "Submit for Review") |
| `tag-release.yml` | workflow_run after the three packaging workflows on release/v* | n/a | Pushes annotated tag `v{version}` |
| `prepare-release.yml` | workflow_dispatch | n/a | Cuts release/v{current}, opens PR for main bump |

All packaging workflows are gated by `github.event.repository.private == false || github.event_name == 'workflow_dispatch'` so private forks don't burn paid Actions minutes on every push.

## Release lifecycle

### Cutting a new release

**Local (canonical):**
```pwsh
nbgv prepare-release
git push origin main release/v{current}
```

**Workflow:** Actions → Prepare Release → Run. Pushes release branch, opens a PR for the main bump.

### Building releases

Push to `release/v0.2` → packaging workflows fire (subject to the public/private gate) → Prod identity, Store-signed → Android auto-publishes to Play Production, iOS auto-uploads to TestFlight (manual review submission), Windows produces a `.msixupload` for manual Partner Center upload → `tag-release.yml` pushes `v0.2.0` after all three succeed.

### Patching a release

Commit fix to `release/v0.2`. NBGV bumps patch (`0.2.1`). Same workflows fire, new tag.

If the fix also belongs on main: cherry-pick or merge `release/v0.2` → main.

### Mainline development

Push to main. NBGV stamps `0.3.0-dev.{height}` (assuming the most recent cut bumped main to 0.3). Workflows produce Dev-identity artifacts; nothing is published anywhere.

### Major version bump

Set `version.json` on main to `1.0` manually instead of running `prepare-release`. NBGV doesn't enforce a major-bump policy.

## Secrets reference

| Secret | Purpose | Consumed by |
|---|---|---|
| `BASE64_ENCODED_WINDOWS_PFX` | Prod Windows signing cert (Store-trusted) | `package-windows.yml` on release/v* |
| `BASE64_ENCODED_WINDOWS_PFX_DEV` | Dev Windows signing cert (self-signed; install as trusted root on test machines) | `package-windows.yml` on main |
| `ANDROID_KEYSTORE_BASE64`, `ANDROID_KEY_ALIAS`, `ANDROID_KEY_PASSWORD`, `ANDROID_STORE_PASSWORD` | Android signing | All `package-android.yml` runs |
| `GOOGLE_PLAY_SERVICE_ACCOUNT_JSON` | Play Console API auth | `package-android.yml` on release/v* (Play Production upload) |
| `APPLE_DISTRIBUTION_P12_BASE64`, `APPLE_P12_PASSWORD`, `APPLE_PROVISIONING_PROFILE_BASE64`, `APPLE_PROVISIONING_PROFILE_UUID`, `APPLE_CODESIGN_KEY`, `APPLE_TEAM_ID` | iOS distribution signing | `package-ios.yml` on release/v* |
| `APPSTORE_ISSUER_ID`, `APPSTORE_API_KEY_ID`, `APPSTORE_API_PRIVATE_KEY` | App Store Connect API auth | `package-ios.yml` on release/v* (TestFlight upload) |

## Why is build `0.X.0-dev.5` instead of `0.X.0`?

Because the branch isn't a release branch. NBGV's `publicReleaseRefSpec` only matches `^refs/heads/release/v\d+\.\d+$`. The `-dev.{height}` suffix indicates a prerelease build with the commit-height since the last `version.json` bump.

## Why is the Dev app a separate install from the Store version?

Because their package identities differ: `dev.mzikmund.gctoolkit.dev` vs `dev.mzikmund.gctoolkit` on Android and iOS, and `GcToolkit.Dev`/Dev-Publisher-CN vs `GcToolkit`/Prod-Publisher-CN on Windows. Different identities mean separate AppData, separate user state, no upgrade collision.
```

- [ ] **Step 2: Stage**

```pwsh
git add docs/versioning.md
```

### Task 15: Write `docs/versioning-migration.md`

**Files:**
- Create: `docs/versioning-migration.md`

- [ ] **Step 1: Write the migration guide**

```markdown
# Migrating an existing app to the new versioning model

This guide walks an existing Uno app (stopwatch, daily-dozen, …) through adopting the model described in [versioning.md](./versioning.md). Apply per-app, on the app's own schedule.

## Pre-flight

Check what's currently shipped. If `version.json` says `0.12` and the last Store/Play submission was `0.12.x`, that's your current minor.

## Steps

### 1. Cut a release branch from current main HEAD

```pwsh
git checkout main && git pull
git checkout -b release/v0.12          # = your current minor
git push -u origin release/v0.12
```

This freezes the existing line. Any further `0.12.x` patches go here.

### 2. Bump main's `version.json`

On main:

```jsonc
{
    "$schema": "...",
    "version": "0.13",
    "nuGetPackageVersion": { "semVer": 2.0 },
    "publicReleaseRefSpec": [
        "^refs/heads/release/v\\d+\\.\\d+$"
    ],
    "cloudBuild": { "buildNumber": { "enabled": true } },
    "release": {
        "branchName": "release/v{version}",
        "firstUnstableTag": "dev"
    }
}
```

### 3. Add `AppChannel` to `src/Directory.Build.props`

```xml
<PropertyGroup>
    <AppChannel Condition="'$(AppChannel)' == ''">Dev</AppChannel>
    <DefineConstants Condition="'$(AppChannel)' == 'Dev'">$(DefineConstants);APP_CHANNEL_DEV</DefineConstants>
</PropertyGroup>
```

### 4. Split `ApplicationId` / `ApplicationTitle` per channel in the head csproj

Replace the existing single line with two conditional groups (your app's reverse-DNS prefix instead of `dev.mzikmund`):

```xml
<PropertyGroup Condition="'$(AppChannel)' == 'Prod'">
    <ApplicationTitle>Fluent Stopwatch</ApplicationTitle>
    <ApplicationId>dev.mzikmund.stopwatch</ApplicationId>
</PropertyGroup>

<PropertyGroup Condition="'$(AppChannel)' == 'Dev'">
    <ApplicationTitle>Fluent Stopwatch Dev</ApplicationTitle>
    <ApplicationId>dev.mzikmund.stopwatch.dev</ApplicationId>
</PropertyGroup>
```

### 5. Add Dev icon variants

Copy `Assets/Icons/icon.svg` → `icon_dev.svg` and `icon_transparent.svg` → `icon_transparent_dev.svg`. Apply a DEV ribbon overlay if you want a visually distinct icon. Add channel-conditional `UnoIconBackgroundFile` lines (see the template's `GcToolkit.csproj`).

### 6. Add `AppEnvironment` + DEV banner

Copy `src/GcToolkit/Infrastructure/AppEnvironment.cs` and `src/GcToolkit/Controls/DevChannelBadge.xaml`(+.cs) from the template. Host the badge in your shell page.

### 7. Update workflows

Replicate the changes from the template's `package-*.yml`, `tag-release.yml`, and `prepare-release.yml`:
- Add `release/v**` to the `push:` triggers.
- Add `if: github.event.repository.private == false || github.event_name == 'workflow_dispatch'` to the primary job.
- Add the "Compute app channel" step.
- Pass `/p:AppChannel=...` to all build/publish/restore steps.
- Gate the Play upload + TestFlight upload steps to `steps.channel.outputs.value == 'Prod'`.
- For Windows, swap the signing cert per channel (Prod → `BASE64_ENCODED_WINDOWS_PFX`, Dev → `BASE64_ENCODED_WINDOWS_PFX_DEV`).

### 8. Add new secrets to the repo

- `BASE64_ENCODED_WINDOWS_PFX_DEV` — self-signed Dev cert (one-time: generate with `New-SelfSignedCertificate -Type CodeSigning -Subject "CN=YourApp.Dev (Self-Signed)" ...`, export as PFX, base64-encode).
- Confirm existing: `GOOGLE_PLAY_SERVICE_ACCOUNT_JSON`, `APPSTORE_ISSUER_ID`, `APPSTORE_API_KEY_ID`, `APPSTORE_API_PRIVATE_KEY`.

### 9. Store-side prerequisites

- **Play Console:** confirm your service account has "Release manager" role and the app is currently uploaded to Production at least once (the API can't promote to a track that has no manual baseline).
- **App Store Connect:** confirm an API key with "App Manager" role exists.

### 10. Verify

Push to main → confirm Dev artifacts produced, no publish triggered.
Push to `release/v0.12` (or whatever minor you cut in step 1) → confirm Prod artifacts produced, Play Production updated, TestFlight populated, tag `v0.12.{patch}` created.

### 11. Clean up

- Remove old "main is a public-release branch" comments from your workflow files.
- Drop any one-off scripts that assumed main published to the Store.

That's it — your app is on the new model.
```

- [ ] **Step 2: Stage**

```pwsh
git add docs/versioning-migration.md
```

### Task 16: Add a "Versioning" section to `README.md`

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Add a short section**

Find an appropriate place in `README.md` (after the feature list or near the contributing section). Insert:

```markdown
## Versioning

This template uses Nerdbank.GitVersioning. `main` produces `0.X.0-dev.{height}` prerelease builds with a Dev-channel identity that installs side-by-side with the Store version. Stable releases come from `release/v{minor}` branches. See [docs/versioning.md](./docs/versioning.md) for the full model and [docs/versioning-migration.md](./docs/versioning-migration.md) to apply it to an existing app.
```

- [ ] **Step 2: Stage**

```pwsh
git add README.md
```

---

## Phase 6 — Final verification + cleanup

### Task 17: Confirm the full set of changes builds end-to-end

**Files:** none (verification only)

- [ ] **Step 1: Clean build, Dev channel**

```pwsh
dotnet build src/GcToolkit/GcToolkit.csproj -f net10.0-desktop
```

Expected: build succeeds. The DEV badge renders if you actually run the app.

- [ ] **Step 2: Clean build, Prod channel**

```pwsh
dotnet build src/GcToolkit/GcToolkit.csproj -f net10.0-desktop -p:AppChannel=Prod
```

Expected: build succeeds. No DEV banner. `obj/.../AssemblyInfo.cs` should show `AssemblyTitle("GC Toolkit")` (not "GC Toolkit Dev").

- [ ] **Step 3: Run the existing test suite to make sure nothing regressed**

```pwsh
dotnet test tests/GcToolkit.Core.Tests/GcToolkit.Core.Tests.csproj
```

Expected: tests pass. (Reminder: do NOT pass `--nologo` — Microsoft.Testing.Platform rejects it and silently runs zero tests.)

- [ ] **Step 4: `dotnet format`**

```pwsh
dotnet format whitespace src/GcToolkit.slnx
```

Expected: no diff (or, if there is one, stage it).

- [ ] **Step 5: Stage any remaining formatting fixes and review the full staged diff**

```pwsh
git add -u
git diff --cached --stat
```

Expected: every file touched by Tasks 1–16 appears in the cached diff with the changes listed.

### Task 18: Update the design spec's secret-name discrepancy

**Files:**
- Modify: `docs/superpowers/specs/2026-05-28-versioning-redesign-design.md`

- [ ] **Step 1: Fix the secret names in §5.4**

The spec's §5.4 secrets table lists `APP_STORE_CONNECT_API_KEY_ID` / `_ISSUER_ID` / `_PRIVATE_KEY`. The actual repo (and this plan) uses `APPSTORE_API_KEY_ID` / `APPSTORE_ISSUER_ID` / `APPSTORE_API_PRIVATE_KEY` — the names that already exist in `package-ios.yml`. Edit the spec to use the actual names so it doesn't drift from reality.

Find the spec's secrets table and replace:
- `APP_STORE_CONNECT_API_KEY_ID` → `APPSTORE_API_KEY_ID`
- `APP_STORE_CONNECT_ISSUER_ID` → `APPSTORE_ISSUER_ID`
- `APP_STORE_CONNECT_PRIVATE_KEY` → `APPSTORE_API_PRIVATE_KEY`

Also fix the same names in §7 step 6 (migration guide outline).

- [ ] **Step 2: Stage**

```pwsh
git add docs/superpowers/specs/2026-05-28-versioning-redesign-design.md
```

### Task 19: Final review and handoff

- [ ] **Step 1: List the full staged change set**

```pwsh
git diff --cached --stat
```

Expected files (no more, no less):
- `version.json`
- `src/Directory.Build.props`
- `src/GcToolkit/GcToolkit.csproj`
- `src/GcToolkit/Assets/Icons/icon_dev.svg`
- `src/GcToolkit/Assets/Icons/icon_transparent_dev.svg`
- `src/GcToolkit/Infrastructure/AppEnvironment.cs`
- `src/GcToolkit/Controls/DevChannelBadge.xaml`
- `src/GcToolkit/Controls/DevChannelBadge.xaml.cs`
- `src/GcToolkit/WindowShell.xaml`
- `src/GcToolkit/Resources/Converters.xaml` (only if the `BoolToVisibility` key had to be added)
- `src/GcToolkit/appsettings.Dev.json`
- `.github/workflows/package-windows.yml`
- `.github/workflows/package-android.yml`
- `.github/workflows/package-ios.yml`
- `.github/workflows/tag-release.yml`
- `.github/workflows/prepare-release.yml`
- `.github/workflows/validate-manifest-version.yml`
- `docs/versioning.md`
- `docs/versioning-migration.md`
- `README.md`
- `docs/superpowers/specs/2026-05-28-versioning-redesign-design.md`

- [ ] **Step 2: Hand off to the user**

Tell the user the work is fully staged, summarize what's expected to happen on first push of the new workflows (the public/private guard means main pushes on a private fork will skip packaging silently — this is by design), and remind them to create the `BASE64_ENCODED_WINDOWS_PFX_DEV` secret before merging if they want main builds to produce signed Dev MSIX artifacts.

---

## Self-review

**Spec coverage:**
- §1 model — Task 14 (versioning.md) summarizes it; Tasks 1, 2 implement the underlying `version.json` + `AppChannel` switches.
- §2 versioning model — Task 1.
- §3 AppChannel mechanism — Tasks 2, 3, 4 (csproj + manifest; spec's token-replacement is intentionally NOT used because Uno SDK already synthesizes the manifest from csproj properties).
- §4 side-by-side identity matrix — Tasks 3, 4, 8 (Windows cert switch), 9 (Android packageName).
- §5 workflows — Tasks 8, 9, 10, 11, 12, 13.
- §6 release lifecycle — covered in `docs/versioning.md` (Task 14).
- §7 migration plan — Task 15 (`docs/versioning-migration.md`).
- §8 documentation — Tasks 14, 15, 16.
- §9 "what we deliberately did not do" — no implementation tasks needed.
- §10 open questions — XmlPoke unused (spec's token replacement is replaced by csproj conditionals); `nbgv prepare-release` PR-fallback handled in Task 12.

**Placeholder scan:** no "TBD"/"TODO" left; every code/YAML block is complete. The `appsettings.Dev.json` body is intentionally placeholder content with a documented reason.

**Type consistency:** `AppEnvironment.IsDevChannel` + `AppEnvironment.ChannelLabel` (Task 5) match the binding paths in `DevChannelBadge.xaml` (Task 6). `AppChannel` MSBuild property name used identically across `Directory.Build.props` (Task 2), `GcToolkit.csproj` (Tasks 3, 4), and all three workflows (Tasks 8, 9, 10). `Prod` / `Dev` values used consistently throughout.

**Plan deviations from spec, documented:**
1. Windows manifest token replacement (spec §3.3) is replaced with csproj conditionals because Uno's SDK already drives manifest synthesis from `ApplicationId` / `ApplicationTitle` and the Publisher CN comes from the signing cert. This is simpler than the spec proposed and produces the same observable result.
2. iOS Dev `.ipa` artifact (spec §5.1) is replaced with verify-only build. The cost (full Apple Developer dev cert/profile setup with extra secrets) outweighs the value (iOS sideload is impractical without registered UDIDs). Apps that migrate can add the Dev-cert/Dev-profile path themselves. Documented in Task 10's note.
3. Secret names corrected: spec says `APP_STORE_CONNECT_*`, repo and plan use `APPSTORE_*`. Spec is patched in Task 18.
