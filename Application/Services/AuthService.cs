using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Application.Security;
using Domain.Entities;
using static Application.DTOs.AuthDtos;

namespace Application.Services;

public sealed class AuthService(IUserRepository users, IJwt jwt, IEmailSender email, TimeProvider clock) : IAuthService
{
    private static readonly string DummyHash = BCrypt.Net.BCrypt.HashPassword("dummy-verification-value", workFactor: 11);

    public async Task RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var address = request.Email.Trim();
        if (await users.GetByEmailAsync(address, ct) is not null)
            return;

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = address,
            Username = request.Username.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 11)
        };

        var token = OpaqueToken.Create(OpaqueToken.ConfirmationPurpose);
        await users.RegisterAsync(user, token.Hash, ct);
        await email.SendConfirmationAsync(address, user.Id, token.Value, ct);
    }

    public async Task ResendConfirmationAsync(ResendConfirmationRequest request, CancellationToken ct = default)
    {
        var user = await users.GetByEmailAsync(request.Email.Trim(), ct);
        var address = user?.Email;

        if (user is null || user.EmailConfirmed || string.IsNullOrWhiteSpace(address))
            return;

        var token = OpaqueToken.Create(OpaqueToken.ConfirmationPurpose);
        if (await users.IssueConfirmationAsync(user.Id, token.Hash, ct))
            await email.SendConfirmationAsync(address, user.Id, token.Value, ct);
    }

    public Task<bool> ConfirmEmailAsync(Guid userId, string token, CancellationToken ct = default)
    {
        var hash = OpaqueToken.Hash(token, OpaqueToken.ConfirmationPurpose);
        return userId == Guid.Empty || hash is null
            ? Task.FromResult(false)
            : users.ConfirmEmailAsync(userId, hash, ct);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await users.GetByEmailAsync(request.Email.Trim(), ct);
        if (user?.LockoutEnd > clock.GetUtcNow().UtcDateTime)
            throw new AccountLockedException();

        var hasPassword = !string.IsNullOrWhiteSpace(user?.PasswordHash);
        var validPassword = BCrypt.Net.BCrypt.Verify(
            request.Password,
            hasPassword ? user!.PasswordHash! : DummyHash);

        if (user is null || !hasPassword || !validPassword)
        {
            if (user is not null && await users.RecordFailedLoginAsync(user.Id, ct))
                throw new AccountLockedException();
            throw new UnauthorizedAccessException();
        }

        if (!user.EmailConfirmed)
            throw new UnauthorizedAccessException();

        var token = OpaqueToken.Create(OpaqueToken.RefreshPurpose);
        var session = await users.CreateSessionAsync(user.Id, token.Hash, ct) ?? throw new UnauthorizedAccessException();
        return Response(session, token.Value);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct = default)
    {
        var hash = OpaqueToken.Hash(request.RefreshToken, OpaqueToken.RefreshPurpose) ?? throw new UnauthorizedAccessException();
        var replacement = OpaqueToken.Create(OpaqueToken.RefreshPurpose);
        var session = await users.RotateRefreshTokenAsync(hash, replacement.Hash, ct) ?? throw new UnauthorizedAccessException();

        return Response(session, replacement.Value);
    }

    public Task RevokeAsync(RevokeRequest request, Guid authenticatedUserId, CancellationToken ct = default)
    {
        var hash = OpaqueToken.Hash(request.RevokeToken, OpaqueToken.RefreshPurpose);
        return hash is null
            ? Task.CompletedTask
            : users.RevokeSessionAsync(authenticatedUserId, hash, ct);
    }

    private AuthResponse Response(AuthenticatedSession session, string refreshToken)
        => new(jwt.GenerateToken(session.User, session.SessionId),
                refreshToken,
                ExpiresIn: jwt.AccessTokenMinutes * 60
              );
}
