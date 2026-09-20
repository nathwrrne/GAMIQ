namespace GAMIQ.Api.Dtos;

public record CreateProductRequest(
    string Name,
    string? Description,
    decimal Price,
    int Stock,
    List<string>? Genres,
    List<string>? Platforms,
    List<string>? Tags,
    Dictionary<string, object>? Attributes);

public record ProductResponse(
    string Id,
    string Name,
    string Description,
    decimal Price,
    int Stock,
    List<string> Genres,
    List<string> Platforms,
    List<string> Tags,
    Dictionary<string, object> Attributes,
    DateTime CreatedAt);

public record PagedResult<T>(List<T> Items, int Page, int PageSize, long TotalCount);
