namespace Application.DTOs
{
    public class AuthDtos
    {
        public record RegisterRequest(string Username, string Email, string Password);
        public record LoginRequest(string Email, string Password);
        public record RefreshRequest(string RefreshToken);
        public record RevokeRequest(string RevokeToken);
        public record AuthResponse(string AccessToken, string RefreshToken, string TokenType = "Bearer");
    }
}
