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
    /// Creates a query with the includes and projection described by <paramref name="tableTree"/> applied.
    /// </summary>
    /// <param name="sourceRepository">Repository on which the projected query will be applied</param>
    /// <param name="tableTree">Object with nodes containing paths for includes</param>
    public IQueryable<TEntity> CreateProjectedQuery<TEntity>(
        IQueryable<TEntity> sourceRepository,
        DynamicQTableTree tableTree
    ) where TEntity : class
    {
        var selector = CreateSelectorLambda<TEntity>(tableTree);
        var includes = tableTree.JoinNodes
            .Select(node => string.Join('.', node.MinimalIncludePath));

        var query = ApplyIncludes(sourceRepository, includes.Where(x => !string.IsNullOrWhiteSpace(x)))
            .Select(selector);

        return query;
    }

    /// <summary>
    /// Finds tree structure to reach all of the tables.
    /// </summary>
    /// <param name="request">Request containing path (navigation to table) and requested fields</param>
    /// <returns>TablesWithIncludes - Tree structure containing starting table, child tables, paths to child tables</returns>
    public DynamicQTableTree? CreateTableTree(DynamicQTableRequest request)
    {
        // Filter out tables from the request that are not registered or select no valid columns
        var tablesToJoin = request.TableSelection
            .Where(node => IsValidTableSelection(node.PathToTable, node.SelectedTableColumns))
            .ToList();

        RemoveExcludedColumns(request);

        var joinNodes = new List<DynamicQNode>();
        foreach (var table in tablesToJoin)
        {
            var pathSegments = table.PathToTable.Split('.');
            var tableType = Options.RegisteredTables.GetTypeByNavigationName(pathSegments[^1])
                ?? throw new InvalidOperationException($"A required table has not been properly registered: '{pathSegments[^1]}'");

            joinNodes.Add(new DynamicQNode
            {
                SelectedTableColumns = table.SelectedTableColumns,
                OriginalIncludePath = table.PathToTable,
                MinimalIncludePath = pathSegments,
                TableType = tableType
            });
        }

        // Collapse the shared path prefix so only the necessary joins remain
        // (for a single table the whole path collapses and no join is required)
        return new DynamicQTableTree { JoinNodes = joinNodes }.CreateMinimalIncludeTableTree();
    }

    #endregion

    #region Select Expression Builder Methods

    private MemberInitExpression BuildMemberInit<TEntity>(
        DynamicQTableTree tableTree,
        Expression lambdaParameter,
        bool nullSafeBindings) where TEntity : class
    {
        var genericType = typeof(TEntity);

        // Create bindings for type: MyField = x.FieldName...
        var bindings = new List<MemberBinding>();
        AddSimplePropertyBindings<TEntity>(tableTree, bindings, lambdaParameter, nullSafeBindings);
        AddNestedPropertyBindings(tableTree, bindings, lambdaParameter, genericType);

        var newInstanceExpression = Expression.New(genericType);
        return Expression.MemberInit(newInstanceExpression, bindings);
    }

    /// <summary>
    /// Generates the selector lambda for an entity type, usable in a .Select() call
    /// </summary>
    /// <param name="tableTree">Tree structure with required nodes (paths and properties to select)</param>
    /// <returns>Selector lambda to use in a .Select() method</returns>
    /// <remarks>
    /// Example shape (illustrative): nested <c>Currency</c> with <c>StructureRoes</c> projected via
    /// <c>Select</c> into new items (e.g. each item carries <c>StructureRoeId</c> from the source row).
    /// </remarks>
    private Expression<Func<TEntity, TEntity>> CreateSelectorLambda<TEntity>(
        DynamicQTableTree tableTree
    ) where TEntity : class
    {
        var genericType = typeof(TEntity);
        // Create the parameter for our lambda: x => ...
        var lambdaAccessor = Options.RegisteredTables.GetNavigationNameByType(genericType);
        var lambdaParameter = Expression.Parameter(genericType, GenerateUniqueLambdaParameterName(lambdaAccessor));

        var memberInit = BuildMemberInit<TEntity>(tableTree, lambdaParameter, false);

        return Expression.Lambda<Func<TEntity, TEntity>>(memberInit, lambdaParameter);
    }

    /// <summary>
    /// Generates the member assignment for a nested (class type) navigation property
    /// </summary>
    /// <param name="tableTree">Tree structure with required nodes (paths and properties to select)</param>
    /// <param name="propertyAccess">Property accessor for accessing object properties</param>
    /// <param name="parentNodeType">Type of the parent node that owns the navigation property</param>
    /// <returns>Member assignment binding the nested projection to the parent property</returns>
    /// <remarks>
    /// Generated shape (illustrative): a lambda from the navigation parameter to <c>new</c> root type
    /// with scalars copied and nested types built with member initializers (null-conditional where applicable).
    /// </remarks>
    private MemberAssignment? CreateNestedMemberAssignment<TEntity>(
        DynamicQTableTree tableTree,
        Expression propertyAccess,
        Type parentNodeType
    ) where TEntity : class
    {
        var genericType = typeof(TEntity);
        // Create the parameter for our lambda: x.NavigationPropName => ...
        var entityTablePath = tableTree.JoinNodes.FirstOrDefault(x => x.TableType == genericType)?.OriginalIncludePath;
        var lambdaAccessor = Options.RegisteredTables.GetNavigationNameByTypeAndPath(genericType, entityTablePath);
        if (lambdaAccessor == null)
        {
            return null;
        }

        var lambdaParameter = Expression.PropertyOrField(propertyAccess, lambdaAccessor);

        // Nested object must use null safe bindings
        var memberInit = BuildMemberInit<TEntity>(tableTree, lambdaParameter, true);

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
        var tableNode = tableTree.GetRootNode();
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
        var groupedNodes = tableTree.GroupByNextSegment();

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
            else if (childProperty.IsCollectionNavigation())
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

        // TODO: double check if this is required, i think i implemented a "security check", if the table is registered that means the user allowed it
        // if not requried just use nestedTableType instead of collectionType

        var collectionType = Options.RegisteredTables.GetTypeByNavigationName(navigationName) 
            ?? throw new InvalidOperationException($"Unable to find '{navigationName}'" + "Please register a Table that implements the navigation name.");

        var selectMethod = GetGenericSelectForType(collectionType);
        var toListMethod = GetGenericToListForType(collectionType);
        var propertyAccess = Expression.PropertyOrField(lambdaParameter, navigationName);

        var callSelect = Expression.Call(null, selectMethod, propertyAccess, (Expression)result!);
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

    private object? InvokeGenericCreateSelectExpression(
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
            functionName = nameof(CreateNestedMemberAssignment);
            functionArguments = [tableTree, propertyAccess, parentNode];
        }
        else
        {
            functionName = nameof(CreateSelectorLambda);
            functionArguments = [tableTree];
        }

        var methodInfo = GetType().GetMethod(functionName, BindingFlags.NonPublic | BindingFlags.IgnoreCase | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Unable to resolve method '{functionName}' for dynamic invocation.");

        return methodInfo
            .MakeGenericMethod(typeForGenericInvoke)
            .Invoke(this, functionArguments);
    }

    private static MethodInfo GetGenericSelectForType(Type elementType)
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
                .MakeGenericMethod(elementType, elementType);

        return selectMethod;
    }

    private static MethodInfo GetGenericToListForType(Type elementType)
    {
        // Get generic ToList
        var toListMethod = typeof(Enumerable).GetMethods()
                .Single(mi => mi.Name == "ToList" && mi.GetParameters().Length == 1)
                .MakeGenericMethod(elementType);

        return toListMethod;
    }

    private static bool IsInstanceOfLambdaExpression(Type tableType, object? invocationResult)
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
    /// The same table can be joined twice, so we need a random string generator to use in lambdas
    /// Example: If a user joins Currency on both Placement and Quote
    /// Currency_ABC.CurrencyId -> to access currency data on placements
    /// Currency_EFG.CurrencyId -> to access currency data on quotes
    /// </summary>
    /// <param name="tableNavigationName"></param>
    private string GenerateUniqueLambdaParameterName(string? tableNavigationName) =>
        $"{tableNavigationName}_{new string([.. Enumerable.Range(0, 3).Select(_ => (char)('A' + Random.Shared.Next(26)))])}";

    #endregion

    #region DynamicQTableTree methods

    /// <summary>
    /// Checks that the requested table is registered and that at least one requested column exists on it
    /// </summary>
    /// <param name="tablePath">Path of the table that is checked for registration</param>
    /// <param name="props">Table props that are checked if valid</param>
    private bool IsValidTableSelection(string tablePath, List<string>? props)
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

    private void RemoveExcludedColumns(DynamicQTableRequest request)
    {
        foreach (var table in request.TableSelection)
        {
            var columnsToExclude = Options.RegisteredTables.GetColumnsToExcludeByPathToTable(table.PathToTable);
            table.SelectedTableColumns = [.. table.SelectedTableColumns.Where(column => !columnsToExclude.Contains(column))];
        }
    }

    #endregion
}