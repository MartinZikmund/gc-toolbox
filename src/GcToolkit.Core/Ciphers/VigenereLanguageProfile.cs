namespace GcToolkit.Core.Ciphers;

/// <summary>
/// A plaintext letter-frequency profile the assisted solver scores columns against — i.e. the language
/// of the text hidden under the cipher, mirroring the reference site's solver "Language" dropdown.
/// Frequencies fold diacritics onto their base A–Z letter, because the cipher itself only moves A–Z.
/// </summary>
public sealed class VigenereLanguageProfile
{
    // Floor for a letter's expected proportion. Czech effectively never uses Q/W/X, and a zero
    // expectation would divide by zero in the chi-squared score.
    private const double MinimumProportion = 0.0001;

    private VigenereLanguageProfile(string id, double expectedIndexOfCoincidence, double[] percentages)
    {
        Id = id;
        ExpectedIndexOfCoincidence = expectedIndexOfCoincidence;

        // Normalize to proportions summing to ~1: the folded tables don't total exactly 100%, and
        // chi-squared compares observed counts against `proportion * n`.
        var total = percentages.Sum();
        Weights = [.. percentages.Select(p => Math.Max(p / total, MinimumProportion))];
    }

    /// <summary>Stable identifier, also the suffix of the localized label key (<c>VigenereLanguage_En</c>).</summary>
    public string Id { get; }

    /// <summary>The Index of Coincidence normal text in this language exhibits — the target the
    /// key-length ranking measures each candidate's averaged per-column IC against.</summary>
    public double ExpectedIndexOfCoincidence { get; }

    /// <summary>Per-letter expected proportions (A–Z), normalized and floored.</summary>
    public IReadOnlyList<double> Proportions => Weights;

    internal double[] Weights { get; }

    /// <summary>English letter frequencies (the classic Lewand table).</summary>
    public static VigenereLanguageProfile English { get; } = new("En", 0.0667,
    [
        8.167, 1.492, 2.782, 4.253, 12.702, 2.228, 2.015, 6.094, 6.966, 0.153,
        0.772, 4.025, 2.406, 6.749, 7.507, 1.929, 0.095, 5.987, 6.327, 9.056,
        2.758, 0.978, 2.360, 0.150, 1.974, 0.074,
    ]);

    /// <summary>Czech letter frequencies with diacritics folded (á→A, č→C, ě→E, ř→R, š→S, ž→Z, …).</summary>
    public static VigenereLanguageProfile Czech { get; } = new("Cs", 0.0600,
    [
        8.421, 1.823, 2.991, 3.601, 10.604, 0.240, 0.240, 1.360, 6.732, 2.106,
        3.869, 3.802, 3.212, 6.682, 8.650, 3.494, 0.020, 4.311, 5.212, 5.727,
        3.723, 4.712, 0.020, 0.030, 2.601, 2.341,
    ]);

    /// <summary>The selectable profiles, in display order.</summary>
    public static IReadOnlyList<VigenereLanguageProfile> All { get; } = [English, Czech];
}
