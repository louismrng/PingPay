# PingPay Security Model

## Overview

PingPay uses **envelope encryption** to protect Solana wallet private keys. This document describes the cryptographic approach and security considerations.

## Encryption Architecture

### Envelope Encryption

Envelope encryption uses two layers of keys:

```
┌─────────────────────────────────────────────────────────────┐
│                    KMS / Key Vault                          │
│                   (Master Key - KEK)                        │
│                         │                                   │
│                         ▼                                   │
│              ┌─────────────────┐                            │
│              │   Wrapped DEK   │  ← RSA-OAEP-256           │
│              └────────┬────────┘                            │
└───────────────────────┼─────────────────────────────────────┘
                        │
                        ▼ Decrypt with KMS
              ┌─────────────────┐
              │  Plaintext DEK  │  ← AES-256 key
              └────────┬────────┘
                        │
                        ▼ AES-256-GCM
              ┌─────────────────┐
              │  Private Key    │
              └─────────────────┘
```

### Key Types

| Key | Size | Algorithm | Storage | Rotation |
|-----|------|-----------|---------|----------|
| Master Key (KEK) | 2048+ bits | RSA | KMS/Key Vault | Annual |
| Data Encryption Key (DEK) | 256 bits | AES-GCM | Encrypted in DB | Per-wallet |
| Wallet Private Key | 64 bytes | Ed25519 | Encrypted in DB | Never |

## Encryption Process

### Wallet Creation

1. Generate Solana keypair (Ed25519)
2. Create payload with magic header + version + timestamp + user ID + private key
3. Generate random 256-bit DEK
4. Encrypt DEK with KMS master key (RSA-OAEP-256)
5. Encrypt payload with DEK (AES-256-GCM with random 96-bit IV)
6. Store: `[encrypted DEK length][encrypted DEK][IV][ciphertext][auth tag]`
7. Securely zero plaintext DEK and private key from memory

### Decryption Process

1. Extract encrypted DEK and encrypted payload from blob
2. Decrypt DEK using KMS
3. Decrypt payload using DEK (AES-256-GCM)
4. Validate magic header, version, and user ID
5. Extract and validate private key matches public key
6. Securely zero DEK from memory after use

## Payload Format

```
Offset  Size  Field
──────  ────  ─────────────────────────
0       4     Magic header ("PPWK")
4       1     Format version (1)
5       8     Unix timestamp (seconds)
13      16    User ID (GUID)
29      64    Ed25519 private key
──────────────────────────────────────
Total: 93 bytes
```

## Security Properties

### Confidentiality
- AES-256-GCM provides 256-bit security
- Master key never leaves KMS hardware boundary
- DEKs are unique per encryption operation

### Integrity
- GCM authentication tag detects tampering
- Magic header detects format corruption
- User ID binding prevents key substitution attacks

### Key Isolation
- Each wallet has a unique DEK
- Compromising one wallet's DEK doesn't affect others
- Master key rotation doesn't require re-encrypting all wallets

## Key Management Providers

### Azure Key Vault (Production)
```json
{
  "KeyManagement": {
    "Provider": "AzureKeyVault",
    "AzureKeyVaultUri": "https://your-vault.vault.azure.net/",
    "AzureKeyName": "pingpay-master-key"
  }
}
```

**Setup:**
```bash
# Create Key Vault
az keyvault create --name your-vault --resource-group your-rg

# Create RSA key for envelope encryption
az keyvault key create --vault-name your-vault --name pingpay-master-key \
  --kty RSA --size 2048 --ops encrypt decrypt wrapKey unwrapKey
```

### AWS KMS (Production)
```json
{
  "KeyManagement": {
    "Provider": "AwsKms",
    "AwsKmsKeyId": "arn:aws:kms:us-east-1:123456789:key/abc-123",
    "AwsRegion": "us-east-1"
  }
}
```

**Setup:**
```bash
# Create symmetric KMS key
aws kms create-key --description "PingPay wallet encryption key"
```

### Local Development (DO NOT USE IN PRODUCTION)
```json
{
  "KeyManagement": {
    "Provider": "Local",
    "LocalDevelopmentKey": "base64-encoded-32-byte-key"
  }
}
```

Generate a development key:
```bash
openssl rand -base64 32
```

## Key Rotation

### When to Rotate
- Annually (compliance requirement)
- After suspected compromise
- When staff with access leave

### Rotation Process
1. Create new key version in KMS
2. Run rotation job for affected wallets:
```csharp
await keyRotationService.RotateWalletsWithKeyVersionAsync("old-version");
```
3. Monitor for failed rotations
4. Disable old key version after all wallets migrated

### Rotation Monitoring
```csharp
// Check key version distribution
var stats = await keyRotationService.GetKeyVersionStatsAsync();

// Validate all wallets can be decrypted
var (valid, invalid, ids) = await keyRotationService.ValidateAllWalletsAsync();
```

## Security Checklist

### Must Have (MVP)
- [x] Envelope encryption with cloud KMS
- [x] Unique DEK per wallet
- [x] AES-256-GCM with random IV
- [x] Memory zeroing after use
- [x] User ID binding in payload
- [x] Key version tracking for rotation

### Should Have (Post-MVP)
- [ ] HSM-backed keys in KMS
- [ ] Automated key rotation alerts
- [ ] Decryption audit logging
- [ ] Rate limiting on decryption
- [ ] Anomaly detection on access patterns

### Nice to Have
- [ ] Multi-party computation for signing
- [ ] Threshold signatures
- [ ] Hardware security module integration

## Incident Response

### Suspected Key Compromise

1. **Immediate:** Enable maintenance mode
2. **Assess:** Determine scope of compromise
3. **Rotate:** Create new master key version
4. **Re-encrypt:** Run key rotation for all wallets
5. **Audit:** Review access logs
6. **Notify:** Inform affected users if required

### Database Breach

If encrypted wallet data is exfiltrated:
- Master key in KMS is NOT compromised
- Attacker cannot decrypt without KMS access
- Rotate master key as precaution
- No user action required (keys remain safe)

## Compliance Notes

### PCI-DSS Considerations
- Private keys are encrypted at rest ✓
- Key management uses dedicated hardware (KMS) ✓
- Separation of duties (API cannot access master key directly) ✓

### SOC 2 Considerations
- Encryption algorithms are industry standard ✓
- Key rotation capability exists ✓
- Audit logging for key operations ✓

## Testing

Run encryption tests:
```bash
dotnet test --filter "FullyQualifiedName~Encryption"
```

Validate production encryption:
```csharp
var (valid, invalid, ids) = await keyRotationService.ValidateAllWalletsAsync();
Console.WriteLine($"Valid: {valid}, Invalid: {invalid}");
```
