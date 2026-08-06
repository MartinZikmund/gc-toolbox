namespace GcToolkit.Core.Numbers;

/// <summary>One occurrence of a searched digit sequence, with surrounding context digits.</summary>
public sealed record EulerNumberMatch(int Position, string Before, string Match, string After);

/// <summary>Result of a digit-sequence search: every hit is counted, matches may be truncated.</summary>
public sealed record EulerNumberSearchResult(int TotalCount, IReadOnlyList<EulerNumberMatch> Matches);
