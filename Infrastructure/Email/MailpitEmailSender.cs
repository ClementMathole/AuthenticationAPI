using System.Net.Http.Json;
using System.Text.Json;
using Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Email;

public sealed class MailpitEmailSender(HttpClient client, IConfiguration configuration) : IEmailSender
{
    public async Task SendConfirmationAsync(string email, Guid userId, string token, CancellationToken ct = default)
    {
        var publicUrl = configuration["Email:PublicApiUrl"] ?? throw new InvalidOperationException("Email:PublicApiUrl is required.");
        if (!Uri.TryCreate(publicUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new InvalidOperationException("Email:PublicApiUrl must be an absolute HTTP(S) URL.");

        var link = $"{publicUrl.TrimEnd('/')}/api/authentication/confirm" + $"?userId={userId:D}&token={Uri.EscapeDataString(token)}";
        var message = new
        {
            From = new
            {
                Email = "no-reply@authentication.test",
                Name = "Authentication API"
            },
            To = new[] { new { Email = email } },
            Subject = "Confirm your account",
            Text = $"Confirm your email using this link:\n{link}\n\n" +
                   "This link expires in 24 hours."
        };

        using var response = await client.PostAsJsonAsync(
            "api/v1/send",
            message,
            new JsonSerializerOptions { PropertyNamingPolicy = null },
            ct);

        response.EnsureSuccessStatusCode();
    }
}
