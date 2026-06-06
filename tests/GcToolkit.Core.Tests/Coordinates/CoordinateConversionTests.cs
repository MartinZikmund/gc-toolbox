using System.Linq;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Coordinates;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Core.Tests.Coordinates;

/// <summary>
/// Covers the conversion helper (<see cref="CoordinateConversions.ToAllFormats"/>) and the
/// <see cref="CoordinateConversionViewModel"/> wiring: parsing valid + invalid input, that the same
/// point renders in every format, that two notations of the same point agree, and that the detected
/// format is reported correctly. The heavy math lives in the foundation library — these tests just
/// cross-check the assembled multi-format result against it.
/// </summary>
[TestClass]
public class CoordinateConversionTests
{
    // Golden vector 49.205750, 16.576117 — the same point the library's own tests pin down.
    private static readonly GeoCoordinate Golden = new(49.205750, 16.576117);

    private const string GoldenDd = "N 49.205750° E 016.576117°";
    private const string GoldenDdm = "N 49° 12.345' E 016° 34.567'";
    private const string GoldenDms = "N 49° 12' 20.70\" E 016° 34' 34.02\"";
    private const string GoldenUtm = "33U 614803 5451524";
    private const string GoldenMgrs = "33U XQ 14803 51524";
    private const string GoldenUsng = "33U XQ 14803 51524"; // USNG == MGRS on WGS84

    // ---- Helper: ToAllFormats ----

    [TestMethod]
    public void ToAllFormats_ReturnsAllEightFormatsInDeclarationOrder()
    {
        var rows = CoordinateConversions.ToAllFormats(Golden);

        CollectionAssert.AreEqual(
            new[]
            {
                CoordinateFormat.DecimalDegrees,
                CoordinateFormat.DegreesDecimalMinutes,
                CoordinateFormat.DegreesMinutesSeconds,
                CoordinateFormat.Utm,
                CoordinateFormat.Mgrs,
                CoordinateFormat.Usng,
                CoordinateFormat.DutchRd,
                CoordinateFormat.BritishGrid,
            },
            rows.Select(r => r.Format).ToArray());
    }

    [TestMethod]
    public void ToAllFormats_Golden_ProducesEveryLibraryFormattedValue()
    {
        var rows = CoordinateConversions.ToAllFormats(Golden);

        Assert.AreEqual(GoldenDd, ValueOf(rows, CoordinateFormat.DecimalDegrees));
        Assert.AreEqual(GoldenDdm, ValueOf(rows, CoordinateFormat.DegreesDecimalMinutes));
        Assert.AreEqual(GoldenDms, ValueOf(rows, CoordinateFormat.DegreesMinutesSeconds));
        Assert.AreEqual(GoldenUtm, ValueOf(rows, CoordinateFormat.Utm));
        Assert.AreEqual(GoldenMgrs, ValueOf(rows, CoordinateFormat.Mgrs));
        Assert.AreEqual(GoldenUsng, ValueOf(rows, CoordinateFormat.Usng));
        // RD and British grid only make sense over their regions; just confirm they are present
        // and non-empty (their exact values are pinned in DutchRdTests / BritishGridTests).
        Assert.IsFalse(string.IsNullOrWhiteSpace(ValueOf(rows, CoordinateFormat.DutchRd)));
        Assert.IsFalse(string.IsNullOrWhiteSpace(ValueOf(rows, CoordinateFormat.BritishGrid)));
    }

    [TestMethod]
    public void ToAllFormats_EachValue_MatchesTheFormatterForThatFormat()
    {
        foreach (var row in CoordinateConversions.ToAllFormats(Golden))
        {
            Assert.AreEqual(CoordinateFormatter.Format(Golden, row.Format), row.Value, row.Format.ToString());
        }
    }

    // ---- ViewModel: live multi-format output ----

