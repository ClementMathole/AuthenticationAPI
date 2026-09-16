using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Application.DTOs;

public static class AuthDtos
{
    public sealed record RegisterRequest(
        [Required, StringLength(64, MinimumLength = 3)] string Username,
        [Required, EmailAddress, StringLength(254)] string Email,
        [Required, StringLength(72, MinimumLength = 15)] string Password) : IValidatableObject
    {
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Password is not null && Encoding.UTF8.GetByteCount(Password) > 72)
                yield return new ValidationResult("Password must not exceed 72 UTF-8 bytes.", new[] { nameof(Password) });
        }
    }

    public sealed record LoginRequest
    (
        [Required, EmailAddress, StringLength(254)] string Email,
        [Required, StringLength(1024)] string Password
    );

    public sealed record ResendConfirmationRequest([Required, EmailAddress, StringLength(254)] string Email);
    public sealed record RefreshRequest([Required, StringLength(256)] string RefreshToken);
    public sealed record RevokeRequest([Required, StringLength(256)] string RevokeToken);
    public sealed record AuthResponse
    (
        string AccessToken,
        string RefreshToken,
        string TokenType = "Bearer",
        int ExpiresIn = 0
    );
}
