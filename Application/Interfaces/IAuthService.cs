using static Application.DTOs.AuthDtos;

namespace Application.Interfaces
{
    public interface IAuthService
    {
        Task RegisterAsync(RegisterRequest request);
        Task<AuthResponse> LoginAsync(LoginRequest request, string ip);
        Task<AuthResponse> RefreshAsync(RefreshRequest request);
        Task RevokeAsync(RevokeRequest request, Guid authenticatedUserId);
    }
}
