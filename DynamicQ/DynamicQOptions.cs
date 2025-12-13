using DynamicQ.DataStructures;

namespace DynamicQ;

public class DynamicQOptions
{
    public HashSet<RegisteredTable> RegisteredTables { get; set; } = [];
    public HashSet<DefaultTables> DefaultTables { get; set; } = [];
}