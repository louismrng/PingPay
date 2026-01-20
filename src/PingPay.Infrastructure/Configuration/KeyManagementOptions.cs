namespace PingPay.Infrastructure.Configuration;

public class KeyManagementOptions
{
    public const string SectionName = "KeyManagement";

    /// <summary>
    /// Provider: "AzureKeyVault" or "AwsKms" or "Local" (for development only).
    /// </summary>
    public string Provider { get; set; } = "Local";

    /// <summary>
    /// Azure Key Vault URI.
    /// </summary>
    public string? AzureKeyVaultUri { get; set; }

    /// <summary>
    /// Azure Key Vault key name.
    /// </summary>
    public string? AzureKeyName { get; set; }

    /// <summary>
    /// AWS KMS key ID or ARN.
    /// </summary>
    public string? AwsKmsKeyId { get; set; }

    /// <summary>
    /// AWS region.
    /// </summary>
    public string? AwsRegion { get; set; }

    /// <summary>
    /// Local development key (32 bytes, base64 encoded). DO NOT USE IN PRODUCTION.
    /// </summary>
    public string? LocalDevelopmentKey { get; set; }
}
