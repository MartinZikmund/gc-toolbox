using GcToolkit.Core.Coordinates;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// One entry in a datum picker: the <see cref="Datum"/> itself plus a human-readable
/// <see cref="Display"/> string (code + reference ellipsoid). The View binds the ComboBox to these and
/// uses <see cref="Value"/> as the selected value, so the ViewModel keeps exposing plain
/// <see cref="Datum"/> for <c>InputDatum</c>/<c>OutputDatum</c>.
/// </summary>
public sealed class DatumOption
{
    public DatumOption(Datum value)
    {
        Value = value;
        Display = $"{value.Code} — {value.Ellipsoid.Name}";
    }

    public Datum Value { get; }

    public string Display { get; }
}
