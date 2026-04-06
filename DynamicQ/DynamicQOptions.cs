using DynamicQ.DataStructures;

namespace DynamicQ;

/// <summary>
/// Configuration for DynamicQ: which entities and navigations are allowed, plus optional default lookup joins.
/// </summary>
public class DynamicQOptions
{
    /// <summary>Tables and virtual navigation metadata used to resolve includes and projections.</summary>
    public HashSet<RegisteredTable> RegisteredTables { get; private set; } = [];
    /// <summary>Default tables to join when specific foreign-key columns are selected.</summary>
    public HashSet<DefaultTables> DefaultTables { get; set; } = [];
}