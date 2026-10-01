namespace UnoTP.Models;

/// <summary>The portal's decryption service: turns what the portal encrypted into plain text.</summary>
public interface IDecryptionService
{
    /// <summary>The plain text, or null when the value cannot be decrypted - tampered with, or not the portal's.</summary>
    Task<string?> DecryptAsync(string cipherText, CancellationToken ct = default);
}
