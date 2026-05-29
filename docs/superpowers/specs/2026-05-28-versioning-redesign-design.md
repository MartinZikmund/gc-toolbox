# Versioning redesign — design

**Date:** 2026-05-28
**Status:** Proposed
**Supersedes:** PR #7 (`docs: dev/release versioning strategy`) — commits `1ce85c5`, `29d7ca6`.
**Applies to:** `uno-app-template` and (via migration guide) `stopwatch`, `daily-dozen`.

---

## 1. Model

> **`main` is where the *next* release is brewing; `release/v{minor}` branches are where releases actually ship. Every build carries an `AppChannel` — `Dev` for local + main, `Prod` for `release/v*` — baked into its package identity so dev and prod can sit side-by-side on every device.**

`version.json` on `main` always names the *next planned* minor. NBGV stamps `main` builds as `{next}.0-dev.{height}` and `release/v*` builds as `{minor}.{patch}` (stable). All `main` artifacts get `AppChannel=Dev`; all `release/v*` artifacts get `AppChannel=Prod`. Dev artifacts can be installed alongside the Store version because they declare a distinct Package Identity / Bundle ID / Android `applicationId`.

This inverts the current scheme (where `main` is the public-release branch). Goals:

- **Releases are explicit and stabilizable.** Cutting `release/v0.2` freezes the line; hotfixes commit there without disturbing `main`.
- **`main` can never accidentally produce a stable version.** `publicReleaseRefSpec` only matches `release/v*`.
- **Local builds never clobber a tester's Store install.** Distinct identity per channel.
- **Mainline iteration is safe.** Every commit is a Dev-channel sideloadable artifact (in public repos, automatic; in private repos, manual to save Actions minutes).

## 2. Versioning model

### 2.1 `version.json`

```jsonc
{
  "$schema": "https://raw.githubusercontent.com/dotnet/Nerdbank.GitVersioning/main/src/NerdBank.GitVersioning/version.schema.json",
  "version": "0.2",                              // NEXT planned release (bumped at each cut)
  "nuGetPackageVersion": { "semVer": 2.0 },
  "publicReleaseRefSpec": [
    "^refs/heads/release/v\\d+\\.\\d+$"          // ONLY release branches publish stable
  ],
  "cloudBuild": { "buildNumber": { "enabled": true } },
  "release": {
    "branchName": "release/v{version}",
    "firstUnstableTag": "dev"                    // was "beta"
  }
}
```

