# Migrating an existing app to the new versioning model

This guide walks an existing Uno app (stopwatch, daily-dozen, …) through adopting the model described in [versioning.md](./versioning.md). Apply per-app, on the app's own schedule.

## Pre-flight

Check what's currently shipped. If `version.json` says `0.12` and the last Store/Play submission was `0.12.x`, that's your current minor.

## Steps

### 1. Cut a release branch from current main HEAD

```pwsh
git checkout main; git pull
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

### 5. Add a Dev icon variant

Copy `Assets/Icons/icon_foreground.svg` → `icon_foreground_dev.svg` and give it a distinct treatment (e.g. recolor to a caution amber or add a DEV ribbon). Add a single channel-conditional line — `<UnoIconForegroundFile Condition="'$(AppChannel)' == 'Dev'">Assets/Icons/icon_foreground_dev.svg</UnoIconForegroundFile>` — see the template's `GcToolkit.csproj`.

> **Do not** override `UnoIconBackgroundFile` per channel. Uno maps the generated app-icon resource name to the background file's base name, so a per-channel background (e.g. `icon_dev.svg`) renames the resource to `@mipmap/icon_dev` while the manifest still references `@mipmap/icon`, breaking the Android build (`APT2260: resource mipmap/icon not found`). Keep the background constant and vary only the foreground.

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
