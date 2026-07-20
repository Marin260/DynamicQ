using DynamicQuery.DataStructures;
using DynamicQuery.Extensions;
using Microsoft.Extensions.Options;
using DynamicQuery.Models;
using Microsoft.EntityFrameworkCore;

namespace DynamicQuery;

/// <summary>
/// Builds dynamic EF Core <see cref="IQueryable{T}"/> projections from a <see cref="DataStructures.DynamicQTableTree"/>.
/// </summary>
/// <remarks>
/// Creates an instance using configured <see cref="DynamicQOptions"/>.
/// </remarks>
/// <param name="options">Options snapshot from DI.</param>
public sealed class DynamicQ(IOptions<DynamicQOptions> options)
{
    private DynamicQOptions Options { get; } = options.Value;
    private static readonly BindingFlags DefaultBindingFlags = BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance;

    #region Public Methods

    /// <summary>
    /// Creates a query with applied filters and includes.
    /// </summary>
    /// <param name="sourceRepository">Repository on which the custom query will be applied</param>
    /// <param name="tableTree">Object with nodes containing paths for includes</param>
    public IQueryable<TEntity> CreateCustomQuery<TEntity>(
        IQueryable<TEntity> sourceRepository,
        DynamicQTableTree tableTree
    ) where TEntity : class
    {
        var customSelector = CreateSelectExpressionBodyForCollection<TEntity>(tableTree);
        var includes = tableTree.JoinNodes
            .Select(node => string.Join('.', node.MinimalIncludePath));

        var query = ApplyIncludes(sourceRepository, includes.Where(x => !string.IsNullOrWhiteSpace(x)))
            .Select(customSelector);

        return query;
    }

    /// <summary>
    /// Finds tree structure to reach all of the tables.
    /// </summary>
    /// <param name="request">Request containing path (navigation to table) and requested fields</param>
    /// <returns>TablesWithIncludes - Tree structure containing starting table, child tables, paths to child tables</returns>
    public DynamicQTableTree? CreateTableTree(DynamicQTableRequest request)
    {
        // Filter out tables from the request that do not require a join
        var tablesToJoin = request.TableSelection
            .Where(node => TableRequiresJoin(node.PathToTable, node.SelectedTableColumns))
            .ToList();

        FilterOutColumns(request);

        // If only one path is required then we only need one table
        // Otherwise create minimal paths to each of the requested tables
        DynamicQTableTree tableWithIncludes;
        if (tablesToJoin.Count == 1)
        {
            var table = tablesToJoin.First();
            var startTable = table.PathToTable.Split('.')[^1];
            var startTableType = Options.RegisteredTables.GetTypeByNavigationName(startTable);

            if (startTable is null || startTableType is null)
            {
                return null;
            }

            tableWithIncludes = new DynamicQTableTree
            {
                StartingTable = startTable,
                // Future changes: apply field filter here
                JoinNodes = [
                    new DynamicQNode
                    {
                        SelectedTableColumns = table.SelectedTableColumns,
                        OriginalIncludePath = table.PathToTable,
                        TableType = startTableType
                    }
                ]
            };
        }
        else
        {
            var exportNodes = tablesToJoin.Select(table =>
            {
                var startTable = table.PathToTable.Split('.')[^1];
                var startTableType = Options.RegisteredTables.GetTypeByNavigationName(startTable);

                if (startTable is null || startTableType is null)
                {
                    return null;
                }

                var exportNode = new DynamicQNode
                {
                    SelectedTableColumns = table.SelectedTableColumns,
                    OriginalIncludePath = table.PathToTable,
                    MinimalIncludePath = table.PathToTable.Split('.'),
                    TableType = startTableType
                };
                return exportNode;
            });

            var validNodes = exportNodes.OfType<DynamicQNode>().ToList();
            if (validNodes.Count != exportNodes.Count())
            {
                return null;
            }

            tableWithIncludes = new DynamicQTableTree { JoinNodes = validNodes }.CreateMinimalIncludeTableTree();
        }

        return tableWithIncludes;
    }

    #endregion

    #region Select Expression Builder Methods

    private MemberInitExpression GenerateCustomSelectBindings<TEntity>(
        DynamicQTableTree tableTree,
        List<MemberBinding> bindings,
        Expression lambdaParameter,
        bool nullSafeBindings) where TEntity : class
    {
        var genericType = typeof(TEntity);

        AddSimplePropertyBindings<TEntity>(tableTree, bindings, lambdaParameter, nullSafeBindings);
        AddNestedPropertyBindings(tableTree, bindings, lambdaParameter, genericType);

        var newInstanceExpression = Expression.New(genericType);
        return Expression.MemberInit(newInstanceExpression, bindings);
    }

