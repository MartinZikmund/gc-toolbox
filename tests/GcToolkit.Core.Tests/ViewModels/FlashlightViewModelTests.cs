using GcToolkit.Core.Services;
using GcToolkit.Core.Services.Devices;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Core.Tests.ViewModels;

/// <summary>
/// Covers the two things this tool must never get wrong: the screen light works with no lamp behind it,
/// and the display request is held exactly while the light is on and released the moment it is not — a
/// leaked request keeps the user's screen awake forever.
/// </summary>
[TestClass]
public sealed class FlashlightViewModelTests
{
    private const string ToolId = "Flashlight";

    // ---- Lazy probe -----------------------------------------------------------------------

    [TestMethod]
    public void Construction_DoesNotTouchTheLamp()
    {
        var (_, torch, _) = CreateSut();

        // Uno's Android probe enumerates cameras; it has no business running as the tool is resolved.
        Assert.AreEqual(0, torch.ProbeCount);
    }

    [TestMethod]
    public void ViewLoaded_WithLamp_ProbesOnceAndOffersTheTorch()
    {
        var (sut, torch, _) = CreateSut();

        sut.ViewLoaded();

        Assert.AreEqual(1, torch.ProbeCount);
        Assert.IsTrue(sut.IsTorchSupported);
        Assert.IsTrue(sut.ToggleTorchCommand.CanExecute(null));
    }

    // ---- No lamp: the screen is still a torch ---------------------------------------------

    [TestMethod]
    public void ViewLoaded_WithoutLamp_HidesTheTorchButKeepsScreenModeWorking()
    {
        var (sut, _, display) = CreateSut(lampPresent: false);

        sut.ViewLoaded();
        sut.ToggleLightCommand.Execute(null);

        Assert.IsFalse(sut.IsTorchSupported, "The torch row is collapsed, never shown disabled.");
        Assert.IsFalse(sut.ToggleTorchCommand.CanExecute(null));
        Assert.IsTrue(sut.IsLit);
        Assert.IsTrue(sut.IsSurfaceLit);
        Assert.AreEqual(1, display.ActiveCount);
    }

    [TestMethod]
    public void ToggleTorch_WithoutLamp_IsANoOpRatherThanAFailure()
    {
        var (sut, _, display) = CreateSut(lampPresent: false);
        sut.ViewLoaded();

        sut.ToggleTorchCommand.Execute(null);

        Assert.IsFalse(sut.IsTorchOn);
        Assert.AreEqual(0, display.ActiveCount);
    }

    // ---- Display request ------------------------------------------------------------------

    [TestMethod]
    public void ToggleLight_OnThenOff_AcquiresThenReleasesTheDisplayRequest()
    {
        var (sut, _, display) = CreateSut();

        sut.ToggleLightCommand.Execute(null);
        Assert.AreEqual(1, display.ActiveCount);

        sut.ToggleLightCommand.Execute(null);
        Assert.AreEqual(0, display.ActiveCount);
        Assert.IsFalse(sut.IsLit);
    }

    [TestMethod]
    public void ToggleLight_RepeatedToggles_NeverHoldMoreThanOneRequest()
    {
        var (sut, _, display) = CreateSut();

        for (var i = 0; i < 10; i++)
        {
            sut.ToggleLightCommand.Execute(null);
            Assert.IsTrue(display.ActiveCount <= 1, "The display request must be ref-counted at one.");
        }

        Assert.AreEqual(0, display.ActiveCount, "Ten toggles end off, so nothing may still be held.");
        Assert.AreEqual(5, display.TotalRequests);
    }

    [TestMethod]
    public void ViewUnloaded_WhileLit_ReleasesTheDisplayRequestAndTheLamp()
    {
        var (sut, torch, display) = CreateSut();
        sut.ViewLoaded();
        sut.ToggleLightCommand.Execute(null);
        sut.ToggleTorchCommand.Execute(null);

        sut.ViewUnloaded();

        Assert.AreEqual(0, display.ActiveCount);
        Assert.AreEqual(1, torch.ReleaseCallCount, "Uno's iOS/Android Lamp holds unmanaged resources.");
        Assert.IsFalse(sut.IsLit);
        Assert.IsFalse(sut.IsTorchOn);
    }

