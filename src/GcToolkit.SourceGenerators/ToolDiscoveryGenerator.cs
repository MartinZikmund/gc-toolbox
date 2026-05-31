using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace GcToolkit.SourceGenerators;

/// <summary>
/// The single incremental generator for attribute-based tool discovery. Runs in the app head
/// (referenced as an analyzer). It scans the referenced <c>GcToolkit.Core</c> assembly's metadata
/// for <c>[Tool]</c> ViewModels (R2), discovers the app head's <c>ViewBase&lt;TVm&gt;</c> views by
/// syntax, validates localization resources from <c>.resw</c> <c>AdditionalFiles</c>, and emits the
/// generated catalog contributor, navigation tree, DI registration, and view registrations.
/// Output carries a static <c>&lt;auto-generated/&gt;</c> header (no timestamp — R13).
/// </summary>
[Generator]
public sealed class ToolDiscoveryGenerator : IIncrementalGenerator
{
    private const string ToolAttributeMetadataName = "GcToolkit.Core.Discovery.ToolAttribute";
    private const string ToolViewModelBaseMetadataName = "GcToolkit.Core.ViewModels.ToolViewModelBase";
    private const string ToolCategoryMetadataName = "GcToolkit.Core.Discovery.ToolCategory";
    private const string ViewBaseMetadataName = "GcToolkit.Views.ViewBase`1";
    private const string ToolHostViewName = "global::GcToolkit.Views.ToolHostView";

    private const string DiagnosticCategory = "GcToolkit.SourceGenerator";

