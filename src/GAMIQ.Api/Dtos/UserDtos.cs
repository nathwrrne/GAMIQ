namespace GAMIQ.Api.Dtos;

public record CreateUserRequest(string Username, string Email, string Password);

public record UserResponse(int UserId, string Username, string Email, DateTime CreatedAt, decimal Balance, string Currency);
