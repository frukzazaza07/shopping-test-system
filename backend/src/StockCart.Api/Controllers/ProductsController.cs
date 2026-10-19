using Microsoft.AspNetCore.Mvc;
using StockCart.Application.Dtos;
using StockCart.Application.Services;

namespace StockCart.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class ProductsController(ProductService productService) : ControllerBase
{
    [HttpGet("categories")]
    public Task<IReadOnlyList<CategoryDto>> GetCategories(CancellationToken ct) =>
        productService.GetCategoriesAsync(ct);

    [HttpGet("products")]
    public Task<IReadOnlyList<ProductDto>> GetProducts(CancellationToken ct) =>
        productService.GetProductsAsync(ct);
}
