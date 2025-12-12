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
- **AWSSDK.KeyManagementService** - AWS SDK for KMS operations
- **Amazon.Runtime** - AWS SDK core library for authentication

**Nethereum:**
- **Nethereum.Signer** - Core signing infrastructure
- **Nethereum.Accounts** - Account management
- **Nethereum.Web3** - Web3 client integration

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

## Usage Examples

### Example 1: Default Credentials Chain

```csharp
using Nethereum.Signer.AWSKeyManagement;
using Nethereum.Web3.Accounts;
using Amazon;

// DefaultCredentialsChain tries in order:
// 1. Environment variables (AWS_ACCESS_KEY_ID, AWS_SECRET_ACCESS_KEY)
// 2. IAM role (if running on EC2/Lambda/ECS)
// 3. ~/.aws/credentials file
// 4. ~/.aws/config file
var signer = new AWSKeyManagementExternalSigner(
    keyId: "alias/ethereum-key",
    region: RegionEndpoint.USEast1
);

var account = new ExternalAccount(signer, chainId: 1);
await account.InitialiseAsync();

Console.WriteLine($"Ethereum Address: {account.Address}");
```

### Example 2: IAM Role (Lambda / ECS / EC2)

```csharp
using Nethereum.Signer.AWSKeyManagement;
using Nethereum.Web3.Accounts;
using Amazon;

// Perfect for AWS Lambda, ECS, EC2
// No secrets in code - AWS manages IAM credentials automatically
var signer = new AWSKeyManagementExternalSigner(
    keyId: "alias/ethereum-key",
    region: RegionEndpoint.USEast1
);

var account = new ExternalAccount(signer, chainId: 1);
await account.InitialiseAsync();

// Now you can sign transactions without managing keys!
```

### Example 3: Access Key + Secret Key

```csharp
using Nethereum.Signer.AWSKeyManagement;
using Nethereum.Web3.Accounts;
using Amazon;

// For external services, CI/CD, non-AWS environments
// Store credentials securely (AWS Secrets Manager, parameter store, etc.)
var signer = new AWSKeyManagementExternalSigner(
    keyId: "alias/ethereum-key",
    accessKeyId: "AKIAIOSFODNN7EXAMPLE",
    accessKey: "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY",
    region: RegionEndpoint.USEast1
);

var account = new ExternalAccount(signer, chainId: 1);
await account.InitialiseAsync();

Console.WriteLine($"Authenticated with Access Key: {account.Address}");
```

### Example 4: AWS Credentials Object

```csharp
using Nethereum.Signer.AWSKeyManagement;
using Nethereum.Web3.Accounts;
using Amazon.Runtime;

// Using AWS Credentials abstraction (supports STS, profiles, etc.)
var credentials = new BasicAWSCredentials("access-key-id", "secret-access-key");
// Or: var credentials = new EnvironmentVariablesAWSCredentials();
// Or: var credentials = FallbackCredentialsFactory.GetCredentials();

var signer = new AWSKeyManagementExternalSigner(
    keyId: "alias/ethereum-key",
    credentials: credentials
);

var account = new ExternalAccount(signer, chainId: 1);
await account.InitialiseAsync();

Console.WriteLine($"Address: {account.Address}");
```

### Example 5: Sign and Send Transaction

```csharp
using Nethereum.Signer.AWSKeyManagement;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Hex.HexTypes;
using Amazon;

var signer = new AWSKeyManagementExternalSigner(
    keyId: "alias/ethereum-key",
    region: RegionEndpoint.USEast1
);

var account = new ExternalAccount(signer, chainId: 1);
await account.InitialiseAsync();

var web3 = new Web3(account, "https://mainnet.infura.io/v3/YOUR-PROJECT-ID");

// Create transaction
var transactionInput = new TransactionInput
{
    From = account.Address,
    To = "0x742d35Cc6634C0532925a3b844Bc454e4438f44e",
    Value = new HexBigInteger(1000000000000000), // 0.001 ETH
    Gas = new HexBigInteger(21000),
    GasPrice = new HexBigInteger(20000000000) // 20 gwei
};

// Signing happens in AWS KMS
Console.WriteLine("Signing with AWS KMS HSM...");
var receipt = await web3.Eth.TransactionManager
    .SendTransactionAndWaitForReceiptAsync(transactionInput);

Console.WriteLine($"Transaction mined! Hash: {receipt.TransactionHash}");
```

### Example 6: Sign EIP-1559 Transaction

