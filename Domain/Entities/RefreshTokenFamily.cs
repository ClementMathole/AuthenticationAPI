namespace Domain.Entities;

public sealed class RefreshTokenFamily
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime Expires { get; set; }
    public DateTime? RevokedAt { get; set; }
}
