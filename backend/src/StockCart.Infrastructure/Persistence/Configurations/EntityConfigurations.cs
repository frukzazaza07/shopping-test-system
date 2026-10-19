using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockCart.Domain.Entities;

namespace StockCart.Infrastructure.Persistence.Configurations;

internal sealed class ProductCategoryConfiguration : IEntityTypeConfiguration<ProductCategory>
{
    public void Configure(EntityTypeBuilder<ProductCategory> b)
    {
        b.ToTable("product_category");
        b.HasKey(x => x.Id);
        b.Property(x => x.CategoryName).HasMaxLength(100).IsRequired();
    }
}

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.ToTable("product", t => t.HasCheckConstraint("ck_product_price_unit_non_negative", "product_price_unit >= 0"));
        b.HasKey(x => x.Id);
        b.Property(x => x.ProductCode).HasMaxLength(20).IsRequired();
        b.HasIndex(x => x.ProductCode).IsUnique();
        b.Property(x => x.ProductName).HasMaxLength(200).IsRequired();
        b.Property(x => x.ProductPriceUnit).HasPrecision(12, 2);

        b.HasOne(x => x.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ProductStockConfiguration : IEntityTypeConfiguration<ProductStock>
{
    public void Configure(EntityTypeBuilder<ProductStock> b)
    {
        // DB-level safety net: stock can never become negative even if app logic is wrong.
        b.ToTable("product_stock", t => t.HasCheckConstraint("ck_product_stock_remain_non_negative", "stock_remain >= 0"));
        b.HasKey(x => x.ProductId);
        b.Property(x => x.ProductId).ValueGeneratedNever();

        b.HasOne(x => x.Product)
            .WithOne(p => p.Stock)
            .HasForeignKey<ProductStock>(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ProductDeductionConfiguration : IEntityTypeConfiguration<ProductDeduction>
{
    public void Configure(EntityTypeBuilder<ProductDeduction> b)
    {
        b.ToTable("product_deduction", t => t.HasCheckConstraint("ck_product_deduction_quantity_positive", "deduct_quantity > 0"));
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.ProductId, x.DeductProductId }).IsUnique();

        b.HasOne(x => x.Product)
            .WithMany(p => p.Deductions)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.DeductProduct)
            .WithMany()
            .HasForeignKey(x => x.DeductProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> b)
    {
        b.ToTable("order");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.OrderNo).HasMaxLength(40).IsRequired();
        b.HasIndex(x => x.OrderNo).IsUnique();
        b.Property(x => x.TotalAmount).HasPrecision(12, 2);
        b.Property(x => x.PaymentMethod).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(x => x.CreatedAt);
        b.Property(x => x.IdempotencyKey).HasMaxLength(64);
        b.HasIndex(x => x.IdempotencyKey).IsUnique();
    }
}

internal sealed class OrderDetailConfiguration : IEntityTypeConfiguration<OrderDetail>
{
    public void Configure(EntityTypeBuilder<OrderDetail> b)
    {
        b.ToTable("order_detail", t => t.HasCheckConstraint("ck_order_detail_quantity_positive", "quantity > 0"));
        b.HasKey(x => x.Id);
        b.Property(x => x.UnitPrice).HasPrecision(12, 2);
        b.Property(x => x.LineTotal).HasPrecision(12, 2);

        b.HasOne(x => x.Order)
            .WithMany(o => o.Details)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
