using GAMIQ.Api.Models.Mongo;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace GAMIQ.Api.Data.Mongo;

public class MongoOptions
{
    public string ConnectionString { get; set; } = null!;
    public string DatabaseName { get; set; } = null!;
}

public class MongoContext
{
    private readonly IMongoDatabase _database;

    public MongoContext(IOptions<MongoOptions> options)
    {
        var client = new MongoClient(options.Value.ConnectionString);
        _database = client.GetDatabase(options.Value.DatabaseName);
    }

    public IMongoCollection<Product> Products => _database.GetCollection<Product>("products");
    public IMongoCollection<Review> Reviews => _database.GetCollection<Review>("reviews");
}