Changes from current `version.json`:
- `publicReleaseRefSpec`: was `^refs/heads/main$` → now matches `release/v{minor}` only.
- `firstUnstableTag`: was `beta` → now `dev`.
- `version` value on `main` is the *next* planned release. On `release/v0.1` it stays at `0.1` (NBGV uses the branch's `version.json`).

### 2.2 Branch semantics

| Branch | `version.json` | NBGV-computed | Manifest version | Channel |
|---|---|---|---|---|
| `main` | `0.2` | `0.2.0-dev.{height}` | `0.2.0.{height}` | Dev |
| `release/v0.1` | `0.1` | `0.1.0`, `0.1.1`, … | `0.1.0.0`, `0.1.1.0`, … | Prod |
| feature/* | `0.2` (inherited from main) | `0.2.0-dev.{height}+{sha}` | (not packaged) | Dev |
| `release/v0.2` (future) | `0.2` | `0.2.0`, `0.2.1`, … | `0.2.0.0`, `0.2.1.0`, … | Prod |

### 2.3 Template's own `version.json`

The template itself is not a shipped app. `version.json` stays at `0.1`; no release branch is cut for the template. Workflows + docs demonstrate the pattern for consumers.

## 3. AppChannel mechanism

### 3.1 Property

`AppChannel` is an MSBuild property with two values: `Dev` (default) and `Prod`. Set in `Directory.Build.props`:

```xml
<PropertyGroup>
  <AppChannel Condition="'$(AppChannel)' == ''">Dev</AppChannel>
  <DefineConstants Condition="'$(AppChannel)' == 'Dev'">$(DefineConstants);APP_CHANNEL_DEV</DefineConstants>
</PropertyGroup>
```

CI overrides by passing `/p:AppChannel=Prod` when building on `release/v*`. Local `dotnet build` / VS F5 omits the property → defaults to `Dev`.

### 3.2 Android + iOS identity (csproj conditionals)

In `src/GcToolkit/GcToolkit.csproj`:

```xml
<PropertyGroup Condition="'$(AppChannel)' == 'Prod'">
  <ApplicationId>dev.zikmund.GcToolkit</ApplicationId>
  <ApplicationTitle>GC Toolkit</ApplicationTitle>
</PropertyGroup>
<PropertyGroup Condition="'$(AppChannel)' == 'Dev'">
  <ApplicationId>dev.zikmund.GcToolkit.dev</ApplicationId>
  <ApplicationTitle>GC Toolkit Dev</ApplicationTitle>
</PropertyGroup>

<ItemGroup>
  <UnoIcon Include="Assets\AppIcon.svg"     Condition="'$(AppChannel)' == 'Prod'" />
  <UnoIcon Include="Assets\AppIcon_Dev.svg" Condition="'$(AppChannel)' == 'Dev'" />
</ItemGroup>
```

`ApplicationId` is mapped by Uno to Android `applicationId` and iOS `CFBundleIdentifier`. `ApplicationTitle` becomes the display name on both.

### 3.3 Windows manifest (token replacement)

Rename `src/GcToolkit/Package.appxmanifest` → `Package.template.appxmanifest` with placeholders:

```xml
<Identity Name="__APP_IDENTITY_NAME__" Publisher="__APP_PUBLISHER__" Version="0.0.0.0" />
<Properties>
  <DisplayName>__APP_DISPLAY_NAME__</DisplayName>
  ...
</Properties>
<Applications>
  <Application Id="App" Executable="$targetnametoken$.exe" EntryPoint="$targetentrypoint$">
    <uap:VisualElements DisplayName="__APP_DISPLAY_NAME__" ... />
  </Application>
</Applications>
```

MSBuild target generates the real manifest into `obj/` before NBGV stamps the version:

```xml
<Target Name="GeneratePackageManifest" BeforeTargets="_GenerateAppxManifest;GenerateNBGVThisAssemblyInfo">
  <PropertyGroup>
    <_AppIdentityName Condition="'$(AppChannel)' == 'Dev'">GcToolkit.Dev</_AppIdentityName>
    <_AppIdentityName Condition="'$(AppChannel)' == 'Prod'">GcToolkit</_AppIdentityName>
    <_AppDisplayName Condition="'$(AppChannel)' == 'Dev'">GC Toolkit Dev</_AppDisplayName>
    <_AppDisplayName Condition="'$(AppChannel)' == 'Prod'">GC Toolkit</_AppDisplayName>
    <_AppPublisher  Condition="'$(AppChannel)' == 'Dev'">CN=GcToolkit.Dev (Self-Signed)</_AppPublisher>
    <_AppPublisher  Condition="'$(AppChannel)' == 'Prod'">CN=YourPublisherCN</_AppPublisher>
    <_OutputManifest>$(IntermediateOutputPath)Package.appxmanifest</_OutputManifest>
  </PropertyGroup>
  <Copy SourceFiles="Package.template.appxmanifest" DestinationFiles="$(_OutputManifest)" />
  <XmlPoke XmlInputPath="$(_OutputManifest)" Query="..." Value="$(_AppIdentityName)" />
  <!-- one XmlPoke per token; or a single text Replace pass via WriteLinesToFile -->
</Target>

<ItemGroup>
  <AppxManifest Remove="@(AppxManifest)" />
  <AppxManifest Include="$(IntermediateOutputPath)Package.appxmanifest" />
</ItemGroup>
```

NBGV stamps `Version` on the generated manifest as it does today. `validate-manifest-version.yml` continues to enforce `Version="0.0.0.0"` on the template file.

### 3.4 In-app DEV banner

```csharp
// src/GcToolkit/AppEnvironment.cs
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

A small overlay control in the shell page (top-right corner badge), bound `Visibility="{x:Bind app:AppEnvironment.IsDevChannel, Mode=OneTime}"`. Single component, no theming dependency, always visible regardless of which page is shown.

## 4. Side-by-side identity matrix

| | **Prod** (`release/v*`) | **Dev** (local + main) |
|---|---|---|
| Windows Identity Name | `GcToolkit` | `GcToolkit.Dev` |
| Windows Publisher CN | `CN=YourPublisherCN` (Store-signed cert) | `CN=GcToolkit.Dev (Self-Signed)` |
| Windows display name | `GC Toolkit` | `GC Toolkit Dev` |
| Windows AppData folder | `Packages\GcToolkit_<storeId>` | `Packages\GcToolkit.Dev_<devId>` |
| Windows signing cert | `BASE64_ENCODED_WINDOWS_PFX` | `BASE64_ENCODED_WINDOWS_PFX_DEV` |
| Android applicationId | `dev.zikmund.GcToolkit` | `dev.zikmund.GcToolkit.dev` |
| Android display label | `GC Toolkit` | `GC Toolkit Dev` |
| Android keystore | Prod keystore (Play App Signing) | Debug keystore (auto-generated locally) |
| iOS Bundle ID | `dev.zikmund.GcToolkit` | `dev.zikmund.GcToolkit.dev` |
| iOS display name | `GC Toolkit` | `GC Toolkit Dev` |
| iOS provisioning profile | App Store distribution | Development / ad-hoc |
| App icon | `Assets/AppIcon.svg` | `Assets/AppIcon_Dev.svg` (DEV overlay) |
| In-app banner | hidden | `"DEV"` corner badge |
| `appsettings` | `appsettings.json` | `appsettings.Dev.json` (overlay) — RevenueCat sandbox keys here |
| NBGV version | `0.1.4`, `0.1.5`, … | `0.2.0-dev.{height}` |

**Critical:** Windows Identity Name *and* Publisher CN both differ between channels. Either alone would not yield a distinct Package Family — only the combination does (`PackageFamilyName = <Name>_<PublisherId>` where `<PublisherId>` is a hash of the Publisher string).

**Consumer customization:** when forking the template, change `GcToolkit` → your app name and `dev.zikmund` → your reverse-DNS prefix, in:
- `Directory.Build.props` (define constants once)
- `Package.template.appxmanifest` (publisher CN — both prod and dev)

Two files.

## 5. Workflows

### 5.1 Trigger + identity matrix

| Workflow | Triggers | Channel | Publishing |
|---|---|---|---|
| `ci.yml` (smoke test) | push: main; PR: main, release/** | n/a (Debug) | n/a |
| `validate-manifest-version.yml` | push: main; PR: main, release/** | n/a | n/a |
| `package-windows.yml` | push: main + release/v* + workflow_dispatch | Dev on main (self-signed), Prod on release/v* (Store-signed) | None — manual Partner Center submission |
| `package-android.yml` | push: main + release/v* + workflow_dispatch | Dev applicationId on main, Prod on release/v* | release/v* → Play Production track via `r0adkll/upload-google-play` |
| `package-ios.yml` | push: main + release/v* + workflow_dispatch | Dev Bundle ID on main, Prod on release/v* | release/v* → TestFlight upload via `apple-actions/upload-testflight-build` (manual "Submit for Review") |
| `tag-release.yml` (new) | needs: package-* on release/v* | n/a | Pushes annotated tag `v{NBGV version}` |
| `prepare-release.yml` (new, workflow_dispatch) | manual | n/a | Cuts release branch + opens PR for main bump |

### 5.2 Public/private repo cost gate

Every packaging workflow's primary job carries:

```yaml
if: github.event.repository.private == false || github.event_name == 'workflow_dispatch'
```

- Public repo, push to main / release/v*: packaging runs automatically.
- Private repo, push: packaging is skipped (saves Actions minutes — macOS is 10×).
- Either repo, `workflow_dispatch`: always runs.

The `tag-release.yml` job depends on the three packaging jobs; if they're skipped, it skips automatically.

`ci.yml` and `validate-manifest-version.yml` are not gated — they're cheap and the only CI signal on private repos by default.

### 5.3 Channel selection step (shared shape)

```yaml
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

Build/pack steps pass `/p:AppChannel=${{ steps.channel.outputs.value }}`. Windows packaging additionally picks the cert: `BASE64_ENCODED_WINDOWS_PFX_DEV` for Dev, `BASE64_ENCODED_WINDOWS_PFX` for Prod.

### 5.4 Secrets

| Secret | Purpose | Consumed by |
|---|---|---|
| `BASE64_ENCODED_WINDOWS_PFX` | Prod Windows signing cert | `package-windows.yml` on release/v* |
| `BASE64_ENCODED_WINDOWS_PFX_DEV` (new) | Self-signed Dev Windows cert | `package-windows.yml` on main |
| `REVENUECAT_IOS_API_KEY`, `REVENUECAT_ANDROID_API_KEY` | Existing RevenueCat keys | All packaging |
| `GOOGLE_PLAY_SERVICE_ACCOUNT_JSON` (new) | Play upload | `package-android.yml` on release/v* |
| `APPSTORE_API_KEY_ID` (new) | App Store Connect API auth | `package-ios.yml` on release/v* |
| `APPSTORE_ISSUER_ID` (new) | App Store Connect API auth | `package-ios.yml` on release/v* |
| `APPSTORE_API_PRIVATE_KEY` (new) | App Store Connect API auth (`.p8` contents) | `package-ios.yml` on release/v* |

Optional: `REVENUECAT_*_API_KEY_DEV` if separating sandbox entitlements.

### 5.5 Tagging

`tag-release.yml` runs only on `release/v*` push, with `needs: [package-windows, package-android, package-ios]`. On success:

```yaml
- run: dotnet tool install -g nbgv
- run: nbgv tag
- run: git push origin --tags
```

`nbgv tag` creates `v{computed-version}` pointing at the just-built commit. Annotated tag with NBGV's metadata.

### 5.6 `prepare-release.yml` (workflow_dispatch)

UI-driven release cut. Inputs: none (NBGV decides the version from current `version.json`). Steps:
- Checkout main with `fetch-depth: 0`.
- `dotnet tool install -g nbgv`.
- `nbgv prepare-release` → cuts `release/v{current}`, bumps main's `version.json`.
- Push the new release branch directly (it's new, branch protection doesn't apply).
- Open a PR on main with the version.json bump (using `peter-evans/create-pull-request` or `gh pr create`).

## 6. Release lifecycle

### 6.1 Cutting a new release

**Option A — local (canonical):**
```pwsh
# from main, with version.json = "0.2"
dotnet tool install -g nbgv      # once per machine
nbgv prepare-release             # cuts release/v0.2 from current HEAD,
                                 # bumps main's version.json to 0.3,
                                 # creates a merge commit on main
git push origin main release/v0.2
```

**Option B — workflow_dispatch (`prepare-release.yml`):** Actions → Prepare Release → Run.

### 6.2 Building releases

Push (or merge PR) to `release/v0.2` → three packaging workflows fire (subject to the public/private gate) → Prod identity, Store-signed → upload artifacts → on success of all three, `tag-release.yml` pushes `v0.2.0`. Android auto-publishes to Play Production; iOS auto-uploads to TestFlight (manual "Submit for Review"); Windows produces a `.msixupload` for manual Partner Center submission.

### 6.3 Patching a release

Commit fix to `release/v0.2`. NBGV bumps patch → `0.2.1`. Same workflows fire, same auto-publish, new tag `v0.2.1`.

If the fix also belongs on `main`: cherry-pick or merge `release/v0.2` → `main`.

### 6.4 Mainline development

Push to `main`. NBGV stamps `0.3.0-dev.{height}` (assuming the most recent cut bumped main to 0.3). Workflows produce Dev-identity artifacts (public repo: auto; private repo: manual dispatch). No publishing.

### 6.5 Next release after 0.2

When 0.2.x is stable and main has accumulated changes for 0.3:
1. `nbgv prepare-release` on main → cuts `release/v0.3`, bumps main's `version.json` to `0.4`.
2. Push.

`release/v0.2` continues to exist for hotfixes if needed; `release/v0.3` is where 0.3.x lives.

### 6.6 Major version bump

Set `version.json` on main to `1.0` manually instead of running `prepare-release`. NBGV doesn't enforce a major-bump policy.

## 7. Migration plan for existing apps

A migration is its own PR per app. Steps are documented in `docs/versioning-migration.md` (shipped by the template). Outline:

1. **Cut a release branch from current main HEAD** at the current version (e.g., `release/v0.12` for stopwatch). Push.
2. **Bump main's `version.json` to next minor** (`0.13`). Update `publicReleaseRefSpec` and `firstUnstableTag` per §2.1.
3. **Replicate template file changes:**
   - Add `AppChannel` default + `APP_CHANNEL_DEV` constant to `Directory.Build.props`.
   - Add per-channel `ApplicationId`/`ApplicationTitle` conditionals to the head csproj.
   - Rename `Package.appxmanifest` → `Package.template.appxmanifest` with tokens; add the `GeneratePackageManifest` MSBuild target.
   - Add `AppEnvironment.cs` + DEV banner control.
   - Add `Assets/AppIcon_Dev.svg`.
   - Add `appsettings.Dev.json` if separating sandbox keys.
4. **Replicate workflow changes:**
   - Add release/v* triggers + public/private guard + channel-selection step to all three `package-*.yml`.
   - Add Play upload step to `package-android.yml` (release/v* only).
   - Add TestFlight upload step to `package-ios.yml` (release/v* only).
   - Add `tag-release.yml`.
   - Add `prepare-release.yml`.
5. **Add the new secrets** to the repo.
6. **Store-side prerequisites:**
   - Play Console: create service account with "Release manager" role, download JSON. First Production upload must be manual; subsequent ones can be API-driven.
   - App Store Connect: create API key with "App Manager" role, save Key ID + Issuer ID + p8 contents.
7. **Verify** by pushing to main (Dev artifacts, no publish) and to `release/v{current}` (Prod artifacts, publish, tag).
8. **Clean up** old `publish-on-main` comments / scripts.

Apps migrate on their own schedule. The `reference-apps` memory gets updated as each lands.

## 8. Documentation

Two files, checked in:

- **`docs/versioning.md`** — full reference for the model, written for someone new to the repo. Replaces whatever landed in PR #7. Sections mirror §1–§6.
- **`docs/versioning-migration.md`** — step-by-step migration for existing apps (§7). Lives in the template so reference-app migrations follow it verbatim.

**README touch-up:** one-paragraph "Versioning" section pointing at `docs/versioning.md`.

**Removed:**
- Prose from PR #7 (commits `1ce85c5`, `29d7ca6`) — superseded by `docs/versioning.md`.
- "main is a public-release branch" comments in `package-windows.yml` etc.

## 9. What we deliberately did NOT do

- **No `release/v0.1` cut for the template itself.** The template isn't a shipped app.
- **No additional CI guardrails** beyond the existing `validate-manifest-version.yml`. (Considered: enforce version.json on release/v* matches branch name; enforce main's version.json > latest tag. Both rejected as YAGNI.)
- **No auto-publish from main.** Main artifacts are sideload-only.
- **No Microsoft Store auto-submission** from `release/v*`. Manual Partner Center upload, same as today.
- **No auto-submit-for-review on iOS.** TestFlight upload only; "Submit for Review" remains a deliberate human gate.
- **No solution-configuration matrix** (`Release-Dev` / `Release-Prod`). One MSBuild property is enough.
- **No auto-migration scripts** for reference apps. Migration guide is prescriptive but human-driven.

## 10. Open questions for the implementation plan

None blocking. Items to confirm during planning, not now:

- Exact `XmlPoke` queries for namespaced AppxManifest XPath (verify against current manifest schema).
- Whether `nbgv prepare-release` works cleanly inside GitHub Actions when branch protection is in effect for main (PR-creation fallback is the safe path).
- Pinned action versions for `r0adkll/upload-google-play`, `apple-actions/upload-testflight-build`, `LanceMcCarthy/Action-MsixBundler` (already pinned to `@v3` for the last one).
