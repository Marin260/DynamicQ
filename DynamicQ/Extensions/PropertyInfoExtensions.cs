namespace DynamicQuery.Extensions;

/// <summary>
/// Helpers for inspecting <see cref="PropertyInfo"/> metadata.
/// </summary>
internal static class PropertyInfoExtensions
{
    /// <summary>
    /// Resolves the CLR type to use for a <see cref="System.Data.DataColumn"/>: nullables are unwrapped and enums map to string.
    /// </summary>
    internal static Type? ResolveDataColumnType(this PropertyInfo propertyInfo)
    {
        var underlyingType = Nullable.GetUnderlyingType(propertyInfo.PropertyType) ?? propertyInfo.PropertyType;
        return underlyingType.IsEnum ? typeof(string) : underlyingType;
    }

    /// <summary>
    /// Whether the property is a collection navigation. Excludes <see cref="string"/>, which implements
    /// <see cref="System.Collections.IEnumerable"/> but is a scalar column.
    /// </summary>
    internal static bool IsCollectionNavigation(this PropertyInfo property) =>
        property.PropertyType != typeof(string) &&
        typeof(System.Collections.IEnumerable).IsAssignableFrom(property.PropertyType);
}
