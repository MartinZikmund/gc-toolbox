namespace GcToolkit.Core.Alphabets;

/// <summary>
/// One physical key as seen on a layout: the character produced unshifted and the character
/// produced with Shift held. Used both to build the remap index and to render the on-screen chart.
/// </summary>
public sealed record DvorakKey(char Unshifted, char Shifted);

/// <summary>
/// A keyboard layout as an ordered sequence of <see cref="DvorakKey"/> in a fixed physical-key
/// order shared by every layout, so remapping is a simple position lookup.
/// </summary>
public sealed class DvorakLayoutInfo
{
    public DvorakLayoutInfo(DvorakLayout layout, IReadOnlyList<DvorakKey> keyMap)
    {
        Layout = layout;
        KeyMap = keyMap;

        var keys = new List<char>(keyMap.Count * 2);
        foreach (var key in keyMap)
        {
            keys.Add(key.Unshifted);
            keys.Add(key.Shifted);
        }

        Keys = keys;
    }

    public DvorakLayout Layout { get; }

    /// <summary>The keys in physical-position order (index <c>i</c> is the same physical key across layouts).</summary>
    public IReadOnlyList<DvorakKey> KeyMap { get; }

    /// <summary>Every character this layout can produce (both unshifted and shifted) — for coverage assertions.</summary>
    public IReadOnlyList<char> Keys { get; }
}
