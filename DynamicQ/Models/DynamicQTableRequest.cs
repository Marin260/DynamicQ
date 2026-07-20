namespace DynamicQuery.Models;

/// <summary>
/// Request for exporting custom data
/// </summary>
public sealed class DynamicQTableRequest
{
    /// <summary>
    /// Table selection for the request
    /// </summary>
    public List<DynamicQSelectedFields> TableSelection { get; set; } = [];
}