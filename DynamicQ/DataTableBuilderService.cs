using System.Data;
using DynamicQuery.DataStructures;
using DynamicQuery.Extensions;
using Microsoft.Extensions.Options;

namespace DynamicQuery;

/// <summary>
/// Flattens entity graphs into a <see cref="DataTable"/> using the same table tree semantics as dynamic queries.
/// </summary>
/// <param name="options">Registered tables and defaults from configuration.</param>
public sealed class DataTableBuilderService(IOptions<DynamicQOptions> options)
{
    private DynamicQOptions Options { get; } = options.Value;
    private static readonly BindingFlags DefaultBindingFlags = BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance;

    /// <summary>
    /// Materializes <paramref name="entityValues"/> into rows and columns derived from <paramref name="tableTree"/>.
    /// </summary>
    public DataTable FlattenToDataTable<TEntity>(
        IEnumerable<TEntity> entityValues,
        DynamicQTableTree tableTree
    ) where TEntity : class
    {
        tableTree.Build();

        var table = new DataTable(typeof(TEntity).Name);
        var entityType = typeof(TEntity);

        // 1. Generate table columns
        var headers = GetDataTableHeaders(entityType, tableTree);
        table.Columns.AddRange([.. headers]);

        IEnumerable<object?> startingRow = [];
        foreach (var entry in entityValues)
        {
            var expandedEntities = ExpandRows(entry, startingRow, tableTree);
            foreach (var expandedEntity in expandedEntities)
            {
                var materializedRow = expandedEntity.ToArray();
                _ = table.Rows.Add(materializedRow);
            }
        }

        return table;
    }

    private List<DataColumn> GetDataTableHeaders(Type entityType, DynamicQTableTree tableTree)
    {
        // 1. Generate table columns
        var headers = new List<DataColumn>();
        AppendTableHeaders(entityType, tableTree, headers);

        headers = [.. headers.Select((header, idx) =>
        {
            header.ColumnName = $"{idx + 1}.{header.ColumnName}";
            return header;
        })];

        return headers;
    }

    private void AppendTableHeaders(Type? entityType, DynamicQTableTree tableTree, List<DataColumn> table)
    {
        if (entityType == null)
        {
            return;
        }

        var entityProperties = entityType.GetProperties();
        var startTable = tableTree.StartingTable;
        var startTableProperties = tableTree.SelectedColumns;

        if (startTableProperties.Any())
        {
            var selectedProps = entityProperties.Where(x => startTableProperties.Contains(x.Name));
            var dataColumns = selectedProps.Select(x => new DataColumn($"{startTable}_{x.Name}", x.ResolveDataColumnType() ?? x.PropertyType));
            table.AddRange(dataColumns);
        }

        // Traverse child subtrees in a single pass; header order matches row order.
        foreach (var child in tableTree.Children)
        {
            var nestedNodeType = child.TableType ?? Options.RegisteredTables.GetTypeByNavigationName(child.StartingTable!);
            AppendTableHeaders(nestedNodeType, child, table);
        }
    }

    /// <summary>
    /// Expands <paramref name="entity"/> into one or more row sequences by traversing its navigation branches.
    /// </summary>
    private IEnumerable<IEnumerable<object?>> ExpandRows<TEntity>(
        TEntity? entity,
        IEnumerable<object?> entryRow,
        DynamicQTableTree tableTree
    ) where TEntity : class
    {
        var entityType = typeof(TEntity);
        var entityProperties = entityType.GetProperties();

        var rows = InitializeRows(entity, entryRow, tableTree, entityProperties);

        // Traverse every child subtree in a single pass. A class navigation is just a
        // one-element collection, so both cases share the same expansion logic and the
        // resulting column order matches AppendTableHeaders.
        foreach (var child in tableTree.Children)
        {
            rows = ExpandChild(entityType, entity, rows, child);
        }

        return rows;
    }

    private IEnumerable<IEnumerable<object?>> ExpandChild<TEntity>(
        Type entityType,
        TEntity? entity,
        IEnumerable<IEnumerable<object?>> rows,
        DynamicQTableTree child
    ) where TEntity : class
    {
        var navigationName = child.StartingTable!;
        var nestedNodeType = child.TableType ?? Options.RegisteredTables.GetTypeByNavigationName(navigationName);
        if (nestedNodeType == null)
        {
            return rows;
        }

        var childNavigationProperty = entityType.GetProperty(navigationName, DefaultBindingFlags);
        var nestedEntities = GetNestedEntities(childNavigationProperty, entity, nestedNodeType);

        var expandedRows = new List<IEnumerable<object?>>();
        foreach (var nestedEntity in nestedEntities)
        {
            foreach (var row in rows)
            {
                if (InvokeGenericExpandRows(nestedNodeType, nestedEntity, row, child)
                    is IEnumerable<IEnumerable<object?>> expandedRow)
                {
                    expandedRows.AddRange(expandedRow);
                }
            }
        }

        return expandedRows;
    }

    /// <summary>
    /// Resolves a navigation value into the sequence of nested entities to expand: collection
    /// navigations yield their elements (a single default when empty), class navigations yield a
    /// single element (a default when null).
    /// </summary>
    private static IEnumerable<object?> GetNestedEntities(PropertyInfo? navigationProperty, object? entity, Type nestedNodeType)
    {
        var value = navigationProperty?.GetValue(entity);

        if (value is IEnumerable<object?> collection)
        {
            return collection.Any() ? collection : [Activator.CreateInstance(nestedNodeType)];
        }

        return [value ?? Activator.CreateInstance(nestedNodeType)];
    }

    private static object? GetValueOrDefault(PropertyInfo propertyInfo, object? entity)
    {
        if (entity == null)
        {
            return GetDefaultValue(propertyInfo.PropertyType);
        }

        var value = propertyInfo.GetValue(entity, null);

        if (value == null)
        {
            // Return default value for the property type
            return GetDefaultValue(propertyInfo.PropertyType);
        }

        var underlyingType = Nullable.GetUnderlyingType(propertyInfo.PropertyType);
        if (underlyingType is not null && underlyingType.IsEnum)
        {
            return ((Enum)value).GetDisplayName();
        }

        return value;
    }

    private static object? GetDefaultValue(Type type)
    {
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }

    private object? InvokeGenericExpandRows(
        Type typeForGenericInvoke,
        object? entity,
        IEnumerable<object?> entryRow,
        DynamicQTableTree tableTree
    )
    {
        var methodInfo = GetType().GetMethod(nameof(ExpandRows), BindingFlags.NonPublic | BindingFlags.IgnoreCase | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Unable to resolve method '{nameof(ExpandRows)}' for dynamic invocation.");

        return methodInfo.MakeGenericMethod(typeForGenericInvoke).Invoke(this, [entity, entryRow, tableTree]);
    }

    private static IEnumerable<IEnumerable<object?>> InitializeRows<TEntity>(
        TEntity? entity,
        IEnumerable<object?> entryRow,
        DynamicQTableTree tableTree,
        PropertyInfo[] entityProperties
    ) where TEntity : class
    {
        var startTableProperties = tableTree.SelectedColumns;

        if (!startTableProperties.Any())
        {
            return [entryRow];
        }

        var selectedProps = entityProperties.Where(x => startTableProperties.Contains(x.Name));
        var newRow = entryRow.Concat(selectedProps.Select(x => GetValueOrDefault(x, entity)));
        return [newRow];
    }
}