using DynamicQuery.DataStructures;

namespace DynamicQuery.Extensions;

internal static class DynamicQTableTreeExtensions
{
    internal static Type? ResolveDataColumnType(this PropertyInfo propertyInfo)
    {
        var underlyingType = Nullable.GetUnderlyingType(propertyInfo.PropertyType) ?? propertyInfo.PropertyType;
        return underlyingType.IsEnum ? typeof(string) : underlyingType;
    }

    /// <summary>
    /// Builds a subtree for one grouped navigation segment (first segment stripped from each node's path).
    /// </summary>
    /// <param name="nodeGroup">Nodes sharing the same first path segment, keyed by that segment.</param>
    internal static DynamicQTableTree GenerateSubTree(this IGrouping<string, DynamicQNode> nodeGroup)
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

    /// <summary>
    /// For a given Table structure tree returns the minimal tree that reaches all of the tables
    /// </summary>
    /// <param name="sourceTree">Tree structure with starting table and nodes</param>
    /// <returns>Minimal subtree that reaches all of the requested tables</returns>
    internal static DynamicQTableTree CreateMinimalIncludeTableTree(this DynamicQTableTree sourceTree)
    {
        var result = new DynamicQTableTree();
        var firstElementInPath = sourceTree.JoinNodes.FirstOrDefault()?.MinimalIncludePath.FirstOrDefault();

        // If no path path is provided
        if (firstElementInPath == null && sourceTree.StartingTable == null)
        {
            return result;
        }

        var shortestPathToNode = sourceTree.JoinNodes.Min(x => x.MinimalIncludePath.Count());
        for (var i = 0; i < shortestPathToNode; i++)
        {
            var compareElement = sourceTree.JoinNodes.FirstOrDefault()?.MinimalIncludePath.ElementAt(i);
            var firstElementsInPathAreEqual = sourceTree.JoinNodes
            .Select(x => x.MinimalIncludePath)
            .All(x => x.ElementAt(i) == compareElement!);

            if (firstElementsInPathAreEqual)
            {
                result.StartingTable = compareElement;
                result.JoinNodes = [.. sourceTree.JoinNodes
                .Select(x => new DynamicQNode
                {
                    SelectedTableColumns = x.SelectedTableColumns,
                    OriginalIncludePath = x.OriginalIncludePath,
                    MinimalIncludePath = x.MinimalIncludePath.Skip(i+1),
                    TableType = x.TableType
                })];
            }
            else
            {
                break;
            }
        }

        return result;
    }
}