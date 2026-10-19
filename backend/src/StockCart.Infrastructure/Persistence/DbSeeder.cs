using Microsoft.EntityFrameworkCore;
using StockCart.Domain.Entities;

namespace StockCart.Infrastructure.Persistence;

/// <summary>Seeds 5 categories and 50 products (40 single items + 10 combo/pack sets).</summary>
internal static class DbSeeder
{
    private sealed record Item(string Code, string Name, decimal Price, int Stock);

    private sealed record Combo(string Code, string Name, decimal Price, (string Code, int Qty)[] Parts);

    private static readonly (string Category, Item[] Items)[] SingleItems =
    [
        ("Drinks",
        [
            new("DRK-001", "Drinking Water 600ml", 10, 200),
            new("DRK-002", "Cola 325ml", 18, 120),
            new("DRK-003", "Lemon Soda 325ml", 18, 80),
            new("DRK-004", "Green Tea 500ml", 25, 60),
            new("DRK-005", "Orange Juice 350ml", 35, 40),
            new("DRK-006", "Iced Americano", 45, 50),
            new("DRK-007", "Iced Latte", 55, 30),
            new("DRK-008", "Thai Milk Tea", 40, 25),
            new("DRK-009", "Fresh Milk 450ml", 22, 3),
            new("DRK-010", "Energy Drink 150ml", 15, 0),
        ]),
        ("Snacks",
        [
            new("SNK-001", "Potato Chips Original", 30, 70),
            new("SNK-002", "Potato Chips BBQ", 30, 45),
            new("SNK-003", "Seaweed Snack", 25, 35),
            new("SNK-004", "Roasted Peanuts", 20, 55),
            new("SNK-005", "Chocolate Bar", 35, 40),
            new("SNK-006", "Gummy Bears", 20, 2),
            new("SNK-007", "Corn Puffs", 15, 60),
            new("SNK-008", "Rice Crackers", 20, 30),
            new("SNK-009", "Dried Mango", 45, 18),
            new("SNK-010", "Caramel Popcorn", 39, 5),
        ]),
        ("Bakery",
        [
            new("BKR-001", "Butter Croissant", 45, 20),
            new("BKR-002", "Chocolate Croissant", 55, 15),
            new("BKR-003", "Blueberry Muffin", 49, 12),
            new("BKR-004", "Banana Cake Slice", 39, 18),
            new("BKR-005", "Cheese Danish", 52, 1),
            new("BKR-006", "Garlic Bread", 35, 25),
            new("BKR-007", "Ham & Cheese Sandwich", 59, 22),
            new("BKR-008", "Tuna Sandwich", 55, 16),
            new("BKR-009", "Egg Tart", 25, 30),
            new("BKR-010", "Cinnamon Roll", 49, 0),
        ]),
        ("Ready Meals",
        [
            new("MEL-001", "Basil Pork with Rice", 59, 25),
            new("MEL-002", "Green Curry Chicken with Rice", 65, 20),
            new("MEL-003", "Shrimp Fried Rice", 69, 15),
            new("MEL-004", "Pad Thai", 65, 18),
            new("MEL-005", "Chicken Teriyaki Bowl", 79, 12),
            new("MEL-006", "Spaghetti Carbonara", 89, 10),
            new("MEL-007", "Tom Yum Noodles", 55, 22),
            new("MEL-008", "Chicken Burger", 79, 20),
            new("MEL-009", "French Fries", 39, 40),
            new("MEL-010", "Fried Chicken Wings (3 pcs)", 69, 4),
        ]),
    ];

    private static readonly Combo[] Combos =
    [
        new("SET-001", "Drinking Water 6-Pack", 55, [("DRK-001", 6)]),
        new("SET-002", "Cola 6-Pack", 99, [("DRK-002", 6)]),
        new("SET-003", "Breakfast Set", 85, [("BKR-001", 1), ("DRK-006", 1)]),
        new("SET-004", "Burger Combo", 129, [("MEL-008", 1), ("MEL-009", 1), ("DRK-002", 1)]),
        new("SET-005", "Sandwich Lunch Set", 85, [("BKR-007", 1), ("DRK-005", 1)]),
        new("SET-006", "Movie Night Pack", 109, [("SNK-010", 2), ("DRK-002", 2)]),
        new("SET-007", "Snack Party Box", 149, [("SNK-001", 2), ("SNK-002", 2), ("SNK-007", 2)]),
        new("SET-008", "Thai Lunch Set", 89, [("MEL-001", 1), ("DRK-008", 1)]),
        new("SET-009", "Bakery Box", 199, [("BKR-002", 2), ("BKR-003", 2), ("BKR-009", 2)]),
        new("SET-010", "Wings & Fries Combo", 119, [("MEL-010", 1), ("MEL-009", 1), ("DRK-003", 1)]),
    ];

    /// <summary>Arbitrary app-wide key for the Postgres advisory lock that guards seeding.</summary>
    private const long SeedLockKey = 7_301_001;

    public static async Task SeedAsync(AppDbContext db, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // If two migrators ever run at once, the second waits here and then sees the data.
        // Transaction-scoped lock: released automatically on commit/rollback.
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({SeedLockKey})", ct);
        if (await db.Products.AnyAsync(ct)) return;

        var now = DateTime.UtcNow;
        var byCode = new Dictionary<string, Product>();
        var sortOrder = 0;

        foreach (var (categoryName, items) in SingleItems)
        {
            var category = new ProductCategory { CategoryName = categoryName, SortOrder = ++sortOrder };
            foreach (var item in items)
            {
                var product = new Product
                {
                    ProductCode = item.Code,
                    ProductName = item.Name,
                    ProductPriceUnit = item.Price,
                    Stock = new ProductStock { StockRemain = item.Stock, UpdatedAt = now }
                };
                // A single item deducts its own stock x1.
                product.Deductions.Add(new ProductDeduction { DeductProduct = product, DeductQuantity = 1 });
                category.Products.Add(product);
                byCode[item.Code] = product;
            }
            db.ProductCategories.Add(category);
        }

        // Combos have no stock of their own - they deduct their components.
        var comboCategory = new ProductCategory { CategoryName = "Combo & Packs", SortOrder = ++sortOrder };
        foreach (var combo in Combos)
        {
            var product = new Product
            {
                ProductCode = combo.Code,
                ProductName = combo.Name,
                ProductPriceUnit = combo.Price
            };
            foreach (var (code, qty) in combo.Parts)
                product.Deductions.Add(new ProductDeduction { DeductProduct = byCode[code], DeductQuantity = qty });

            comboCategory.Products.Add(product);
        }
        db.ProductCategories.Add(comboCategory);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