    [TestMethod]
    public void Vm_DecimalDegreesInput_PopulatesAllEightFormats()
    {
        var vm = CreateViewModel();

        vm.InputText = GoldenDd;

        Assert.IsTrue(vm.HasResult);
        Assert.IsFalse(vm.HasError);
        Assert.AreEqual(8, vm.Results.Count);
        Assert.AreEqual(GoldenDd, RowValue(vm, CoordinateFormat.DecimalDegrees));
        Assert.AreEqual(GoldenDdm, RowValue(vm, CoordinateFormat.DegreesDecimalMinutes));
        Assert.AreEqual(GoldenDms, RowValue(vm, CoordinateFormat.DegreesMinutesSeconds));
        Assert.AreEqual(GoldenUtm, RowValue(vm, CoordinateFormat.Utm));
        Assert.AreEqual(GoldenMgrs, RowValue(vm, CoordinateFormat.Mgrs));
        Assert.AreEqual(GoldenUsng, RowValue(vm, CoordinateFormat.Usng));
        Assert.IsFalse(string.IsNullOrWhiteSpace(RowValue(vm, CoordinateFormat.DutchRd)));
        Assert.IsFalse(string.IsNullOrWhiteSpace(RowValue(vm, CoordinateFormat.BritishGrid)));
    }

    [TestMethod]
    public void Vm_Results_ExposeAllEightFormatsInDeclarationOrder()
    {
        var vm = CreateViewModel();

        vm.InputText = GoldenDd;

        CollectionAssert.AreEqual(
            new[]
            {
                CoordinateFormat.DecimalDegrees,
                CoordinateFormat.DegreesDecimalMinutes,
                CoordinateFormat.DegreesMinutesSeconds,
                CoordinateFormat.Utm,
                CoordinateFormat.Mgrs,
                CoordinateFormat.Usng,
                CoordinateFormat.DutchRd,
                CoordinateFormat.BritishGrid,
            },
            vm.Results.Select(r => r.Format).ToArray());
    }

    [TestMethod]
    public void Vm_DegreesDecimalMinutesInput_YieldsSameOutputsAsDecimalDegrees()
    {
        var vm = CreateViewModel();

        // The same point entered in DDM must render identically across every format.
        vm.InputText = GoldenDdm;

        Assert.IsTrue(vm.HasResult);
        Assert.AreEqual(GoldenDd, RowValue(vm, CoordinateFormat.DecimalDegrees));
        Assert.AreEqual(GoldenDdm, RowValue(vm, CoordinateFormat.DegreesDecimalMinutes));
        Assert.AreEqual(GoldenDms, RowValue(vm, CoordinateFormat.DegreesMinutesSeconds));
        Assert.AreEqual(GoldenUtm, RowValue(vm, CoordinateFormat.Utm));
        Assert.AreEqual(GoldenMgrs, RowValue(vm, CoordinateFormat.Mgrs));
    }

    [TestMethod]
    public void Vm_UtmInput_RoundTripsToTheSamePointInEveryFormat()
    {
        var vm = CreateViewModel();

        vm.InputText = GoldenUtm;

        Assert.IsTrue(vm.HasResult);
        Assert.AreEqual(CoordinateFormat.Utm, vm.DetectedFormat);
        // UTM truncates to the metre, so DD comes back within a metre (~1e-5°) but the grids are exact.
        Assert.AreEqual(GoldenUtm, RowValue(vm, CoordinateFormat.Utm));
        Assert.AreEqual(GoldenMgrs, RowValue(vm, CoordinateFormat.Mgrs));
        StringAssert.StartsWith(RowValue(vm, CoordinateFormat.DecimalDegrees), "N 49.2057");
    }

    // ---- ViewModel: detected-format reporting ----

    [TestMethod]
    public void Vm_DetectedFormat_ReportsDecimalDegrees()
    {
        var vm = CreateViewModel();
        vm.InputText = GoldenDd;
        Assert.AreEqual(CoordinateFormat.DecimalDegrees, vm.DetectedFormat);
    }

    [TestMethod]
    public void Vm_DetectedFormat_ReportsDegreesDecimalMinutes()
    {
        var vm = CreateViewModel();
        vm.InputText = GoldenDdm;
        Assert.AreEqual(CoordinateFormat.DegreesDecimalMinutes, vm.DetectedFormat);
    }

    [TestMethod]
    public void Vm_DetectedFormat_ReportsDegreesMinutesSeconds()
    {
        var vm = CreateViewModel();
        vm.InputText = GoldenDms;
        Assert.AreEqual(CoordinateFormat.DegreesMinutesSeconds, vm.DetectedFormat);
    }

