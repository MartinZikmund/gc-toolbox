using GcToolkit.Core.Alphabets;

namespace GcToolkit.Core.Tests.Alphabets;

[TestClass]
public class SignalFlagsAlphabetTests
{
    [TestMethod]
    public void Letters_ContainsAllTwentySixInOrder_ReturnsAToZ()
    {
        var symbols = SignalFlagsAlphabet.Letters.Select(f => f.Symbol).ToArray();

        CollectionAssert.AreEqual("ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray(), symbols);
    }

    [TestMethod]
    public void Numerals_ContainsAllTenDigitsInOrder_ReturnsZeroToNine()
    {
        var symbols = SignalFlagsAlphabet.Numerals.Select(f => f.Symbol).ToArray();

        CollectionAssert.AreEqual("0123456789".ToCharArray(), symbols);
    }

    [TestMethod]
    public void Specials_ContainsAnswerAndThreeSubstitutes_InOrder()
    {
        var ids = SignalFlagsAlphabet.Specials.Select(f => f.Id).ToArray();

        CollectionAssert.AreEqual(new[] { "Answer", "Substitute1", "Substitute2", "Substitute3" }, ids);
    }

    [TestMethod]
    [DataRow('a', "A")]
    [DataRow('A', "A")]
    [DataRow('z', "Z")]
    [DataRow('7', "7")]
    public void TryGet_MappableSymbol_ReturnsFlag(char symbol, string expectedId)
    {
        var found = SignalFlagsAlphabet.TryGet(symbol, out var flag);

        Assert.IsTrue(found);
        Assert.AreEqual(expectedId, flag!.Id);
    }

    [TestMethod]
    [DataRow('!')]
    [DataRow(' ')]
    [DataRow('~')]
    public void TryGet_UnmappableSymbol_ReturnsFalse(char symbol)
        => Assert.IsFalse(SignalFlagsAlphabet.TryGet(symbol, out _));

    [TestMethod]
    [DataRow('A', "Alfa")]
    [DataRow('J', "Juliett")]
    [DataRow('X', "Xray")]
    [DataRow('Z', "Zulu")]
    [DataRow('0', "Nadazero")]
    [DataRow('5', "Pantafive")]
    [DataRow('9', "Novenine")]
    public void TryGet_KnownSymbol_HasIcsPhoneticName(char symbol, string expectedPhonetic)
    {
        SignalFlagsAlphabet.TryGet(symbol, out var flag);

        Assert.AreEqual(expectedPhonetic, flag!.PhoneticName);
    }

    [TestMethod]
    public void Letters_AllHaveMeaningKey_FollowingConvention()
    {
        foreach (var flag in SignalFlagsAlphabet.Letters)
        {
            Assert.AreEqual($"SignalFlagsMeaning{flag.Symbol}", flag.MeaningKey);
        }
    }

    [TestMethod]
    public void Numerals_HaveNoMeaningKey_NumeralsCarryNoSingleFlagSignal()
    {
        foreach (var flag in SignalFlagsAlphabet.Numerals)
        {
            Assert.IsNull(flag.MeaningKey);
        }
    }

    [TestMethod]
    public void Specials_AllHaveCaptionAndMeaningKeys()
    {
        foreach (var flag in SignalFlagsAlphabet.Specials)
        {
            Assert.IsNotNull(flag.CaptionKey, $"{flag.Id} caption");
            Assert.IsNotNull(flag.MeaningKey, $"{flag.Id} meaning");
        }
    }

    [TestMethod]
    public void All_EveryFlag_HasValidOutlineAndShapesWithinBounds()
    {
        const double epsilon = 0.0001;

        foreach (var flag in SignalFlagsAlphabet.All)
        {
            Assert.IsTrue(flag.AspectRatio > 0, $"{flag.Id} aspect");
            Assert.IsTrue(flag.Outline.Count >= 3, $"{flag.Id} outline point count");
            Assert.IsTrue(flag.Shapes.Count >= 1, $"{flag.Id} shape count");

            foreach (var shape in flag.Shapes)
            {
                switch (shape)
                {
                    case SignalFlagPolygon polygon:
                        Assert.IsTrue(polygon.Points.Count >= 3, $"{flag.Id} polygon point count");
                        foreach (var point in polygon.Points)
                        {
                            Assert.IsTrue(point.X >= -epsilon && point.X <= flag.AspectRatio + epsilon, $"{flag.Id} polygon X {point.X}");
                            Assert.IsTrue(point.Y >= -epsilon && point.Y <= 1 + epsilon, $"{flag.Id} polygon Y {point.Y}");
                        }

                        break;

                    case SignalFlagCircle circle:
                        Assert.IsTrue(circle.Radius > 0, $"{flag.Id} circle radius");
                        Assert.IsTrue(circle.CenterX - circle.Radius >= -epsilon, $"{flag.Id} circle left");
                        Assert.IsTrue(circle.CenterX + circle.Radius <= flag.AspectRatio + epsilon, $"{flag.Id} circle right");
                        Assert.IsTrue(circle.CenterY - circle.Radius >= -epsilon, $"{flag.Id} circle top");
                        Assert.IsTrue(circle.CenterY + circle.Radius <= 1 + epsilon, $"{flag.Id} circle bottom");
                        break;

                    default:
                        Assert.Fail($"{flag.Id}: unexpected shape type {shape.GetType().Name}");
                        break;
                }
            }
        }
    }

    [TestMethod]
    public void Letters_SquareFlags_HaveUnitAspectRatio()
    {
        foreach (var flag in SignalFlagsAlphabet.Letters)
        {
            Assert.AreEqual(1.0, flag.AspectRatio, 0.0001, $"{flag.Id}");
        }
    }

    [TestMethod]
    public void Numerals_Pennants_AreWiderThanTall()
    {
        foreach (var flag in SignalFlagsAlphabet.Numerals)
        {
            Assert.IsTrue(flag.AspectRatio > 1.5, $"{flag.Id} aspect {flag.AspectRatio}");
        }
    }

    [TestMethod]
    public void TryGet_AlfaFlag_HasSwallowtailOutline()
    {
        // The Alfa (and Bravo) flag is swallowtailed: a square with a notch cut from the fly edge.
        SignalFlagsAlphabet.TryGet('A', out var flag);

        Assert.AreEqual(5, flag!.Outline.Count);
    }

    [TestMethod]
    public void TryGet_NovemberFlag_HasCheckerboardShapes()
    {
        // 4x4 chequy: a white field plus the 8 blue checks.
        SignalFlagsAlphabet.TryGet('N', out var flag);

        var blueShapes = flag!.Shapes.Count(s => s.Color == SignalFlagColor.Blue);
        Assert.AreEqual(8, blueShapes);
    }

    [TestMethod]
    public void TryGet_YankeeFlag_HasFiveYellowDiagonalStripes()
    {
        SignalFlagsAlphabet.TryGet('Y', out var flag);

        var yellowStripes = flag!.Shapes.Count(s => s.Color == SignalFlagColor.Yellow);
        Assert.AreEqual(5, yellowStripes);
    }
}
