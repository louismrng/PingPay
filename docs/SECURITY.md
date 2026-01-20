# Security Model

## Envelope Encryption

Private keys are protected with envelope encryption:

```
KMS (Master Key) → wraps → DEK (per wallet) → encrypts → Private Key
```

| Key | Algorithm | Storage |
|-----|-----------|---------|
| Master Key (KEK) | RSA-2048 | KMS/Key Vault |
| Data Encryption Key | AES-256 | Encrypted in DB |
| Wallet Private Key | Ed25519 | Encrypted in DB |

## Encrypted Blob Format

```
[encrypted DEK length][encrypted DEK][IV][ciphertext][auth tag]
```

Payload contains: magic header + version + timestamp + user ID + private key (93 bytes).

## Provider Configuration

**Azure Key Vault:**
```json
{
  "KeyManagement": {
    "Provider": "AzureKeyVault",
    "AzureKeyVaultUri": "https://your-vault.vault.azure.net/",
    "AzureKeyName": "pingpay-master-key"
  }
}
```

**AWS KMS:**
```json
{
  "KeyManagement": {
    "Provider": "AwsKms",
    "AwsKmsKeyId": "arn:aws:kms:us-east-1:123456789:key/abc-123",
    "AwsRegion": "us-east-1"
  }
}
```

**Local (dev only):**
```json
{
  "KeyManagement": {
    "Provider": "Local",
    "LocalDevelopmentKey": "<base64-32-byte-key>"
  }
}
```

Generate: `openssl rand -base64 32`

## Key Rotation

```csharp
// Rotate wallets from old key version
await keyRotationService.RotateWalletsWithKeyVersionAsync("old-version");

// Check version distribution
var stats = await keyRotationService.GetKeyVersionStatsAsync();

// Validate all encryptions
var (valid, invalid, ids) = await keyRotationService.ValidateAllWalletsAsync();
```

## Security Properties

- **Confidentiality:** AES-256-GCM, unique DEK per wallet
- **Integrity:** GCM auth tag, magic header validation, user ID binding
- **Key Isolation:** Master key never leaves KMS, compromising one wallet doesn't affect others

## Memory Safety

Always zero sensitive data after use:
```csharp
var privateKey = await _walletEncryption.DecryptPrivateKeyAsync(wallet);
try { /* use key */ }
finally { CryptographicOperations.ZeroMemory(privateKey); }
```
