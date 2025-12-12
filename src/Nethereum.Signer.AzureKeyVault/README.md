# Nethereum.Signer.AzureKeyVault

Azure Key Vault integration for Ethereum transaction signing with cloud-based Hardware Security Module (HSM) backed keys.

## Overview

Nethereum.Signer.AzureKeyVault provides **external signing capability** for Ethereum transactions and messages using **Azure Key Vault** as a secure key management solution. Private keys are generated and stored in Azure's FIPS 140-2 Level 2 validated HSMs, and signing operations are performed remotely without exposing the private key.

**Key Features:**
- Cloud-based HSM (Hardware Security Module) signing
- Private keys never leave Azure Key Vault
- FIPS 140-2 Level 2 (standard tier) or Level 3 (premium HSM tier) validated
- Support for Legacy, EIP-1559, and EIP-7702 transactions
- Message signing with secp256k1 (ES256K)
- Azure Active Directory authentication (Managed Identity, Service Principal, etc.)
- Scalable for enterprise and serverless architectures
- Audit logging and access control via Azure RBAC

**Use Cases:**
- Enterprise custody solutions
- Serverless transaction signing (Azure Functions, App Service)
- Multi-region hot wallet infrastructure
- Regulatory compliance requiring HSM-backed keys
- Secure key management without on-premises HSM hardware
- API-based signing services

## Installation

```bash
dotnet add package Nethereum.Signer.AzureKeyVault
```

## Dependencies

**External:**
- **Azure.Security.KeyVault.Keys** - Azure Key Vault SDK for key operations
- **Azure.Core** - Azure SDK core library for authentication

**Nethereum:**
- **Nethereum.Signer** - Core signing infrastructure
- **Nethereum.Accounts** - Account management
- **Nethereum.Web3** - Web3 client integration

## Prerequisites

### Azure Setup

1. **Create Azure Key Vault:**
   ```bash
   az keyvault create --name my-ethereum-vault --resource-group my-rg --location eastus
   ```

2. **Create secp256k1 Key:**
   ```bash
   az keyvault key create --vault-name my-ethereum-vault --name ethereum-key --kty EC --curve SECP256K1 --ops sign verify
   ```

3. **Configure Access Policy:**
   ```bash
   # Grant your identity permission to sign
   az keyvault set-policy --name my-ethereum-vault --upn user@domain.com --key-permissions sign get

   # Or use Managed Identity for Azure resources
   az keyvault set-policy --name my-ethereum-vault --object-id <managed-identity-object-id> --key-permissions sign get
   ```

### Authentication Options

- **DefaultAzureCredential** - Auto-detects: Managed Identity, Azure CLI, VS Code, etc.
- **ManagedIdentityCredential** - For Azure VMs, App Service, Functions
- **ClientSecretCredential** - Service Principal with client secret
- **ClientCertificateCredential** - Service Principal with certificate

## Quick Start

```csharp
using Nethereum.Signer.AzureKeyVault;
using Nethereum.Web3.Accounts;
using Azure.Identity;

// Authenticate to Azure (DefaultAzureCredential tries multiple methods)
var credential = new DefaultAzureCredential();

// Create external signer
var signer = new AzureKeyVaultExternalSigner(
    keyName: "ethereum-key",
    vaultUri: "https://my-ethereum-vault.vault.azure.net/",
    credential: credential
);

// Create external account
var account = new ExternalAccount(signer, chainId: 1);
await account.InitialiseAsync();

// Use with Web3
var web3 = new Web3.Web3(account, "https://mainnet.infura.io/v3/YOUR-PROJECT-ID");

Console.WriteLine($"Address: {account.Address}");
```

## Usage Examples

### Example 1: Basic Setup with DefaultAzureCredential

```csharp
using Nethereum.Signer.AzureKeyVault;
using Nethereum.Web3.Accounts;
using Azure.Identity;

// DefaultAzureCredential automatically tries:
// 1. Environment variables (AZURE_TENANT_ID, AZURE_CLIENT_ID, AZURE_CLIENT_SECRET)
// 2. Managed Identity (if running in Azure)
// 3. Visual Studio / VS Code authentication
// 4. Azure CLI authentication (az login)
var credential = new DefaultAzureCredential();

var signer = new AzureKeyVaultExternalSigner(
    keyName: "ethereum-mainnet-key",
    vaultUri: "https://my-company-vault.vault.azure.net/",
    credential: credential
);

var account = new ExternalAccount(signer, chainId: 1);
await account.InitialiseAsync();

Console.WriteLine($"Ethereum Address: {account.Address}");
Console.WriteLine($"Key Vault: {signer.KeyClient.VaultUri}");
Console.WriteLine($"Key Name: {signer.KeyName}");
```