    /// <summary>
    /// Generates custom selector for collection types
    /// </summary>
    /// <param name="tableTree">Tree structure with required nodes (paths and properties to select)</param>
    /// <returns>Custom Expression to use in a .Select() method</returns>
    /// <remarks>
    /// Example shape (illustrative): nested <c>Currency</c> with <c>StructureRoes</c> projected via
    /// <c>Select</c> into new items (e.g. each item carries <c>StructureRoeId</c> from the source row).
    /// </remarks>
    private Expression<Func<TEntity, TEntity>> CreateSelectExpressionBodyForCollection<TEntity>(
        DynamicQTableTree tableTree
    ) where TEntity : class
    {
        var genericType = typeof(TEntity);
        // Create the parameter for our lambada: x => ...
        var lambdaAccessor = Options.RegisteredTables.GetNavigationNameByType(genericType);
        var lambdaParameter = Expression.Parameter(genericType, GenerateUniqueLambdaParameterName(lambdaAccessor));

        // Create bindings for type: MyField = x.FieldName...
        var bindings = new List<MemberBinding>();

        var memberInit = GenerateCustomSelectBindings<TEntity>(tableTree, bindings, lambdaParameter, false);

        return Expression.Lambda<Func<TEntity, TEntity>>(memberInit, lambdaParameter);
    }

    /// <summary>
    /// Generates custom selector for class types
    /// </summary>
    /// <param name="tableTree">Tree structure with required nodes (paths and properties to select)</param>
    /// <param name="propertyAccess">Property accessor for accessing object properties</param>
    /// <param name="parentNodeType"></param>
    /// <returns>Custom Expression to use in a .Select() method</returns>
    /// <remarks>
    /// Generated shape (illustrative): a lambda from the navigation parameter to <c>new</c> root type
    /// with scalars copied and nested types built with member initializers (null-conditional where applicable).
    /// </remarks>
    private MemberAssignment? CreateSelectExpressionBodyForClass<TEntity>(
        DynamicQTableTree tableTree,
        Expression propertyAccess,
        Type parentNodeType
    ) where TEntity : class
    {
        var genericType = typeof(TEntity);
        // Create the parameter for our lambada: x.NavigationPropName => ...
        var entityTablePath = tableTree.JoinNodes.FirstOrDefault(x => x.TableType == genericType)?.OriginalIncludePath;
        var lambdaAccessor = Options.RegisteredTables.GetNavigationNameByTypeAndPath(genericType, entityTablePath);
        if (lambdaAccessor == null)
        {
            return null;
        }

        var lambdaParameter = Expression.PropertyOrField(propertyAccess, lambdaAccessor);

        // Create bindings for type: MyField = x.FieldName...
        var bindings = new List<MemberBinding>();

        // Nested object must use null sage bindings
        var memberInit = GenerateCustomSelectBindings<TEntity>(tableTree, bindings, lambdaParameter, true);

        return CreateMemberBinding(parentNodeType, tableTree.StartingTable!, memberInit);
    }

    #endregion

    #region Join Utils

    /// <summary>
    /// Applies includes to a query
    /// </summary>
    /// <param name="query"></param>
    /// <param name="includes">Navigation paths to include</param>
    /// <returns>Query with includes applied</returns>
    private static IQueryable<TEntity> ApplyIncludes<TEntity>(IQueryable<TEntity> query, IEnumerable<string> includes)
        where TEntity : class
    {
        return includes.Aggregate(query, (current, includeProperty) =>
                   current.Include(includeProperty));
    }

    #endregion

    #region Select Utils

    private static void AddSimplePropertyBindings<TEntity>(
       DynamicQTableTree tableTree,
       List<MemberBinding> bindings,
       Expression lambdaParameter,
       bool nullSafeBindings) where TEntity : class
    {
        var tableNode = tableTree.JoinNodes.FirstOrDefault(x => !x.MinimalIncludePath.Any());
        if (tableNode == null)
        {
            return;
        }

        var newBindings = nullSafeBindings
            ? GenerateNullSafeBindings<TEntity>(tableNode, lambdaParameter)
            : GenerateBindings<TEntity>(tableNode, lambdaParameter);

        bindings.AddRange(newBindings);
    }

