namespace GcToolkit.Core.Catalog;

/// <summary>
/// SP-1 sample data: contributes a handful of placeholder tools across the planned
/// categories so the shell is demonstrable end-to-end before real tools land. Every
/// placeholder targets the stub tool-host view model (supplied by the app head, so Core
/// stays decoupled from the UI) and is flagged <see cref="ToolDescriptor.IsPlaceholder"/>.
/// </summary>
public sealed class PlaceholderToolContributor : IToolContributor, ICategoryContributor
{
    private readonly Type _hostViewModelType;

    public PlaceholderToolContributor(Type hostViewModelType)
    {
        _hostViewModelType = hostViewModelType;
    }

    public IEnumerable<Category> GetCategories()
    {
        yield return new Category("coordinates", "Category_Coordinates", Order: 0, IconKey: "");
        yield return new Category("ciphers", "Category_Ciphers", Order: 1, IconKey: "");
        yield return new Category("numbers", "Category_Numbers", Order: 2, IconKey: "");
        yield return new Category("field", "Category_Field", Order: 3, IconKey: "");
    }

    public IEnumerable<ToolDescriptor> GetTools()
    {
        // Coordinates
        yield return Tool("coordinates.conversion", "Tool_CoordinateConversion", "coordinates",
            ["wgs84", "utm", "convert", "souřadnice", "převod", "gps"], "");
        yield return Tool("coordinates.distance", "Tool_CoordinateDistance", "coordinates",
            ["distance", "vzdálenost", "azimuth", "bearing", "azimut"], "");
        yield return Tool("coordinates.projection", "Tool_CoordinateProjection", "coordinates",
            ["projection", "projekce", "waypoint", "bod"], "");

        // Ciphers
        yield return Tool("ciphers.caesar", "Tool_CaesarCipher", "ciphers",
            ["caesar", "rot13", "shift", "posun", "šifra"], "");
        yield return Tool("ciphers.morse", "Tool_MorseCode", "ciphers",
            ["morse", "code", "kód", "telegraph"], "");
        yield return Tool("ciphers.vigenere", "Tool_VigenereCipher", "ciphers",
            ["vigenère", "vigenere", "key", "klíč", "šifra"], "");

        // Numbers
        yield return Tool("numbers.base", "Tool_BaseConverter", "numbers",
            ["binary", "hex", "base", "soustava", "převod", "číslo"], "");
        yield return Tool("numbers.checksum", "Tool_Checksum", "numbers",
            ["checksum", "crc", "kontrolní", "součet", "hash"], "");

        // Field
        yield return Tool("field.flashlight", "Tool_Flashlight", "field",
            ["flashlight", "torch", "light", "baterka", "světlo"], "");
        yield return Tool("field.compass", "Tool_Compass", "field",
            ["compass", "kompas", "heading", "směr", "north"], "");
    }

    private ToolDescriptor Tool(string id, string nameKey, string categoryId, string[] keywords, string iconKey)
        => new(id, nameKey, categoryId, keywords, iconKey, _hostViewModelType, IsPlaceholder: true);
}
