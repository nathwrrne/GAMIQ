using Bogus;
using GAMIQ.Api.Models.Mongo;
using GAMIQ.Api.Models.Postgres;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GAMIQ.Api.Data;

// Reproducible seed script for the PostgreSQL Users / Accounts / Orders / OrderItems tables.
public static class DbInitializer
{
    public static async Task SeedAsync(AppDbContext context, List<Product> mongoProducts)
    {
        await context.Database.MigrateAsync();

        if (await context.Users.AnyAsync())
        {
            return; // already seeded
        }

        Randomizer.Seed = new Random(42);
        var hasher = new PasswordHasher<User>();

        var userFaker = new Faker<User>()
            .RuleFor(u => u.Username, (f, _) => f.Internet.UserName().ToLowerInvariant() + f.Random.Number(1000, 9999))
            .RuleFor(u => u.Email, (f, u) => f.Internet.Email(u.Username))
            .RuleFor(u => u.CreatedAt, f => f.Date.Past(2));

        var users = userFaker.Generate(1000);
        foreach (var user in users)
        {
            user.PasswordHash = hasher.HashPassword(user, "Passw0rd!");
        }

        await context.Users.AddRangeAsync(users);
        await context.SaveChangesAsync(); // assigns UserId values

        var accountFaker = new Faker<Account>()
            .RuleFor(a => a.Balance, f => Math.Round(f.Finance.Amount(0, 5000), 2))
            .RuleFor(a => a.Currency, _ => "THB")
            .RuleFor(a => a.CreatedAt, f => f.Date.Past(2));

        var accounts = users.Select(user =>
        {
            var account = accountFaker.Generate();
            account.UserId = user.UserId;
            return account;
        }).ToList();

        await context.Accounts.AddRangeAsync(accounts);
        await context.SaveChangesAsync();

        if (mongoProducts.Count > 0)
        {
            var random = new Random(42);
            var orders = new List<Order>();

            for (var i = 0; i < 400; i++)
            {
                var user = users[random.Next(users.Count)];
                var itemCount = random.Next(1, 4);
                var items = new List<OrderItem>();
                decimal total = 0m;

                for (var j = 0; j < itemCount; j++)
                {
                    var product = mongoProducts[random.Next(mongoProducts.Count)];
                    var quantity = random.Next(1, 3);
                    items.Add(new OrderItem
                    {
                        ProductId = product.Id,
                        ProductName = product.Name,
                        UnitPrice = product.Price,
                        Quantity = quantity
                    });
                    total += product.Price * quantity;
                }

                orders.Add(new Order
                {
                    UserId = user.UserId,
                    TotalAmount = total,
                    Status = OrderStatus.Completed,
                    CreatedAt = DateTime.UtcNow.AddDays(-random.Next(0, 365)),
                    Items = items
                });
            }

            await context.Orders.AddRangeAsync(orders);
            await context.SaveChangesAsync();
        }
    }
}
