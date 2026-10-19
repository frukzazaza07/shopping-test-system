using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using StockCart.Application.Abstractions;
using StockCart.Infrastructure.Redis;
using StockCart.Infrastructure.Persistence;
using StockCart.Infrastructure.Persistence.Repositories;

namespace StockCart.Infrastructure;

public static class DependencyInjection
{
    public const string ReadyTag = "ready";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Comes from env var ConnectionStrings__Default (docker-compose / k8s Secret) - never hardcoded.
        // It also carries "Maximum Pool Size": replicas x pool size must stay below Postgres max_connections.
        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Connection string 'Default' is not configured.");

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IStockRepository, StockRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();

        // Redis is deliberately NOT part of readiness: if it is down the API keeps selling from Postgres.
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", tags: [ReadyTag]);

        AddRedis(services, configuration);
        return services;
    }

    /// <summary>Redis = read cache + stock gate. Optional: without Redis:ConnectionString everything uses Postgres.</summary>
    private static void AddRedis(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration["Redis:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddSingleton<IAppCache, NoAppCache>();
            services.AddSingleton<IStockGate, NoStockGate>();
            return;
        }

        var syncSeconds = int.TryParse(configuration["Redis:StockSyncSeconds"], out var s) && s > 0 ? s : 30;
        services.AddSingleton(new RedisSettings(connectionString, TimeSpan.FromSeconds(syncSeconds)));

        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var options = ConfigurationOptions.Parse(connectionString);
            options.AbortOnConnectFail = false; // start (and keep serving from Postgres) even if Redis is down
            options.ConnectTimeout = 2000;
            options.AsyncTimeout = 500;         // a slow Redis must not slow down checkout
            options.SyncTimeout = 500;
            // While disconnected, fail immediately (-> Postgres fallback) instead of queueing
            // every command until the timeout, which would add latency to each request.
            options.BacklogPolicy = BacklogPolicy.FailFast;
            options.ClientName = "stockcart-api";
            return ConnectionMultiplexer.Connect(options);
        });
        services.AddSingleton<RedisStockGate>();
        services.AddSingleton<IStockGate>(sp => sp.GetRequiredService<RedisStockGate>());
        services.AddSingleton<IAppCache, RedisAppCache>();
        services.AddHostedService<StockSyncService>();
    }

    /// <summary>
    /// Applies EF Core migrations and seeds mock data. Runs ONCE per deploy as a separate step
    /// (k8s Job / compose "migrate" service), never inside every API replica at startup.
    /// Retries while Postgres is still starting.
    /// </summary>
    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbMigrate");

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync(ct);
                await DbSeeder.SeedAsync(db, ct);
                logger.LogInformation("Database migrated and seeded.");
                return;
            }
            catch (Exception ex) when (attempt < 10)
            {
                logger.LogWarning("Database not ready (attempt {Attempt}): {Message}", attempt, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(3), ct);
            }
        }
    }
}