    private static readonly DiagnosticDescriptor MissingRequiredValue = new(
        "GCTOOL001", "Invalid [Tool] metadata",
        "Tool '{0}': {1}",
        DiagnosticCategory, DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidTarget = new(
        "GCTOOL002", "Invalid [Tool] target",
        "'{0}' is marked [Tool] but is not a navigable tool ViewModel (must derive ToolViewModelBase)",
        DiagnosticCategory, DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidDate = new(
        "GCTOOL003", "Invalid [Tool] date",
        "Tool '{0}': '{1}' is not a valid ISO date (yyyy-MM-dd)",
        DiagnosticCategory, DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MissingResource = new(
        "GCTOOL004", "Missing tool resource",
        "Tool '{0}': resource '{1}' not found in culture '{2}' (Strings/{2}/Resources.resw)",
        DiagnosticCategory, DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UpdatedBeforeIntroduced = new(
        "GCTOOL010", "Tool dates inconsistent",
        "Tool '{0}': Updated ({1}) precedes Introduced ({2}); treated as introduced-only",
        DiagnosticCategory, DiagnosticSeverity.Info, isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Tools are discovered from referenced-assembly metadata, so the transform is keyed on the
        // CompilationProvider (coarser incrementality, fine at this scale — R13).
        var discovery = context.CompilationProvider.Select(static (compilation, ct) => Discover(compilation, ct));

        var views = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax { BaseList: not null },
                transform: static (ctx, ct) => TryGetView(ctx, ct))
            .Where(static v => v is not null)
            .Select(static (v, _) => v!.Value)
            .Collect();

        var resources = context.AdditionalTextsProvider
            .Where(static a => a.Path.Replace('\\', '/').EndsWith("Resources.resw", StringComparison.OrdinalIgnoreCase))
            .Select(static (a, ct) => ReadResource(a, ct))
            .Collect();

        var rootNamespace = context.AnalyzerConfigOptionsProvider.Select(static (provider, _) =>
        {
            provider.GlobalOptions.TryGetValue("build_property.RootNamespace", out var ns);
            return string.IsNullOrEmpty(ns) ? "GcToolkit" : ns!;
        });

        var combined = discovery.Combine(views).Combine(resources).Combine(rootNamespace);

        context.RegisterSourceOutput(combined, static (spc, data) =>
        {
            var (((result, viewList), resourceList), rootNs) = data;
            Execute(spc, result, viewList, resourceList, rootNs);
        });
    }

    // ---- Discovery (metadata) -------------------------------------------------------------

    private static DiscoveryResult Discover(Compilation compilation, System.Threading.CancellationToken ct)
    {
        var toolAttribute = compilation.GetTypeByMetadataName(ToolAttributeMetadataName);
        var categoryEnum = compilation.GetTypeByMetadataName(ToolCategoryMetadataName);
        if (toolAttribute is null || categoryEnum is null)
        {
            return DiscoveryResult.Empty;
        }

        var toolBase = compilation.GetTypeByMetadataName(ToolViewModelBaseMetadataName);
        var hasToolHostView = compilation.GetTypeByMetadataName("GcToolkit.Views.ToolHostView") is not null;

        var categoryNames = categoryEnum.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(f => f.HasConstantValue && f.ConstantValue is not null)
            .ToDictionary(f => Convert.ToInt32(f.ConstantValue, CultureInfo.InvariantCulture), f => f.Name);

        var tools = new List<ToolModel>();
        var diagnostics = new List<DiagnosticInfo>();

        foreach (var type in EnumerateCandidateTypes(compilation, toolAttribute.ContainingAssembly))
        {
            ct.ThrowIfCancellationRequested();

            var attribute = type.GetAttributes().FirstOrDefault(
                a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, toolAttribute));
            if (attribute is null)
            {
                continue;
            }

            var typeName = type.ToDisplayString();
            var id = attribute.ConstructorArguments.Length > 0 ? attribute.ConstructorArguments[0].Value as string : null;

            if (string.IsNullOrEmpty(id) || !IsResourceSafePascalCase(id!))
            {
                diagnostics.Add(new DiagnosticInfo(MissingRequiredValue, typeName,
                    "'Id' is required and must be a resource-safe PascalCase identifier."));
                continue;
            }

            // Target must derive ToolViewModelBase.
            if (toolBase is not null && !DerivesFrom(type, toolBase))
            {
                diagnostics.Add(new DiagnosticInfo(InvalidTarget, typeName));
                continue;
            }

            var categoryValue = attribute.ConstructorArguments.Length > 1 ? attribute.ConstructorArguments[1].Value : null;
            if (categoryValue is null || !categoryNames.TryGetValue(Convert.ToInt32(categoryValue, CultureInfo.InvariantCulture), out var categoryName))
            {
                diagnostics.Add(new DiagnosticInfo(MissingRequiredValue, id!, "'Category' is required."));
                continue;
            }

            var categoryOrder = Convert.ToInt32(categoryValue, CultureInfo.InvariantCulture);

            var introduced = GetNamedString(attribute, "Introduced");
            var updated = GetNamedString(attribute, "Updated");

            if (!TryParseIsoDate(introduced, out var iy, out var im, out var idd))
            {
                diagnostics.Add(new DiagnosticInfo(InvalidDate, id!, introduced ?? "<missing>"));
                continue;
            }

            if (!TryParseIsoDate(updated, out var uy, out var um, out var udd))
            {
                diagnostics.Add(new DiagnosticInfo(InvalidDate, id!, updated ?? "<missing>"));
                continue;
            }

            if (CompareDate(uy, um, udd, iy, im, idd) < 0)
            {
                diagnostics.Add(new DiagnosticInfo(UpdatedBeforeIntroduced, id!, updated!, introduced!));
            }

            var keywords = GetNamedStringArray(attribute, "Keywords");

            tools.Add(new ToolModel(
                id!,
                categoryName,
                categoryOrder,
                keywords,
                iy, im, idd,
                uy, um, udd,
                type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
        }

        // Deterministic order: category order, then tool id ordinal (matches CatalogService, FR-023).
        tools.Sort((a, b) =>
        {
            var byCategory = a.CategoryOrder.CompareTo(b.CategoryOrder);
            return byCategory != 0 ? byCategory : string.CompareOrdinal(a.Id, b.Id);
        });

        return new DiscoveryResult(tools, diagnostics, hasToolHostView);
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateCandidateTypes(Compilation compilation, IAssemblySymbol coreAssembly)
    {
        // The app head's own source (in case a tool VM ever lives there).
        foreach (var type in GetAllTypes(compilation.Assembly.GlobalNamespace))
        {
            yield return type;
        }

        // Core plus any referenced assembly that references Core (forward-compat, FR-017).
        foreach (var reference in compilation.References)
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assembly)
            {
                continue;
            }

            var isCore = SymbolEqualityComparer.Default.Equals(assembly, coreAssembly);
            if (!isCore && !ReferencesAssembly(assembly, coreAssembly.Name))
            {
                continue;
            }

            foreach (var type in GetAllTypes(assembly.GlobalNamespace))
            {
                yield return type;
            }
        }
    }

    private static bool ReferencesAssembly(IAssemblySymbol assembly, string name)
    {
        foreach (var module in assembly.Modules)
        {
            foreach (var referenced in module.ReferencedAssemblies)
            {
                if (string.Equals(referenced.Name, name, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static IEnumerable<INamedTypeSymbol> GetAllTypes(INamespaceSymbol root)
    {
        var stack = new Stack<INamespaceOrTypeSymbol>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            foreach (var member in current.GetMembers())
            {
                if (member is INamespaceSymbol ns)
                {
                    stack.Push(ns);
                }
                else if (member is INamedTypeSymbol type)
                {
                    yield return type;
                    foreach (var nested in type.GetTypeMembers())
                    {
                        stack.Push(nested);
                    }
                }
            }
        }
    }

    private static bool DerivesFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
            {
                return true;
            }
        }

        return false;
    }

    // ---- Discovery (views, syntax) --------------------------------------------------------

    private static ViewModel? TryGetView(GeneratorSyntaxContext context, System.Threading.CancellationToken ct)
    {
        if (context.Node is not ClassDeclarationSyntax declaration)
        {
            return null;
        }

        if (context.SemanticModel.GetDeclaredSymbol(declaration, ct) is not INamedTypeSymbol symbol)
        {
            return null;
        }

        var viewBase = context.SemanticModel.Compilation.GetTypeByMetadataName(ViewBaseMetadataName);
        if (viewBase is null)
        {
            return null;
        }

        INamedTypeSymbol? viewModel = null;
        for (var current = symbol.BaseType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, viewBase))
            {
                viewModel = current.TypeArguments.Length == 1 ? current.TypeArguments[0] as INamedTypeSymbol : null;
                break;
            }
        }

        if (viewModel is null)
        {
            return null;
        }

        return new ViewModel(
            symbol.ToDisplayString(),
            symbol.BaseType?.ToDisplayString(),
            symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            viewModel.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
    }

    // ---- Resources ------------------------------------------------------------------------

    private static ResourceFile ReadResource(AdditionalText text, System.Threading.CancellationToken ct)
    {
        var culture = CultureFromPath(text.Path);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var content = text.GetText(ct)?.ToString();
        if (!string.IsNullOrEmpty(content))
        {
            try
            {
                var document = XDocument.Parse(content);
                foreach (var data in document.Descendants("data"))
                {
                    var name = data.Attribute("name")?.Value;
                    if (!string.IsNullOrEmpty(name))
                    {
                        keys.Add(name!);
                    }
                }
            }
            catch (System.Xml.XmlException)
            {
                // A malformed resw is the localization toolchain's concern, not ours.
            }
        }

        return new ResourceFile(text.Path, culture, keys);
    }

    private static string CultureFromPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        var parts = normalized.Split('/');
        return parts.Length >= 2 ? parts[parts.Length - 2] : string.Empty;
    }

    // ---- Emit -----------------------------------------------------------------------------

    private static void Execute(
        SourceProductionContext context,
        DiscoveryResult result,
        IReadOnlyList<ViewModel> views,
        IReadOnlyList<ResourceFile> resources,
        string rootNamespace)
    {
        foreach (var diagnostic in result.Diagnostics)
        {
            context.ReportDiagnostic(diagnostic.ToDiagnostic());
        }

        // GCTOOL004: every tool's <Id>_Name and <Id>_Tooltip must exist in every culture.
        foreach (var tool in result.Tools)
        {
            foreach (var resource in resources)
            {
                foreach (var suffix in new[] { "_Name", "_Tooltip" })
                {
                    var key = tool.Id + suffix;
                    if (!resource.Keys.Contains(key))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            MissingResource, ResourceLocation(resource.Path), tool.Id, key, resource.Culture));
                    }
                }
            }
        }

        if (result.Tools.Count == 0)
        {
            return;
        }

        var leafViews = ResolveLeafViews(views);

        // A tool VM that has a dedicated ViewBase<TVm> view is a real tool: navigation routes to that
        // view (not the shared ToolHostView) and its descriptor is not a placeholder. Tools without a
        // dedicated view keep the feature-002 behaviour (shared host + placeholder notice, R3).
        var viewBackedToolVms = new HashSet<string>(
            leafViews.Select(v => v.ViewModelFullName), StringComparer.Ordinal);

        context.AddSource("GeneratedToolCatalog.g.cs", EmitCatalog(result, viewBackedToolVms, rootNamespace));
        context.AddSource("ToolDiscoveryServiceCollectionExtensions.g.cs", EmitServiceCollection(result, rootNamespace));
        context.AddSource("ViewRegistrations.g.cs", EmitViewRegistrations(result, leafViews, viewBackedToolVms, rootNamespace));
    }