    private void AddNestedPropertyBindings(
        DynamicQTableTree tableTree,
        List<MemberBinding> bindings,
        Expression lambdaParameter,
        Type genericType)
    {
        var groupedNodes = tableTree.JoinNodes
            .Where(x => x.MinimalIncludePath.Any())
            .GroupBy(x => x.MinimalIncludePath.First());

        foreach (var nodeGroup in groupedNodes)
        {
            var navigationName = nodeGroup.Key;
            var nestedTableType = Options.RegisteredTables.GetTypeByNavigationName(navigationName);
            if (nestedTableType == null)
            {
                continue;
            }

            var nestedTableTree = nodeGroup.GenerateSubTree();
            var childProperty = genericType.GetProperty(navigationName, DefaultBindingFlags);
            if (childProperty == null)
            {
                continue;
            }

            if (Options.RegisteredTables.NavigationPropertyTypeExists(childProperty.PropertyType))
            {
                AddNestedEntityBinding(bindings, nestedTableType, nestedTableTree, lambdaParameter, genericType);
            }
            // TODO: check if this actually does what we expect it to do -> should check if IsCollection()
            else if (typeof(System.Collections.IEnumerable).IsAssignableFrom(childProperty.PropertyType))
            {
                AddNestedCollectionBinding(bindings, nestedTableType, nestedTableTree, lambdaParameter, genericType, navigationName);
            }
        }
    }

    private void AddNestedEntityBinding(
        List<MemberBinding> bindings,
        Type nestedTableType,
        DynamicQTableTree nestedTableTree,
        Expression lambdaParameter,
        Type genericType)
    {
        var result = InvokeGenericCreateSelectExpression(nestedTableType, nestedTableTree, lambdaParameter, genericType);
        if (result is MemberAssignment assignment)
        {
            bindings.Add(assignment);
        }
    }

    private void AddNestedCollectionBinding(
        List<MemberBinding> bindings,
        Type nestedTableType,
        DynamicQTableTree nestedTableTree,
        Expression lambdaParameter,
        Type genericType,
        string navigationName)
    {
        var result = InvokeGenericCreateSelectExpression(nestedTableType, nestedTableTree, null, null);
        if (!IsInstanceOfLambdaExpression(nestedTableType, result))
        {
            return;
        }

        var collectionType = Options.RegisteredTables.GetTypeByNavigationName(navigationName);
        if (collectionType == null)
        {
            throw new InvalidOperationException(
                $"Unable to find '{navigationName}'" + "Please register a Table that implements the navigation name.");
        }

        var selectMethod = GetGenericSelectForType(collectionType);
        var toListMethod = GetGenericToListForType(collectionType);
        var propertyAccess = Expression.PropertyOrField(lambdaParameter, navigationName);

        var callSelect = Expression.Call(null, selectMethod, propertyAccess, (Expression)result);
        var callToList = Expression.Call(toListMethod, callSelect);
        var assignment = CreateMemberBinding(genericType, navigationName, callToList);

        if (assignment != null)
        {
            bindings.Add(assignment);
        }
    }

    private static List<MemberBinding> GenerateBindings<TEntity>(DynamicQNode node, Expression propertyAccess)
    {
        // Entry points necessary for our selector
        var entityType = typeof(TEntity);
        var bindings = node.SelectedTableColumns
            .Select(field => entityType.GetProperty(field, DefaultBindingFlags))
            .Where(typeProperty => typeProperty != null)
            .Select(typeProperty =>
            {
                var objectProperty = Expression.Property(propertyAccess, typeProperty!);
                return Expression.Bind(typeProperty!, objectProperty);
            });

        return [.. bindings];
    }

    private object InvokeGenericCreateSelectExpression(
        Type typeForGenericInvoke,
        DynamicQTableTree tableTree,
        Expression? propertyAccess,
        Type? parentNode
    )
    {
        string functionName;
        object[] functionArguments;
        if (parentNode != null && propertyAccess != null)
        {
            functionName = nameof(CreateSelectExpressionBodyForClass);
            functionArguments = [tableTree, propertyAccess, parentNode];
        }
        else
        {
            functionName = nameof(CreateSelectExpressionBodyForCollection);
            functionArguments = [tableTree];
        }

        var methodInfo = GetType().GetMethod(functionName, BindingFlags.NonPublic | BindingFlags.IgnoreCase | BindingFlags.Instance);
        var genericMethod = methodInfo?.MakeGenericMethod(typeForGenericInvoke);

        var result = genericMethod?.Invoke(this, functionArguments);

        return result ?? new object();
    }

    private static MethodInfo GetGenericSelectForType(Type collectionType)
    {
        // Get generic Select
        var selectMethod = typeof(Enumerable).GetMethods()
                .Where(mi => mi.Name == "Select" && mi.GetParameters().Length == 2)
                .Single(mi =>
                {
                    var selectorParam = mi.GetParameters()[1];
                    if (!selectorParam.ParameterType.IsGenericType)
                    {
                        return false;
                    }
                    var genDef = selectorParam.ParameterType.GetGenericTypeDefinition();
                    return genDef == typeof(Func<,>);
                })
                .MakeGenericMethod(collectionType, collectionType);

        return selectMethod;
    }

