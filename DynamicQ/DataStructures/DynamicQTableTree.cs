namespace DynamicQ.DataStructures;

public class DynamicQTableTree
{
    public string? StartingTable { get; private set; }
    public List<DynamicQNode> JoinNodes { get; private set; } = [];
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

public class DynamicQNode
{
    public required Type TableType { get; init; }
    public IEnumerable<string> SelectedTableColumns { get; init; } = [];
    public IEnumerable<string> MinimalIncludePath { get; init; } = [];
    public string? OriginalIncludePath { get; init; }
}