### Example 2: Managed Identity (Azure App Service / Functions)

```csharp
using Nethereum.Signer.AzureKeyVault;
using Nethereum.Web3.Accounts;
using Azure.Identity;

// Perfect for Azure Functions, App Service, VMs, AKS
// No secrets in code - Azure manages the identity
var credential = new ManagedIdentityCredential();

var signer = new AzureKeyVaultExternalSigner(
    keyName: "ethereum-key",
    vaultUri: "https://my-vault.vault.azure.net/",
    credential: credential
);

var account = new ExternalAccount(signer, chainId: 1);
await account.InitialiseAsync();

// Now you can sign transactions without managing keys!
```

### Example 3: Service Principal with Client Secret

```csharp
using Nethereum.Signer.AzureKeyVault;
using Nethereum.Web3.Accounts;
using Azure.Identity;

// For CI/CD, automation, external services
// Create service principal: az ad sp create-for-rbac --name ethereum-signer
var credential = new ClientSecretCredential(
    tenantId: "your-tenant-id",
    clientId: "your-client-id",
    clientSecret: "your-client-secret"
);

var signer = new AzureKeyVaultExternalSigner(
    keyName: "ethereum-key",
    vaultUri: "https://my-vault.vault.azure.net/",
    credential: credential
);

var account = new ExternalAccount(signer, chainId: 1);
await account.InitialiseAsync();

Console.WriteLine($"Authenticated as Service Principal: {account.Address}");
```

### Example 4: Sign and Send Transaction

```csharp
using Nethereum.Signer.AzureKeyVault;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Hex.HexTypes;
using Azure.Identity;

var credential = new DefaultAzureCredential();

var signer = new AzureKeyVaultExternalSigner(
    keyName: "ethereum-key",
    vaultUri: "https://my-vault.vault.azure.net/",
    credential: credential
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

// Signing happens in Azure Key Vault
Console.WriteLine("Signing with Azure Key Vault HSM...");
var receipt = await web3.Eth.TransactionManager
    .SendTransactionAndWaitForReceiptAsync(transactionInput);

Console.WriteLine($"Transaction mined! Hash: {receipt.TransactionHash}");
```

### Example 5: Sign EIP-1559 Transaction

```csharp
using Nethereum.Signer.AzureKeyVault;
using Nethereum.Web3.Accounts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Hex.HexTypes;
using Azure.Identity;

var credential = new DefaultAzureCredential();

var signer = new AzureKeyVaultExternalSigner(
    keyName: "ethereum-key",
    vaultUri: "https://my-vault.vault.azure.net/",
    credential: credential
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

### Example 6: Sign Personal Message

```csharp
using Nethereum.Signer.AzureKeyVault;
using Nethereum.Signer;
using Nethereum.Web3.Accounts;
using Azure.Identity;
using System.Text;

var credential = new DefaultAzureCredential();

var signer = new AzureKeyVaultExternalSigner(
    keyName: "ethereum-key",
    vaultUri: "https://my-vault.vault.azure.net/",
    credential: credential
);

var account = new ExternalAccount(signer, chainId: 1);
await account.InitialiseAsync();

// Message to sign
string message = "Sign this message to authenticate";
byte[] messageBytes = Encoding.UTF8.GetBytes(message);

// Sign with Azure Key Vault
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

### Example 7: Multiple Keys for Different Networks

```csharp
using Nethereum.Signer.AzureKeyVault;
using Nethereum.Web3.Accounts;
using Azure.Identity;

var credential = new DefaultAzureCredential();
var vaultUri = "https://my-vault.vault.azure.net/";

// Mainnet account
var mainnetSigner = new AzureKeyVaultExternalSigner("ethereum-mainnet", vaultUri, credential);
var mainnetAccount = new ExternalAccount(mainnetSigner, chainId: 1);
await mainnetAccount.InitialiseAsync();
Console.WriteLine($"Mainnet: {mainnetAccount.Address}");

// Sepolia testnet account
var sepoliaSigner = new AzureKeyVaultExternalSigner("ethereum-sepolia", vaultUri, credential);
var sepoliaAccount = new ExternalAccount(sepoliaSigner, chainId: 11155111);
await sepoliaAccount.InitialiseAsync();
Console.WriteLine($"Sepolia: {sepoliaAccount.Address}");

// Polygon account
var polygonSigner = new AzureKeyVaultExternalSigner("polygon-mainnet", vaultUri, credential);
var polygonAccount = new ExternalAccount(polygonSigner, chainId: 137);
await polygonAccount.InitialiseAsync();
Console.WriteLine($"Polygon: {polygonAccount.Address}");
```

