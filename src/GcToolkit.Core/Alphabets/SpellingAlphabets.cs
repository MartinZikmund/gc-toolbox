namespace GcToolkit.Core.Alphabets;

/// <summary>
/// The eleven spelling-alphabet variants offered by cachesleuth.com's tool. Code words are transcribed
/// from the authoritative Wikipedia spelling-alphabet tables; NATO/ICAO is exact (Alfa/Juliett/Quebec/
/// X-ray/Zulu). Each variant is pure data so its full chart can be asserted in tests.
/// </summary>
public static class SpellingAlphabets
{
    /// <summary>NATO/ICAO — the modern international standard and the default selection.</summary>
    public static SpellingAlphabetVariant Default { get; } = BuildNato();

    public static IReadOnlyList<SpellingAlphabetVariant> All { get; } =
    [
        Default,
        BuildItu1932(),
        BuildWesternUnion(),
        BuildAbleBaker(),
        BuildRaf1924(),
        BuildApco(),
        BuildDutch(),
        BuildGerman(),
        BuildSwedish(),
        BuildRussianOfficial(),
        BuildRussianUnofficial(),
    ];

    public static SpellingAlphabetVariant ById(string id)
        => All.FirstOrDefault(v => v.Id == id) ?? Default;

    // ---- Latin variants (A–Z, plus digits where defined) ----

    private static SpellingAlphabetVariant BuildNato()
    {
        var chart = Latin(
            "Alfa", "Bravo", "Charlie", "Delta", "Echo", "Foxtrot", "Golf", "Hotel", "India",
            "Juliett", "Kilo", "Lima", "Mike", "November", "Oscar", "Papa", "Quebec", "Romeo",
            "Sierra", "Tango", "Uniform", "Victor", "Whiskey", "X-ray", "Yankee", "Zulu");

        // ICAO digit code words.
        chart.AddRange(Digits(
            "Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Niner"));

        return new("Nato", isCyrillic: false, chart, aliases: new Dictionary<string, char>(StringComparer.OrdinalIgnoreCase)
        {
            ["Alpha"] = 'A',   // common mis-spelling of Alfa
            ["Juliet"] = 'J',  // common mis-spelling of Juliett
            ["Nine"] = '9',    // plain "Nine" for the spoken "Niner"
            ["Tree"] = '3',    // ICAO spoken forms
            ["Fower"] = '4',
            ["Fife"] = '5',
        });
    }

    private static SpellingAlphabetVariant BuildItu1932()
        => new("Itu1932", isCyrillic: false, Latin(
            "Amsterdam", "Baltimore", "Casablanca", "Denmark", "Edison", "Florida", "Gallipoli",
            "Havana", "Italia", "Jerusalem", "Kilogramme", "Liverpool", "Madagascar", "New York",
            "Oslo", "Paris", "Quebec", "Roma", "Santiago", "Tripoli", "Upsala", "Valencia",
            "Washington", "Xanthippe", "Yokohama", "Zurich"));

    private static SpellingAlphabetVariant BuildWesternUnion()
        => new("WesternUnion", isCyrillic: false, Latin(
            "Adams", "Boston", "Chicago", "Denver", "Easy", "Frank", "George", "Henry", "Ida",
            "John", "King", "Lincoln", "Mary", "New York", "Ocean", "Peter", "Queen", "Roger",
            "Sugar", "Thomas", "Union", "Victor", "William", "X-ray", "Young", "Zero"));

    private static SpellingAlphabetVariant BuildAbleBaker()
        => new("AbleBaker", isCyrillic: false, Latin(
            "Able", "Baker", "Charlie", "Dog", "Easy", "Fox", "George", "How", "Item", "Jig",
            "King", "Love", "Mike", "Nan", "Oboe", "Peter", "Queen", "Roger", "Sugar", "Tare",
            "Uncle", "Victor", "William", "X-ray", "Yoke", "Zebra"));

    private static SpellingAlphabetVariant BuildRaf1924()
        => new("Raf1924", isCyrillic: false, Latin(
            "Ace", "Beer", "Charlie", "Don", "Edward", "Freddie", "George", "Harry", "Ink",
            "Johnnie", "King", "London", "Monkey", "Nuts", "Orange", "Pip", "Queen", "Robert",
            "Sugar", "Toc", "Uncle", "Vic", "William", "X-ray", "Yorker", "Zebra"));

    private static SpellingAlphabetVariant BuildApco()
        => new("Apco", isCyrillic: false, Latin(
            "Adam", "Boy", "Charles", "David", "Edward", "Frank", "George", "Henry", "Ida",
            "John", "King", "Lincoln", "Mary", "Nora", "Ocean", "Paul", "Queen", "Robert", "Sam",
            "Tom", "Union", "Victor", "William", "X-ray", "Young", "Zebra"));