    [TestMethod]
    public void Vm_DetectedFormat_ReportsMgrs()
    {
        var vm = CreateViewModel();
        vm.InputText = GoldenMgrs;
        Assert.AreEqual(CoordinateFormat.Mgrs, vm.DetectedFormat);
    }

    [TestMethod]
    public void Vm_DetectedFormatLabelKey_FollowsTheDetectedFormat()
    {
        var vm = CreateViewModel();
        vm.InputText = GoldenUtm;
        Assert.AreEqual(CoordinateFormatResources.LabelKey(CoordinateFormat.Utm), vm.DetectedFormatLabelKey);
    }

    // ---- ViewModel: invalid + empty input ----

    [TestMethod]
    public void Vm_InvalidInput_SetsErrorAndClearsResults()
    {
        var vm = CreateViewModel();
        vm.InputText = GoldenDd;

        vm.InputText = "not a coordinate";

        Assert.IsTrue(vm.HasError);
        Assert.IsFalse(vm.HasResult);
        Assert.AreEqual(0, vm.Results.Count);
    }

    [TestMethod]
    public void Vm_EmptyInput_ClearsBothErrorAndResults()
    {
        var vm = CreateViewModel();
        vm.InputText = "garbage";
        Assert.IsTrue(vm.HasError);

        vm.InputText = "   ";

        Assert.IsFalse(vm.HasError);
        Assert.IsFalse(vm.HasResult);
        Assert.AreEqual(0, vm.Results.Count);
    }

    [TestMethod]
    public void Vm_Clear_ResetsInputAndState()
    {
        var vm = CreateViewModel();
        vm.InputText = GoldenDd;

        vm.ClearCommand.Execute(null);

        Assert.AreEqual(string.Empty, vm.InputText);
        Assert.IsFalse(vm.HasResult);
        Assert.IsFalse(vm.HasError);
    }

    // ---- ViewModel: copy / share ----

    [TestMethod]
    public void Vm_CopyAll_PutsEveryFormattedRowOnTheClipboard()
    {
        var clipboard = new FakeClipboardService();
        var vm = CreateViewModel(clipboard);
        vm.InputText = GoldenDd;

        vm.CopyAllCommand.Execute(null);

        StringAssert.Contains(clipboard.LastText, GoldenDd);
        StringAssert.Contains(clipboard.LastText, GoldenUtm);
        StringAssert.Contains(clipboard.LastText, GoldenMgrs);
    }

    [TestMethod]
    public void Vm_RowCopy_CopiesJustThatRowValue()
    {
        var clipboard = new FakeClipboardService();
        var vm = CreateViewModel(clipboard);
        vm.InputText = GoldenDd;

        Row(vm, CoordinateFormat.Mgrs).CopyCommand.Execute(null);

        Assert.AreEqual(GoldenMgrs, clipboard.LastText);
    }

    [TestMethod]
    public void Vm_CopyAndShare_DisabledUntilThereIsAResult()
    {
        var vm = CreateViewModel();

        Assert.IsFalse(vm.CopyAllCommand.CanExecute(null));
        Assert.IsFalse(vm.ShareCommand.CanExecute(null));

        vm.InputText = GoldenDd;

        Assert.IsTrue(vm.CopyAllCommand.CanExecute(null));
        Assert.IsTrue(vm.ShareCommand.CanExecute(null));
    }

    // ---- Helpers ----

    private static string ValueOf(IReadOnlyList<CoordinateFormatResult> rows, CoordinateFormat format)
        => rows.First(r => r.Format == format).Value;

    private static CoordinateFormatRow Row(CoordinateConversionViewModel vm, CoordinateFormat format)
        => vm.Results.First(r => r.Format == format);

    private static string RowValue(CoordinateConversionViewModel vm, CoordinateFormat format)
        => Row(vm, format).Value;

    private static CoordinateConversionViewModel CreateViewModel(FakeClipboardService? clipboard = null)
    {
        var catalog = new StubCatalogService("CoordinateConversion");
        return new CoordinateConversionViewModel(
            catalog,
            new FakeRecentsService(),
            new FakeFavoriteToolsService(),
            new FakeStringLocalizer(),
            clipboard ?? new FakeClipboardService(),
            new FakeShareService());
    }
}
