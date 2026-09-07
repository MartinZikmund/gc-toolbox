namespace GcToolkit.Core.Catalog;

/// <summary>
/// Decides whether a discovered tool is offered on this device. All registered policies are ANDed;
/// with none registered the catalog behaves exactly as it did before.
/// </summary>
public interface IToolAvailabilityPolicy
{
    bool IsAvailable(ToolDescriptor tool);
}
