namespace DynamicQuery.Models;

/// <summary>
/// Selected fields for a table
/// </summary>
public sealed class DynamicQSelectedFields
{
    /// <summary>
    /// Path to the table
    /// </summary>
    public required string PathToTable { get; set; }
    /// <summary>
    /// Selected fields for the table
    /// </summary>
    public List<string> SelectedTableColumns { get; set; } = [];
}