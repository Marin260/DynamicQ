namespace DynamicQ.DataStructures;

/// <summary>
/// Describes the root table and join nodes used to build a dynamic query or <see cref="System.Data.DataTable"/> shape.
/// </summary>
public class DynamicQTableTree
{
    /// <summary>Table name or key for the subtree root.</summary>
    public string? StartingTable { get; private set; }
    /// <summary>Nodes participating in includes and column selection for this level.</summary>
    public List<DynamicQNode> JoinNodes { get; private set; } = [];
    /// <summary>
    /// Builds a subtree for one grouped navigation segment (first segment stripped from each node's path).
    /// </summary>
    /// <param name="nodeGroup">Nodes sharing the same first path segment, keyed by that segment.</param>
    public static DynamicQTableTree GenerateSubTree(IGrouping<string, DynamicQNode> nodeGroup)
    {
        var nestedTableTree = new DynamicQTableTree()
        {
            StartingTable = nodeGroup.Key,
            JoinNodes = [.. nodeGroup.Select(node => new DynamicQNode
                {
                    TableType = node.TableType,
                    SelectedTableColumns = node.SelectedTableColumns,
                    MinimalIncludePath = node.MinimalIncludePath.Skip(1),
                    OriginalIncludePath = node.OriginalIncludePath
                })]
        };
        return nestedTableTree;
    }
}

/// <summary>
/// One node in a <see cref="DynamicQTableTree"/>: entity type, columns to project, and include path fragments.
/// </summary>
public class DynamicQNode
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