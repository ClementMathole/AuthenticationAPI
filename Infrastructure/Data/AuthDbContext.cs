using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

public sealed class AuthDbContext(DbContextOptions<AuthDbContext> options)
    : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<RefreshTokenFamily> RefreshTokenFamilies => Set<RefreshTokenFamily>();
    public DbSet<EmailConfirmationToken> EmailConfirmationTokens => Set<EmailConfirmationToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var families = modelBuilder.Entity<RefreshTokenFamily>();
        families.HasKey(x => x.Id);
        families.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        families.HasIndex(x => x.UserId);

        var refresh = modelBuilder.Entity<RefreshToken>();
        refresh.HasKey(x => x.Id);
        refresh.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        refresh.HasIndex(x => x.TokenHash).IsUnique();
        refresh.HasOne<RefreshTokenFamily>()
            .WithMany()
            .HasForeignKey(x => x.FamilyId)
            .OnDelete(DeleteBehavior.Cascade);
        refresh.HasIndex(x => x.FamilyId);

        var confirmations = modelBuilder.Entity<EmailConfirmationToken>();
        confirmations.HasKey(x => x.Id);
        confirmations.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        confirmations.HasIndex(x => x.TokenHash).IsUnique();
        confirmations.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        confirmations.HasIndex(x => x.UserId);
    }
}
