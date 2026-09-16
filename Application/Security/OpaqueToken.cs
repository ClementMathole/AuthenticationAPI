using System.Security.Cryptography;
using System.Text;

namespace Application.Security;

public static class OpaqueToken
{
    public const string RefreshPurpose = "refresh-v1";
    public const string ConfirmationPurpose = "email-confirmation-v1";

    public static (string Value, string Hash) Create(string purpose)
    {
        var value = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        return (value, Hash(value, purpose)!);
    }

    public static string? Hash(string? value, string purpose)
    {
        if (value is null || value.Length != 64 || !value.All(char.IsAsciiHexDigit))
            return null;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{purpose}:{value}")));
    }
}
