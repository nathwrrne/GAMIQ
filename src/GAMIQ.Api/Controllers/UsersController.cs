using GAMIQ.Api.Data;
using GAMIQ.Api.Dtos;
using GAMIQ.Api.Models.Postgres;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GAMIQ.Api.Controllers;

/// <summary>
/// Handles user-related endpoints such as creating a new account and retrieving account details.
/// </summary>
[ApiController]
[Route("api/v1/users")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly PasswordHasher<User> _passwordHasher = new();

    public UsersController(AppDbContext db)
    {
        _db = db;
    }

    // POST /api/v1/users
    // Creates a new user account and assigns a hashed password before saving it to PostgreSQL.
    [HttpPost]
    public async Task<ActionResult<UserResponse>> CreateUser(CreateUserRequest request)
    {
        // Ensure all required values are provided before processing.
        if (string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Username, email and password are required." });
        }

        // Prevent duplicate username or email values.
        var exists = await _db.Users.AnyAsync(u => u.Username == request.Username || u.Email == request.Email);
        if (exists)
        {
            return Conflict(new { message = "Username or email already exists." });
        }

        // Create the user and initialize the linked account with default wallet details.
        var user = new User
        {
            Username = request.Username,
            Email = request.Email,
            CreatedAt = DateTime.UtcNow,
            Account = new Account { Balance = 0, Currency = "THB", CreatedAt = DateTime.UtcNow }
        };

        // Hash the password before storing it in the database for security.
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var response = ToResponse(user);
        return CreatedAtAction(nameof(GetUser), new { id = user.UserId }, response);
    }

    // GET /api/v1/users/{id}
    // Retrieves a user and their account information by user ID.
    [HttpGet("{id:int}")]
    public async Task<ActionResult<UserResponse>> GetUser(int id)
    {
        // Load the profile together with its associated account record.
        var user = await _db.Users.Include(u => u.Account).FirstOrDefaultAsync(u => u.UserId == id);
        if (user is null)
        {
            return NotFound(new { message = $"User {id} not found." });
        }

        return Ok(ToResponse(user));
    }

    // Converts the entity model to the response contract used by the API.
    private static UserResponse ToResponse(User user) =>
        new(user.UserId, user.Username, user.Email, user.CreatedAt, user.Account?.Balance ?? 0, user.Account?.Currency ?? "THB");
}
