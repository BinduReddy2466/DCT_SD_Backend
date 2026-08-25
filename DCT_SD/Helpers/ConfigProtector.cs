using System.Security.Cryptography;

namespace DCT_SD.Helpers;

// Encrypts/decrypts config values (e.g. connection strings) so the ciphertext can be committed
// to appsettings.json while the AES-256 key stays out of source control (env var / user secrets).
// Format: base64( 12-byte nonce | 16-byte GCM tag | ciphertext ).
public static class ConfigProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public static string Encrypt(string plainText, string base64Key)
    {
        var key = Convert.FromBase64String(base64Key);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plainBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using var aesGcm = new AesGcm(key, TagSize);
        aesGcm.Encrypt(nonce, plainBytes, cipherBytes, tag);

        var result = new byte[NonceSize + TagSize + cipherBytes.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, result, NonceSize, TagSize);
        Buffer.BlockCopy(cipherBytes, 0, result, NonceSize + TagSize, cipherBytes.Length);

        return Convert.ToBase64String(result);
    }

    public static string Decrypt(string base64CipherText, string base64Key)
    {
        var key = Convert.FromBase64String(base64Key);
        var data = Convert.FromBase64String(base64CipherText);

        var nonce = data[..NonceSize];
        var tag = data[NonceSize..(NonceSize + TagSize)];
        var cipherBytes = data[(NonceSize + TagSize)..];
        var plainBytes = new byte[cipherBytes.Length];

        using var aesGcm = new AesGcm(key, TagSize);
        aesGcm.Decrypt(nonce, cipherBytes, tag, plainBytes);

        return System.Text.Encoding.UTF8.GetString(plainBytes);
    }

    // Names of ADO.NET connection-string fields whose values are stored encrypted in
    // appsettings.json. Everything else (Encrypt, TrustServerCertificate, etc.) stays plain.
    private static readonly string[] SensitiveFieldNames =
        ["Server", "Data Source", "Database", "Initial Catalog", "User Id", "Uid", "Password", "Pwd"];

    // A connection string keeps its normal "Key=Value; Key=Value" shape, but the value of each
    // sensitive field (server, database, user, password) is itself ciphertext produced by
    // Encrypt(). This decrypts just those values and reassembles a real connection string,
    // leaving structural/non-secret flags (Encrypt=True, TrustServerCertificate=True, ...) as-is.
    public static string DecryptConnectionString(string connectionString, string base64Key)
    {
        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var decryptedParts = new List<string>(parts.Length);

        foreach (var part in parts)
        {
            var separatorIndex = part.IndexOf('=');
            if (separatorIndex < 0)
            {
                decryptedParts.Add(part);
                continue;
            }

            var fieldName = part[..separatorIndex].Trim();
            var fieldValue = part[(separatorIndex + 1)..].Trim();

            if (SensitiveFieldNames.Contains(fieldName, StringComparer.OrdinalIgnoreCase))
            {
                fieldValue = Decrypt(fieldValue, base64Key);
            }

            decryptedParts.Add($"{fieldName}={fieldValue}");
        }

        return string.Join(';', decryptedParts);
    }
}
