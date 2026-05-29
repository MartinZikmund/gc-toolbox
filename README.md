# GC Toolkit

## Views

### `IViewBase`

`ViewBase<TViewModel>` is the base class for views (pages). It resolves the view model from the
hosting `WindowShell` service provider and forwards lifecycle events to it.

`IViewBase` is a non-generic interface that exposes the resolved view model as `object?`:

```csharp
public interface IViewBase
{
    object? ViewModel { get; }
}
```

Every `ViewBase<TViewModel>` implements `IViewBase`, so a view can be referenced through the
interface without depending on its concrete view model type.

Use it when:

- A `DataTemplate` (or other loosely-typed code) needs to reach a view's view model without
  knowing its generic argument.
- A test needs to inspect a view's resolved view model without knowing its concrete type at
  compile time.

### Restore dotnet tools

The repository ships a local tool manifest (`.config/dotnet-tools.json`). Restore the tools once after cloning:

```bash
dotnet tool restore
```

### Build and Run

```bash
cd src/GcToolkit
# Windows (requires Windows + WinAppSDK / Windows SDK)
dotnet build -f net10.0-windows10.0.26100
dotnet run -f net10.0-windows10.0.26100

# Cross-platform desktop (macOS, Linux, Windows)
dotnet build -f net10.0-desktop
dotnet run -f net10.0-desktop
```

Swap the target framework (`net10.0-android`, `net10.0-ios`, `net10.0-browserwasm`) to build for other platforms.

## XAML Styler

XAML formatting is kept consistent with [XAML Styler](https://github.com/Xavalon/XamlStyler), pinned as a local dotnet tool. The [`Settings.XamlStyler`](Settings.XamlStyler) file at the repository root holds the rules (aligned with the Uno Platform and Windows Community Toolkit conventions).

From the repository root, format all XAML files under `src/`:

```bash
dotnet xstyler -c Settings.XamlStyler -r -d ./src
```

Or verify formatting without writing changes (useful in CI):

```bash
dotnet xstyler -c Settings.XamlStyler -r -d ./src --passive
```

### Enforcement

Formatting is enforced on every pull request by the **XAML Style Check** workflow
([`.github/workflows/xaml-style-check.yml`](.github/workflows/xaml-style-check.yml)). If a PR contains
unformatted XAML, the check fails, uploads a `xaml-style-patch` artifact, and comments with how to fix it:

- **Branches in this repo:** comment `/apply-xaml-style` on the PR and a bot
  ([`.github/workflows/xaml-style-apply.yml`](.github/workflows/xaml-style-apply.yml)) formats the XAML and
  pushes `chore: Apply XAML styler` to the PR branch.
- **Forks:** download the `xaml-style-patch` artifact and apply it locally (`git apply xaml-style.patch`),
  or just re-run the formatter command above and commit the result.

## Versioning

This template uses Nerdbank.GitVersioning. `main` produces `0.X.0-dev.{height}` prerelease builds with a Dev-channel identity that installs side-by-side with the Store version. Stable releases come from `release/v{minor}` branches. See [docs/versioning.md](./docs/versioning.md) for the full model and [docs/versioning-migration.md](./docs/versioning-migration.md) to apply it to an existing app.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