    [TestMethod]
    public void TorchAlone_HoldsTheDisplayRequest_AndReleasesItWhenSwitchedOff()
    {
        var (sut, _, display) = CreateSut();
        sut.ViewLoaded();

        sut.ToggleTorchCommand.Execute(null);
        Assert.IsTrue(sut.IsTorchOn);
        Assert.AreEqual(1, display.ActiveCount);

        sut.ToggleTorchCommand.Execute(null);
        Assert.IsFalse(sut.IsTorchOn);
        Assert.AreEqual(0, display.ActiveCount);
    }

    // ---- Torch refusals -------------------------------------------------------------------

    [TestMethod]
    public void ToggleTorch_WhenTheLampIsHeldElsewhere_SnapsBackAndReportsBusy()
    {
        var (sut, torch, _) = CreateSut();
        sut.ViewLoaded();
        torch.FailureStatus = SensorStatus.Busy;

        sut.ToggleTorchCommand.Execute(null);

        Assert.IsFalse(sut.IsTorchOn, "The lamp, not the switch, is the truth.");
        Assert.IsTrue(sut.IsTorchBusy);
        Assert.IsFalse(sut.IsTorchPermissionDenied);
    }

    [TestMethod]
    public void ToggleTorch_WhenPermissionIsDenied_ReportsPermissionDenied()
    {
        var (sut, torch, _) = CreateSut();
        sut.ViewLoaded();
        torch.FailureStatus = SensorStatus.PermissionDenied;

        sut.ToggleTorchCommand.Execute(null);

        Assert.IsFalse(sut.IsTorchOn);
        Assert.IsTrue(sut.IsTorchPermissionDenied);
        Assert.IsFalse(sut.IsTorchBusy);
    }

    [TestMethod]
    public void TorchStatus_WithoutALamp_NeverRaisesAnInfoBar()
    {
        var (sut, torch, _) = CreateSut(lampPresent: false);
        sut.ViewLoaded();
        torch.FailureStatus = SensorStatus.Busy;

        sut.ToggleTorchCommand.Execute(null);

        // The whole row is collapsed on a lampless head, so neither notice may ever open behind it.
        Assert.IsFalse(sut.IsTorchBusy);
        Assert.IsFalse(sut.IsTorchPermissionDenied);
    }

    // ---- Brightness -----------------------------------------------------------------------

    [TestMethod]
    [DataRow(-4d, 0.1d)]
    [DataRow(0d, 0.1d)]
    [DataRow(0.5d, 0.5d)]
    [DataRow(9d, 1d)]
    public void Brightness_IsClampedToTheUsableRange(double requested, double expected)
    {
        var (sut, _, _) = CreateSut();

        sut.Brightness = requested;

        Assert.AreEqual(expected, sut.Brightness, 1e-9);
    }

    [TestMethod]
    public void Brightness_UpdatesTheMonoReadout()
    {
        var (sut, _, _) = CreateSut();

        sut.Brightness = 0.5;

        Assert.IsFalse(string.IsNullOrWhiteSpace(sut.BrightnessText));
        StringAssert.Contains(sut.BrightnessText, "50");
    }

    [TestMethod]
    public void Brightness_WithABrightnessCapableLampOn_ReachesTheHardware()
    {
        var (sut, torch, _) = CreateSut(supportsBrightness: true);
        sut.ViewLoaded();
        sut.ToggleTorchCommand.Execute(null);

        sut.Brightness = 0.4;

        Assert.IsTrue(sut.SupportsBrightness);
        Assert.AreEqual(0.4, torch.LastBrightness, 1e-9);
        Assert.IsTrue(sut.IsTorchOn);
    }

    // ---- Colour ---------------------------------------------------------------------------

    [TestMethod]
    public void Colour_SelectingRed_KeepsTheSwatchIndexAndFlagsInStep()
    {
        var (sut, _, _) = CreateSut();

        sut.Colour = FlashlightColour.Red;

        Assert.AreEqual((int)FlashlightColour.Red, sut.ColourIndex);
        Assert.IsTrue(sut.IsColourRed);
        Assert.IsFalse(sut.IsColourWhite);
        Assert.IsFalse(sut.IsColourGreen);
    }

    [TestMethod]
    public void ColourIndex_TransientMinusOneFromRadioButtons_IsIgnored()
    {
        var (sut, _, _) = CreateSut();
        sut.ColourIndex = (int)FlashlightColour.Green;

        sut.ColourIndex = -1;

        Assert.AreEqual(FlashlightColour.Green, sut.Colour);
    }

    // ---- SOS ------------------------------------------------------------------------------

