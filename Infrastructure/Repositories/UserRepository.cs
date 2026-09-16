using System.Data;
using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public sealed class UserRepository(IDbContextFactory<AuthDbContext> factory, TimeProvider clock) : IUserRepository
{
    private DateTime UtcNow => clock.GetUtcNow().UtcDateTime;

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Email == email, ct);
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
    }

    public Task RegisterAsync(User user, string confirmationHash, CancellationToken ct = default)
        => TransactionAsync(async db =>
            {
                if (await db.Users.AnyAsync(x => x.Id == user.Id, ct))
                    return true;

                db.Users.Add(user);
                db.EmailConfirmationTokens.Add(NewConfirmation(user.Id, confirmationHash));
                return true;
            }, ct);

    public Task<bool> IssueConfirmationAsync(Guid userId, string hash, CancellationToken ct = default)
        => TransactionAsync(async db =>
            {
                var user = await LockUserAsync(db, userId, ct);
                if (user is null || user.EmailConfirmed)
                    return false;

                if (await db.EmailConfirmationTokens.AnyAsync(x => x.TokenHash == hash, ct))
                    return true;

                var now = UtcNow;
                await db.EmailConfirmationTokens.Where(x => x.UserId == userId && x.ConsumedAt == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.ConsumedAt, (DateTime?)now), ct);

                db.EmailConfirmationTokens.Add(NewConfirmation(userId, hash));
                return true;
            }, ct);

    public Task<bool> ConfirmEmailAsync(Guid userId, string hash, CancellationToken ct = default)
    {
        var operationId = Guid.NewGuid();

        return TransactionAsync(async db =>
        {
            var user = await LockUserAsync(db, userId, ct);
            if (user is null)
                return false;

            var token = await db.EmailConfirmationTokens.SingleOrDefaultAsync(x => x.UserId == userId && x.TokenHash == hash, ct);
            if (token is null)
                return false;

            if (token.ConsumedAt is not null)
                return token.ConsumptionId == operationId && user.EmailConfirmed;

            var now = UtcNow;
            if (user.EmailConfirmed || token.Expires <= now)
                return false;

            token.ConsumedAt = now;
            token.ConsumptionId = operationId;
            user.EmailConfirmed = true;

            await db.EmailConfirmationTokens
                .Where(x =>
                    x.UserId == userId &&
                    x.Id != token.Id &&
                    x.ConsumedAt == null)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(x => x.ConsumedAt, (DateTime?)now),
                    ct);

            return true;
        }, ct);
    }

    public Task<bool> RecordFailedLoginAsync(Guid userId, CancellationToken ct = default)
        => TransactionAsync(async db =>
            {
                var user = await LockUserAsync(db, userId, ct);
                if (user is null)
                    return false;

                var now = UtcNow;

                if (user.LockoutEnd > now)
                    return true;

                if (user.LockoutEnd is not null)
                {
                    user.LockoutEnd = null;
                    user.FailedLoginAttempts = 0;
                }

                user.FailedLoginAttempts++;
                if (user.FailedLoginAttempts >= 5)
                {
                    user.FailedLoginAttempts = 0;
                    user.LockoutEnd = now.AddMinutes(15);
                }

                return user.LockoutEnd > now;
            }, ct);

    public Task<AuthenticatedSession?> CreateSessionAsync(Guid userId, string hash, CancellationToken ct = default)
    {
        var familyId = Guid.NewGuid();

        return TransactionAsync<AuthenticatedSession?>(async db =>
        {
            var user = await LockUserAsync(db, userId, ct);
            var now = UtcNow;
            if (user is null || !user.EmailConfirmed || user.LockoutEnd > now)
                return null;

            var existing = await db.RefreshTokenFamilies.SingleOrDefaultAsync(x => x.Id == familyId, ct);
            if (existing is not null)
                return existing.RevokedAt is null && existing.Expires > now
                    ? new AuthenticatedSession(user, familyId)
                    : null;

            user.FailedLoginAttempts = 0;
            user.LockoutEnd = null;

            db.RefreshTokenFamilies.Add(new RefreshTokenFamily
            {
                Id = familyId,
                UserId = userId,
                CreatedAt = now,
                Expires = now.AddDays(30)
            });

            db.RefreshTokens.Add(new RefreshToken
            {
                FamilyId = familyId,
                TokenHash = hash,
                CreatedAt = now,
                Expires = now.AddDays(7)
            });

            return new AuthenticatedSession(user, familyId);
        }, ct);
    }

    public Task<AuthenticatedSession?> RotateRefreshTokenAsync(string currentHash, string replacementHash, CancellationToken ct = default)
    {
        var replacementId = Guid.NewGuid();

        return TransactionAsync<AuthenticatedSession?>(async db =>
        {
            var familyId = await db.RefreshTokens
                .Where(x => x.TokenHash == currentHash)
                .Select(x => (Guid?)x.FamilyId)
                .SingleOrDefaultAsync(ct);

            if (familyId is null)
                return null;

            var family = await LockFamilyAsync(db, familyId.Value, ct);
            var now = UtcNow;
            if (family is null || family.RevokedAt is not null || family.Expires <= now)
                return null;

            var token = await db.RefreshTokens.SingleAsync(x => x.TokenHash == currentHash, ct);

            var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == family.UserId, ct);
            if (user is null || !user.EmailConfirmed || user.LockoutEnd > now)
            {
                family.RevokedAt = now;
                return null;
            }

            if (token.ConsumedAt is not null)
            {
                if (token.ReplacementTokenId == replacementId &&
                    await db.RefreshTokens.AnyAsync(x =>
                        x.Id == replacementId &&
                        x.TokenHash == replacementHash &&
                        x.ConsumedAt == null &&
                        x.Expires > now, ct))
                {
                    return new AuthenticatedSession(user, family.Id);
                }

                family.RevokedAt = now;
                return null;
            }

            if (token.Expires <= now)
                return null;

            token.ConsumedAt = now;
            token.ReplacementTokenId = replacementId;

            var expires = now.AddDays(7);

            db.RefreshTokens.Add(new RefreshToken
            {
                Id = replacementId,
                FamilyId = family.Id,
                TokenHash = replacementHash,
                CreatedAt = now,
                Expires = expires < family.Expires
                    ? expires
                    : family.Expires
            });

            return new AuthenticatedSession(user, family.Id);
        }, ct);
    }

    public Task RevokeSessionAsync(Guid userId, string hash, CancellationToken ct = default)
        => TransactionAsync(async db =>
            {
                var familyId = await db.RefreshTokens
                    .Where(x => x.TokenHash == hash)
                    .Select(x => (Guid?)x.FamilyId)
                    .SingleOrDefaultAsync(ct);

                if (familyId is null)
                    return false;

                var family = await LockFamilyAsync(db, familyId.Value, ct);
                if (family is null || family.UserId != userId)
                    return false;

                family.RevokedAt ??= UtcNow;
                return true;
            }, ct);

    public async Task<bool> IsSessionActiveAsync(Guid userId, Guid sessionId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var now = UtcNow;

        return await db.RefreshTokenFamilies.AsNoTracking().AnyAsync(x =>
            x.Id == sessionId &&
            x.UserId == userId &&
            x.RevokedAt == null &&
            x.Expires > now, ct
        );
    }

    private EmailConfirmationToken NewConfirmation(Guid userId, string hash)
    {
        var now = UtcNow;
        return new EmailConfirmationToken
        {
            UserId = userId,
            TokenHash = hash,
            CreatedAt = now,
            Expires = now.AddHours(24)
        };
    }

    private static Task<User?> LockUserAsync(AuthDbContext db, Guid id, CancellationToken ct)
        => db.Users.FromSqlInterpolated(
            $"SELECT * FROM \"Users\" WHERE \"Id\" = {id} FOR UPDATE")
            .SingleOrDefaultAsync(ct);

    private static Task<RefreshTokenFamily?> LockFamilyAsync(AuthDbContext db, Guid id, CancellationToken ct)
        => db.RefreshTokenFamilies.FromSqlInterpolated(
            $"SELECT * FROM \"RefreshTokenFamilies\" WHERE \"Id\" = {id} FOR UPDATE")
            .SingleOrDefaultAsync(ct);

    private async Task<T> TransactionAsync<T>(Func<AuthDbContext, Task<T>> action, CancellationToken ct)
    {
        await using var strategyContext = await factory.CreateDbContextAsync(ct);
        var strategy = strategyContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

            var result = await action(db);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return result;
        });
    }
}
