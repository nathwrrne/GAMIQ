namespace GAMIQ.Api.Models.Postgres;

// One wallet/account per user, holding the balance used to pay for orders.
public class Account
{
    public int AccountId { get; set; }
    public int UserId { get; set; }
    public decimal Balance { get; set; }
    public string Currency { get; set; } = "THB";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
