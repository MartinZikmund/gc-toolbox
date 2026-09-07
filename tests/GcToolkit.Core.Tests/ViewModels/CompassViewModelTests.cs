using GcToolkit.Core.Services;
using GcToolkit.Core.Services.Devices;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Core.Tests.ViewModels;

[TestClass]
public class CompassViewModelTests
{
    private const double Tolerance = 1e-6;

    private readonly FakeCompassService _compass = new();
    private readonly FakeClipboardService _clipboard = new();
    private readonly RecordingDisplayRequestManager _display = new();

    private CompassViewModel CreateViewModel(FakeStringLocalizer? localizer = null) => new(
        new StubCatalogService("Compass"),
        new FakeRecentsService(),
        new FakeFavoriteToolsService(),
        localizer ?? new FakeStringLocalizer(),
        _compass,
        _display,
        _clipboard);

    // ---- Sensor states ---------------------------------------------------------

    [TestMethod]
    public void ViewUnloaded_AfterARefusedStart_StillDetachesTheSensorHandlers()
    {
        _compass.IsSupported = false;
        var vm = CreateViewModel();

        vm.ViewLoaded();
        vm.ViewUnloaded();

        // ViewLoaded subscribes before Start, so a refused start still leaves handlers attached.
        Assert.AreEqual(0, _compass.HeadingSubscriberCount);
        Assert.AreEqual(0, _compass.StatusSubscriberCount);
    }

    [TestMethod]
    public void ViewLoaded_AfterARefusedStartAndUnload_DoesNotSubscribeTwice()
    {
        _compass.IsSupported = false;
        var vm = CreateViewModel();
        vm.ViewLoaded();
        vm.ViewUnloaded();

        _compass.IsSupported = true;
        vm.ViewLoaded();

        Assert.AreEqual(1, _compass.HeadingSubscriberCount);
        Assert.AreEqual(1, _compass.StatusSubscriberCount);
    }

    [TestMethod]
    public void ViewLoaded_SensorPresent_StartsAtTenHertzAndWarmsUp()
    {
        var vm = CreateViewModel();

        vm.ViewLoaded();

        Assert.AreEqual(1, _compass.StartCallCount);
        Assert.AreEqual(TimeSpan.FromMilliseconds(100), _compass.ReportInterval);
        Assert.AreEqual(SensorStatus.NoData, vm.Status);
        Assert.IsTrue(vm.IsWarmingUp);
        Assert.IsFalse(vm.IsLive);
        Assert.IsFalse(vm.HasError);
    }

    [TestMethod]
    public void ViewLoaded_SensorAbsent_ReportsUnavailableWithoutThrowing()
    {
        _compass.IsSupported = false;
        var vm = CreateViewModel();

        vm.ViewLoaded();

        Assert.IsTrue(vm.HasError);
        Assert.AreEqual("Compass_Unavailable", vm.ErrorMessage);
        Assert.IsFalse(vm.IsWarmingUp);
        Assert.IsFalse(vm.IsLive);
    }

    [TestMethod]
    public void StatusChanged_PermissionRevokedMidSession_SwapsTheMessageAndDropsOutOfLive()
    {
        var vm = CreateViewModel();
        vm.ViewLoaded();
        _compass.PublishHeading(90);

        _compass.SetStatus(SensorStatus.PermissionDenied);

        Assert.IsTrue(vm.HasError);
        Assert.AreEqual("Compass_PermissionDenied", vm.ErrorMessage);
        Assert.IsFalse(vm.IsLive);
    }

    [TestMethod]
    public void StatusChanged_SensorGoesSilentMidSession_ReturnsToTheWarmUpState()
    {
        var vm = CreateViewModel();
        vm.ViewLoaded();
        _compass.PublishHeading(90);

        // How an uncalibrated or stalled magnetometer surfaces: the service's watchdog says NoData.
        _compass.SetStatus(SensorStatus.NoData);

        Assert.IsTrue(vm.IsWarmingUp);
        Assert.IsFalse(vm.IsLive);
        Assert.IsFalse(vm.HasError, "A silent sensor is a warm-up, not a failure.");
        Assert.AreEqual("090°", vm.HeadingText);
    }

    [TestMethod]
    public void ViewLoaded_CalledTwice_StartsTheSensorOnce()
    {
        var vm = CreateViewModel();

        vm.ViewLoaded();
        vm.ViewLoaded();

        Assert.AreEqual(1, _compass.StartCallCount);
        Assert.AreEqual(1, _display.ActiveRequests);
    }

