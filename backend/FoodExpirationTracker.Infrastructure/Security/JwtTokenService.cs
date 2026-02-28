using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FoodExpirationTracker.Application.Abstractions;

namespace FoodExpirationTracker.Infrastructure.Security;

public class JwtTokenService : ITokenService
{
    private readonly byte[] _key;

    public JwtTokenService(string secret)
    {
        _key = Encoding.UTF8.GetBytes(secret);
    }

    public string GenerateToken(Guid userId, string email, string role)
    {
        var header = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new { alg = "HS256", typ = "JWT" }));
        var exp = DateTimeOffset.UtcNow.AddHours(12).ToUnixTimeSeconds();
        var payload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new { sub = userId, email, role, exp }));
        var signature = ComputeSignature($"{header}.{payload}");
        return $"{header}.{payload}.{signature}";
    }

    public bool TryValidateToken(string token, out Guid userId)
    {
        userId = Guid.Empty;
        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return false;
        }

        var computed = ComputeSignature($"{parts[0]}.{parts[1]}");
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(computed), Encoding.UTF8.GetBytes(parts[2])))
        {
            return false;
        }

        var payloadBytes = Base64UrlDecode(parts[1]);
        using var doc = JsonDocument.Parse(payloadBytes);
        var root = doc.RootElement;

        if (!root.TryGetProperty("exp", out var expElement) || DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expElement.GetInt64())
        {
            return false;
        }

        if (!root.TryGetProperty("sub", out var subElement) || !Guid.TryParse(subElement.ToString(), out userId))
        {
            return false;
        }

        return true;
    }

    private string ComputeSignature(string message)
    {
        using var hmac = new HMACSHA256(_key);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        return Base64UrlEncode(hash);
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string text)
    {
        text = text.Replace('-', '+').Replace('_', '/');
        switch (text.Length % 4)
        {
            case 2: text += "=="; break;
            case 3: text += "="; break;
        }

        return Convert.FromBase64String(text);
    }
}
