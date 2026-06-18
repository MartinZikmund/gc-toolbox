using System.Collections.Generic;
using GcToolkit.Core.Alphabets;

namespace GcToolkit.Controls;

using Point = (double X, double Y);
using Segment = (string Label, (double X, double Y)[] Points);

/// <summary>
/// Vector geometry for the segment families, authored on a 120×200 logical canvas. Each segment is a
/// beveled bar (hexagon) or thin diagonal so the rendered glyph reads like a real LCD/starburst display.
/// The Core codec owns which segments are <i>lit</i>; this owns where they <i>are</i>. The 9-/14-/16-segment
/// layouts share the 7-segment outer bars and add the centre verticals and diagonals.
/// </summary>
internal static class SegmentGeometry
{
    // Outer frame and bar thickness.
    private const double Left = 22;
    private const double Right = 98;
    private const double Top = 16;
    private const double Bottom = 184;
    private const double MidY = 100;
    private const double T = 11;   // half-thickness of a bar
    private const double Bevel = 9; // 45° corner cut

    // ---- 7-segment bars (shared by every family) ----

    // Horizontal bar centred on yCenter spanning x1..x2 (with beveled ends).
    private static Point[] HBar(double x1, double x2, double y) =>
    [
        (x1 + Bevel, y - T), (x2 - Bevel, y - T), (x2, y),
        (x2 - Bevel, y + T), (x1 + Bevel, y + T), (x1, y),
    ];

    // Vertical bar centred on x spanning y1..y2 (with beveled ends).
    private static Point[] VBar(double x, double y1, double y2) =>
    [
        (x - T, y1 + Bevel), (x, y1), (x + T, y1 + Bevel),
        (x + T, y2 - Bevel), (x, y2), (x - T, y2 - Bevel),
    ];

    // A thin diagonal strip from p1 to p2 (rendered as a slim quad).
    private static Point[] Diagonal(double x1, double y1, double x2, double y2)
    {
        const double w = 7; // half-width across the diagonal
        // Perpendicular offset (rough; the diagonals are short so a fixed offset reads fine).
        var dx = x2 - x1;
        var dy = y2 - y1;
        var len = System.Math.Sqrt((dx * dx) + (dy * dy));
        var ox = -dy / len * w;
        var oy = dx / len * w;
        return
        [
            (x1 + ox, y1 + oy), (x2 + ox, y2 + oy),
            (x2 - ox, y2 - oy), (x1 - ox, y1 - oy),
        ];
    }

    private static readonly double CenterX = (Left + Right) / 2;

    private static readonly Segment SegA = ("a", HBar(Left, Right, Top));
    private static readonly Segment SegB = ("b", VBar(Right, Top, MidY));
    private static readonly Segment SegC = ("c", VBar(Right, MidY, Bottom));
    private static readonly Segment SegD = ("d", HBar(Left, Right, Bottom));
    private static readonly Segment SegE = ("e", VBar(Left, MidY, Bottom));
    private static readonly Segment SegF = ("f", VBar(Left, Top, MidY));
    private static readonly Segment SegG = ("g", HBar(Left, Right, MidY));

    // 14/16-segment split middle and centre verticals + diagonals.
    private static readonly Segment SegG1 = ("g1", HBar(Left, CenterX, MidY));
    private static readonly Segment SegG2 = ("g2", HBar(CenterX, Right, MidY));
    private static readonly Segment SegI = ("i", VBar(CenterX, Top, MidY));
    private static readonly Segment SegL = ("l", VBar(CenterX, MidY, Bottom));
    private static readonly Segment SegH = ("h", Diagonal(Left + T, Top + T, CenterX - T, MidY - T));
    private static readonly Segment SegJ = ("j", Diagonal(Right - T, Top + T, CenterX + T, MidY - T));
    private static readonly Segment SegK = ("k", Diagonal(Left + T, Bottom - T, CenterX - T, MidY + T));
    private static readonly Segment SegM = ("m", Diagonal(Right - T, Bottom - T, CenterX + T, MidY + T));

    // 9-segment diagonals (upper-left ╲, upper-right ╱) — only the top half.
    private static readonly Segment SegH9 = ("h", Diagonal(Left + T, Top + T, CenterX - T, MidY - T));
    private static readonly Segment SegI9 = ("i", Diagonal(Right - T, Top + T, CenterX + T, MidY - T));

    // 16-segment split top/bottom bars.
    private static readonly Segment SegA1 = ("a1", HBar(Left, CenterX, Top));
    private static readonly Segment SegA2 = ("a2", HBar(CenterX, Right, Top));
    private static readonly Segment SegD1 = ("d1", HBar(Left, CenterX, Bottom));
    private static readonly Segment SegD2 = ("d2", HBar(CenterX, Right, Bottom));

    private static readonly Segment[] Seven = [SegA, SegB, SegC, SegD, SegE, SegF, SegG];

    private static readonly Segment[] Nine = [SegA, SegB, SegC, SegD, SegE, SegF, SegG, SegH9, SegI9];

    private static readonly Segment[] Fourteen =
        [SegA, SegB, SegC, SegD, SegE, SegF, SegG1, SegG2, SegH, SegI, SegJ, SegK, SegL, SegM];

    private static readonly Segment[] Sixteen =
        [SegA1, SegA2, SegB, SegC, SegD1, SegD2, SegE, SegF, SegG1, SegG2, SegH, SegI, SegJ, SegK, SegL, SegM];

    public static IReadOnlyList<Segment> For(SegmentDisplayType type) => type switch
    {
        SegmentDisplayType.SevenSegment => Seven,
        SegmentDisplayType.NineSegment => Nine,
        SegmentDisplayType.SixteenSegment => Sixteen,
        _ => Fourteen,
    };
}