    // ---- Smoothing -------------------------------------------------------------

    [TestMethod]
    public void HeadingChanged_FirstReading_SeedsTheFilterExactly()
    {
        var vm = CreateViewModel();
        vm.ViewLoaded();

        _compass.PublishHeading(90);

        Assert.AreEqual(90d, vm.HeadingDegrees, Tolerance);
        Assert.AreEqual("090°", vm.HeadingText);
        Assert.AreEqual("Compass_East", vm.CardinalPoint);
        Assert.IsTrue(vm.IsLive);
    }

    [TestMethod]
    public void HeadingChanged_SecondReading_LagsBehindTheRawValue()
    {
        var vm = CreateViewModel();
        vm.ViewLoaded();

        _compass.PublishHeading(90);
        _compass.PublishHeading(100);

        // One alpha=0.15 step, so ~91.5°: proof the raw jitter is being filtered, not passed through.
        Assert.IsTrue(vm.HeadingDegrees > 90d, $"Expected movement toward 100, got {vm.HeadingDegrees}.");
        Assert.IsTrue(vm.HeadingDegrees < 92d, $"Expected smoothing, got the raw jump to {vm.HeadingDegrees}.");
    }

    [TestMethod]
    public void HeadingChanged_CrossingNorth_TakesTheShortWayRound()
    {
        var vm = CreateViewModel();
        vm.ViewLoaded();

        _compass.PublishHeading(359);
        _compass.PublishHeading(1);

        // A scalar average would land near 305° here; the circular form stays just below north.
        Assert.IsTrue(
            vm.HeadingDegrees > 355d,
            $"Needle unwound the long way round: {vm.HeadingDegrees}.");
    }

    [TestMethod]
    public void HeadingChanged_NonFiniteReading_LeavesTheLastBearingOnScreen()
    {
        var vm = CreateViewModel();
        vm.ViewLoaded();
        _compass.PublishHeading(90);

        _compass.PublishHeading(double.NaN);

        Assert.AreEqual(90d, vm.HeadingDegrees, Tolerance);
        Assert.AreEqual("090°", vm.HeadingText);
    }

    // ---- Cardinal points -------------------------------------------------------

    [TestMethod]
    [DataRow(0d, "Compass_North")]
    [DataRow(22.5d, "Compass_NorthNorthEast")]
    [DataRow(45d, "Compass_NorthEast")]
    [DataRow(67.5d, "Compass_EastNorthEast")]
    [DataRow(90d, "Compass_East")]
    [DataRow(135d, "Compass_SouthEast")]
    [DataRow(180d, "Compass_South")]
    [DataRow(202.5d, "Compass_SouthSouthWest")]
    [DataRow(225d, "Compass_SouthWest")]
    [DataRow(270d, "Compass_West")]
    [DataRow(315d, "Compass_NorthWest")]
    [DataRow(337.5d, "Compass_NorthNorthWest")]
    [DataRow(350d, "Compass_North")]
    public void CardinalPoint_SixteenPointTable_ResolvesTheSector(double degrees, string expectedKey)
    {
        var vm = CreateViewModel();
        vm.ViewLoaded();

        _compass.PublishHeading(degrees);

        Assert.AreEqual(expectedKey, vm.CardinalPoint);
    }

    // ---- True north ------------------------------------------------------------

    [TestMethod]
    public void TrueNorth_ReadingCarriesIt_TracksTheSmoothedHeading()
    {
        var vm = CreateViewModel();
        vm.ViewLoaded();

        _compass.PublishHeading(90, 92);

        Assert.IsTrue(vm.HasTrueNorth);
        Assert.AreEqual(92d, vm.TrueNorthDegrees!.Value, Tolerance);
        Assert.AreEqual("092°", vm.TrueNorthText);
    }

    [TestMethod]
    public void TrueNorth_NeverReported_StaysHidden()
    {
        var vm = CreateViewModel();
        vm.ViewLoaded();

        _compass.PublishHeading(90);

        Assert.IsFalse(vm.HasTrueNorth);
        Assert.IsNull(vm.TrueNorthDegrees);
    }

    [TestMethod]
    public void TrueNorth_ReadingArrivesWithoutAFix_KeepsTheLastDeclination()
    {
        var vm = CreateViewModel();
        vm.ViewLoaded();
        _compass.PublishHeading(90, 92);

        // Losing the location fix must not blink the second line out mid-walk.
        _compass.PublishHeading(90);

        Assert.IsTrue(vm.HasTrueNorth);
    }

