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

    /// <summary>
    /// Materializes <paramref name="entityValues"/> into rows and columns derived from <paramref name="tableTree"/>.
    /// </summary>
    public DataTable FlattenToDataTable<TEntity>(
        IEnumerable<TEntity> entityValues,
        DynamicQTableTree tableTree
    ) where TEntity : class
    {
        var table = new DataTable(typeof(TEntity).Name);
        var entityType = typeof(TEntity);

        // 1. Generate table columns
        var headers = GetDataTableHeaders(entityType, tableTree);
        table.Columns.AddRange([.. headers]);

        IEnumerable<object?> startingRow = [];
        foreach (var entry in entityValues)
        {
            var expandedEntities = ExpandRowWithRelationshipObject(entry, startingRow, tableTree);
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
        var startNode = tableTree.JoinNodes
            .FirstOrDefault(x => !x.MinimalIncludePath.Any());

        var startTableProperties = startNode?.SelectedTableColumns;

        if (startTableProperties != null)
        {
            var selectedProps = entityProperties.Where(x => startTableProperties.Contains(x.Name));
            var dataColumns = selectedProps.Select(x => new DataColumn($"{startTable}_{x.Name}", x.ResolveDataColumnType() ?? x.PropertyType));
            table.AddRange(dataColumns);
        }

        // 1. group paths by next table
        var groupedNodes = tableTree.JoinNodes
            .Where(x => x.MinimalIncludePath.Any())
            .GroupBy(x => x.MinimalIncludePath.First());

        var classNodes = groupedNodes.Where(node => !NodeIsCollection(entityType, node.Key));
        var collectionNodes = groupedNodes.Where(node => NodeIsCollection(entityType, node.Key));

        // 2. Must first traverse non collection base navigation props
        foreach (var node in classNodes)
        {
            var nestedNodeType = Options.RegisteredTables.GetTypeByNavigationName(node.Key);
            var nestedDynamicQTableTree = node.GenerateSubTree();
            AppendTableHeaders(nestedNodeType, nestedDynamicQTableTree, table);
        }

        // 3. Traverse collection base navigation props
        foreach (var node in collectionNodes)
        {
            var nestedNodeType = Options.RegisteredTables.GetTypeByNavigationName(node.Key);
            var nestedDynamicQTableTree = node.GenerateSubTree();
            AppendTableHeaders(nestedNodeType, nestedDynamicQTableTree, table);
        }
    }

    /// <summary>
    /// Expands <paramref name="entity"/> into one or more row sequences for one-to-one navigation branches.
    /// </summary>
    private IEnumerable<IEnumerable<object?>> ExpandRowWithRelationshipObject<TEntity>(
        TEntity? entity,
        IEnumerable<object?> entryRow,
        DynamicQTableTree tableTree
    ) where TEntity : class
    {
        var entityType = typeof(TEntity);
        var entityProperties = entityType.GetProperties();

        var rows = InitializeRows(entity, entryRow, tableTree, entityProperties);

        // 1. group paths by next table
        var groupedNodes = GroupExportNodes(tableTree);

        // 2. separate grouped nodes in 2 groups, Collection based and Class based navigation based types
        //    Note: shouldn't really matter which type of navigation it is, but it's easier to track this way
        var classNodes = groupedNodes.Where(node => !NodeIsCollection(entityType, node.Key));
        var collectionNodes = groupedNodes.Where(node => NodeIsCollection(entityType, node.Key));

        // 3. First traverse non collection based navigation props
        rows = TraverseClassNodes(entityType, entity, rows, classNodes);

        // 4. Then traverse collection based navigation props
        rows = TraverseCollectionNodes(entityType, entity, rows, collectionNodes);

        return rows;
    }

    private static bool NodeIsCollection(Type? tableType, string tableName)
    {
        var property = tableType?.GetProperty(tableName);
        // TODO: check if this actually does what we expect it to do -> should check if IsCollection()
        return typeof(System.Collections.IEnumerable).IsAssignableFrom(property?.PropertyType);
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

    private object InvokeGenericExpandRows(
        Type typeForGenericInvoke,
        object? entity,
        IEnumerable<object?> entryRow,
        DynamicQTableTree tableTree
    )
    {
        var methodInfo = GetType().GetMethod(nameof(ExpandRowWithRelationshipObject), BindingFlags.NonPublic | BindingFlags.IgnoreCase | BindingFlags.Instance);
        var genericMethod = methodInfo?.MakeGenericMethod(typeForGenericInvoke);

        var result = genericMethod?.Invoke(this, [entity, entryRow, tableTree]);

        return result ?? new object();
    }

    private static IEnumerable<IEnumerable<object?>> InitializeRows<TEntity>(
        TEntity? entity,
        IEnumerable<object?> entryRow,
        DynamicQTableTree tableTree,
        PropertyInfo[] entityProperties
    ) where TEntity : class
    {
        var startNode = tableTree.JoinNodes.FirstOrDefault(x => !x.MinimalIncludePath.Any());
        var startTableProperties = startNode?.SelectedTableColumns;

        if (startTableProperties == null)
        {
            return [entryRow];
        }

        var selectedProps = entityProperties.Where(x => startTableProperties.Contains(x.Name));
        var newRow = entryRow.Concat(selectedProps.Select(x => GetValueOrDefault(x, entity)));
        return [newRow];
    }

    private static IEnumerable<IGrouping<string, DynamicQNode>> GroupExportNodes(DynamicQTableTree tableTree)
        => tableTree.JoinNodes
            .Where(x => x.MinimalIncludePath.Any())
            .GroupBy(x => x.MinimalIncludePath.First());

    private IEnumerable<IEnumerable<object?>> TraverseClassNodes<TEntity>(
        Type entityType,
        TEntity? entity,
        IEnumerable<IEnumerable<object?>> rows,
        IEnumerable<IGrouping<string, DynamicQNode>> classNodes
    ) where TEntity : class
    {
        foreach (var node in classNodes)
        {
            var tmpRows = new List<IEnumerable<object?>>();
            var childNavigationProperty = entityType.GetProperty(node.Key);
            var nestedNodeType = Options.RegisteredTables.GetTypeByNavigationName(node.Key);
            var nestedDynamicQTableTree = node.GenerateSubTree();
            var nestedEntity = childNavigationProperty?.GetValue(entity);

            foreach (var row in rows)
            {
                if (nestedEntity == null && nestedNodeType != null)
                {
                    nestedEntity = Activator.CreateInstance(nestedNodeType);
                }

                if (nestedNodeType != null &&
                    InvokeGenericExpandRows(nestedNodeType, nestedEntity, row, nestedDynamicQTableTree)
                        is IEnumerable<IEnumerable<object?>> expandedRow)
                {
                    tmpRows.AddRange(expandedRow);
                }
            }

            rows = tmpRows;
        }

        return rows;
    }

    private IEnumerable<IEnumerable<object?>> TraverseCollectionNodes<TEntity>(
        Type entityType,
        TEntity? entity,
        IEnumerable<IEnumerable<object?>> rows,
        IEnumerable<IGrouping<string, DynamicQNode>> collectionNodes
    ) where TEntity : class
    {
        foreach (var node in collectionNodes)
        {
            var tmpRows = new List<IEnumerable<object?>>();
            var childNavigationProperty = entityType.GetProperty(node.Key);
            var nestedNodeType = Options.RegisteredTables.GetTypeByNavigationName(node.Key);
            var nestedDynamicQTableTree = node.GenerateSubTree();

            if (nestedNodeType != null &&
                childNavigationProperty?.GetValue(entity) is IEnumerable<object?> nestedEntityCollection)
            {
                if (!nestedEntityCollection.Any())
                {
                    nestedEntityCollection = [Activator.CreateInstance(nestedNodeType)];
                }

                foreach (var entityElement in nestedEntityCollection)
                {
                    foreach (var row in rows)
                    {
                        if (InvokeGenericExpandRows(nestedNodeType, entityElement, row, nestedDynamicQTableTree)
                            is IEnumerable<IEnumerable<object?>> expandedRow)
                        {
                            tmpRows.AddRange(expandedRow);
                        }
                    }
                }

                rows = tmpRows;
            }
        }

        return rows;
    }
}