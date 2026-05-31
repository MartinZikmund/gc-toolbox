using System.Collections.Immutable;
using System.Reflection;
using GcToolkit.SourceGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace GcToolkit.SourceGenerators.Tests;

/// <summary>
/// Drives <see cref="ToolDiscoveryGenerator"/> the way it runs in production: the attributed
/// ViewModels are compiled into a referenced "GcToolkit.Core" assembly so discovery exercises the
/// metadata-scan path (R2), not source discovery.
/// </summary>
internal static class GeneratorTestHarness
{
    private static readonly ImmutableArray<MetadataReference> FrameworkReferences = LoadFrameworkReferences();

    /// <summary>The minimal GcToolkit.Core surface the generator reads from metadata.</summary>
    public const string CoreSource = """
        using System;
        using System.Collections.Generic;

        namespace GcToolkit.Core.Discovery
        {
            public enum ToolCategory { Coordinates, Ciphers, Numbers, Field }

            public enum ToolGroup { Conversion }

            public static class ToolGrouping
            {
                public static IReadOnlyDictionary<ToolCategory, ToolGroup> CategoryGroups { get; } =
                    new Dictionary<ToolCategory, ToolGroup> { [ToolCategory.Coordinates] = ToolGroup.Conversion };
            }

            [AttributeUsage(AttributeTargets.Class)]
            public sealed class ToolAttribute : Attribute
            {
                public ToolAttribute(string id, ToolCategory category) { Id = id; Category = category; }
                public string Id { get; }
                public ToolCategory Category { get; }
                public string Introduced { get; init; } = "";
                public string Updated { get; init; } = "";
                public string[] Keywords { get; init; } = Array.Empty<string>();
            }
        }

        namespace GcToolkit.Core.ViewModels
        {
            public abstract class ToolViewModelBase
            {
                protected ToolViewModelBase(string id) { }
            }
        }
        """;

    /// <summary>A minimal app-head surface (ViewBase, the shared host, and a page view) for view discovery.</summary>
    public const string AppHeadSource = """
        namespace GcToolkit.Views
        {
            public abstract class ViewBase<TViewModel> { }
            public sealed class ToolHostView { }
            public sealed class HomeView : ViewBase<GcToolkit.Core.ViewModels.HomeViewModel> { }
        }

        namespace GcToolkit.Core.ViewModels
        {
            public sealed class HomeViewModel { }
        }
        """;

    public static GeneratorDriverRunResult Run(
        string toolsSource,
        string? appHeadSource = null,
        IEnumerable<(string Path, string Content)>? additionalFiles = null)
    {
        var coreReference = CompileCore(toolsSource);

        var appCompilation = CSharpCompilation.Create(
            "GcToolkit",
            new[] { CSharpSyntaxTree.ParseText(appHeadSource ?? AppHeadSource) },
            FrameworkReferences.Add(coreReference),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var additionalTexts = (additionalFiles ?? Enumerable.Empty<(string, string)>())
            .Select(f => (AdditionalText)new InMemoryAdditionalText(f.Path, f.Content))
            .ToImmutableArray();

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: new[] { new ToolDiscoveryGenerator().AsSourceGenerator() },
            additionalTexts: additionalTexts);

        driver = driver.RunGenerators(appCompilation);
        return driver.GetRunResult();
    }

    private static MetadataReference CompileCore(string toolsSource)
    {
        var compilation = CSharpCompilation.Create(
            "GcToolkit.Core",
            new[] { CSharpSyntaxTree.ParseText(CoreSource), CSharpSyntaxTree.ParseText(toolsSource) },
            FrameworkReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success)
        {
            var errors = string.Join(Environment.NewLine, result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            throw new InvalidOperationException($"Fake Core failed to compile:{Environment.NewLine}{errors}");
        }

        return MetadataReference.CreateFromImage(stream.ToArray());
    }

    private static ImmutableArray<MetadataReference> LoadFrameworkReferences()
    {
        var trusted = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? string.Empty;
        return trusted
            .Split(Path.PathSeparator)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) && File.Exists(path))
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToImmutableArray();
    }

    /// <summary>Returns the concatenated text of every generated tree (for substring assertions).</summary>
    public static string GeneratedText(this GeneratorDriverRunResult result)
        => string.Join(Environment.NewLine, result.GeneratedTrees.Select(t => t.GetText().ToString()));

    public static string GeneratedText(this GeneratorDriverRunResult result, string fileNameContains)
        => result.GeneratedTrees
            .Where(t => t.FilePath.Contains(fileNameContains, StringComparison.OrdinalIgnoreCase))
            .Select(t => t.GetText().ToString())
            .FirstOrDefault() ?? string.Empty;

    private sealed class InMemoryAdditionalText : AdditionalText
    {
        private readonly SourceText _text;

        public InMemoryAdditionalText(string path, string content)
        {
            Path = path;
            _text = SourceText.From(content);
        }

        public override string Path { get; }

        public override SourceText GetText(System.Threading.CancellationToken cancellationToken = default) => _text;
    }
}

/// <summary>Standard .resw content with the supplied keys, for AdditionalFiles-based resource validation.</summary>
internal static class ReswBuilder
{
    public static string Build(params string[] keys)
    {
        var entries = string.Join(Environment.NewLine, keys.Select(k =>
            $"  <data name=\"{k}\" xml:space=\"preserve\"><value>{k}</value></data>"));
        return $"""
            <?xml version="1.0" encoding="utf-8"?>
            <root>
            {entries}
            </root>
            """;
    }
}
