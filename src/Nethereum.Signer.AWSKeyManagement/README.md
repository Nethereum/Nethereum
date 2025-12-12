# Nethereum.Signer.AWSKeyManagement

AWS Key Management Service (KMS) integration for Ethereum transaction signing with cloud-based Hardware Security Module (HSM) backed keys.

## Overview

Nethereum.Signer.AWSKeyManagement provides **external signing capability** for Ethereum transactions and messages using **AWS Key Management Service** as a secure key management solution. Private keys are generated and stored in AWS's FIPS 140-2 Level 2/3 validated HSMs, and signing operations are performed remotely without exposing the private key.

**Key Features:**
- Cloud-based HSM (Hardware Security Module) signing
- Private keys never leave AWS KMS
- FIPS 140-2 Level 2 (standard keys) or Level 3 (CloudHSM) validated
- Support for Legacy, EIP-1559, and EIP-7702 transactions
- Message signing with secp256k1 (ECDSA_SHA_256)
- IAM-based authentication (IAM roles, access keys, credentials chain)
- Scalable for enterprise and serverless architectures
- CloudTrail audit logging and access control via IAM policies

**Use Cases:**
- Enterprise custody solutions
- Serverless transaction signing (AWS Lambda, ECS)
- Multi-region hot wallet infrastructure
- Regulatory compliance requiring HSM-backed keys
- Secure key management without on-premises HSM hardware
- API-based signing services

## Installation

```bash
dotnet add package Nethereum.Signer.AWSKeyManagement
```

## Dependencies

**External:**
- **AWSSDK.KeyManagementService** (v3.7.4.13) - AWS SDK for KMS operations
- **System.Security.Cryptography.Algorithms** (v4.3.1) - Cryptographic operations

**Nethereum:**
- **Nethereum.Signer** - Core signing infrastructure (provides EthExternalSignerBase)

## Prerequisites

### AWS Setup

1. **Create KMS Key:**
   ```bash
   aws kms create-key \
     --key-spec ECC_SECG_P256K1 \
     --key-usage SIGN_VERIFY \
     --description "Ethereum signing key"
   ```

2. **Create Key Alias (Optional but Recommended):**
   ```bash
   aws kms create-alias \
     --alias-name alias/ethereum-mainnet \
     --target-key-id <key-id-from-step-1>
   ```

3. **Configure IAM Policy:**
   ```json
   {
     "Version": "2012-10-17",
     "Statement": [
       {
         "Effect": "Allow",
         "Action": [
           "kms:GetPublicKey",
           "kms:Sign"
         ],
         "Resource": "arn:aws:kms:us-east-1:123456789012:key/*"
       }
     ]
   }
   ```

### Authentication Options

- **Default Credentials Chain** - Auto-detects: Environment variables, IAM role, config file, etc.
- **IAM Role** - For EC2, Lambda, ECS, Fargate (recommended)
- **Access Key + Secret** - For external services
- **Temporary Credentials** - STS AssumeRole
- **AWS Profile** - Named profile from ~/.aws/credentials

## Quick Start

```csharp
using Nethereum.Signer.AWSKeyManagement;
using Nethereum.Web3.Accounts;
using Amazon;

// Use default AWS credentials chain
var signer = new AWSKeyManagementExternalSigner(
    keyId: "alias/ethereum-mainnet", // or key ARN/ID
    region: RegionEndpoint.USEast1
);

// Create external account
var account = new ExternalAccount(signer, chainId: 1);
await account.InitialiseAsync();

// Use with Web3
var web3 = new Web3.Web3(account, "https://mainnet.infura.io/v3/YOUR-PROJECT-ID");

Console.WriteLine($"Address: {account.Address}");
```

## API Reference

### AWSKeyManagementExternalSigner

External signer implementation for AWS KMS.

```csharp
public class AWSKeyManagementExternalSigner : EthExternalSignerBase
{
    // Constructors
    public AWSKeyManagementExternalSigner(string keyId, string accessKeyId, string accessKey, RegionEndpoint region);
    public AWSKeyManagementExternalSigner(string keyId, AWSCredentials credentials);
    public AWSKeyManagementExternalSigner(string keyId, RegionEndpoint region);
    public AWSKeyManagementExternalSigner(IAmazonKeyManagementService keyClient, string keyId);

    // Properties
    protected IAmazonKeyManagementService KeyClient { get; }
    public string KeyId { get; }
    public override bool CalculatesV { get; } = false;
    public override ExternalSignerTransactionFormat ExternalSignerTransactionFormat { get; } = ExternalSignerTransactionFormat.Hash;
    public override bool Supported1559 { get; } = true;

    // Methods
    protected override Task<byte[]> GetPublicKeyAsync();
    protected override Task<ECDSASignature> SignExternallyAsync(byte[] hash);
    public override Task SignAsync(LegacyTransaction transaction);
    public override Task SignAsync(LegacyTransactionChainId transaction);
    public override Task SignAsync(Transaction1559 transaction);
    public override Task SignAsync(Transaction7702 transaction);
}
```

## Important Notes

### Key Creation

```bash
# Standard KMS key (FIPS 140-2 Level 2)
aws kms create-key \
  --key-spec ECC_SECG_P256K1 \
  --key-usage SIGN_VERIFY \
  --description "Ethereum mainnet signing key"

# With CloudHSM (FIPS 140-2 Level 3)
# Requires AWS CloudHSM cluster setup
```

