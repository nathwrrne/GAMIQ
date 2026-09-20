using GAMIQ.Api.Data;
using GAMIQ.Api.Data.Mongo;
using Microsoft.EntityFrameworkCore;

// Load variables from a .env file (repo root or this project's folder) into the process
// environment *before* the configuration is built, so ASP.NET Core's built-in environment
// variable provider (ConnectionStrings__Postgres, MongoDb__ConnectionString, etc.) picks them
// up. No secrets live in appsettings.json — see README §9 and .env.example.
LoadDotEnvFile();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var postgresConnectionString = builder.Configuration.GetConnectionString("Postgres");
if (string.IsNullOrWhiteSpace(postgresConnectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:Postgres is not set. Copy .env.example to .env at the repo root " +
        "(see README §2 and §9) or set the ConnectionStrings__Postgres environment variable.");
}

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(postgresConnectionString));

builder.Services.Configure<MongoOptions>(builder.Configuration.GetSection("MongoDb"));
builder.Services.AddSingleton<MongoContext>();

var app = builder.Build();

// Apply EF Core migrations and seed both databases with reproducible sample data on startup.
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var dbContext = services.GetRequiredService<AppDbContext>();
    var mongoContext = services.GetRequiredService<MongoContext>();

    var seededProducts = await MongoSeeder.SeedAsync(mongoContext);
    await DbInitializer.SeedAsync(dbContext, seededProducts);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

// Minimal, dependency-free .env loader: walks up from the current working directory looking
// for a .env file, then sets each KEY=VALUE pair as a process environment variable, without
// overwriting a variable that was already set (e.g. by docker-compose or the shell).
static void LoadDotEnvFile()
{
    var directory = Directory.GetCurrentDirectory();

    for (var depth = 0; depth < 5 && directory is not null; depth++)
    {
        var candidate = Path.Combine(directory, ".env");
        if (File.Exists(candidate))
        {
            foreach (var rawLine in File.ReadAllLines(candidate))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                {
                    continue;
                }

                var separatorIndex = line.IndexOf('=');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                var key = line[..separatorIndex].Trim();
                var value = line[(separatorIndex + 1)..].Trim().Trim('"');

                if (Environment.GetEnvironmentVariable(key) is null)
                {
                    Environment.SetEnvironmentVariable(key, value);
                }
            }

            return;
        }

        directory = Directory.GetParent(directory)?.FullName;
    }
}
