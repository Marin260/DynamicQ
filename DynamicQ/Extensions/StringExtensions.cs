namespace DynamicQ.Extensions;

/// <summary>
/// String helpers used by DynamicQ.
/// </summary>
public static class StringExtensions
{
    /// <summary>True if <paramref name="s"/> is null or contains only whitespace.</summary>
    public static bool IsNullOrWhiteSpace(this string? s)
    {
        return s == null || s.Trim().Length == 0;
    }
}