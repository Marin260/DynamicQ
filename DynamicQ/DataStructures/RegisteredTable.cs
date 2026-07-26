namespace DynamicQuery.DataStructures;

/// <summary>
/// Used to register supported tables for dynamic joins and select, if a table isn't registered
/// it will not be possible to dynamically join it
/// </summary>
/// <param name="TableType">Type of the table that for inclusion in DynamicQ</param>
/// <param name="VirtualNavigationName">Name of the navigation property in a nested class</param>
/// <param name="PathToTable">Virtual path to reach the registered table</param>
/// <param name="ExcludedColumns">List of columns to exclude from selection</param>
public sealed record RegisteredTable(
    Type TableType,
    string VirtualNavigationName,
    string PathToTable,
    HashSet<string>? ExcludedColumns
    );

/// <summary>
/// Used to register default tables to join. Used For "lookup" tables, if a FK is selected
/// the table will be joined automatically and the value will be selected.
/// </summary>
/// <param name="TableType">Type of the default table to join</param>
/// <param name="TriggerColumn">Column to watch for join</param>
/// <param name="FieldsToAppend">Column to select from joined table</param>
/// <example>
/// // if the user table is marked for selection with the RoleId columns,
/// // the Role table will be joined automatically and RoleName selected instead.
/// new(
///     typeof(Role)
///     "RoleId",
///     ["RoleValue"]
/// )
/// </example>
public sealed record DefaultTable(
    Type TableType,
    string TriggerColumn,
    List<string> FieldsToAppend
    );