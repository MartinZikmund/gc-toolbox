# Phase 1 Contracts: Services & Tool Registration

The "contracts" for this UI application are the **service interfaces** the shell exposes/consumes and the **tool-registration contract** that later phases implement to add tools. All live in `GCToolkit.Core` (logic) and are registered in `App.xaml.cs` `RegisterServices`. Signatures are indicative C#; final shapes may refine during TDD.

## Tool registration contract (extensibility — FR-015)

A tool is contributed by registering one or more `ToolDescriptor`s. Contributors are resolved from DI so the shell never references individual tools.

```csharp
namespace GCToolkit.Core.Catalog;

public interface IToolContributor
{
    IEnumerable<ToolDescriptor> GetTools();
}
```

Registration example (later phases add a line; SP-1 registers a placeholder contributor):

```csharp
services.AddSingleton<IToolContributor, CoordinatesToolContributor>();
services.AddSingleton<IToolContributor, PlaceholderToolContributor>(); // SP-1 only
```

## ICatalogService

Aggregates all contributed tools, exposes categories, and runs search.

```csharp
namespace GCToolkit.Core.Catalog;

public interface ICatalogService
{
    IReadOnlyList<Category> GetCategories();
    IReadOnlyList<ToolDescriptor> GetTools();
    IReadOnlyList<ToolDescriptor> GetToolsByCategory(string categoryId);

    // Accent- and case-insensitive match over name + keywords (FR-003).
    // Empty/whitespace query returns the full catalog.
    IReadOnlyList<ToolDescriptor> Search(string query);
}
```

Search matching is a separately unit-tested pure function:

```csharp
namespace GCToolkit.Core.Search;

public interface IToolMatcher
{
    bool Matches(ToolDescriptor tool, string query); // IgnoreCase | IgnoreNonSpace
}
```

## IFavoritesService

```csharp
namespace GCToolkit.Core.Favorites;

public interface IFavoritesService
{
    bool IsFavorite(string toolId);
    Task ToggleAsync(string toolId);                 // add if absent, remove if present
    IReadOnlyList<string> GetFavoriteToolIds();      // stable order (AddedUtc)
    event EventHandler FavoritesChanged;
}
```

## IRecentsService

```csharp
namespace GCToolkit.Core.Recents;

public interface IRecentsService
{
    Task RecordOpenedAsync(string toolId);           // dedupe + bump to top
    IReadOnlyList<string> GetRecentToolIds();         // most-recent-first, max 10
    Task ClearAsync();                                // empties the list
    event EventHandler RecentsChanged;
}
```

Rules (see data-model): deduplicated by `toolId`, capped at 10, most-recent-first, clearable.

## ILanguageService (language selection — FR-008/FR-009)

```csharp
namespace GCToolkit.Core.Localization;

public sealed record AppLanguage(string Code, string NativeNameKey); // "en", "cs"

public interface ILanguageService
{
    AppLanguage Current { get; }
    IReadOnlyList<AppLanguage> Available { get; }     // { en, cs }
    Task SetAsync(string code);                       // persists; applied on next launch (restart)
    event EventHandler LanguageChanged;               // optional; drives a restart-to-apply prompt
}
```

A language change is persisted and applied at startup; applying it **may require an app restart** (user-accepted, 2026-05-30). The `LocalizeExtension` is reused unchanged (one-shot); `LanguageChanged` is optional and, if used, drives a "restart to apply" notice rather than live text refresh.

## Reused template contracts (no change to their shape)

- **`INavigationService`** — view-model-first navigation: `RegisterView<TView,TViewModel>()`, `Navigate<TViewModel>(parameter?)`, back-stack. The shell opens a tool by navigating to its `ToolDescriptor.ViewModelType` and records it via `IRecentsService`.
- **`IThemeManager`** — `CurrentTheme` (`ElementTheme`), `SetTheme(ElementTheme)`, `ActualTheme`, OS-change + title-bar sync (extended to color the WinUI `TitleBar`). Implements FR-007.
- **`IPreferences` / `IAppPreferences`** — typed local store (`Get/Set`, `GetComplex/SetComplex`); backs favorites, recents, theme, language (FR-010).

## Catalog/shell consumption summary

| Screen | Consumes |
|---|---|
| Home | `IFavoritesService`, `IRecentsService`, `ICatalogService` (search entry), `INavigationService` |
| Catalog | `ICatalogService` (categories, tools, search), `IFavoritesService`, `INavigationService` |
| Tool host (stub) | `INavigationService`, `IRecentsService` (record on open), `IFavoritesService` (toggle) |
| Settings | `IThemeManager`, `ILanguageService`, `IRecentsService` (clear recents) |
| Shell / title bar | `ICatalogService` (search), platform-adaptive search placement (FR-018) |
