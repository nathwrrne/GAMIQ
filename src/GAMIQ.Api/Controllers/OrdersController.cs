using GAMIQ.Api.Data;
using GAMIQ.Api.Data.Mongo;
using GAMIQ.Api.Dtos;
using GAMIQ.Api.Models.Mongo;
using GAMIQ.Api.Models.Postgres;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;

namespace GAMIQ.Api.Controllers;

[ApiController]
[Route("api/v1/orders")]
public class OrdersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly MongoContext _mongo;

    public OrdersController(AppDbContext db, MongoContext mongo)
    {
        _db = db;
        _mongo = mongo;
    }

    // POST /api/v1/orders - create transaction order (dual-DB: debits PostgreSQL wallet and
    // decrements MongoDB stock). If a Mongo stock update fails partway through, already-applied
    // stock decrements are compensated via CompensateStockAsync and the PostgreSQL side is
    // rolled back, so neither database is left holding a half-completed order.
    [HttpPost]
    public async Task<ActionResult<OrderResponse>> CreateOrder(CreateOrderRequest request)
    {
        if (request.Items is null || request.Items.Count == 0)
        {
            return BadRequest(new { message = "Order must contain at least one item." });
        }

        var user = await _db.Users.Include(u => u.Account).FirstOrDefaultAsync(u => u.UserId == request.UserId);
        if (user is null)
        {
            return NotFound(new { message = $"User {request.UserId} not found." });
        }

        if (user.Account is null)
        {
            return UnprocessableEntity(new { message = "User has no account/wallet." });
        }

        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var filter = Builders<Product>.Filter.In(p => p.Id, productIds);
        var products = await _mongo.Products.Find(filter).ToListAsync();
        var productMap = products.ToDictionary(p => p.Id);

        var orderItems = new List<OrderItem>();
        var stockUpdates = new List<(string ProductId, int Quantity)>();
        var total = 0m;

        foreach (var item in request.Items)
        {
            if (item.Quantity <= 0)
            {
                return BadRequest(new { message = "Quantity must be positive." });
            }

            if (!productMap.TryGetValue(item.ProductId, out var product))
            {
                return NotFound(new { message = $"Product {item.ProductId} not found." });
            }

            if (product.Stock < item.Quantity)
            {
                return UnprocessableEntity(new { message = $"Insufficient stock for '{product.Name}'." });
            }

            orderItems.Add(new OrderItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                UnitPrice = product.Price,
                Quantity = item.Quantity
            });
            total += product.Price * item.Quantity;
            stockUpdates.Add((product.Id, item.Quantity));
        }

        if (user.Account.Balance < total)
        {
            return UnprocessableEntity(new { message = "Insufficient account balance." });
        }

        var order = new Order
        {
            UserId = user.UserId,
            TotalAmount = total,
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            Items = orderItems
        };

        // PostgreSQL and MongoDB cannot share a single distributed transaction, so if a later
        // stock decrement fails we compensate by restoring stock for every decrement that
        // already succeeded, rather than leaving MongoDB permanently out of sync with the
        // PostgreSQL rollback. See README §6 for the full design rationale.
        var appliedStockUpdates = new List<(string ProductId, int Quantity)>();

        await using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            _db.Orders.Add(order);
            user.Account.Balance -= total;
            await _db.SaveChangesAsync();

            foreach (var (productId, quantity) in stockUpdates)
            {
                var update = Builders<Product>.Update.Inc(p => p.Stock, -quantity);
                var mongoFilter = Builders<Product>.Filter.Eq(p => p.Id, productId) &
                                   Builders<Product>.Filter.Gte(p => p.Stock, quantity);
                var result = await _mongo.Products.UpdateOneAsync(mongoFilter, update);
                if (result.ModifiedCount == 0)
                {
                    throw new InvalidOperationException($"Stock update failed for product {productId} (concurrent purchase?).");
                }

                appliedStockUpdates.Add((productId, quantity));
            }

            order.Status = OrderStatus.Completed;
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (Exception)
        {
            await CompensateStockAsync(appliedStockUpdates);
            await transaction.RollbackAsync();
            return Conflict(new { message = "Order could not be completed due to a stock conflict. Please retry." });
        }

        return CreatedAtAction(nameof(GetOrder), new { id = order.OrderId }, ToResponse(order));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderResponse>> GetOrder(int id)
    {
        var order = await _db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.OrderId == id);
        if (order is null)
        {
            return NotFound(new { message = $"Order {id} not found." });
        }

        return Ok(ToResponse(order));
    }

    // Reverses stock decrements that already succeeded in MongoDB before a later item in the
    // same order failed. This is a best-effort compensating action, not a distributed
    // transaction: if the restore itself fails (e.g. MongoDB becomes unreachable), stock is
    // left under-counted. A production system would make this durable with an outbox table
    // and a background reconciler — see the "Known limitation" note in README §6.
    private async Task CompensateStockAsync(IReadOnlyList<(string ProductId, int Quantity)> appliedStockUpdates)
    {
        foreach (var (productId, quantity) in appliedStockUpdates)
        {
            try
            {
                var restoreFilter = Builders<Product>.Filter.Eq(p => p.Id, productId);
                var restore = Builders<Product>.Update.Inc(p => p.Stock, quantity);
                await _mongo.Products.UpdateOneAsync(restoreFilter, restore);
            }
            catch (Exception)
            {
                // Swallow so that every applied update gets a restore attempt; the underlying
                // rollback/conflict response to the client is unaffected either way.
            }
        }
    }

    private static OrderResponse ToResponse(Order order) => new(
        order.OrderId,
        order.UserId,
        order.TotalAmount,
        order.Status.ToString(),
        order.CreatedAt,
        order.Items.Select(i => new OrderItemResponse(i.ProductId, i.ProductName, i.UnitPrice, i.Quantity)).ToList());
}
