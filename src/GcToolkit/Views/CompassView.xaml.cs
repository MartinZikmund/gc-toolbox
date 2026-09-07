using System;
using System.ComponentModel;
using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class CompassViewBase : ViewBase<CompassViewModel> { }

/// <summary>
/// Drives the needle. The view model already smooths the sensor; this adds the short tween that
/// bridges the 100 ms gaps between readings, and it is the only place that knows how to cross north
/// without unwinding the long way round.
/// </summary>
public sealed partial class CompassView : CompassViewBase
{
    /// <summary>Just longer than the report interval, so one sweep runs into the next.</summary>
    private static readonly Duration _sweepDuration = new(TimeSpan.FromMilliseconds(120));

    /// <summary>Below this a sweep is invisible and only costs a storyboard restart.</summary>
    private const double MinimumSweepDegrees = 0.1;

    private readonly Storyboard _needleStoryboard = new();

    private readonly DoubleAnimation _needleSweep = new()
    {
        Duration = _sweepDuration,
        // WinUI classes RotateTransform.Angle as a dependent animation; without this it is skipped.
        EnableDependentAnimation = true,
        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
    };

    /// <summary>Continuous and deliberately un-wrapped: 359° → 1° must advance to 361°, not unwind 358°.</summary>
    private double _sweptAngle;
    private bool _hasSweptAngle;

    public CompassView()
    {
        this.InitializeComponent();

        Storyboard.SetTarget(_needleSweep, NeedleRotation);
        Storyboard.SetTargetProperty(_needleSweep, "Angle");
        _needleStoryboard.Children.Add(_needleSweep);
        _needleStoryboard.Completed += OnSweepCompleted;

        Loaded += OnCompassLoaded;
        Unloaded += OnCompassUnloaded;
    }

    private void OnCompassLoaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;

        // Only when a bearing already exists (a re-attached page). Seeding from the default 0 would
        // make the first real reading sweep in from north instead of simply appearing.
        if (ViewModel.IsLive)
        {
            SweepTo(ViewModel.HeadingDegrees);
        }
    }

    private void OnCompassUnloaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _needleStoryboard.Stop();
        _hasSweptAngle = false;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CompassViewModel.HeadingDegrees) && ViewModel is not null)
        {
            SweepTo(ViewModel.HeadingDegrees);
        }
    }

    private void SweepTo(double headingDegrees)
    {
        // The first bearing of a session snaps: easing in from an arbitrary zero would swing the
        // needle right across the rose the moment the tool opens.
        if (!_hasSweptAngle)
        {
            _hasSweptAngle = true;
            _sweptAngle = headingDegrees;
            _needleStoryboard.Stop();
            NeedleRotation.Angle = _sweptAngle;
            return;
        }

        var delta = ((((headingDegrees - _sweptAngle) % 360d) + 540d) % 360d) - 180d;
        if (Math.Abs(delta) < MinimumSweepDegrees)
        {
            return;
        }

        var from = _sweptAngle;
        _sweptAngle += delta;

        // Readings outpace the sweep, so re-targeting mid-flight is the normal case. Stopping first
        // and stating From explicitly keeps the restart deterministic instead of racing the old sweep.
        _needleStoryboard.Stop();
        NeedleRotation.Angle = from;
        _needleSweep.From = from;
        _needleSweep.To = _sweptAngle;
        _needleStoryboard.Begin();
    }

    private void OnSweepCompleted(object? sender, object e) => NeedleRotation.Angle = _sweptAngle;
}
