using Domain.Entities;

namespace Application.Interfaces
{
    public interface IJwt
    {
        string GenerateToken(User user);
        int AccessTokenMinutes { get; }
    }
}