**Important:**
- Use `ECC_SECG_P256K1` (Ethereum's secp256k1 curve)
- Set `key-usage` to `SIGN_VERIFY`
- Keys cannot be exported from AWS KMS
- Use key aliases for easier management

### Authentication Methods

| Method | Use Case | Environment |
|--------|----------|-------------|
| **Default Chain** | Auto-detect | Any AWS environment |
| **IAM Role** | Lambda, ECS, EC2 | AWS compute resources |
| **Access Key** | External services | Non-AWS environments |
| **Temporary Credentials** | Cross-account access | STS AssumeRole |
| **Profile** | Local development | ~/.aws/credentials |

### Transaction Types Supported

| Type | Supported | Notes |
|------|-----------|-------|
| Legacy | Yes | EIP-155 with chain ID (no raw Legacy without chain ID) |
| EIP-1559 (Type 2) | Yes | MaxFeePerGas, MaxPriorityFeePerGas |
| EIP-2930 (Type 1) | Yes | Access lists |
| EIP-7702 (Type 4) | Yes | Account abstraction |

### Security Considerations

**Private Key Security:**
- Private keys **never leave** AWS KMS
- Signing operations performed server-side in AWS HSMs
- Standard KMS: FIPS 140-2 Level 2 validated
- CloudHSM: FIPS 140-2 Level 3 validated
- Keys cannot be exported by anyone, including AWS

**IAM Policy Best Practices:**
```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "AllowEthereumSigning",
      "Effect": "Allow",
      "Action": [
        "kms:GetPublicKey",
        "kms:Sign"
      ],
      "Resource": "arn:aws:kms:us-east-1:123456789012:key/12345678-1234-1234-1234-123456789012",
      "Condition": {
        "StringEquals": {
          "kms:SigningAlgorithm": "ECDSA_SHA_256"
        }
      }
    }
  ]
}
```

**Audit Logging:**
- Enable AWS CloudTrail for KMS
- All signing operations logged
- View in CloudWatch Logs or CloudTrail console
- Example log entry:
  ```json
  {
    "eventName": "Sign",
    "requestParameters": {
      "keyId": "alias/ethereum-key",
      "signingAlgorithm": "ECDSA_SHA_256"
    }
  }
  ```

### Cost Considerations

| Operation | Cost (US East 1) | Notes |
|-----------|------------------|-------|
| **Key storage** | $1/month per key | Asymmetric keys |
| **Sign operation** | $0.03 per 10,000 | ECDSA signatures |
| **GetPublicKey** | Free | No charge |
| **CloudHSM** | ~$1.50/hour | Premium tier |

**Optimization Tips:**
- Cache public key (doesn't change)
- Use IAM roles (no credentials management)
- Monitor usage with CloudWatch
- Consider AWS Free Tier (1 million requests/month for new accounts)

### Error Handling

```csharp
using Amazon.KeyManagementService.Model;

try
{
    var signature = await account.TransactionManager.SignTransactionAsync(transactionInput);
}
catch (AccessDeniedException ex)
{
    // IAM permissions insufficient
    Console.WriteLine($"Access denied: {ex.Message}");
}
catch (NotFoundException ex)
{
    // Key not found or alias doesn't exist
    Console.WriteLine($"Key not found: {ex.Message}");
}
catch (KMSInvalidStateException ex)
{
    // Key disabled or pending deletion
    Console.WriteLine($"Invalid key state: {ex.Message}");
}
catch (AmazonServiceException ex)
{
    // Other AWS errors
    Console.WriteLine($"AWS error: {ex.ErrorCode} - {ex.Message}");
}
```

### Performance

- **Latency**: ~100-300ms per signing operation (network + HSM)
- **Throughput**: Thousands of operations per second per key
- **Caching**: Cache public key to avoid repeated KMS calls

### Regions

AWS KMS is available in all AWS regions. Use the region closest to your application:

```csharp
// Common regions
RegionEndpoint.USEast1      // Virginia
RegionEndpoint.USWest2      // Oregon
RegionEndpoint.EUWest1      // Ireland
RegionEndpoint.APNortheast1 // Tokyo
RegionEndpoint.APSoutheast1 // Singapore
```

### Comparison with Other Solutions

| Solution | Security | Cost | Latency | Use Case |
|----------|----------|------|---------|----------|
| **AWS KMS** | HSM-backed | Medium | ~200ms | AWS-based infrastructure |
| **Azure Key Vault** | HSM-backed | Medium | ~200ms | Azure-based infrastructure |
| **Ledger/Trezor** | Hardware wallet | Low | User-dependent | Development, manual signing |
| **HDWallet** | Software | Free | <1ms | Development, non-production |

## Related Packages

### Used By (Consumers)
- Enterprise custody solutions
- Serverless signing services (Lambda-based)
- Multi-region hot wallet infrastructure
- API-based signing platforms

### Dependencies
- **Nethereum.Signer** - Core signing
- **AWSSDK.KeyManagementService** - AWS SDK for KMS

### Alternatives
- **Nethereum.Signer.AzureKeyVault** - Azure Key Vault integration
- **Nethereum.Signer.Ledger** - Ledger hardware wallet
- **Nethereum.Signer.Trezor** - TREZOR hardware wallet

## Additional Resources

- [AWS KMS Documentation](https://docs.aws.amazon.com/kms/)
- [AWS KMS Developer Guide](https://docs.aws.amazon.com/kms/latest/developerguide/)
- [FIPS 140-2 Validation](https://csrc.nist.gov/projects/cryptographic-module-validation-program)
- [AWS SDK for .NET](https://aws.amazon.com/sdk-for-net/)
- [Nethereum Documentation](http://docs.nethereum.com/)
