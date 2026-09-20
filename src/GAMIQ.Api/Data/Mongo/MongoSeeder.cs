using Bogus;
using GAMIQ.Api.Models.Mongo;
using MongoDB.Driver;

namespace GAMIQ.Api.Data.Mongo;

// Reproducible seed script for the MongoDB "products" and "reviews" collections.
public static class MongoSeeder
{
    private static readonly string[] Genres =
        { "Action", "RPG", "Strategy", "Simulation", "Sports", "Adventure", "Indie", "Puzzle", "Horror", "Racing" };

    private static readonly string[] Platforms =
        { "Windows", "MacOS", "Linux", "PlayStation5", "XboxSeriesX", "NintendoSwitch" };

    private static readonly string[] Tags =
        { "Multiplayer", "Singleplayer", "Co-op", "Open World", "Early Access", "VR Supported", "Controller Support" };

    public static async Task<List<Product>> SeedAsync(MongoContext mongo)
    {
        Randomizer.Seed = new Random(42);

        var existingCount = await mongo.Products.CountDocumentsAsync(FilterDefinition<Product>.Empty);
        if (existingCount == 0)
        {
            var productFaker = new Faker<Product>()
                .RuleFor(p => p.Name, f => $"{f.Commerce.ProductAdjective()} {f.Hacker.Noun()}: {f.Commerce.ProductName()}")
                .RuleFor(p => p.Description, f => f.Lorem.Paragraph())
                .RuleFor(p => p.Price, f => Math.Round(f.Random.Decimal(4.99m, 59.99m), 2))
                .RuleFor(p => p.Stock, f => f.Random.Number(0, 500))
                .RuleFor(p => p.Genres, f => f.PickRandom(Genres, f.Random.Number(1, 3)).ToList())
                .RuleFor(p => p.Platforms, f => f.PickRandom(Platforms, f.Random.Number(1, 4)).ToList())
                .RuleFor(p => p.Tags, f => f.PickRandom(Tags, f.Random.Number(1, 3)).ToList())
                .RuleFor(p => p.Attributes, f => new Dictionary<string, object>
                {
                    ["developer"] = f.Company.CompanyName(),
                    ["publisher"] = f.Company.CompanyName(),
                    ["releaseYear"] = f.Date.Past(10).Year,
                    ["ageRating"] = f.PickRandom("E", "T", "M", "AO"),
                    ["discountPercent"] = f.Random.Number(0, 75)
                })
                .RuleFor(p => p.CreatedAt, f => f.Date.Past(3));

            var products = productFaker.Generate(1200);
            await mongo.Products.InsertManyAsync(products);
        }

        var seededProducts = await mongo.Products.Find(FilterDefinition<Product>.Empty).ToListAsync();

        var reviewCount = await mongo.Reviews.CountDocumentsAsync(FilterDefinition<Review>.Empty);
        if (reviewCount == 0 && seededProducts.Count > 0)
        {
            var random = new Random(42);
            var reviewFaker = new Faker<Review>()
                .RuleFor(r => r.Rating, f => f.Random.Number(1, 5))
                .RuleFor(r => r.Comment, f => f.Lorem.Sentence())
                .RuleFor(r => r.PlaytimeHours, f => Math.Round(f.Random.Double(0.5, 400), 1))
                .RuleFor(r => r.Metadata, f => new Dictionary<string, object>
                {
                    ["platform"] = f.PickRandom(Platforms),
                    ["patchVersion"] = $"{f.Random.Number(1, 5)}.{f.Random.Number(0, 20)}.{f.Random.Number(0, 20)}",
                    ["verifiedPurchase"] = f.Random.Bool(0.8f)
                })
                .RuleFor(r => r.CreatedAt, f => f.Date.Past(2));

            var reviews = new List<Review>();
            for (var i = 0; i < 1200; i++)
            {
                var review = reviewFaker.Generate();
                review.ProductId = seededProducts[random.Next(seededProducts.Count)].Id;
                review.UserId = random.Next(1, 1001); // matches the ~1000 users seeded in PostgreSQL
                reviews.Add(review);
            }

            await mongo.Reviews.InsertManyAsync(reviews);
        }

        return seededProducts;
    }
}
