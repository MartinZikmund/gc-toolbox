using Microsoft.CodeAnalysis;

namespace GcToolkit.SourceGenerators.Tests;

[TestClass]
public class ToolDiscoveryGeneratorTests
{
    private const string TwoTools = """
        using GcToolkit.Core.Discovery;
        using GcToolkit.Core.ViewModels;

        namespace GcToolkit.Core.ViewModels.Tools
        {
            [Tool("SampleConversion", ToolCategory.Coordinates, Introduced = "2026-05-20", Updated = "2026-05-20", Keywords = new[] { "wgs84", "gps" })]
            public sealed class SampleConversionViewModel : ToolViewModelBase
            {
                public SampleConversionViewModel() : base("SampleConversion") { }
            }

            [Tool("SampleCipher", ToolCategory.Ciphers, Introduced = "2026-02-10", Updated = "2026-02-10")]
            public sealed class SampleCipherViewModel : ToolViewModelBase
            {
                public SampleCipherViewModel() : base("SampleCipher") { }
            }
        }
        """;

    private static IEnumerable<(string, string)> ValidResources() =>
    [
        ("Strings/en/Resources.resw", ReswBuilder.Build(
            "SampleConversion_Name", "SampleConversion_Tooltip", "SampleCipher_Name", "SampleCipher_Tooltip")),
        ("Strings/cs/Resources.resw", ReswBuilder.Build(
            "SampleConversion_Name", "SampleConversion_Tooltip", "SampleCipher_Name", "SampleCipher_Tooltip")),
    ];

    [TestMethod]
    public void Generator_DiscoversAttributedTools_EmitsCatalogContributorAndRegistrations()
    {
        var result = GeneratorTestHarness.Run(TwoTools, additionalFiles: ValidResources());

        Assert.IsFalse(
            result.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error),
            "Expected no error diagnostics. Got: " + string.Join(", ", result.Diagnostics));

        var catalog = result.GeneratedText("GeneratedToolCatalog");
        StringAssert.Contains(catalog, "\"SampleConversion\"");
        StringAssert.Contains(catalog, "\"SampleConversion_Name\"");
        StringAssert.Contains(catalog, "\"SampleConversion_Tooltip\"");
        StringAssert.Contains(catalog, "typeof(global::GcToolkit.Core.ViewModels.Tools.SampleConversionViewModel)");
        StringAssert.Contains(catalog, "new global::System.DateOnly(2026, 5, 20)");
        StringAssert.Contains(catalog, "GroupIdOf(global::GcToolkit.Core.Discovery.ToolCategory.Coordinates)");
        StringAssert.Contains(catalog, "NavigationTreeBuilder.Build(Categories, Tools)");

        var services = result.GeneratedText("ToolDiscoveryServiceCollectionExtensions");
        StringAssert.Contains(services, "AddDiscoveredTools");
        StringAssert.Contains(services, "AddTransient<global::GcToolkit.Core.ViewModels.Tools.SampleConversionViewModel>");
        StringAssert.Contains(services, "GeneratedToolContributor");

