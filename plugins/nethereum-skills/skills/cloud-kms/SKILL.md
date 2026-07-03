---
name: cloud-kms
description: "Sign Ethereum transactions, derive public addresses, and manage secp256k1 keys using AWS KMS or Azure Key Vault HSMs with Nethereum. Use this skill whenever the user asks about AWS KMS Ethereum signing, Azure Key Vault signing, cloud HSM, managed key signing, serverless wallet, key management service, or cloud-based transaction signing in C#/.NET."
user-invocable: true
---

# Cloud KMS Signing with Nethereum

Private key generated inside HSM, never exported. Both support Legacy, EIP-1559, EIP-2930, and EIP-7702 transactions.

## AWS KMS

NuGet: `Nethereum.Signer.AWSKeyManagement`

```bash
dotnet add package Nethereum.Signer.AWSKeyManagement
```

### Create and Validate Key
```bash
aws kms create-key --key-spec ECC_SECG_P256K1 --key-usage SIGN_VERIFY
```

Validate the key is usable before signing:
```csharp
var signer = new AWSKeyManagementExternalSigner(keyId: "your-kms-key-id");
var externalAccount = new ExternalAccount(signer, chainId: 1);
await externalAccount.InitialiseAsync();
// If InitialiseAsync succeeds, the key exists and the Ethereum address is derived
Console.WriteLine($"Derived address: {externalAccount.Address}");
```

### Sign Transactions
```csharp
using Nethereum.Signer.AWSKeyManagement;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;

// Uses default AWS credentials chain
var signer = new AWSKeyManagementExternalSigner(keyId: "your-kms-key-id");

var externalAccount = new ExternalAccount(signer, chainId: 1);
await externalAccount.InitialiseAsync();

var web3 = new Web3(externalAccount, "https://your-rpc-url");

var receipt = await web3.Eth.GetEtherTransferService()
    .TransferEtherAndWaitForReceiptAsync(toAddress, 0.1m);
```

### Auth Methods
```csharp
// Default credentials (Lambda, ECS, EC2)
var signer = new AWSKeyManagementExternalSigner(keyId);

// Explicit credentials
var signer = new AWSKeyManagementExternalSigner(
    keyId, accessKeyId: "AKIA...", secretAccessKey: "...");

// Specific region
var signer = new AWSKeyManagementExternalSigner(
    keyId, region: Amazon.RegionEndpoint.EUWest1);
```

## Azure Key Vault

NuGet: `Nethereum.Signer.AzureKeyVault`

```bash
dotnet add package Nethereum.Signer.AzureKeyVault
```

### Create Key
```bash
az keyvault key create --vault-name my-vault --name ethereum-key --kty EC --curve SECP256K1
```

### Sign Transactions
```csharp
using Nethereum.Signer.AzureKeyVault;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Azure.Identity;

var signer = new AzureKeyVaultExternalSigner(
    keyIdentifier: "https://my-vault.vault.azure.net/keys/ethereum-key");

var externalAccount = new ExternalAccount(signer, chainId: 1);
await externalAccount.InitialiseAsync();

var web3 = new Web3(externalAccount, "https://your-rpc-url");

var receipt = await web3.Eth.GetEtherTransferService()
    .TransferEtherAndWaitForReceiptAsync(toAddress, 0.1m);
```

### Auth Methods
```csharp
// DefaultAzureCredential (auto-detect)
var signer = new AzureKeyVaultExternalSigner(keyIdentifier);

// Managed identity
var signer = new AzureKeyVaultExternalSigner(
    keyIdentifier, new ManagedIdentityCredential());

// Service principal
var signer = new AzureKeyVaultExternalSigner(
    keyIdentifier, new ClientSecretCredential(tenantId, clientId, clientSecret));
```

## Error Handling

```csharp
try
{
    var receipt = await web3.Eth.GetEtherTransferService()
        .TransferEtherAndWaitForReceiptAsync(toAddress, 0.1m);
}
catch (Amazon.KeyManagementService.AmazonKeyManagementServiceException ex)
{
    // Key not found, access denied, or KMS service unavailable
    Console.WriteLine($"KMS error: {ex.Message}");
}
catch (Azure.RequestFailedException ex)
{
    // Key Vault access denied, key not found, or vault unavailable
    Console.WriteLine($"Key Vault error: {ex.Message}");
}
```

For full documentation, see: https://docs.nethereum.com/docs/signing-and-key-management/guide-cloud-kms
