using static Application.DTOs.AuthDtos;

namespace Application.Interfaces;

public interface IAuthService
{
    Task RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task ResendConfirmationAsync(ResendConfirmationRequest request, CancellationToken ct = default);
    Task<bool> ConfirmEmailAsync(Guid userId, string token, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct = default);
    Task RevokeAsync(RevokeRequest request, Guid authenticatedUserId, CancellationToken ct = default);
}