### Example 8: Azure Functions Serverless Signing

```csharp
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Nethereum.Signer.AzureKeyVault;
using Nethereum.Web3.Accounts;
using Azure.Identity;
using System.Net;

public class EthereumSignerFunction
{
    private readonly ExternalAccount _account;

    public EthereumSignerFunction()
    {
        // Managed Identity for Azure Functions
        var credential = new ManagedIdentityCredential();

        var signer = new AzureKeyVaultExternalSigner(
            keyName: Environment.GetEnvironmentVariable("KEY_NAME"),
            vaultUri: Environment.GetEnvironmentVariable("VAULT_URI"),
            credential: credential
        );

        _account = new ExternalAccount(signer, chainId: 1);
    }

    [Function("GetAddress")]
    public async Task<HttpResponseData> GetAddress(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        await _account.InitialiseAsync();

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new { address = _account.Address });
        return response;
    }

    [Function("SignTransaction")]
    public async Task<HttpResponseData> SignTransaction(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        await _account.InitialiseAsync();

        // Parse transaction from request body
        // Sign with Azure Key Vault
        // Return signed transaction

        var response = req.CreateResponse(HttpStatusCode.OK);
        return response;
    }
}
```

### Example 9: Using Premium HSM (FIPS 140-2 Level 3)

```csharp
using Nethereum.Signer.AzureKeyVault;
using Azure.Identity;

// Create Premium Key Vault with HSM
// az keyvault create --name my-premium-vault --resource-group my-rg --sku premium

// Create HSM-backed key
// az keyvault key create --vault-name my-premium-vault --name ethereum-hsm-key --kty EC-HSM --curve SECP256K1 --ops sign verify

var credential = new DefaultAzureCredential();

var signer = new AzureKeyVaultExternalSigner(
    keyName: "ethereum-hsm-key", // Note: Key type EC-HSM
    vaultUri: "https://my-premium-vault.vault.azure.net/",
    credential: credential
);

var account = new ExternalAccount(signer, chainId: 1);
await account.InitialiseAsync();

Console.WriteLine($"HSM-backed Address: {account.Address}");
// This key is stored in FIPS 140-2 Level 3 validated HSM
// Cannot be exported, even by Microsoft
```

## API Reference

### AzureKeyVaultExternalSigner

External signer implementation for Azure Key Vault.

```csharp
public class AzureKeyVaultExternalSigner : EthExternalSignerBase
{
    // Constructors
    public AzureKeyVaultExternalSigner(string keyName, string vaultUri, TokenCredential credential);
    public AzureKeyVaultExternalSigner(string keyName, KeyClient keyClient, TokenCredential credential);

    // Properties
    public CryptographyClient CryptoClient { get; }
    public KeyClient KeyClient { get; }
    public string KeyName { get; }
    public bool UseLegacyECDSA256 { get; set; } = true; // Use "ECDSA256" instead of "ES256K"
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
# Standard tier (FIPS 140-2 Level 2)
az keyvault key create \
  --vault-name my-vault \
  --name ethereum-key \
  --kty EC \
  --curve SECP256K1 \
  --ops sign verify

# Premium tier (FIPS 140-2 Level 3 HSM)
az keyvault key create \
  --vault-name my-premium-vault \
  --name ethereum-hsm-key \
  --kty EC-HSM \
  --curve SECP256K1 \
  --ops sign verify
```

