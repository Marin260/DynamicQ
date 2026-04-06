using DynamicQ.DataStructures;

namespace DynamicQ.Extensions;

/// <summary>
/// Lookups on <see cref="RegisteredTable"/> collections used when resolving navigations and paths.
/// </summary>
internal static class RegisteredTableExtensions
{
        /// <summary>Virtual navigation name for the registered row whose <see cref="RegisteredTable.TableType"/> matches <paramref name="type"/>.</summary>
        internal static string? GetNavigationNameByType(this HashSet<RegisteredTable> registeredTables, Type type) =>
            registeredTables.FirstOrDefault(registeredTable => registeredTable.TableType == type)
                ?.VirtualNavigationName;

        /// <summary>Navigation name when both type and <see cref="RegisteredTable.PathToTable"/> match.</summary>
        internal static string? GetNavigationNameByTypeAndPath(this HashSet<RegisteredTable> registeredTables, Type type, string? path) =>
            registeredTables.FirstOrDefault(registeredTable => registeredTable.TableType == type &&
                                                               registeredTable.PathToTable == path)
                ?.VirtualNavigationName;

        /// <summary>CLR type for the given virtual navigation property name.</summary>
        internal static Type? GetTypeByNavigationName(this HashSet<RegisteredTable> registeredTables, string virtualNavigationName) =>
            registeredTables.FirstOrDefault(registeredTable => registeredTable.VirtualNavigationName == virtualNavigationName)
                ?.TableType;

        /// <summary>Registration entry whose virtual name equals <paramref name="path"/>.</summary>
        internal static RegisteredTable? GetRegisteredTableByPath(this HashSet<RegisteredTable> registeredTables, string path) =>
            registeredTables.FirstOrDefault(registeredTable => registeredTable.VirtualNavigationName == path);

        /// <summary>Registration entry whose <see cref="RegisteredTable.TableType"/> simple name matches <paramref name="typeName"/>.</summary>
        internal static RegisteredTable? GetRegisteredTableByTypeName(this HashSet<RegisteredTable> registeredTables, string typeName) =>
            registeredTables.FirstOrDefault(registeredTable => registeredTable.TableType.Name == typeName);

        /// <summary>Whether any registered table uses <paramref name="navigationType"/> as its entity type.</summary>
        internal static bool NavigationPropertyTypeExists(this HashSet<RegisteredTable> registeredTables, Type navigationType) =>
            registeredTables.Any(registeredTable => registeredTable.TableType == navigationType);
}