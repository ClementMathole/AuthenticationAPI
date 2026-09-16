namespace Domain.Entities;

public sealed class EmailConfirmationToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime Expires { get; set; }
    public DateTime? ConsumedAt { get; set; }
    public Guid? ConsumptionId { get; set; }
}
