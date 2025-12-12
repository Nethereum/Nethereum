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
- **Azure.Security.KeyVault.Keys** (v4.2.0) - Azure Key Vault SDK for key operations and cryptography

**Nethereum:**
- **Nethereum.Signer** - Core signing infrastructure (provides EthExternalSignerBase)

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
| Legacy | Yes | EIP-155 with chain ID (no raw Legacy without chain ID) |
| EIP-1559 (Type 2) | Yes | MaxFeePerGas, MaxPriorityFeePerGas |
| EIP-2930 (Type 1) | Yes | Access lists |
| EIP-7702 (Type 4) | Yes | Account abstraction |

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
