using Microsoft.Extensions.Options;
using DynamicQuery.Extensions;
using DynamicQuery.Models;

namespace DynamicQuery.DataStructures;

/// <summary>
/// Describes the root table and join nodes used to build a dynamic query or <see cref="System.Data.DataTable"/> shape.
/// </summary>
public sealed class DynamicQTableTree
{
    /// <summary>Table name or key for the subtree root.</summary>
    public string? StartingTable { get; set; }
    /// <summary>Nodes participating in includes and column selection for this level.</summary>
    public List<DynamicQNode> JoinNodes { get; set; } = [];
}

/// <summary>
/// One node in a <see cref="DynamicQTableTree"/>: entity type, columns to project, and include path fragments.
/// </summary>
public sealed class DynamicQNode
{
    /// <summary>CLR type of the entity at this node.</summary>
    public required Type TableType { get; init; }
    /// <summary>Property names to include in the projection.</summary>
    public IEnumerable<string> SelectedTableColumns { get; init; } = [];
    /// <summary>Remaining navigation segments for EF includes (minimal path).</summary>
    public IEnumerable<string> MinimalIncludePath { get; init; } = [];
    /// <summary>Original include path before normalization.</summary>
    public string? OriginalIncludePath { get; init; }
}