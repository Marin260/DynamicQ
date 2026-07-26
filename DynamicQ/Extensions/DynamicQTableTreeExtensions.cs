using DynamicQuery.DataStructures;

namespace DynamicQuery.Extensions;

internal static class DynamicQTableTreeExtensions
{
    /// <summary>
    /// For a given Table structure tree returns the minimal tree that reaches all of the tables
    /// </summary>
    /// <param name="sourceTree">Tree structure with starting table and nodes</param>
    /// <returns>Minimal subtree that reaches all of the requested tables</returns>
    internal static DynamicQTableTree CreateMinimalIncludeTableTree(this DynamicQTableTree sourceTree)
    {
        var result = new DynamicQTableTree();
        var firstElementInPath = sourceTree.JoinNodes.FirstOrDefault()?.MinimalIncludePath.FirstOrDefault();

        // If no path is provided
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