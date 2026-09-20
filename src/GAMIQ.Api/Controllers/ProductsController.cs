using GAMIQ.Api.Data.Mongo;
using GAMIQ.Api.Dtos;
using GAMIQ.Api.Models.Mongo;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace GAMIQ.Api.Controllers;

[ApiController]
[Route("api/v1/products")]
public class ProductsController : ControllerBase
{
    private readonly MongoContext _mongo;

    public ProductsController(MongoContext mongo)
    {
        _mongo = mongo;
    }

    // GET /api/v1/products - fetch paginated catalog (MongoDB)
    [HttpGet]
    public async Task<ActionResult<PagedResult<ProductResponse>>> GetProducts([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var filter = FilterDefinition<Product>.Empty;
        var totalCount = await _mongo.Products.CountDocumentsAsync(filter);
        var products = await _mongo.Products.Find(filter)
            .SortByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();

        var items = products.Select(ToResponse).ToList();
        return Ok(new PagedResult<ProductResponse>(items, page, pageSize, totalCount));
    }

    // POST /api/v1/products - create product with dynamic attributes (MongoDB)
    [HttpPost]
    public async Task<ActionResult<ProductResponse>> CreateProduct(CreateProductRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Price < 0)
        {
            return BadRequest(new { message = "Name is required and price cannot be negative." });
        }

        var product = new Product
        {
            Name = request.Name,
            Description = request.Description ?? string.Empty,
            Price = request.Price,
            Stock = request.Stock,
            Genres = request.Genres ?? new List<string>(),
            Platforms = request.Platforms ?? new List<string>(),
            Tags = request.Tags ?? new List<string>(),
            Attributes = request.Attributes ?? new Dictionary<string, object>(),
            CreatedAt = DateTime.UtcNow
        };

        await _mongo.Products.InsertOneAsync(product);
        return CreatedAtAction(nameof(GetProducts), new { }, ToResponse(product));
    }

    private static ProductResponse ToResponse(Product p) =>
        new(p.Id, p.Name, p.Description, p.Price, p.Stock, p.Genres, p.Platforms, p.Tags, p.Attributes, p.CreatedAt);
}
