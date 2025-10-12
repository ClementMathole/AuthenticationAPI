namespace Domain.Entities
{
    public class RefreshToken
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string? Token { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime Expires {  get; set; }
        public bool Revoked { get; set; }
        public DateTime? RevokedAt { get; set; }
        public string? ReplacedBy { get; set; }
        public Guid UserId { get; set; }
        public User? User { get; set; }
    }
}