    private static IReadOnlyList<ViewModel> ResolveLeafViews(IReadOnlyList<ViewModel> views)
    {
        // A view is a leaf (the concrete navigable type) when no other candidate derives from it.
        var baseNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var view in views)
        {
            if (view.BaseName is not null)
            {
                baseNames.Add(view.BaseName);
            }
        }

        var leaves = new List<ViewModel>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var view in views)
        {
            if (!baseNames.Contains(view.SelfName) && seen.Add(view.ViewModelFullName))
            {
                leaves.Add(view);
            }
        }

        leaves.Sort((a, b) => string.CompareOrdinal(a.ViewFullName, b.ViewFullName));
        return leaves;
    }

    private static SourceText EmitCatalog(DiscoveryResult result, HashSet<string> viewBackedToolVms, string rootNamespace)
    {
        var sb = new StringBuilder();
        sb.AppendAutoGeneratedHeader();
        sb.AppendLine($"namespace {rootNamespace};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>Generated, discovered tool/category data + the navigation tree.</summary>");
        sb.AppendLine("public static class GeneratedToolCatalog");
        sb.AppendLine("{");

        sb.AppendLine("    public static global::System.Collections.Generic.IReadOnlyList<global::GcToolkit.Core.Catalog.Category> Categories { get; } = new global::GcToolkit.Core.Catalog.Category[]");
        sb.AppendLine("    {");
        foreach (var category in DistinctCategories(result.Tools))
        {
            sb.AppendLine($"        new global::GcToolkit.Core.Catalog.Category(\"{category.Name}\", \"Category_{category.Name}\", {category.Order}, \"{category.Name}\", GroupIdOf(global::GcToolkit.Core.Discovery.ToolCategory.{category.Name})),");
        }
        sb.AppendLine("    };");
        sb.AppendLine();

        sb.AppendLine("    public static global::System.Collections.Generic.IReadOnlyList<global::GcToolkit.Core.Catalog.ToolDescriptor> Tools { get; } = new global::GcToolkit.Core.Catalog.ToolDescriptor[]");
        sb.AppendLine("    {");
        foreach (var tool in result.Tools)
        {
            sb.AppendLine("        new global::GcToolkit.Core.Catalog.ToolDescriptor(");
            sb.AppendLine($"            \"{tool.Id}\",");
            sb.AppendLine($"            \"{tool.Id}_Name\",");
            sb.AppendLine($"            \"{tool.CategoryName}\",");
            sb.AppendLine($"            new string[] {{ {string.Join(", ", tool.Keywords.Select(EscapeLiteral))} }},");
            sb.AppendLine($"            \"{tool.Id}\",");
            sb.AppendLine($"            typeof({tool.ViewModelFullName}),");
            // A tool with a dedicated ViewBase<TVm> view is real; one without renders in the shared
            // host as a placeholder (R3).
            sb.AppendLine($"            {(viewBackedToolVms.Contains(tool.ViewModelFullName) ? "false" : "true")},");
            sb.AppendLine($"            \"{tool.Id}_Tooltip\",");
            sb.AppendLine($"            GroupIdOf(global::GcToolkit.Core.Discovery.ToolCategory.{tool.CategoryName}),");
            sb.AppendLine($"            new global::System.DateOnly({tool.IntroducedYear}, {tool.IntroducedMonth}, {tool.IntroducedDay}),");
            sb.AppendLine($"            new global::System.DateOnly({tool.UpdatedYear}, {tool.UpdatedMonth}, {tool.UpdatedDay})),");
        }
        sb.AppendLine("    };");
        sb.AppendLine();

        sb.AppendLine("    public static global::System.Collections.Generic.IReadOnlyList<global::System.Type> ToolViewModelTypes { get; } = new global::System.Type[]");
        sb.AppendLine("    {");
        foreach (var tool in result.Tools)
        {
            sb.AppendLine($"        typeof({tool.ViewModelFullName}),");
        }
        sb.AppendLine("    };");
        sb.AppendLine();

        sb.AppendLine("    public static global::GcToolkit.Core.Navigation.NavigationTree NavigationTree { get; } =");
        sb.AppendLine("        global::GcToolkit.Core.Navigation.NavigationTreeBuilder.Build(Categories, Tools);");
        sb.AppendLine();

        sb.AppendLine("    private static string? GroupIdOf(global::GcToolkit.Core.Discovery.ToolCategory category)");
        sb.AppendLine("        => global::GcToolkit.Core.Discovery.ToolGrouping.CategoryGroups.TryGetValue(category, out var group) ? group.ToString() : null;");
        sb.AppendLine("}");

        return SourceText.From(sb.ToString(), Encoding.UTF8);
    }

    private static SourceText EmitServiceCollection(DiscoveryResult result, string rootNamespace)
    {
        var sb = new StringBuilder();
        sb.AppendAutoGeneratedHeader();
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine();
        sb.AppendLine($"namespace {rootNamespace};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>Registers the generated catalog contributor and each discovered tool ViewModel.</summary>");
        sb.AppendLine("public static class ToolDiscoveryServiceCollectionExtensions");
        sb.AppendLine("{");
        sb.AppendLine("    public static IServiceCollection AddDiscoveredTools(this IServiceCollection services)");
        sb.AppendLine("    {");
        sb.AppendLine("        services.AddSingleton<GeneratedToolContributor>();");
        sb.AppendLine("        services.AddSingleton<global::GcToolkit.Core.Catalog.IToolContributor>(sp => sp.GetRequiredService<GeneratedToolContributor>());");
        sb.AppendLine("        services.AddSingleton<global::GcToolkit.Core.Catalog.ICategoryContributor>(sp => sp.GetRequiredService<GeneratedToolContributor>());");
        foreach (var tool in result.Tools)
        {
            sb.AppendLine($"        services.AddTransient<{tool.ViewModelFullName}>();");
        }
        sb.AppendLine("        return services;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("/// <summary>Feeds the discovered tools/categories into the unchanged CatalogService.</summary>");
        sb.AppendLine("internal sealed class GeneratedToolContributor : global::GcToolkit.Core.Catalog.IToolContributor, global::GcToolkit.Core.Catalog.ICategoryContributor");
        sb.AppendLine("{");
        sb.AppendLine("    public global::System.Collections.Generic.IEnumerable<global::GcToolkit.Core.Catalog.ToolDescriptor> GetTools() => GeneratedToolCatalog.Tools;");
        sb.AppendLine("    public global::System.Collections.Generic.IEnumerable<global::GcToolkit.Core.Catalog.Category> GetCategories() => GeneratedToolCatalog.Categories;");
        sb.AppendLine("}");

        return SourceText.From(sb.ToString(), Encoding.UTF8);
    }

    private static SourceText EmitViewRegistrations(DiscoveryResult result, IReadOnlyList<ViewModel> leafViews, HashSet<string> viewBackedToolVms, string rootNamespace)
    {
        var sb = new StringBuilder();
        sb.AppendAutoGeneratedHeader();
        sb.AppendLine($"namespace {rootNamespace};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>Registers every discovered view↔ViewModel pair, plus each tool ViewModel without a dedicated view → the shared ToolHostView.</summary>");
        sb.AppendLine("public static class ViewRegistrations");
        sb.AppendLine("{");
        sb.AppendLine("    public static void RegisterDiscoveredViews(this global::GcToolkit.Services.Navigation.NavigationService nav)");
        sb.AppendLine("    {");
        foreach (var view in leafViews)
        {
            sb.AppendLine($"        nav.RegisterView(typeof({view.ViewFullName}), typeof({view.ViewModelFullName}));");
        }

        if (result.HasToolHostView)
        {
            sb.AppendLine();
            sb.AppendLine("        // Tool ViewModels without a dedicated view fall back to the shared tool host (R10).");
            foreach (var tool in result.Tools)
            {
                if (viewBackedToolVms.Contains(tool.ViewModelFullName))
                {
                    continue; // a dedicated ViewBase<TVm> view already handles this tool
                }

                sb.AppendLine($"        nav.RegisterView(typeof({ToolHostViewName}), typeof({tool.ViewModelFullName}));");
            }
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return SourceText.From(sb.ToString(), Encoding.UTF8);
    }

    private static IEnumerable<(string Name, int Order)> DistinctCategories(IReadOnlyList<ToolModel> tools)
        => tools
            .GroupBy(t => t.CategoryName, StringComparer.Ordinal)
            .Select(g => (Name: g.Key, Order: g.First().CategoryOrder))
            .OrderBy(c => c.Order);

    // ---- Helpers --------------------------------------------------------------------------

    private static string? GetNamedString(AttributeData attribute, string name)
    {
        foreach (var pair in attribute.NamedArguments)
        {
            if (string.Equals(pair.Key, name, StringComparison.Ordinal))
            {
                return pair.Value.Value as string;
            }
        }

        return null;
    }

    private static string[] GetNamedStringArray(AttributeData attribute, string name)
    {
        foreach (var pair in attribute.NamedArguments)
        {
            if (string.Equals(pair.Key, name, StringComparison.Ordinal) && !pair.Value.IsNull)
            {
                return pair.Value.Values
                    .Select(v => v.Value as string)
                    .Where(v => v is not null)
                    .Select(v => v!)
                    .ToArray();
            }
        }

        return Array.Empty<string>();
    }

    private static bool TryParseIsoDate(string? value, out int year, out int month, out int day)
    {
        year = month = day = 0;
        if (string.IsNullOrEmpty(value)
            || !DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return false;
        }

        year = parsed.Year;
        month = parsed.Month;
        day = parsed.Day;
        return true;
    }

    private static int CompareDate(int y1, int m1, int d1, int y2, int m2, int d2)
    {
        if (y1 != y2)
        {
            return y1.CompareTo(y2);
        }

        if (m1 != m2)
        {
            return m1.CompareTo(m2);
        }

        return d1.CompareTo(d2);
    }

    private static bool IsResourceSafePascalCase(string id)
    {
        if (id.Length == 0 || !char.IsUpper(id[0]))
        {
            return false;
        }

        foreach (var c in id)
        {
            if (!char.IsLetterOrDigit(c))
            {
                return false;
            }
        }

        return true;
    }

    private static string EscapeLiteral(string value)
    {
        var sb = new StringBuilder(value.Length + 2);
        sb.Append('"');
        foreach (var c in value)
        {
            switch (c)
            {
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '"':
                    sb.Append("\\\"");
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }

        sb.Append('"');
        return sb.ToString();
    }

    private static Location ResourceLocation(string path)
        => Location.Create(path, new TextSpan(0, 0), new LinePositionSpan(new LinePosition(0, 0), new LinePosition(0, 0)));

    // ---- Models ---------------------------------------------------------------------------

    private sealed class DiscoveryResult
    {
        public static readonly DiscoveryResult Empty = new(new List<ToolModel>(), new List<DiagnosticInfo>(), false);

        public DiscoveryResult(IReadOnlyList<ToolModel> tools, IReadOnlyList<DiagnosticInfo> diagnostics, bool hasToolHostView)
        {
            Tools = tools;
            Diagnostics = diagnostics;
            HasToolHostView = hasToolHostView;
        }

        public IReadOnlyList<ToolModel> Tools { get; }

        public IReadOnlyList<DiagnosticInfo> Diagnostics { get; }

        public bool HasToolHostView { get; }
    }

    private sealed class ToolModel
    {
        public ToolModel(
            string id, string categoryName, int categoryOrder, string[] keywords,
            int introducedYear, int introducedMonth, int introducedDay,
            int updatedYear, int updatedMonth, int updatedDay,
            string viewModelFullName)
        {
            Id = id;
            CategoryName = categoryName;
            CategoryOrder = categoryOrder;
            Keywords = keywords;
            IntroducedYear = introducedYear;
            IntroducedMonth = introducedMonth;
            IntroducedDay = introducedDay;
            UpdatedYear = updatedYear;
            UpdatedMonth = updatedMonth;
            UpdatedDay = updatedDay;
            ViewModelFullName = viewModelFullName;
        }

        public string Id { get; }
        public string CategoryName { get; }
        public int CategoryOrder { get; }
        public string[] Keywords { get; }
        public int IntroducedYear { get; }
        public int IntroducedMonth { get; }
        public int IntroducedDay { get; }
        public int UpdatedYear { get; }
        public int UpdatedMonth { get; }
        public int UpdatedDay { get; }
        public string ViewModelFullName { get; }
    }

    private readonly struct ViewModel
    {
        public ViewModel(string selfName, string? baseName, string viewFullName, string viewModelFullName)
        {
            SelfName = selfName;
            BaseName = baseName;
            ViewFullName = viewFullName;
            ViewModelFullName = viewModelFullName;
        }

        public string SelfName { get; }
        public string? BaseName { get; }
        public string ViewFullName { get; }
        public string ViewModelFullName { get; }
    }

    private sealed class ResourceFile
    {
        public ResourceFile(string path, string culture, HashSet<string> keys)
        {
            Path = path;
            Culture = culture;
            Keys = keys;
        }

        public string Path { get; }
        public string Culture { get; }
        public HashSet<string> Keys { get; }
    }

    private sealed class DiagnosticInfo
    {
        private readonly DiagnosticDescriptor _descriptor;
        private readonly object[] _args;

        public DiagnosticInfo(DiagnosticDescriptor descriptor, params object[] args)
        {
            _descriptor = descriptor;
            _args = args;
        }

        public Diagnostic ToDiagnostic() => Diagnostic.Create(_descriptor, Location.None, _args);
    }
}
