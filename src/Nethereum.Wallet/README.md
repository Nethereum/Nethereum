# Nethereum.Wallet

Core wallet infrastructure for managing accounts, encrypted vaults, chain configurations, and dApp interactions. Provides BIP32/BIP39 HD wallet support, multiple account types, password-protected vault storage, ChainList integration, and UI abstraction layer for wallet applications.

## Overview

Nethereum.Wallet provides complete wallet infrastructure including:

- **Account Types** - Private key, HD wallet (BIP32/BIP39), view-only, and smart contract accounts
- **BIP32/BIP39 HD Wallets** - MinimalHDWallet with full BIP32 derivation (m/44'/60'/0'/0/{index}) and BIP39 mnemonic generation
- **Vault Services** - AES-256 encrypted password-protected storage for accounts, mnemonics, and hardware devices
- **Chain Management** - Network configuration with ChainList.org API integration and 30-minute caching
- **Hardware Wallet Support** - Track and manage hardware wallet devices (Trezor, etc.)
- **Transaction Services** - Gas estimation, pending transaction tracking, 4-byte function signature decoding
- **dApp Integration** - Permission management, transaction prompts, signature requests
- **UI Abstractions** - Prompt services for login, signatures, permissions, and chain switching

## Installation

```bash
dotnet add package Nethereum.Wallet
```

Or via Package Manager Console:

```powershell
Install-Package Nethereum.Wallet
```

## Dependencies

**Project References:**
- Nethereum.KeyStore
- Nethereum.Accounts
- Nethereum.Beaconchain
- Nethereum.ChainStateVerification
- Nethereum.Contracts
- Nethereum.Consensus.LightClient
- Nethereum.Consensus.Ssz
- Nethereum.DataServices (for ChainList integration)
- Nethereum.EVM
- Nethereum.ENS
- Nethereum.GnosisSafe
- Nethereum.JsonRpc.WebSocketClient
- Nethereum.RPC
- Nethereum.Signer
- Nethereum.Signer.Bls
- Nethereum.UI
- Nethereum.Web3
- Nethereum.TokenServices

**Package References:**
- Nethereum.Signer.Bls.Herumi
- Microsoft.Extensions.DependencyInjection.Abstractions 9.0.0

**Target Frameworks:**
- net6.0; net8.0; net9.0; net10.0

## Account Types

Nethereum.Wallet supports four account types, each implementing `IWalletAccount`.

### Private Key Account

Stores an encrypted private key.

```csharp
using Nethereum.Wallet;
using Nethereum.Wallet.WalletAccounts;

// TypeName constant
PrivateKeyWalletAccount.TypeName; // "privateKey"

// Create private key account
var privateKey = "0x...";
var account = new PrivateKeyWalletAccount(
    address: "0xAddress",
    label: "My Private Key",
    privateKey: privateKey);

// Get IAccount for signing
var signingAccount = await account.GetAccountAsync();
```

**From:** `src/Nethereum.Wallet/WalletAccounts/PrivateKeyWalletAccount.cs:12`

### Mnemonic Account (HD Wallet)

Derives keys from BIP39 mnemonic using BIP32 HD wallet at path m/44'/60'/0'/0/{index}.

```csharp
using Nethereum.Accounts.Bip32;
using Nethereum.Wallet.WalletAccounts;

// TypeName constant
MnemonicWalletAccount.TypeName; // "mnemonic"

// Create HD wallet from mnemonic
var mnemonic = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
var hdWallet = new MinimalHDWallet(mnemonic, password: "");

// Create mnemonic account at index 0
var account = new MnemonicWalletAccount(
    address: hdWallet.GetEthereumAddress(0),
    label: "HD Account 0",
    index: 0,
    mnemonicId: "main-mnemonic-id",
    wallet: hdWallet);

// GroupId is set to mnemonicId
account.GroupId; // "main-mnemonic-id"

// Get IAccount for signing
var signingAccount = await account.GetAccountAsync();
```

**From:** `src/Nethereum.Wallet/WalletAccounts/MnemonicWalletAccount.cs:15`, `src/Nethereum.Accounts/Bip32/MinimalHDWallet.cs:108`

### View-Only Account

Address-only account without signing capability.

```csharp
using Nethereum.Wallet.WalletAccounts;
using Nethereum.Accounts.ViewOnly;

// TypeName constant
ViewOnlyWalletAccount.TypeName; // "viewonly"

// Create view-only account
var account = new ViewOnlyWalletAccount(
    address: "0xAddress",
    label: "Watch Wallet");

// Returns ViewOnlyAccount (cannot sign)
var viewAccount = await account.GetAccountAsync();
```

**From:** `src/Nethereum.Wallet/WalletAccounts/ViewOnlyWalletAccount.cs:11`

### Smart Contract Account

Smart contract wallet address (Safe, etc.).

```csharp
using Nethereum.Wallet.WalletAccounts;

// TypeName constant
SmartContractWalletAccount.TypeName; // "smartcontract"

// Create smart contract account
var account = new SmartContractWalletAccount(
    address: "0xSafeAddress",
    label: "Gnosis Safe");

// Returns ViewOnlyAccount
var safeAccount = await account.GetAccountAsync();
```

**From:** `src/Nethereum.Wallet/WalletAccounts/SmartContractWalletAccount.cs:11`

## BIP32/BIP39 HD Wallet (MinimalHDWallet)

Full BIP32 hierarchical deterministic wallet implementation with BIP39 mnemonic support.

### BIP39 Mnemonic Generation

Generate 12/15/18/21/24 word mnemonics from 2048-word wordlist.

```csharp
using Nethereum.Accounts.Bip32;

// Generate 12-word mnemonic (default)
var mnemonic12 = Bip39.GenerateMnemonic(12);

// Generate 24-word mnemonic
var mnemonic24 = Bip39.GenerateMnemonic(24);

// 2048-word BIP39 wordlist available
Bip39.WordList; // string[] with 2048 words
```

**From:** `src/Nethereum.Accounts/Bip32/Bip39.cs:18`

### Seed Derivation

Convert mnemonic to 64-byte seed using PBKDF2 with 2048 iterations.

```csharp
using Nethereum.Accounts.Bip32;

var mnemonic = "word1 word2 ... word12";
var passphrase = "optional passphrase";

// PBKDF2-HMAC-SHA512, 2048 iterations, salt="mnemonic{passphrase}"
var seed = Bip39.MnemonicToSeed(mnemonic, passphrase);
// Returns 64-byte seed
```

**From:** `src/Nethereum.Accounts/Bip32/Bip39.cs:59`

**Algorithm:**
- Normalizes mnemonic and passphrase to NormalizationForm.FormKD
- Salt: "mnemonic" + passphrase
- PBKDF2 with 2048 iterations using SHA512
- Returns 64 bytes

### BIP32 Key Derivation

Derive Ethereum keys using BIP32 with HMACSHA512.

```csharp
using Nethereum.Accounts.Bip32;

var mnemonic = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
var hdWallet = new MinimalHDWallet(mnemonic, password: "");

// Get key at index 0 using default Ethereum path: m/44'/60'/0'/0/0
var key0 = hdWallet.GetEthereumKey(0);
var address0 = key0.GetPublicAddress(); // "0x9858EfFD232B4033E47d90003D41EC34EcaEda94"

// Get key at custom derivation path
var customKey = hdWallet.GetKeyFromPath("m/44'/60'/0'/0/5");

// Get address directly
var address5 = hdWallet.GetEthereumAddress(5);
```

**From:** `src/Nethereum.Accounts/Bip32/MinimalHDWallet.cs:108`, `tests/Nethereum.Wallet.UnitTests/MinimalHDWalletTests.cs:47`

**BIP32 Implementation Details:**
- Master key derived from seed using HMACSHA512 with key "Bitcoin seed"
- Supports hardened (') and non-hardened derivation
- Uses secp256k1 curve order for key addition
- Default Ethereum path: m/44'/60'/0'/0/{index}

**From:** `src/Nethereum.Accounts/Bip32/MinimalHDWallet.cs:31`

## Wallet Vault

Encrypted storage for accounts, mnemonics, and hardware devices.

### Vault Structure

```csharp
using Nethereum.Wallet;

var vault = new WalletVault(encryptionStrategy);

// Vault stores three collections:
vault.Mnemonics;        // List<MnemonicInfo>
vault.Accounts;         // List<IWalletAccount>
vault.HardwareDevices;  // List<HardwareWalletInfo>

// Account factories for deserialization
vault.Factories;        // List<IWalletAccountJsonFactory>
```

**From:** `src/Nethereum.Wallet/WalletVault.cs:19`

### Mnemonic Info

Store mnemonic phrases with labels.

```csharp
using Nethereum.Wallet;

var mnemonicInfo = new MnemonicInfo(
    label: "Main Wallet",
    mnemonic: "word1 word2 ... word12",
    passphrase: null);

mnemonicInfo.Id; // Guid.NewGuid().ToString()

vault.AddMnemonic(mnemonicInfo);
vault.FindMnemonicById("mnemonic-id");
```

**From:** `src/Nethereum.Wallet/MnemonicInfo.cs:8`

### Hardware Wallet Info

Track hardware wallet devices.

```csharp
using Nethereum.Wallet;

var hardwareInfo = vault.AddOrUpdateHardwareDevice(
    deviceId: "trezor-device-123",
    type: "Trezor",
    label: "My Trezor");

vault.FindHardwareDevice("trezor-device-123");
vault.GetHardwareDevicesByType("Trezor");
```

**From:** `src/Nethereum.Wallet/WalletVault.cs:172`, `src/Nethereum.Wallet/HardwareWalletInfo.cs:6`

### Vault Encryption

Encrypt vault with password using AES-256.

```csharp
using Nethereum.Wallet;
using Nethereum.Accounts.Bip32;
using Nethereum.Wallet.WalletAccounts;
using Nethereum.Signer;

// Create vault with encryption strategy
var vault = new WalletVault(new DefaultAes256EncryptionStrategy());

// Add mnemonic
var mnemonic = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
var hdWallet = new MinimalHDWallet(mnemonic);
var mnemonicInfo = new MnemonicInfo("Main", mnemonic, null);
vault.AddMnemonic(mnemonicInfo);

// Add mnemonic account
var mnemonicAccount = new MnemonicWalletAccount(
    hdWallet.GetEthereumAddress(0),
    "Account 0",
    index: 0,
    mnemonicInfo.Id,
    hdWallet);
vault.AddAccount(mnemonicAccount);

// Add private key account
var privateKey = EthECKey.GenerateKey().GetPrivateKey();
var privateAccount = new PrivateKeyWalletAccount(
    new Account(privateKey).Address,
    "Cold Wallet",
    privateKey);
vault.AddAccount(privateAccount);

// Encrypt to base64 string
var encrypted = vault.Encrypt("password123");

// Decrypt
var decryptedVault = new WalletVault(new DefaultAes256EncryptionStrategy());
decryptedVault.Decrypt(encrypted, "password123");

// Access accounts
var accounts = decryptedVault.Accounts; // All accounts restored
```

**From:** `tests/Nethereum.Wallet.UnitTests/WalletVaultTests.cs:16`, `src/Nethereum.Wallet/WalletVault.cs:76`

**Encryption Process:**
1. Serializes mnemonics, accounts, and hardware devices to JSON
2. Encrypts JSON bytes using IEncryptionStrategy
3. Returns base64-encoded encrypted data

**Deserialization:**
1. Base64 decodes encrypted data
2. Decrypts using IEncryptionStrategy
3. Parses JSON and uses factories to deserialize accounts by type

**From:** `src/Nethereum.Wallet/WalletVault.cs:111`

## Encryption Strategies

### DefaultAes256EncryptionStrategy

AES-256 encryption using .NET Crypto with PBKDF2 key derivation.

```csharp
using Nethereum.Wallet;

var strategy = new DefaultAes256EncryptionStrategy();
```

**Algorithm:**
- AES-256-CBC
- PBKDF2 with 10,000 iterations, SHA256
- Fixed salt: "NethereumWallet16"
- Random IV (16 bytes) prepended to ciphertext

**From:** `src/Nethereum.Wallet/DefaultAes256EncryptionStrategy.cs:12`

### BouncyCastleAes256EncryptionStrategy

AES-256 encryption using BouncyCastle library.

```csharp
using Nethereum.Wallet;

var strategy = new BouncyCastleAes256EncryptionStrategy();
```

**From:** `src/Nethereum.Wallet/BouncyCastleAes256EncryptionStrategy.cs`

## Vault Services

### In-Memory Vault

Vault stored in memory (lost on restart).

```csharp
using Nethereum.Wallet;

var vaultService = new InMemoryWalletVaultService();

// Create new vault
await vaultService.CreateNewAsync("password123");

// Vault exists in memory
bool exists = await vaultService.VaultExistsAsync(); // true

// Get accounts
var accounts = await vaultService.GetAccountsAsync();

// Lock vault (clears memory)
await vaultService.LockAsync();
```

**From:** `src/Nethereum.Wallet/InMemoryWalletVaultService.cs:7`

### File Vault

Vault persisted to file.

```csharp
using Nethereum.Wallet;

var vaultService = new FileWalletVaultService("./vault.json");

// Or with custom encryption strategy
var vaultService2 = new FileWalletVaultService(
    "./vault.json",
    new BouncyCastleAes256EncryptionStrategy());

// Create vault (saves to file)
await vaultService.CreateNewAsync("password123");

// Unlock existing vault
bool unlocked = await vaultService.UnlockAsync("password123");

// Save changes
await vaultService.SaveAsync();

// Reset (deletes file)
await vaultService.ResetAsync();
```

**From:** `src/Nethereum.Wallet/FileWalletVaultService.cs:8`

### Vault Service Interface

Common interface for all vault services.

```csharp
public interface IWalletVaultService
{
    Task<bool> VaultExistsAsync();
    Task CreateNewAsync(string password);
    Task CreateNewVaultWithAccountAsync(string password, IWalletAccount account);
    Task<bool> UnlockAsync(string password);
    Task SaveAsync(string password);
    Task SaveAsync(); // Uses current password
    Task LockAsync();
    Task ResetAsync();

    Task<IReadOnlyList<IWalletAccount>> GetAccountsAsync();
    Task<IReadOnlyList<AccountGroup>> GetAccountGroupsAsync();
    WalletVault? GetCurrentVault();
}
```

**From:** `src/Nethereum.Wallet/IWalletVaultService.cs:6`

## Account Groups

Accounts can be grouped by GroupId (mnemonic ID or hardware device ID).

```csharp
using Nethereum.Wallet;

var groups = await vaultService.GetAccountGroupsAsync();

foreach (var group in groups)
{
    group.GroupId;      // e.g., mnemonic ID or "type:privateKey"
    group.Accounts;     // IReadOnlyList<IWalletAccount>
    group.Count;        // Number of accounts
    group.IsStandalone; // true if no GroupId

    // Get group metadata (MnemonicInfo or HardwareWalletInfo)
    var mnemonicInfo = group.GetGroupMetadata<MnemonicInfo>();
    var hardwareInfo = group.GetGroupMetadata<HardwareWalletInfo>();
}
```

**From:** `src/Nethereum.Wallet/AccountGroup.cs:6`, `src/Nethereum.Wallet/WalletVaultServiceBase.cs:20`

## Chain Management

Manage network configurations with ChainList.org integration.

### Chain Management Service

```csharp
using Nethereum.Wallet.Services.Network;
using Nethereum.RPC.Chain;
using System.Numerics;

// Get chain by ID
var mainnet = await chainService.GetChainAsync(new BigInteger(1));

mainnet.ChainId;        // 1
mainnet.ChainName;      // "Ethereum Mainnet"
mainnet.HttpRpcs;       // List<string> RPC URLs
mainnet.WsRpcs;         // List<string> WebSocket URLs
mainnet.Explorers;      // List<string> explorer URLs
mainnet.NativeCurrency; // { Name, Symbol, Decimals }
mainnet.SupportEIP1559; // true
mainnet.IsTestnet;      // false

// Get all configured chains
var chains = await chainService.GetAllChainsAsync();

// Get best RPC endpoint
var rpcUrl = await chainService.GetBestRpcEndpointAsync(new BigInteger(1));
```

**From:** `src/Nethereum.Wallet/Services/Network/IChainManagementService.cs:8`, `src/Nethereum.Wallet/Services/Network/ChainManagementService.cs:37`

### ChainList Integration

Add networks from ChainList.org API.

```csharp
using Nethereum.Wallet.Services.Network;
using System.Numerics;

// Add network from ChainList (e.g., Arbitrum)
var arbitrum = await chainService.AddNetworkFromChainListAsync(new BigInteger(42161));

// Refresh RPC endpoints from ChainList
bool refreshed = await chainService.RefreshRpcsFromChainListAsync(new BigInteger(1));
```

**From:** `src/Nethereum.Wallet/Services/Network/IChainManagementService.cs:13`

**ChainList Provider:**
- Fetches chain data from ChainList.org API
- Caches with 30-minute TTL
- Converts ChainlistChainInfo to ChainFeature
- Takes top 3 HTTP RPC URLs per chain

**From:** `src/Nethereum.Wallet/Services/Network/Strategies/ChainListExternalChainFeaturesProvider.cs:29`

### Custom Chains

Add and manage custom chains.

```csharp
using Nethereum.Wallet.Services.Network;
using Nethereum.RPC.Chain;

// Add custom chain
var customChain = new ChainFeature
{
    ChainId = 31337,
    ChainName = "Localhost",
    NativeCurrency = new NativeCurrency
    {
        Name = "ETH",
        Symbol = "ETH",
        Decimals = 18
    },
    HttpRpcs = new List<string> { "http://localhost:8545" },
    WsRpcs = new List<string>(),
    Explorers = new List<string>(),
    SupportEIP1559 = true,
    SupportEIP155 = true,
    IsTestnet = true
};

await chainService.AddCustomChainAsync(customChain);

// Update chain RPC configuration
await chainService.UpdateChainRpcConfigurationAsync(
    chainId: 31337,
    httpRpcs: new List<string> { "http://localhost:8545", "http://localhost:8546" },
    wsRpcs: new List<string> { "ws://localhost:8545" });

// Delete custom chain
await chainService.DeleteUserNetworkAsync(new BigInteger(31337));

// Reset chain to default
await chainService.ResetChainToDefaultAsync(new BigInteger(1));
```

**From:** `src/Nethereum.Wallet/Services/Network/IChainManagementService.cs:10`, `src/Nethereum.Wallet/Services/Network/ChainManagementService.cs:70`

### Chain Feature Strategies

Configure how chain data is sourced.

```csharp
using Nethereum.Wallet.Services.Network;

// PreconfiguredOnly - use built-in chain data only
var strategy = ChainFeatureStrategyType.PreconfiguredOnly;

// ExternalOnly - fetch from ChainList API only
var strategy = ChainFeatureStrategyType.ExternalOnly;

// PreconfiguredEnrich - use built-in data enriched with ChainList data
var strategy = ChainFeatureStrategyType.PreconfiguredEnrich;
```

**From:** `src/Nethereum.Wallet/Services/Network/ChainFeatureStrategyType.cs`

## Wallet Host Provider

`NethereumWalletHostProvider` implements `IWalletContext` and provides the main wallet interface.

```csharp
using Nethereum.Wallet.Hosting;
using Nethereum.Wallet.UI;

public class NethereumWalletHostProvider : IWalletContext
{
    string Name { get; }                // "Nethereum Wallet"
    bool Available { get; }             // true
    bool Enabled { get; }               // Wallet enabled state

    string SelectedAccount { get; }     // Current account address
    long SelectedNetworkChainId { get; }// Current chain ID

    IReadOnlyList<IWalletAccount> Accounts { get; }
    IWalletAccount? SelectedWalletAccount { get; }
    DappConnectionContext? SelectedDapp { get; }

    Task<IWeb3> GetWeb3Async();         // Get Web3 with RPC interceptor
    Task<IWeb3> GetWalletWeb3Async();   // Get Web3 with signing account

    event Func<string, Task> SelectedAccountChanged;
    event Func<long, Task> NetworkChanged;
    event Func<bool, Task> EnabledChanged;
    event Func<Task>? AccountsRefreshed;
}
```

**From:** `src/Nethereum.Wallet/Hosting/NethereumWalletHostProvider.cs:23`

## dApp Permission Management

Control which dApps can access wallet functionality.

### Permission Service

```csharp
using Nethereum.Wallet.Services;

public interface IDappPermissionService
{
    Task<bool> IsApprovedAsync(string origin, string accountAddress);
    Task ApproveAsync(string origin, string accountAddress);
    Task RevokeAsync(string origin, string accountAddress);
    Task<IReadOnlyList<DappPermission>> GetPermissionsAsync(string? accountAddress = null);
}

// Permissions are keyed by (origin, account address):
public sealed record DappPermission(string Origin, string AccountAddress, long TimestampUtcSeconds);
```

**From:** `src/Nethereum.Wallet/Services/IDappPermissionService.cs:6`

**Default Implementations:**

**PermissiveDappPermissionService** - Grants all permissions automatically

```csharp
using Nethereum.Wallet.Services;

var permissive = new PermissiveDappPermissionService();
await permissive.IsApprovedAsync("https://app.example.com", "0xAddress"); // Always true
```

**DefaultDappPermissionService** - Persists approvals via `IWalletStorageService`

```csharp
using Nethereum.Wallet.Services;
using Nethereum.Wallet.Storage;

var permissionService = new DefaultDappPermissionService(storageService);

// Approve, then check stored permissions for an origin/account pair
await permissionService.ApproveAsync("https://app.example.com", "0xAddress");
bool permitted = await permissionService.IsApprovedAsync("https://app.example.com", "0xAddress");
```

**From:** `src/Nethereum.Wallet/Services/PermissiveDappPermissionService.cs:6`, `src/Nethereum.Wallet/Services/DefaultDappPermissionService.cs:9`

## Transaction Services

### Pending Transactions

Track pending transactions.

```csharp
using System.Collections.ObjectModel;
using System.Numerics;
using Nethereum.Wallet.Services.Transactions;

public interface IPendingTransactionService
{
    Task<string> SubmitTransactionAsync(TransactionInfo transaction);
    Task<string> RetryTransactionAsync(TransactionInfo transaction);
    Task<ObservableCollection<TransactionInfo>> GetPendingTransactionsAsync(BigInteger chainId);
    Task<ObservableCollection<TransactionInfo>> GetRecentTransactionsAsync(BigInteger chainId);

    event EventHandler<TransactionSubmittedEventArgs>? TransactionSubmitted;
    event EventHandler<TransactionStatusChangedEventArgs>? TransactionStatusChanged;
    event EventHandler<TransactionConfirmedEventArgs>? TransactionConfirmed;
    event EventHandler<TransactionFailedEventArgs>? TransactionFailed;

    void StartMonitoring();
    void StopMonitoring();
}
```

**From:** `src/Nethereum.Wallet/Services/Transactions/IPendingTransactionService.cs:8`

### Gas Configuration

Persist gas configuration per chain.

```csharp
using System.Numerics;
using Nethereum.Wallet.Services.Transaction;

public interface IGasConfigurationPersistenceService
{
    Task SaveCustomGasConfigurationAsync(BigInteger chainId, CustomGasConfiguration config);
    Task<CustomGasConfiguration?> GetCustomGasConfigurationAsync(BigInteger chainId);
    Task ClearCustomGasConfigurationAsync(BigInteger chainId);
    Task<bool> GetGasModePreferenceAsync(BigInteger chainId);
    Task SaveGasModePreferenceAsync(BigInteger chainId, bool preferEip1559);
}
```

**From:** `src/Nethereum.Wallet/Services/Transaction/IGasConfigurationPersistenceService.cs:7`

### Transaction Data Decoding

Decode transaction data using 4byte.directory.

```csharp
using Nethereum.DataServices.FourByteDirectory;
using Nethereum.Wallet.Services.Transaction;

// Requires a FourByteDirectoryService (uses its own HttpClient by default)
var decodingService = new FourByteDataDecodingService(new FourByteDirectoryService());

// transactionData, optional contractAddress, chainId (defaults to 1)
TransactionDataInfo info = await decodingService.DecodeTransactionDataAsync(
    "0xa9059cbb000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000001");

info.FunctionName;      // e.g. "transfer" if resolved via 4byte.directory
info.TextSignature;     // e.g. "transfer(address,uint256)"
info.DecodedParameters; // JSON string of decoded inputs, or null
```

**From:** `src/Nethereum.Wallet/Services/Transaction/FourByteDataDecodingService.cs:12`

## UI Prompt Services

Abstract interfaces for user interaction (implemented by UI frameworks).

### Login Prompt

```csharp
using Nethereum.Wallet.UI;

public interface ILoginPromptService
{
    Task<bool> PromptLoginAsync();
    Task LogoutAsync();
}
```

**From:** `src/Nethereum.Wallet/UI/ILoginPromptService.cs:9`

### Transaction Prompt

```csharp
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Wallet.UI;

public interface ITransactionPromptService
{
    // Returns the transaction hash if approved and sent, or null if cancelled
    Task<string?> PromptTransactionAsync(TransactionInput input);
}
```

**From:** `src/Nethereum.Wallet/UI/ITransactionPromptService.cs:6`

### Signature Prompt

```csharp
using Nethereum.Wallet.UI;

public interface ISignaturePromptService
{
    // Each returns the signature if approved, or null if cancelled
    Task<string?> PromptSignatureAsync(SignaturePromptContext context);
    Task<string?> PromptTypedDataSignAsync(TypedDataSignPromptContext context);
}
```

**From:** `src/Nethereum.Wallet/UI/ISignaturePromptService.cs:10`

### Chain Management Prompts

```csharp
using Nethereum.Wallet.UI;

// Chain addition prompt
public interface IChainAdditionPromptService
{
    Task<ChainAdditionPromptResult> RequestAddChainAsync(ChainAdditionPromptRequest request);
}

// Chain switch prompt (returns true if the switch was approved)
public interface IChainSwitchPromptService
{
    Task<bool> RequestSwitchAsync(ChainSwitchPromptRequest request);
}
```

**From:** `src/Nethereum.Wallet/UI/IChainAdditionPromptService.cs:5`, `src/Nethereum.Wallet/UI/IChainSwitchPromptService.cs:5`

**NoOp Implementations:** Three NoOp prompt implementations exist for headless scenarios: `NoOpChainAdditionPromptService`, `NoOpChainSwitchPromptService`, and `NoOpDappPermissionPromptService`.

**From:** `src/Nethereum.Wallet/UI/NoOpChainAdditionPromptService.cs`, `src/Nethereum.Wallet/UI/NoOpChainSwitchPromptService.cs`, `src/Nethereum.Wallet/UI/NoOpDappPermissionPromptService.cs`

## RPC Handler Registry

Register custom RPC method handlers.

Handlers implement `IRpcMethodHandler` (they are objects, not lambdas):

```csharp
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Wallet.Hosting;
using Nethereum.Wallet.UI;

public class EthAccountsHandler : IRpcMethodHandler
{
    public string MethodName => "eth_accounts";

    public Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, IWalletContext context)
    {
        var result = new[] { "0xAddress1", "0xAddress2" };
        return Task.FromResult(new RpcResponseMessage(request.Id, result));
    }
}

var registry = new RpcHandlerRegistry();

// Register by explicit method name...
registry.RegisterHandler("eth_accounts", new EthAccountsHandler());

// ...or register using the handler's own MethodName
registry.Register(new EthAccountsHandler());

// Get handler
IRpcMethodHandler? handler = registry.GetHandler("eth_accounts");
```

**From:** `src/Nethereum.Wallet/Hosting/RpcHandlerRegistry.cs:8`

## ENS Service

Resolve Ethereum Name Service names.

```csharp
using System.Collections.Generic;
using Nethereum.Wallet.Services;

public interface IEnsService
{
    // Reverse resolution: address -> ENS name
    Task<string?> ResolveAddressToNameAsync(string address);
    // Forward resolution: ENS name -> address
    Task<string?> ResolveNameToAddressAsync(string ensName);
    Task<Dictionary<string, string?>> BatchResolveAddressesToNamesAsync(IEnumerable<string> addresses);
    void ClearCache();
    string? GetCachedName(string address);
}
```

**From:** `src/Nethereum.Wallet/Services/IEnsService.cs:6`

## Dependency Injection

Register wallet services with Microsoft.Extensions.DependencyInjection.

```csharp
using System.Numerics;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.Wallet;
using Nethereum.Wallet.Hosting;
using Nethereum.Wallet.Services;
using Nethereum.Wallet.Services.Network;
using Nethereum.Wallet.Storage;
using Nethereum.Wallet.UI;

services.AddSingleton<IWalletVaultService, FileWalletVaultService>(sp =>
    new FileWalletVaultService("./vault.json"));

services.AddSingleton<ICoreWalletAccountService>(sp =>
    new CoreWalletAccountService(sp.GetRequiredService<WalletVault>()));
services.AddSingleton<IWalletConfigurationService, InMemoryWalletConfigurationService>();
services.AddSingleton<IWalletStorageService, FileWalletStorageService>();

// Chain management with ChainList integration
services.AddNethereumChainManagement(options =>
{
    options.Strategy = ChainFeatureStrategyType.PreconfiguredEnrich;
    options.DefaultChainIds = new[] { new BigInteger(1) }; // Ethereum Mainnet
});

services.AddSingleton<RpcHandlerRegistry>();
services.AddSingleton<NethereumWalletHostProvider>();

// UI prompt services (provide your own implementations; NoOp versions exist
// only for chain-addition, chain-switch, and dApp-permission prompts)
services.AddSingleton<ILoginPromptService, LoginPromptService>();
services.AddSingleton<IDappPermissionPromptService, NoOpDappPermissionPromptService>();
services.AddSingleton<IChainAdditionPromptService, NoOpChainAdditionPromptService>();
services.AddSingleton<IChainSwitchPromptService, NoOpChainSwitchPromptService>();
```

**From:** `src/Nethereum.Wallet/Hosting/WalletHostingServiceCollectionExtensions.cs`, `src/Nethereum.Wallet/Services/Network/ServiceCollectionChainManagementExtensions.cs`

## Complete Example

```csharp
using Nethereum.Wallet;
using Nethereum.Accounts.Bip32;
using Nethereum.Wallet.WalletAccounts;
using Nethereum.Wallet.Services.Network;
using Nethereum.RPC.Chain;
using System.Numerics;

// 1. Generate mnemonic
var mnemonic = Bip39.GenerateMnemonic(12);

// 2. Create HD wallet
var hdWallet = new MinimalHDWallet(mnemonic, password: "");

// 3. Create vault
var vault = new WalletVault(new DefaultAes256EncryptionStrategy());

// 4. Add mnemonic to vault
var mnemonicInfo = new MnemonicInfo("Main Wallet", mnemonic, null);
vault.AddMnemonic(mnemonicInfo);

// 5. Add accounts from HD wallet
for (int i = 0; i < 5; i++)
{
    var account = new MnemonicWalletAccount(
        hdWallet.GetEthereumAddress(i),
        $"Account {i}",
        i,
        mnemonicInfo.Id,
        hdWallet);
    vault.AddAccount(account);
}

// 6. Encrypt and save vault
var vaultService = new FileWalletVaultService("./my-wallet.json");
await vaultService.CreateNewAsync("password123");

var currentVault = vaultService.GetCurrentVault();
currentVault.Mnemonics.Clear();
currentVault.Mnemonics.Add(mnemonicInfo);
currentVault.Accounts.Clear();
foreach (var acc in vault.Accounts)
    currentVault.AddAccount(acc);

await vaultService.SaveAsync("password123");

// 7. Later: Unlock vault
var loadedVault = new FileWalletVaultService("./my-wallet.json");
bool unlocked = await loadedVault.UnlockAsync("password123");

// 8. Get accounts
var accounts = await loadedVault.GetAccountsAsync();
var groups = await loadedVault.GetAccountGroupsAsync();

// 9. Use account for signing
var selectedAccount = accounts[0];
var signingAccount = await selectedAccount.GetAccountAsync();

// 10. Chain management (requires DI setup)
// var mainnet = await chainService.GetChainAsync(new BigInteger(1));
// var rpcUrl = await chainService.GetBestRpcEndpointAsync(new BigInteger(1));
```

**From:** `tests/Nethereum.Wallet.UnitTests/WalletVaultTests.cs:16`, `tests/Nethereum.Wallet.UnitTests/MinimalHDWalletTests.cs:36`

## Related Packages

- **Nethereum.Wallet.RpcRequests** - EIP-1193 RPC request handling
- **Nethereum.Wallet.Trezor** - Trezor hardware wallet integration
- **Nethereum.Wallet.UI.Components** - Shared UI component models
- **Nethereum.Wallet.UI.Components.Blazor** - Blazor UI components
- **Nethereum.Wallet.UI.Components.Avalonia** - Avalonia UI components
- **Nethereum.Wallet.UI.Components.Maui** - MAUI UI components
- **Nethereum.UI** - Base `IEthereumHostProvider` abstraction
- **Nethereum.DataServices** - ChainList API integration

## Additional Resources

- [Nethereum Documentation](http://docs.nethereum.com)
- [BIP32: Hierarchical Deterministic Wallets](https://github.com/bitcoin/bips/blob/master/bip-0032.mediawiki)
- [BIP39: Mnemonic Code for Generating Deterministic Keys](https://github.com/bitcoin/bips/blob/master/bip-0039.mediawiki)
- [BIP44: Multi-Account Hierarchy for Deterministic Wallets](https://github.com/bitcoin/bips/blob/master/bip-0044.mediawiki)
- [EIP-1193: Ethereum Provider JavaScript API](https://eips.ethereum.org/EIPS/eip-1193)
- [ChainList](https://chainlist.org/)
