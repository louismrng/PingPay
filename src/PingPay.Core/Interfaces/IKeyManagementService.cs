namespace PingPay.Core.Interfaces;

public interface IKeyManagementService
{
    /// <summary>
    /// Encrypts data using envelope encryption.
    /// </summary>
    /// <param name="plaintext">Data to encrypt</param>
    /// <returns>Encrypted blob containing IV, encrypted DEK, and encrypted data</returns>
    Task<(string EncryptedBlob, string KeyVersion)> EncryptAsync(byte[] plaintext, CancellationToken ct = default);

    /// <summary>
    /// Decrypts data that was encrypted using envelope encryption.
    /// </summary>
    /// <param name="encryptedBlob">The encrypted blob</param>
    /// <param name="keyVersion">The key version used for encryption</param>
    /// <returns>Decrypted plaintext</returns>
    Task<byte[]> DecryptAsync(string encryptedBlob, string keyVersion, CancellationToken ct = default);
}