    [TestMethod]
    public void TrueNorth_AfterUnload_IsForgotten()
    {
        var vm = CreateViewModel();
        vm.ViewLoaded();
        _compass.PublishHeading(90, 92);

        vm.ViewUnloaded();
        vm.ViewLoaded();
        _compass.PublishHeading(120);

        Assert.IsFalse(vm.HasTrueNorth);
    }

    // ---- Teardown --------------------------------------------------------------

    [TestMethod]
    public void ViewUnloaded_StopsTheSensorAndUnsubscribes()
    {
        var vm = CreateViewModel();
        vm.ViewLoaded();
        _compass.PublishHeading(90);

        vm.ViewUnloaded();

        Assert.AreEqual(1, _compass.StopCallCount);

        // A magnetometer still feeding a dead view is the battery bug this tool must not ship.
        _compass.Start(TimeSpan.FromMilliseconds(100));
        _compass.PublishHeading(200);

        Assert.AreEqual(90d, vm.HeadingDegrees, Tolerance);
    }

    [TestMethod]
    public void ViewUnloaded_AfterAFailedStart_ClearsTheErrorState()
    {
        _compass.IsSupported = false;
        var vm = CreateViewModel();
        vm.ViewLoaded();

        vm.ViewUnloaded();

        Assert.IsFalse(vm.HasError);
        Assert.AreEqual(string.Empty, vm.ErrorMessage);
    }

    [TestMethod]
    public void ViewUnloaded_ReleasesTheDisplayRequest()
    {
        var vm = CreateViewModel();
        vm.ViewLoaded();

        vm.ViewUnloaded();

        Assert.AreEqual(0, _display.ActiveRequests);
    }

    // ---- Keep-awake ------------------------------------------------------------

    [TestMethod]
    public void KeepScreenAwake_OnByDefault_HoldsARequestWhileLoaded()
    {
        var vm = CreateViewModel();
        Assert.IsTrue(vm.KeepScreenAwake);

        vm.ViewLoaded();

        Assert.AreEqual(1, _display.ActiveRequests);
    }

    [TestMethod]
    public void KeepScreenAwake_Toggled_ReleasesAndRetakesTheRequest()
    {
        var vm = CreateViewModel();
        vm.ViewLoaded();

        vm.KeepScreenAwake = false;
        Assert.AreEqual(0, _display.ActiveRequests);

        vm.KeepScreenAwake = true;
        Assert.AreEqual(1, _display.ActiveRequests);
    }

    [TestMethod]
    public void KeepScreenAwake_ToggledBeforeLoad_TakesNothing()
    {
        var vm = CreateViewModel();

        vm.KeepScreenAwake = false;
        vm.KeepScreenAwake = true;

        Assert.AreEqual(0, _display.ActiveRequests);
    }

    // ---- Copy ------------------------------------------------------------------

    [TestMethod]
    public void CopyHeading_AfterAReading_CopiesTheBearingAndItsCardinalPoint()
    {
        var vm = CreateViewModel(new FakeStringLocalizer(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Compass_SouthWest"] = "SW",
        }));
        vm.ViewLoaded();
        _compass.PublishHeading(217);

        vm.CopyHeadingCommand.Execute(null);

        Assert.AreEqual("217° SW", _clipboard.LastText);
    }

    [TestMethod]
    public void CopyHeading_BeforeAnyReading_IsDisabled()
    {
        var vm = CreateViewModel();
        vm.ViewLoaded();

        Assert.IsFalse(vm.CopyHeadingCommand.CanExecute(null));

        _compass.PublishHeading(90);

        Assert.IsTrue(vm.CopyHeadingCommand.CanExecute(null));
    }

    /// <summary>Counts outstanding display requests, so a leaked wake-lock shows up as a non-zero tail.</summary>
    private sealed class RecordingDisplayRequestManager : IDisplayRequestManager
    {
        public int ActiveRequests { get; private set; }

        public IDisposable RequestActive()
        {
            ActiveRequests++;
            return new Token(this);
        }

        public void Clear() => ActiveRequests = 0;

        private sealed class Token(RecordingDisplayRequestManager owner) : IDisposable
        {
            private bool _released;

            public void Dispose()
            {
                if (_released)
                {
                    return;
                }

                _released = true;
                owner.ActiveRequests--;
            }
        }
    }
}
