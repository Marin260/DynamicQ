using Microsoft.Extensions.DependencyInjection;

namespace DynamicQ.Extensions;

public static class DynamicQServiceCollectionExtensions
{
    public static IServiceCollection AddDynamicQ(
        this IServiceCollection services,
        Action<DynamicQOptions> configureOptions)
    {
        services.Configure(configureOptions);
        services.AddSingleton<DynamicQ>();
        return services;
    }
}