        var views = result.GeneratedText("ViewRegistrations");
        StringAssert.Contains(views, "RegisterDiscoveredViews");
        // Each discovered tool VM is registered against the shared host.
        StringAssert.Contains(views, "typeof(global::GcToolkit.Views.ToolHostView), typeof(global::GcToolkit.Core.ViewModels.Tools.SampleConversionViewModel)");
        // Page views are discovered by syntax and registered to their own VM.
        StringAssert.Contains(views, "typeof(global::GcToolkit.Views.HomeView), typeof(global::GcToolkit.Core.ViewModels.HomeViewModel)");
    }

    [TestMethod]
    public void Generator_ToolWithDedicatedView_RoutesToViewAndMarksNotPlaceholder()
    {
        // The app head supplies a real view for one of the two tools, using the same base+sealed pair
        // shape the app actually authors (e.g. CatalogViewBase + CatalogView).
        const string appHead = """
            namespace GcToolkit.Views
            {
                public abstract class ViewBase<TViewModel> { }
                public sealed class ToolHostView { }
                public class SampleConversionViewBase : ViewBase<GcToolkit.Core.ViewModels.Tools.SampleConversionViewModel> { }
                public sealed class SampleConversionView : SampleConversionViewBase { }
            }
            """;

        var result = GeneratorTestHarness.Run(TwoTools, appHead, ValidResources());

        Assert.IsFalse(
            result.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error),
            "Expected no error diagnostics. Got: " + string.Join(", ", result.Diagnostics));

        var views = result.GeneratedText("ViewRegistrations");
        // The view-backed tool routes to its dedicated view...
        StringAssert.Contains(views, "typeof(global::GcToolkit.Views.SampleConversionView), typeof(global::GcToolkit.Core.ViewModels.Tools.SampleConversionViewModel)");
        // ...and is NOT also registered against the shared host (which would overwrite the dedicated view).
        StringAssert.DoesNotMatch(
            views,
            new System.Text.RegularExpressions.Regex(@"ToolHostView\), typeof\(global::GcToolkit\.Core\.ViewModels\.Tools\.SampleConversionViewModel\)"));
        // The tool without a dedicated view still falls back to the shared host.
        StringAssert.Contains(views, "typeof(global::GcToolkit.Views.ToolHostView), typeof(global::GcToolkit.Core.ViewModels.Tools.SampleCipherViewModel)");

        // A view-backed tool emits a non-placeholder descriptor.
        var catalog = result.GeneratedText("GeneratedToolCatalog");
        StringAssert.Contains(catalog, "false,");
    }

    [TestMethod]
    public void Generator_OrdersToolsByCategoryThenId()
    {
        var result = GeneratorTestHarness.Run(TwoTools, additionalFiles: ValidResources());
        var catalog = result.GeneratedText("GeneratedToolCatalog");

        // Coordinates (order 0) before Ciphers (order 1).
        var conversionIndex = catalog.IndexOf("\"SampleConversion\"", StringComparison.Ordinal);
        var cipherIndex = catalog.IndexOf("\"SampleCipher\"", StringComparison.Ordinal);
        Assert.IsTrue(conversionIndex >= 0 && cipherIndex >= 0);
        Assert.IsTrue(conversionIndex < cipherIndex, "Tools must be emitted in category-then-id order.");
    }

    [TestMethod]
    public void Generator_IsDeterministic_ForIdenticalInputs()
    {
        var first = GeneratorTestHarness.Run(TwoTools, additionalFiles: ValidResources()).GeneratedText();
        var second = GeneratorTestHarness.Run(TwoTools, additionalFiles: ValidResources()).GeneratedText();

        Assert.AreEqual(first, second, "Generated output must be byte-identical for identical inputs (no timestamp).");
    }

    [TestMethod]
    public void Generator_NonToolViewModelBaseTarget_ReportsGCTOOL002()
    {
        const string badTarget = """
            using GcToolkit.Core.Discovery;

            namespace GcToolkit.Core.ViewModels.Tools
            {
                [Tool("NotAViewModel", ToolCategory.Numbers, Introduced = "2026-01-01", Updated = "2026-01-01")]
                public sealed class NotAViewModel { }
            }
            """;

        var result = GeneratorTestHarness.Run(badTarget, additionalFiles: ValidResources());

        Assert.IsTrue(result.Diagnostics.Any(d => d.Id == "GCTOOL002" && d.Severity == DiagnosticSeverity.Error));
    }

    [TestMethod]
    public void Generator_InvalidDate_ReportsGCTOOL003()
    {
        const string badDate = """
            using GcToolkit.Core.Discovery;
            using GcToolkit.Core.ViewModels;

            namespace GcToolkit.Core.ViewModels.Tools
            {
                [Tool("BadDate", ToolCategory.Numbers, Introduced = "not-a-date", Updated = "2026-01-01")]
                public sealed class BadDateViewModel : ToolViewModelBase
                {
                    public BadDateViewModel() : base("BadDate") { }
                }
            }
            """;

        var result = GeneratorTestHarness.Run(badDate, additionalFiles: ValidResources());

        Assert.IsTrue(result.Diagnostics.Any(d => d.Id == "GCTOOL003" && d.Severity == DiagnosticSeverity.Error));
    }

    [TestMethod]
    public void Generator_MissingTooltipResource_ReportsGCTOOL004()
    {
        // en is missing SampleCipher_Tooltip.
        var resources = new (string, string)[]
        {
            ("Strings/en/Resources.resw", ReswBuilder.Build(
                "SampleConversion_Name", "SampleConversion_Tooltip", "SampleCipher_Name")),
            ("Strings/cs/Resources.resw", ReswBuilder.Build(
                "SampleConversion_Name", "SampleConversion_Tooltip", "SampleCipher_Name", "SampleCipher_Tooltip")),
        };

        var result = GeneratorTestHarness.Run(TwoTools, additionalFiles: resources);

        Assert.IsTrue(result.Diagnostics.Any(d =>
            d.Id == "GCTOOL004" && d.Severity == DiagnosticSeverity.Error && d.GetMessage().Contains("SampleCipher_Tooltip")));
    }

    [TestMethod]
    public void Generator_UpdatedBeforeIntroduced_ReportsGCTOOL010Info()
    {
        const string inconsistent = """
            using GcToolkit.Core.Discovery;
            using GcToolkit.Core.ViewModels;

            namespace GcToolkit.Core.ViewModels.Tools
            {
                [Tool("Backwards", ToolCategory.Numbers, Introduced = "2026-05-01", Updated = "2026-01-01")]
                public sealed class BackwardsViewModel : ToolViewModelBase
                {
                    public BackwardsViewModel() : base("Backwards") { }
                }
            }
            """;

        var resources = new (string, string)[]
        {
            ("Strings/en/Resources.resw", ReswBuilder.Build("Backwards_Name", "Backwards_Tooltip")),
            ("Strings/cs/Resources.resw", ReswBuilder.Build("Backwards_Name", "Backwards_Tooltip")),
        };

        var result = GeneratorTestHarness.Run(inconsistent, additionalFiles: resources);

        Assert.IsTrue(result.Diagnostics.Any(d => d.Id == "GCTOOL010" && d.Severity == DiagnosticSeverity.Info));
        Assert.IsFalse(result.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error));
    }
}
