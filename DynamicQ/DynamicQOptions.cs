using DynamicQuery.DataStructures;

namespace DynamicQuery;

/// <summary>
/// Configuration for DynamicQ: which entities and navigations are allowed, plus optional default lookup joins.
/// </summary>
public sealed class DynamicQOptions
{
    /// <summary>Tables and virtual navigation metadata used to resolve includes and projections.</summary>
    public HashSet<RegisteredTable> RegisteredTables { get; } = [];
    /// <summary>Default tables to join when specific foreign-key columns are selected.</summary>
    public HashSet<DefaultTable> DefaultTables { get; } = []; // Not yet implemented
}