    private static SpellingAlphabetVariant BuildDutch()
        => new("Dutch", isCyrillic: false, Latin(
            "Anton", "Bernard", "Cornelis", "Dirk", "Eduard", "Ferdinand", "Gerard", "Hendrik",
            "Izaak", "Johan", "Karel", "Lodewijk", "Maria", "Nico", "Otto", "Pieter", "Quotiënt",
            "Richard", "Simon", "Theodoor", "Utrecht", "Victor", "Willem", "Xantippe", "Ypsilon",
            "Zaandam"));

    private static SpellingAlphabetVariant BuildGerman()
        => new("German", isCyrillic: false, Latin(
            "Anton", "Berta", "Cäsar", "Dora", "Emil", "Friedrich", "Gustav", "Heinrich", "Ida",
            "Julius", "Kaufmann", "Ludwig", "Martha", "Nordpol", "Otto", "Paula", "Quelle",
            "Richard", "Samuel", "Theodor", "Ulrich", "Viktor", "Wilhelm", "Xanthippe", "Ypsilon",
            "Zacharias"));

    private static SpellingAlphabetVariant BuildSwedish()
        => new("Swedish", isCyrillic: false, Latin(
            "Adam", "Bertil", "Cesar", "David", "Erik", "Filip", "Gustav", "Helge", "Ivar",
            "Johan", "Kalle", "Ludvig", "Martin", "Niklas", "Olof", "Petter", "Qvintus", "Rudolf",
            "Sigurd", "Tore", "Urban", "Viktor", "Wilhelm", "Xerxes", "Yngve", "Zäta"));

    // ---- Cyrillic variants ----

    private static SpellingAlphabetVariant BuildRussianOfficial()
        => new("RussianOfficial", isCyrillic: true, Cyrillic(
            "Anna", "Boris", "Vasiliy", "Grigoriy", "Dmitriy", "Yelena", "Zhenya", "Zinaida",
            "Ivan", "Ivan kratkiy", "Konstantin", "Leonid", "Mikhail", "Nikolay", "Olga", "Pavel",
            "Roman", "Semyon", "Tatyana", "Ulyana", "Fyodor", "Khariton", "Tsaplya", "Chelovek",
            "Shura", "Shchuka", "Tvyordyy znak", "Yery", "Myagkiy znak", "Ekho", "Yuriy", "Yakov"));

    private static SpellingAlphabetVariant BuildRussianUnofficial()
        => new("RussianUnofficial", isCyrillic: true, Cyrillic(
            "Anton", "Boris", "Vasiliy", "Galina", "Dmitriy", "Yelena", "Zhuk", "Zoya", "Ivan",
            "Yot", "Kilovatt", "Leonid", "Mariya", "Nikolay", "Olga", "Pavel", "Radio", "Sergey",
            "Tamara", "Ulyana", "Fyodor", "Khariton", "Tsentr", "Chelovek", "Shura", "Shchuka",
            "Tvyordyy znak", "Igrek", "Myagkiy znak", "Emma", "Yuriy", "Yakov"));

    // ---- Builders ----

    private static List<SpellingAlphabetEntry> Latin(params string[] words)
    {
        List<SpellingAlphabetEntry> chart = new(words.Length);
        for (var i = 0; i < words.Length; i++)
        {
            chart.Add(new((char)('A' + i), words[i]));
        }

        return chart;
    }

    private static List<SpellingAlphabetEntry> Digits(params string[] words)
    {
        List<SpellingAlphabetEntry> chart = new(words.Length);
        for (var i = 0; i < words.Length; i++)
        {
            chart.Add(new((char)('0' + i), words[i]));
        }

        return chart;
    }

    private static List<SpellingAlphabetEntry> Cyrillic(params string[] words)
    {
        ReadOnlySpan<char> letters =
        [
            'А', 'Б', 'В', 'Г', 'Д', 'Е', 'Ж', 'З', 'И', 'Й', 'К', 'Л', 'М', 'Н', 'О', 'П', 'Р', 'С',
            'Т', 'У', 'Ф', 'Х', 'Ц', 'Ч', 'Ш', 'Щ', 'Ъ', 'Ы', 'Ь', 'Э', 'Ю', 'Я',
        ];

        List<SpellingAlphabetEntry> chart = new(words.Length);
        for (var i = 0; i < words.Length; i++)
        {
            chart.Add(new(letters[i], words[i]));
        }

        return chart;
    }
}
