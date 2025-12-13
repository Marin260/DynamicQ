using DynamicQ.DataStructures;

namespace DynamicQ.Extensions;

public static class RegisteredTableExtensions
{
    extension(HashSet<RegisteredTable> registeredTables)
    {
        public string? GetNavigationNameByType(Type type) =>
            registeredTables.FirstOrDefault(registeredTable => registeredTable.TableType == type)
                ?.VirtualNavigationName;
    
        public string? GetNavigationNameByTypeAndPath(Type type, string? path) =>
            registeredTables.FirstOrDefault(registeredTable => registeredTable.TableType == type && 
                                                               registeredTable.PathToTable == path)
                ?.VirtualNavigationName;
    
        public Type? GetTypeByNavigationName(string virtualNavigationName) =>
            registeredTables.FirstOrDefault(registeredTable => registeredTable.VirtualNavigationName == virtualNavigationName)
                ?.TableType;

        public RegisteredTable? GetRegisteredTableByPath(string path) =>
            registeredTables.FirstOrDefault(registeredTable => registeredTable.VirtualNavigationName == path);
    
        public RegisteredTable? GetRegisteredTableByTypeName(string typeName) =>
            registeredTables.FirstOrDefault(registeredTable => registeredTable.TableType.Name == typeName);
        
        public bool NavigationPropertyTypeExists(Type navigationType) =>
            registeredTables.Any(registeredTable => registeredTable.TableType == navigationType);
    }
}