    private static MethodInfo GetGenericToListForType(Type collectionType)
    {
        // Get generic ToList
        var toListMethod = typeof(Enumerable).GetMethods()
                .Single(mi => mi.Name == "ToList" && mi.GetParameters().Length == 1)
                .MakeGenericMethod(collectionType);

        return toListMethod;
    }

    private static bool IsInstanceOfLambdaExpression(Type tableType, object invocationResult)
    {
        var lambdaGenericExpression = typeof(Func<,>).MakeGenericType(tableType, tableType);
        var funcExpression = typeof(Expression<>).MakeGenericType(lambdaGenericExpression);

        return funcExpression.IsInstanceOfType(invocationResult);
    }

    private static MemberAssignment? CreateMemberBinding(Type entityType, string field, Expression expression)
    {
        var typeProperty = entityType.GetProperty(field, DefaultBindingFlags);
        return typeProperty != null ? Expression.Bind(typeProperty, expression) : null;
    }

    private static List<MemberBinding> GenerateNullSafeBindings<TEntity>(DynamicQNode node, Expression propertyAccess)
    {
        // Entry points necessary for our selector
        var entityType = typeof(TEntity);
        var bindings = node.SelectedTableColumns
            .Select(field => entityType.GetProperty(field, DefaultBindingFlags))
            .Where(typeProperty => typeProperty != null)
            .Select(typeProperty =>
            {
                var objectProperty = CreateNullSafePropertyAccess(propertyAccess, typeProperty!);
                return Expression.Bind(typeProperty!, objectProperty);
            });

        return [.. bindings];
    }

    private static Expression CreateNullSafePropertyAccess(Expression source, PropertyInfo targetProperty)
    {
        // Get the default value for the target property type
        var defaultValue = GetDefaultValueExpression(targetProperty.PropertyType);

        // Create the property access chain
        var propertyAccess = Expression.Property(source, targetProperty);

        // Check if the source (navigation property) could be null
        if (!CanBeNull(source.Type))
        {
            return propertyAccess;
        }

        // Create: source == null ? defaultValue : source.Property
        var nullCheck = Expression.Equal(source, Expression.Constant(null, source.Type));
        return Expression.Condition(nullCheck, defaultValue, propertyAccess, targetProperty.PropertyType);
    }

    private static Expression GetDefaultValueExpression(Type type)
    {
        if (type == typeof(string))
        {
            return Expression.Constant(string.Empty, typeof(string));
        }
        else if (type.IsValueType)
        {
            // For value types (int, decimal, DateTime, etc.), use default(T)
            return Expression.Default(type);
        }
        else
        {
            // For reference types, use null
            return Expression.Constant(null, type);
        }
    }

    private static bool CanBeNull(Type type)
       => !type.IsValueType || Nullable.GetUnderlyingType(type) != null;

    /// <summary>
    /// The same table can be joins twice we need a random string generator to use in lambdas
    /// Example: If a users joins Currency on both Placement and Quote
    /// Currency_ABC.CurrencyId -> to access currency data on placements
    /// Currency_EFG.CurrencyId -> to access currency data on quotes
    /// </summary>
    /// <param name="tableNavigationName"></param>
    private string GenerateUniqueLambdaParameterName(string? tableNavigationName) =>
        $"{tableNavigationName}_{new string([.. Enumerable.Range(0, 3).Select(_ => (char)('A' + Random.Shared.Next(26)))])}";

    #endregion

    #region DynamicQTableTree methods

    /// <summary>
    /// Checks if a table requires a join
    /// </summary>
    /// <param name="tablePath">Table that is checked if necessary for joing</param>
    /// <param name="props">Table props that are checked if valid</param>
    private bool TableRequiresJoin(string tablePath, List<string>? props)
    {
        var approvedTable = Options.RegisteredTables.GetRegisteredTableByPath(tablePath);

        // Check if approved table exists and path matches
        if (approvedTable == null || approvedTable.PathToTable != tablePath || props == null)
        {
            return false;
        }

        // Future changes: add filter to remove props that are not allowed
        var tableProps = approvedTable.TableType.GetProperties().Select(x => x.Name);
        return tableProps.Any(props.Contains);
    }

    private void FilterOutColumns(DynamicQTableRequest request){
        foreach (var table in request.TableSelection)
        {
            var columnsToExclude = Options.RegisteredTables.GetColumnsToExcludeByPathToTable(table.PathToTable);
            table.SelectedTableColumns = [.. table.SelectedTableColumns.Where(column => !columnsToExclude.Contains(column))];
        }
    }

    #endregion
}