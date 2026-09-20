namespace GAMIQ.Api.Dtos;

public record CreateOrderItemRequest(string ProductId, int Quantity);

public record CreateOrderRequest(int UserId, List<CreateOrderItemRequest> Items);

public record OrderItemResponse(string ProductId, string ProductName, decimal UnitPrice, int Quantity);

public record OrderResponse(
    int OrderId,
    int UserId,
    decimal TotalAmount,
    string Status,
    DateTime CreatedAt,
    List<OrderItemResponse> Items);
