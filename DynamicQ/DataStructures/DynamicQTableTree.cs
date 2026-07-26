namespace DynamicQuery.DataStructures;

/// <summary>
/// A node in the table hierarchy used to build a dynamic query or <see cref="System.Data.DataTable"/> shape.
/// The flat <see cref="JoinNodes"/> are the public construction input; <see cref="Build"/> folds them into
/// the recursive <see cref="Children"/> hierarchy that every consumer traverses.
/// </summary>
public sealed class DynamicQTableTree
{
    /// <summary>Navigation name of this node (also the member name bound on the parent). Root uses the collapsed table name.</summary>
    public string? StartingTable { get; set; }
    /// <summary>Flat construction input: one entry per requested table, each carrying its include path.</summary>
    public List<DynamicQNode> JoinNodes { get; set; } = [];

    /// <summary>CLR type of the entity at this node.</summary>
    public Type? TableType { get; set; }
    /// <summary>Property names to project at this node.</summary>
    public IEnumerable<string> SelectedColumns { get; set; } = [];
    /// <summary>Original (pre-collapse) include path for this node, used to disambiguate the navigation accessor.</summary>
    public string? OriginalIncludePath { get; set; }
    /// <summary>Child nodes keyed by navigation name.</summary>
    public List<DynamicQTableTree> Children { get; set; } = [];

    /// <summary>
    /// Folds <see cref="JoinNodes"/> into the <see cref="Children"/> hierarchy via a lookup-or-create per path
    /// segment (no grouping). Idempotent: existing children are cleared and rebuilt.
    /// </summary>
    public DynamicQTableTree Build()
    {
        Children.Clear();

        var root = JoinNodes.FirstOrDefault(n => !n.MinimalIncludePath.Any());
        TableType = root?.TableType;
        SelectedColumns = root?.SelectedTableColumns ?? [];
        OriginalIncludePath = root?.OriginalIncludePath;

        foreach (var node in JoinNodes.Where(n => n.MinimalIncludePath.Any()))
        {
            var current = this;
            foreach (var segment in node.MinimalIncludePath)
            {
                var next = current.Children.FirstOrDefault(c => c.StartingTable == segment);
                if (next == null)
                {
                    next = new DynamicQTableTree { StartingTable = segment };
                    current.Children.Add(next);
                }
                current = next;
            }

            current.TableType = node.TableType;
            current.SelectedColumns = node.SelectedTableColumns;
            current.OriginalIncludePath = node.OriginalIncludePath;
        }

        return this;
    }
}

/// <summary>
/// One entry in a <see cref="DynamicQTableTree.JoinNodes"/> construction input: entity type, columns to project,
/// and include path fragments.
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
