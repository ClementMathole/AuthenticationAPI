using Application.DTOs;
using Domain.Entities;

namespace Application.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task RegisterAsync(User user, string confirmationHash, CancellationToken ct = default);
    Task<bool> IssueConfirmationAsync(Guid userId, string hash, CancellationToken ct = default);
    Task<bool> ConfirmEmailAsync(Guid userId, string hash, CancellationToken ct = default);
    Task<bool> RecordFailedLoginAsync(Guid userId, CancellationToken ct = default);
    Task<AuthenticatedSession?> CreateSessionAsync(Guid userId, string hash, CancellationToken ct = default);
    Task<AuthenticatedSession?> RotateRefreshTokenAsync(string currentHash, string replacementHash, CancellationToken ct = default);
    Task RevokeSessionAsync(Guid userId, string hash, CancellationToken ct = default);
    Task<bool> IsSessionActiveAsync(Guid userId, Guid sessionId, CancellationToken ct = default);
}
