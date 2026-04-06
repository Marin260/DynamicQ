using Microsoft.Extensions.DependencyInjection;

namespace DynamicQ.Extensions;

/// <summary>
/// DI registration for DynamicQ.
/// </summary>
public static class DynamicQServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="DynamicQ"/> and binds <see cref="DynamicQOptions"/>.
    /// </summary>
    public static IServiceCollection AddDynamicQ(
        this IServiceCollection services,
        Action<DynamicQOptions> configureOptions)
    {
        services.Configure(configureOptions);
        services.AddSingleton<DynamicQ>();
        return services;
    }
}