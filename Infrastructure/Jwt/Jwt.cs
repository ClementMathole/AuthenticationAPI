using Application.Interfaces;
using Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Infrastructure.Jwt
{
    public class Jwt : IJwt
    {
        private readonly string _key;
        private readonly string _issuer;
        private readonly string _audience;
        private readonly int _accessTokenMinutes;

        public Jwt(IConfiguration configuration)
        {
            var jwtSection = configuration.GetSection("Jwt");

            _key = jwtSection.GetValue<string>("Key")
                   ?? throw new InvalidOperationException("JWT Key is not configured.");
            _issuer = jwtSection.GetValue<string>("Issuer")
                      ?? throw new InvalidOperationException("JWT Issuer is not configured.");
            _audience = jwtSection.GetValue<string>("Audience")
                        ?? throw new InvalidOperationException("JWT Audience is not configured.");
            _accessTokenMinutes = jwtSection.GetValue<int>("AccessTokenMinutes");
        }

        public int AccessTokenMinutes => _accessTokenMinutes;

        public string GenerateToken(User user)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Email ?? string.Empty),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            };

            var token = new JwtSecurityToken(
                issuer: _issuer,
                audience: _audience,
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddMinutes(_accessTokenMinutes),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
