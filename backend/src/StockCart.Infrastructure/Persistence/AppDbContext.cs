using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StockCart.Application.Abstractions;
using StockCart.Application.Exceptions;
using StockCart.Domain.Entities;

namespace StockCart.Infrastructure.Persistence;

public sealed partial class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductStock> ProductStocks => Set<ProductStock>();
    public DbSet<ProductDeduction> ProductDeductions => Set<ProductDeduction>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderDetail> OrderDetails => Set<OrderDetail>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // PascalCase properties -> snake_case columns (product_price_unit, stock_remain, ...)
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
            foreach (var property in entity.GetProperties())
                property.SetColumnName(ToSnakeCase(property.Name));
    }

    public async Task<IAppTransaction> BeginTransactionAsync(CancellationToken ct)
        => new EfTransaction(await Database.BeginTransactionAsync(ct));

    /// <summary>Translates Postgres unique violations (23505) into a provider-agnostic exception.</summary>
    async Task<int> IUnitOfWork.SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            return await SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg)
        {
            throw new DuplicateKeyException(pg.ConstraintName, ex);
        }
    }

    private static string ToSnakeCase(string name) => SnakeCaseRegex().Replace(name, "$1_$2").ToLowerInvariant();

    [GeneratedRegex("([a-z0-9])([A-Z])")]
    private static partial Regex SnakeCaseRegex();
}
