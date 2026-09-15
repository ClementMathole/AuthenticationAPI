using Application.Interfaces;
using Domain.Entities;
using System.Security.Cryptography;
using static Application.DTOs.AuthDtos;

namespace Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _user;
        private readonly IJwt _jwt;
        private readonly IEmailSender _email;

        public AuthService(IUserRepository user, IJwt jwt, IEmailSender email)
        {
            _user = user;
            _jwt = jwt;
            _email = email;
        }

        public async Task RegisterAsync(RegisterRequest request)
        {
            var exists = await _user.GetByEmailAsync(request.Email);
            if (exists != null)
                throw new InvalidOperationException("Email alredy registered");

            var user = new User
            {
                Email = request.Email,
                Username = request.Username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
            };
            await _user.AddAsync(user);

            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
            var refreshToken = new RefreshToken
            {
                Token = token,
                UserId = user.Id,
                Expires = DateTime.UtcNow.AddHours(24)
            };
            await _user.AddRefreshTokenAync(refreshToken);

            var link = $"/api/authentication/confirm?userId={user.Id}&token={Uri.EscapeDataString(token)}";
            await _email.SendEmailAsync(user.Email, "Confirm your account", $"Click to confirm: {link}");
        }

        public async Task<AuthResponse> LoginAsync(LoginRequest request, string ip)
        {
            var user = await _user.GetByEmailAsync(request.Email);
            if (user == null)
                throw new UnauthorizedAccessException("Invalid credentials");

            if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.UtcNow)
                throw new InvalidOperationException($"Account has been locked, wait until {user.LockoutEnd.Value:u}");

            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                user.FailedLoginAttempts++;
                if (user.FailedLoginAttempts >= 5)
                {
                    user.LockoutEnd = DateTime.UtcNow.AddMinutes(15);
                    user.FailedLoginAttempts = 0;
                }
                await _user.UpdateAsync(user);
                throw new UnauthorizedAccessException("Invalid credentials");
            }

            user.FailedLoginAttempts = 0;
            user.LockoutEnd = null;
            await _user.UpdateAsync(user);

            var access = _jwt.GenerateToken(user);
            var refresh = new RefreshToken
            {
                Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
                UserId = user.Id,
                Expires = DateTime.UtcNow.AddDays(7),
            };
            await _user.AddRefreshTokenAync(refresh);
            return new AuthResponse(access, refresh.Token);
        }

        public async Task<AuthResponse> RefreshAsync(RefreshRequest request)
        {
            var storedToken = await _user.GetRefreshTokenAsync(request.RefreshToken);
            if (storedToken is null || storedToken.Revoked || storedToken.Expires <= DateTime.UtcNow)
                throw new UnauthorizedAccessException("Invalid refresh token");

            await _user.RevokeRefreshTokenAsync(storedToken);
            var user = await _user.GetByIdAsync(storedToken.UserId);

            if (user == null)
                throw new UnauthorizedAccessException("Invalid token owner");

            var access = _jwt.GenerateToken(user);
            var newRefreshToken = new RefreshToken
            {
                Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
                UserId = user.Id,
                Expires = DateTime.UtcNow.AddDays(7),
                ReplacedBy = null
            };

            await _user.AddRefreshTokenAync(newRefreshToken);
            storedToken.ReplacedBy = newRefreshToken.Token;
            await _user.RevokeRefreshTokenAsync(storedToken);
            return new AuthResponse(access, newRefreshToken.Token);
        }

        public async Task RevokeAsync(RevokeRequest request, Guid authenticatedUserId)
        {
            var storedToken = await _user.GetRefreshTokenAsync(request.RevokeToken);
            if (storedToken is null || storedToken.UserId != authenticatedUserId || storedToken.Revoked)
                return;
            await _user.RevokeRefreshTokenAsync(storedToken);
        }
    }
}
