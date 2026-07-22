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

    private IReadOnlyList<DynamicQTableTreeChild>? _children;

    /// <summary>
    /// Child subtrees grouped by their next navigation segment, computed once and cached.
    /// Reusing the cached subtree instances lets the whole hierarchy materialize a single time
    /// even when traversal is repeated per entity/row.
    /// </summary>
    internal IReadOnlyList<DynamicQTableTreeChild> Children =>
        _children ??= [.. this.GroupByNextSegment()
            .Select(group => new DynamicQTableTreeChild(group.Key, group.GenerateSubTree()))];
}

/// <summary>
/// A single grouped child of a <see cref="DynamicQTableTree"/>: the navigation segment and its subtree.
/// </summary>
internal sealed record DynamicQTableTreeChild(string NavigationKey, DynamicQTableTree Subtree);

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