using Microsoft.Extensions.DependencyInjection;
using StockCart.Application.Services;

namespace StockCart.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ProductService>();
        services.AddScoped<OrderService>();
        return services;
    }
}
