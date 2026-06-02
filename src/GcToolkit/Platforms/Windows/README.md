# Windows (WinAppSDK) head

On the `net10.0-windows10.0.26100` target framework the app is compiled as a plain
WinUI / Windows App SDK application using Microsoft's own tooling (Uno Platform is not
involved at runtime on this head). See
[How Uno Platform Works](https://platform.uno/docs/articles/how-uno-works.html#windows-using-winappsdk).

## Running from the command line

### Packaged (`dotnet run`)

`dotnet run` support for the packaged WinAppSDK head is provided by the
[`Microsoft.Windows.SDK.BuildTools.WinApp`](https://www.nuget.org/packages/Microsoft.Windows.SDK.BuildTools.WinApp)
package, referenced for the Windows target in `GcToolkit.csproj`. Its MSBuild targets
hook into the standard `dotnet run` pipeline: they build the project, create a
loose-layout package, register it with Windows (like a real MSIX install) and launch it.

```powershell
# From the src/GcToolkit folder
dotnet run -f net10.0-windows10.0.26100
```

The project defaults to a packaged build (`WindowsPackageType=MSIX`), so no extra flags
are required. Requirements: Windows 10 or later and Developer Mode enabled (or an elevated
terminal) so the loose-layout package identity can be registered.

Optional MSBuild properties exposed by the package (set on the command line with
`-p:Name=Value` or in the `.csproj`):

| Property | Default | Description |
|----------|---------|-------------|
| `EnableWinAppRunSupport` | `true` | Enable/disable the `dotnet run` integration. |
| `WinAppLaunchArgs` | (empty) | Arguments passed to the app on launch. |
| `WinAppRunUseExecutionAlias` | `false` | Launch via execution alias instead of AUMID activation (keeps console I/O in the current terminal). |
| `WinAppRunNoLaunch` | `false` | Register identity without launching (attach your own debugger afterwards). |
| `WinAppRunDebugOutput` | `false` | Capture `OutputDebugString` and first-chance exceptions. |

### Unpackaged (`dotnet run`)

To run unpackaged, override `WindowsPackageType` so no MSIX identity is created and select
the unpackaged launch profile explicitly (otherwise `dotnet run` picks the first
Windows-compatible profile):

```powershell
# From the src/GcToolkit folder
dotnet run -f net10.0-windows10.0.26100 -p:WindowsPackageType=None --launch-profile "GC Toolkit (WinAppSDK Unpackaged)"
```

The matching launch profile is **GC Toolkit (WinAppSDK Unpackaged)** in
`Properties/launchSettings.json` (`commandName: Project`).

### Desktop (Skia) head — for comparison

The cross-platform Skia desktop head runs on Windows (and Linux/macOS) and does not use
Windows App SDK:

```powershell
# From the src/GcToolkit folder
dotnet run -f net10.0-desktop
```

## IDE

In Visual Studio / Rider pick one of the Windows launch profiles from the debug target
dropdown:

- **GC Toolkit (WinAppSDK Unpackaged)** — unpackaged run (`commandName: Project`).
- **GC Toolkit (WinAppSDK Packaged)** — packaged MSIX run (`commandName: MsixPackage`).

> Note: a [Visual Studio issue](https://aka.platform.uno/wasdk-maui-debug-profile-issue)
> can hide the unpackaged profile when iOS/Android target frameworks are present. If the
> profile is not selectable, comment out the packaged profile in
> `Properties/launchSettings.json` until it is fixed.
