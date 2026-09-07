using GcToolkit.Core.Services.Devices;

namespace GcToolkit.Core.Tests.Fakes;

/// <summary>
/// A compass with no hardware behind it: tests set <see cref="IsSupported"/>, call
/// <see cref="PublishHeading"/> to feed readings, and <see cref="SetStatus"/> to simulate a permission
/// being revoked mid-session. Mirrors <c>CompassService</c>: <see cref="Start"/> reports
/// <see cref="SensorStatus.NoData"/> until the first reading arrives.
/// </summary>
public sealed class FakeCompassService : ICompassService
{
    private SensorStatus _status = SensorStatus.Unavailable;

    public bool IsSupported { get; set; } = true;

    /// <summary>The floor <see cref="Start"/> clamps the requested interval to, standing in for a driver minimum.</summary>
    public TimeSpan MinimumReportInterval { get; set; } = TimeSpan.Zero;

    public SensorStatus Status
    {
        get => _status;
        private set
        {
            if (_status == value)
            {
                return;
            }

            _status = value;
            _statusChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public TimeSpan ReportInterval { get; private set; }

    public int StartCallCount { get; private set; }

    public int StopCallCount { get; private set; }

    public bool IsStarted { get; private set; }

    /// <summary>How many handlers are attached right now, so a test can assert that teardown detached
    /// them — a leaked subscription is otherwise invisible, since re-raising just sets the same values.</summary>
    public int HeadingSubscriberCount { get; private set; }

    public int StatusSubscriberCount { get; private set; }

    private EventHandler<CompassHeading>? _headingChanged;
    private EventHandler? _statusChanged;

    public event EventHandler<CompassHeading>? HeadingChanged
    {
        add { _headingChanged += value; HeadingSubscriberCount++; }
        remove { _headingChanged -= value; HeadingSubscriberCount--; }
    }

    public event EventHandler? StatusChanged
    {
        add { _statusChanged += value; StatusSubscriberCount++; }
        remove { _statusChanged -= value; StatusSubscriberCount--; }
    }

    public bool Start(TimeSpan reportInterval)
    {
        StartCallCount++;
        ReportInterval = reportInterval < MinimumReportInterval ? MinimumReportInterval : reportInterval;

        if (!IsSupported)
        {
            Status = SensorStatus.Unavailable;
            return false;
        }

        IsStarted = true;
        Status = SensorStatus.NoData;
        return true;
    }

    public void Stop()
    {
        StopCallCount++;
        IsStarted = false;
        ReportInterval = TimeSpan.Zero;
        Status = SensorStatus.Unavailable;
    }

    /// <summary>Feeds a reading, exactly as a live sensor would: the first one flips the status to Ready.</summary>
    public void PublishHeading(double magneticNorthDegrees, double? trueNorthDegrees = null)
    {
        if (!IsStarted)
        {
            return;
        }

        Status = SensorStatus.Ready;
        _headingChanged?.Invoke(
            this,
            new CompassHeading(magneticNorthDegrees, trueNorthDegrees, DateTimeOffset.UnixEpoch));
    }

    /// <summary>Forces a status, for the revoked-permission and stalled-sensor cases.</summary>
    public void SetStatus(SensorStatus status) => Status = status;
}