    [TestMethod]
    public void ToggleSos_StartsBlinking_AndLightsTheScreenEvenIfItWasOff()
    {
        var (sut, _, display) = CreateSut();

        sut.ToggleSosCommand.Execute(null);

        Assert.IsTrue(sut.IsSosBlinking);
        Assert.IsTrue(sut.IsLit, "An SOS with the light off would signal nothing.");
        Assert.AreEqual(1, display.ActiveCount);

        sut.ViewUnloaded();
    }

    [TestMethod]
    public void ToggleSos_Twice_StopsBlinking()
    {
        var (sut, _, _) = CreateSut();

        sut.ToggleSosCommand.Execute(null);
        sut.ToggleSosCommand.Execute(null);

        Assert.IsFalse(sut.IsSosBlinking);

        sut.ViewUnloaded();
    }

    [TestMethod]
    public async Task ToggleSos_RestartedImmediately_IsNotStoppedByTheCancelledRun()
    {
        var (sut, _, _) = CreateSut();

        sut.ToggleSosCommand.Execute(null);
        sut.ToggleSosCommand.Execute(null);
        sut.ToggleSosCommand.Execute(null);

        // The cancelled run unwinds on its own continuation; give it room to land. Its teardown must
        // leave the restarted run alone, so this only ever fails in one direction.
        await Task.Delay(50);

        Assert.IsTrue(sut.IsSosBlinking);

        sut.ViewUnloaded();
    }

    [TestMethod]
    public void TurningTheLightOff_AlsoStopsAnSos()
    {
        var (sut, _, _) = CreateSut();
        sut.ToggleSosCommand.Execute(null);

        sut.ToggleLightCommand.Execute(null);

        Assert.IsFalse(sut.IsLit);
        Assert.IsFalse(sut.IsSosBlinking);
    }

    [TestMethod]
    public void IsSurfaceLit_IsDarkDuringTheOffPhaseOfABlink()
    {
        // Driven by hand rather than by ToggleSos: the real blink runs on a timer and would race the
        // assertions. What matters here is the rule the surface is painted from.
        var (sut, _, _) = CreateSut();
        sut.IsLit = true;
        sut.IsSosBlinking = true;

        sut.IsBlinkOn = false;
        Assert.IsFalse(sut.IsSurfaceLit, "The gaps between flashes are what makes it readable as Morse.");

        sut.IsBlinkOn = true;
        Assert.IsTrue(sut.IsSurfaceLit);
    }

    [TestMethod]
    public void IsSurfaceLit_FollowsTheLightWhenNotBlinking()
    {
        var (sut, _, _) = CreateSut();

        sut.ToggleLightCommand.Execute(null);
        Assert.IsTrue(sut.IsSurfaceLit);

        sut.ToggleLightCommand.Execute(null);
        Assert.IsFalse(sut.IsSurfaceLit);
    }

    // ---- Helpers --------------------------------------------------------------------------

    private static (FlashlightViewModel Sut, FakeTorchService Torch, RecordingDisplayRequestManager Display) CreateSut(
        bool lampPresent = true,
        bool supportsBrightness = false)
    {
        FakeTorchService torch = new() { IsLampPresent = lampPresent, SupportsBrightness = supportsBrightness };
        RecordingDisplayRequestManager display = new();

        FlashlightViewModel sut = new(
            new StubCatalogService(ToolId),
            new FakeRecentsService(),
            new FakeFavoriteToolsService(),
            new FakeStringLocalizer(),
            torch,
            display);

        return (sut, torch, display);
    }

    /// <summary>
    /// Counts live requests so a leak is a failed assertion rather than a user's flat battery. Nested
    /// rather than added to <c>Fakes/</c> so it cannot collide with a shared fake landing there.
    /// </summary>
    private sealed class RecordingDisplayRequestManager : IDisplayRequestManager
    {
        public int ActiveCount { get; private set; }

        public int TotalRequests { get; private set; }

        public IDisposable RequestActive()
        {
            ActiveCount++;
            TotalRequests++;
            return new Token(this);
        }

        public void Clear() => ActiveCount = 0;

        private sealed class Token(RecordingDisplayRequestManager owner) : IDisposable
        {
            private bool _isDisposed;

            public void Dispose()
            {
                if (_isDisposed)
                {
                    return;
                }

                _isDisposed = true;
                owner.ActiveCount--;
            }
        }
    }
}