**Important:**
- Use `--curve SECP256K1` (Ethereum's curve)
- Only `sign` and `verify` operations needed
- HSM keys (`EC-HSM`) cannot be exported
- Standard keys (`EC`) can be exported with proper permissions

### Authentication Methods

| Method | Use Case | Code |
|--------|----------|------|
| **DefaultAzureCredential** | Development, auto-detect | `new DefaultAzureCredential()` |
| **ManagedIdentityCredential** | Azure services (Functions, App Service) | `new ManagedIdentityCredential()` |
| **ClientSecretCredential** | Service principal | `new ClientSecretCredential(tenant, client, secret)` |
| **ClientCertificateCredential** | Certificate auth | `new ClientCertificateCredential(tenant, client, cert)` |
| **AzureCliCredential** | Local development (az login) | `new AzureCliCredential()` |

### Transaction Types Supported

| Type | Supported | Notes |
|------|-----------|-------|
| Legacy | ✅ Yes | EIP-155 with chain ID (no raw Legacy without chain ID) |
| EIP-1559 (Type 2) | ✅ Yes | MaxFeePerGas, MaxPriorityFeePerGas |
| EIP-2930 (Type 1) | ✅ Yes | Access lists |
| EIP-7702 (Type 4) | ✅ Yes | Account abstraction |

### Security Considerations

**Private Key Security:**
- Private keys **never leave** Azure Key Vault
- Signing operations performed server-side in Azure HSMs
- Standard tier: FIPS 140-2 Level 2 validated
- Premium tier: FIPS 140-2 Level 3 validated HSMs
- HSM keys (`EC-HSM`) cannot be exported by anyone, including Microsoft

**Access Control:**
```bash
# Use Azure RBAC for fine-grained access control
az role assignment create \
  --role "Key Vault Crypto User" \
  --assignee <user-or-managed-identity> \
  --scope /subscriptions/<subscription-id>/resourceGroups/<rg>/providers/Microsoft.KeyVault/vaults/<vault-name>

# Or Key Vault Access Policies (legacy)
az keyvault set-policy \
  --name my-vault \
  --object-id <object-id> \
  --key-permissions sign get
```

**Audit Logging:**
- Enable Azure Monitor for Key Vault
- All signing operations logged
- View in Azure Portal under "Monitoring" → "Logs"
- Query: `AzureDiagnostics | where ResourceProvider == "MICROSOFT.KEYVAULT"`

### Cost Considerations

| Tier | Cost per 10,000 operations | Key Storage | FIPS Level |
|------|---------------------------|-------------|------------|
| **Standard** | ~$0.03 | $1/month per key | Level 2 |
| **Premium HSM** | ~$1.00 | $5/month per key | Level 3 |

**Optimization Tips:**
- Cache public key (doesn't change)
- Use Managed Identity (no secrets management)
- Consider rate limiting for high-volume scenarios
- Monitor with Azure Application Insights

### Error Handling

```csharp
using Azure;

try
{
    var signature = await account.TransactionManager.SignTransactionAsync(transactionInput);
}
catch (RequestFailedException ex) when (ex.Status == 403)
{
    // Access denied - check Key Vault permissions
    Console.WriteLine($"Access denied: {ex.Message}");
}
catch (RequestFailedException ex) when (ex.Status == 404)
{
    // Key not found
    Console.WriteLine($"Key not found: {ex.Message}");
}
catch (RequestFailedException ex)
{
    // Other Azure errors
    Console.WriteLine($"Azure error: {ex.Status} - {ex.Message}");
}
```

### Performance

- **Latency**: ~100-300ms per signing operation (network + HSM)
- **Throughput**: Thousands of operations per second per vault
- **Caching**: Cache public key to avoid repeated Key Vault calls

### Comparison with Other Solutions

| Solution | Security | Cost | Latency | Use Case |
|----------|----------|------|---------|----------|
| **Azure Key Vault** | HSM-backed | Medium | ~200ms | Enterprise, cloud-native |
| **Ledger/Trezor** | Hardware wallet | Low | User-dependent | Development, manual signing |
| **AWS KMS** | HSM-backed | Medium | ~200ms | AWS-based infrastructure |
| **HDWallet** | Software | Free | <1ms | Development, non-production |

## Related Packages

### Used By (Consumers)
- Enterprise custody solutions
- Serverless signing services
- Multi-region hot wallet infrastructure
- API-based signing platforms

### Dependencies
- **Nethereum.Signer** - Core signing
- **Azure.Security.KeyVault.Keys** - Azure Key Vault SDK

### Alternatives
- **Nethereum.Signer.AWSKeyManagement** - AWS KMS integration
- **Nethereum.Signer.Ledger** - Ledger hardware wallet
- **Nethereum.Signer.Trezor** - TREZOR hardware wallet

## Additional Resources

- [Azure Key Vault Documentation](https://docs.microsoft.com/en-us/azure/key-vault/)
- [FIPS 140-2 Validation](https://csrc.nist.gov/projects/cryptographic-module-validation-program/certificate/4531)
- [Azure Key Vault Security](https://docs.microsoft.com/en-us/azure/key-vault/general/security-features)
- [Azure Identity SDK](https://docs.microsoft.com/en-us/dotnet/api/overview/azure/identity-readme)
- [Nethereum Documentation](http://docs.nethereum.com/)

## License

This package is part of the Nethereum project and follows the same MIT license.