```csharp
using Nethereum.Signer.AWSKeyManagement;
using Nethereum.Web3.Accounts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Hex.HexTypes;
using Amazon;

var signer = new AWSKeyManagementExternalSigner(
    keyId: "alias/ethereum-key",
    region: RegionEndpoint.USEast1
);

var account = new ExternalAccount(signer, chainId: 1);
await account.InitialiseAsync();

// EIP-1559 transaction
var transactionInput = new TransactionInput
{
    From = account.Address,
    To = "0x742d35Cc6634C0532925a3b844Bc454e4438f44e",
    Value = new HexBigInteger(1000000000000000),
    Gas = new HexBigInteger(21000),
    MaxFeePerGas = new HexBigInteger(50000000000), // 50 gwei
    MaxPriorityFeePerGas = new HexBigInteger(2000000000) // 2 gwei tip
};

var signedTx = await account.TransactionManager.SignTransactionAsync(transactionInput);
Console.WriteLine($"Signed EIP-1559: {signedTx}");
```

### Example 7: Sign Personal Message

```csharp
using Nethereum.Signer.AWSKeyManagement;
using Nethereum.Signer;
using Nethereum.Web3.Accounts;
using Amazon;
using System.Text;

var signer = new AWSKeyManagementExternalSigner(
    keyId: "alias/ethereum-key",
    region: RegionEndpoint.USEast1
);

var account = new ExternalAccount(signer, chainId: 1);
await account.InitialiseAsync();

// Message to sign
string message = "Sign this message to authenticate";
byte[] messageBytes = Encoding.UTF8.GetBytes(message);

// Sign with AWS KMS
var signature = await account.AccountSigningService
    .SignAndCalculateVAsync(messageBytes);

Console.WriteLine($"Message: {message}");
Console.WriteLine($"Signature: {signature.CreateStringSignature()}");

// Verify
var messageSigner = new EthereumMessageSigner();
var recoveredAddress = messageSigner.EncodeUTF8AndEcRecover(message, signature.CreateStringSignature());

Console.WriteLine($"Signer: {account.Address}");
Console.WriteLine($"Recovered: {recoveredAddress}");
Console.WriteLine($"Match: {account.Address.Equals(recoveredAddress, StringComparison.OrdinalIgnoreCase)}");
```

### Example 8: Multiple Keys for Different Networks

```csharp
using Nethereum.Signer.AWSKeyManagement;
using Nethereum.Web3.Accounts;
using Amazon;

var region = RegionEndpoint.USEast1;

// Mainnet account
var mainnetSigner = new AWSKeyManagementExternalSigner("alias/ethereum-mainnet", region);
var mainnetAccount = new ExternalAccount(mainnetSigner, chainId: 1);
await mainnetAccount.InitialiseAsync();
Console.WriteLine($"Mainnet: {mainnetAccount.Address}");

// Sepolia testnet account
var sepoliaSigner = new AWSKeyManagementExternalSigner("alias/ethereum-sepolia", region);
var sepoliaAccount = new ExternalAccount(sepoliaSigner, chainId: 11155111);
await sepoliaAccount.InitialiseAsync();
Console.WriteLine($"Sepolia: {sepoliaAccount.Address}");

// Polygon account
var polygonSigner = new AWSKeyManagementExternalSigner("alias/polygon-mainnet", region);
var polygonAccount = new ExternalAccount(polygonSigner, chainId: 137);
await polygonAccount.InitialiseAsync();
Console.WriteLine($"Polygon: {polygonAccount.Address}");
```

### Example 9: AWS Lambda Serverless Signing

```csharp
using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Nethereum.Signer.AWSKeyManagement;
using Nethereum.Web3.Accounts;
using Amazon;
using System.Text.Json;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

public class Function
{
    private readonly ExternalAccount _account;

    public Function()
    {
        // IAM role attached to Lambda function
        var signer = new AWSKeyManagementExternalSigner(
            keyId: Environment.GetEnvironmentVariable("KMS_KEY_ID"),
            region: RegionEndpoint.GetBySystemName(Environment.GetEnvironmentVariable("AWS_REGION"))
        );

        _account = new ExternalAccount(signer, chainId: 1);
    }

    public async Task<APIGatewayProxyResponse> GetAddress(APIGatewayProxyRequest request, ILambdaContext context)
    {
        await _account.InitialiseAsync();

        return new APIGatewayProxyResponse
        {
            StatusCode = 200,
            Body = JsonSerializer.Serialize(new { address = _account.Address }),
            Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
        };
    }

    public async Task<APIGatewayProxyResponse> SignTransaction(APIGatewayProxyRequest request, ILambdaContext context)
    {
        await _account.InitialiseAsync();

        // Parse transaction from request body
        // Sign with AWS KMS
        // Return signed transaction

        return new APIGatewayProxyResponse
        {
            StatusCode = 200,
            Body = "{ \"signature\": \"0x...\" }",
            Headers = new Dictionary<string, string> { { "Content-Type": "application/json" } }
        };
    }
}
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
| Legacy | ✅ Yes | EIP-155 with chain ID (no raw Legacy without chain ID) |
| EIP-1559 (Type 2) | ✅ Yes | MaxFeePerGas, MaxPriorityFeePerGas |
| EIP-2930 (Type 1) | ✅ Yes | Access lists |
| EIP-7702 (Type 4) | ✅ Yes | Account abstraction |

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

## License

This package is part of the Nethereum project and follows the same MIT license.
