using Domain.Entities;

namespace Application.Interfaces
{
    public interface IUserRepository
    {
        Task<User?> GetByEmailAsync(string email);
        Task<User?> GetByIdAsync(Guid id);
        Task AddAsync(User user);
        Task UpdateAsync(User user);
        Task<RefreshToken?> GetRefreshTokenAsync(string token);
        Task AddRefreshTokenAync(RefreshToken token);
        Task RevokeRefreshTokenAsync(RefreshToken token);
    }
}
