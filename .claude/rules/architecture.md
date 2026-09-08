---
description: Project layout, MVVM, DI, navigation, and localization conventions
---

# Architecture & MVVM

## Project layout
- **`GcToolkit.Core`** — the unit-testable layer: view models, service *interfaces*, models, `ViewModelBase`. Core **references Uno.UI**, so WinUI/Uno UI *types* (e.g. `ElementTheme`) are available here — using a UI type is **not** a reason to push a VM into the head.
- **`GcToolkit`** (the head) — Views, platform/service *implementations*, and app composition (`App`, `WindowShell`). A view model belongs here only when it depends on a type or service that is itself defined in the head.
- **Default to Core for view models and logic** so they're testable without a UI head. Where practical, define a service's interface in Core so its consumers can live in Core too.

## ViewModels
- Derive from `ViewModelBase : ObservableObject` (CommunityToolkit.Mvvm).
- Observable state uses the **partial-property** form: `[ObservableProperty] public partial bool IsBusy { get; set; }`. React to changes with the generated `partial void OnIsBusyChanged(...)`.
- Commands use `[RelayCommand]`.
- Override the lifecycle hooks on `ViewModelBase` instead of wiring page events: `ViewCreated`, `ViewLoading`, `ViewLoaded`, `ViewUnloaded`, `OnNavigatedTo(object?)`, `OnNavigatedFrom`.
- **VMs never manipulate views directly.** A VM may use UI *data types* (e.g. `ElementTheme`), but must not reach into controls/visual elements — interact with the UI only through bindings, the lifecycle hooks above, and service abstractions (navigation, dialogs, theming, etc.). This keeps VMs head-independent and testable.
- **When a VM genuinely must drive its view** (move focus, scroll, run an animation), define a small *view-service interface* in Core, have the View implement it and register itself on the VM (e.g. from `ViewCreated`/`ViewLoaded`), and call through that interface. Never hold a reference to a concrete control.

## Views
- XAML can't reference a generic base, so each view uses a **non-generic intermediate base** that closes the generic: `public partial class XViewBase : ViewBase<XViewModel> { }`, then `public sealed partial class XView : XViewBase`. The XAML root element is the intermediate base (`<local:XViewBase x:Class="…XView" …>`).
- `[NavigationInfo(NavigationSection.X)]` goes on the view type (`XView`).
- `ViewBase<TViewModel>` resolves the VM from the per-window scope and sets `DataContext`. **Never** `new` a ViewModel or assign `DataContext` by hand.

## Dependency injection
- Everything is registered in `App.RegisterServices` (`src/GcToolkit/App.xaml.cs`).
- Lifetimes: **Singleton** for app-wide state, **Scoped** for per-window services (resolved from the `WindowShell` scope), **Transient** for page ViewModels.
- Scope validation (`ValidateScopes`/`ValidateOnBuild`) is on — a captive dependency (e.g. a singleton holding a scoped service) fails at startup. Get lifetimes right.

## Navigation
- Convention-based, type-driven. Navigate with `INavigationService.Navigate<TViewModel>()`.
- Register each page explicitly (avoid reflection) in the `NavigationService` factory in `RegisterServices`: `service.RegisterView(typeof(Views.XView), typeof(XViewModel));`.

## Messaging
- For decoupled cross-ViewModel communication, use CommunityToolkit.Mvvm's **`WeakReferenceMessenger`** (`WeakReferenceMessenger.Default.Send(...)` / `.Register<TMessage>(this, ...)`) rather than direct VM-to-VM references or events. Weak references avoid the leaks that `StrongReferenceMessenger` risks.
- Define message types in Core (e.g. a `Messages/` folder) so both sender and receiver stay head-independent.

## Adding a page (recipe)
1. Create `XViewModel : ViewModelBase` — in Core by default; in the head only if it depends on a head-defined type or service.
2. Create the view as a pair: `XViewBase : ViewBase<XViewModel>` in code-behind, and `XView : XViewBase` (carrying `[NavigationInfo(NavigationSection.X)]`) with the XAML rooted at `<local:XViewBase x:Class="…XView" …>`.
3. In `RegisterServices`: `service.RegisterView(typeof(Views.XView), typeof(XViewModel));` and `services.AddTransient<XViewModel>();`.

## Persisting state
- Persist local data through the toolkit's `IPreferences`, not a custom store.
- **Every type stored with `SetComplex`/`GetComplex` must be declared in `GcToolkit.Core.Serialization.PreferencesJsonContext`.** The WASM release publish is trimmed, and trimming turns reflection-based `System.Text.Json` off; `Preferences` is registered in `App.RegisterServices` with that context as its `TypeInfoResolver`, so an unregistered type throws `InvalidOperationException: JsonSerializerIsReflectionDisabled` at runtime — on WASM only, so Windows/Desktop won't catch it. The `TrimmedHead()` preferences fake in the tests models this.
- Avoid reflection-based `System.Text.Json` calls (`JsonSerializer.Serialize(value)` and friends) elsewhere for the same reason — pass a `JsonTypeInfo<T>` from a source-generated context.

## Localization
- **Never hardcode user-facing strings.** Add the key to **both** `src/GcToolkit/Strings/en/Resources.resw` and `src/GcToolkit/Strings/cs/Resources.resw`.
- In XAML: `{markup:Localize Key=MyKey}` (the `markup` prefix maps to `using:GcToolkit.Markup`). In code: inject `IStringLocalizer` (constructor) or use `Localizer.Instance["MyKey"]`.
