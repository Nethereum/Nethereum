# Nethereum 5.1.0 - 10 Year Anniversary Release

**Celebrating 10 years of .NET Ethereum integration!** (November 2015 - November 2025)

This release marks a major milestone with comprehensive new libraries for **Light Client verification**, **enterprise wallet infrastructure**, **hardware wallet integration**, and **DeFi protocol support**.

**119 commits** since 5.0.0 | **23+ new libraries** | **Significant updates** to existing packages

---

## Ethereum Light Client & Trustless State Verification

A complete light client implementation enabling trustless Ethereum state verification without running a full node. This is a major milestone for Nethereum, allowing applications to cryptographically verify balances, storage, and contract state using the Beacon Chain consensus layer.

### Nethereum.Beaconchain

HTTP client for the Beacon Chain REST API (Ethereum consensus layer).

- **BeaconApiClient** - Main API client with configurable endpoints
- **ILightClientApi** - Light client specific API endpoints:
  - `GetBootstrapAsync()` - Fetch initial checkpoint data
  - `GetUpdatesAsync()` - Period-based update fetches
  - `GetFinalityUpdateAsync()` - Latest finalized state
  - `GetOptimisticUpdateAsync()` - Latest optimistic header
- **LightClientResponseMapper** - Maps HTTP responses to domain objects with hex string conversions

Commits: [`bfd7f862`](https://github.com/Nethereum/Nethereum/commit/bfd7f862), [`e927d06f`](https://github.com/Nethereum/Nethereum/commit/e927d06f)

---

### Nethereum.Consensus.LightClient

Beacon chain light client implementation for trustless state verification.

```csharp
// Initialize light client with Beacon API
var beaconClient = new BeaconApiClient("https://beacon-api.example.com");
var lightClientApi = new LightClientApiClient(beaconClient);
var blsSigner = new HerumiNativeBindings();
await blsSigner.EnsureAvailableAsync();

var config = new LightClientConfig
{
    GenesisValidatorsRoot = genesisRoot,
    CurrentForkVersion = forkVersion,
    WeakSubjectivityRoot = checkpointRoot,
    SlotsPerEpoch = 32,
    SecondsPerSlot = 12
};

var lightClient = new LightClientService(lightClientApi, blsSigner, config, store);
await lightClient.InitializeAsync();

// Update to latest finalized state
await lightClient.UpdateAsync();

// Or use optimistic updates for faster tracking
await lightClient.UpdateOptimisticAsync();
```

**Key Features:**
- **Three update modes**: Finality (most secure), Optimistic (fastest), Standard
- **Sync committee verification** via BLS aggregate signatures
- **Merkle proof validation** for execution and finality branches
- **Block hash tracking** for up to 256 recent blocks
- **Persistent state** via `ILightClientStore` abstraction

Commits: [`2b21ec00`](https://github.com/Nethereum/Nethereum/commit/2b21ec00), [`7fd890cf`](https://github.com/Nethereum/Nethereum/commit/7fd890cf), [`093d2836`](https://github.com/Nethereum/Nethereum/commit/093d2836)

---

### Nethereum.Consensus.Ssz

Ethereum 2.0 consensus layer domain objects with SSZ (Simple Serialize) encoding/decoding.

**Beacon Chain Types:**
- `BeaconBlockHeader` - Slot, proposer index, parent/state/body roots
- `ExecutionPayloadHeader` - Block metadata, state root, gas metrics, blob gas
- `SyncAggregate` - 64-byte participation bitmap + 96-byte aggregate signature
- `SyncCommittee` - 512 public keys + aggregate key

**Light Client Messages:**
- `LightClientBootstrap` - Initial checkpoint with sync committee
- `LightClientUpdate` - Period updates with signatures
- `LightClientFinalityUpdate` - Finalized header with proofs
- `LightClientOptimisticUpdate` - Latest header tracking

**Hash Tree Root Support:**
- `SszBasicTypes.HashTreeRoot()` for all consensus types
- Merkle tree depth constants and generalized indices
- Support for Deneb and Electra fork differences

Commits: [`89541e37`](https://github.com/Nethereum/Nethereum/commit/89541e37), [`a62fa294`](https://github.com/Nethereum/Nethereum/commit/a62fa294), [`fef498c8`](https://github.com/Nethereum/Nethereum/commit/fef498c8)

---

### Nethereum.Ssz

Core SSZ (Simple Serialize) infrastructure for Ethereum 2.0.

```csharp
// SSZ Serialization
var writer = new SszWriter(buffer);
writer.WriteUInt64(slot);
writer.WriteBytes32(parentRoot);
writer.WriteBytes32(stateRoot);

// SSZ Deserialization
var reader = new SszReader(data);
var slot = reader.ReadUInt64();
var parentRoot = reader.ReadBytes32();

// Merkle Proof Verification
var isValid = SszMerkleizer.VerifyProof(
    root: stateRoot,
    leaf: leafHash,
    proof: proofBranches,
    generalizedIndex: index
);
```

**Components:**
- **SszWriter** - Efficient serialization with little-endian encoding
- **SszReader** - Ref struct deserializer with bounds checking
- **SszMerkleizer** - SHA-256 Merkleization with proof verification

Commits: [`e69092c4`](https://github.com/Nethereum/Nethereum/commit/e69092c4), [`58a7a8bc`](https://github.com/Nethereum/Nethereum/commit/58a7a8bc), [`88ddeeae`](https://github.com/Nethereum/Nethereum/commit/88ddeeae)

---

### Nethereum.ChainStateVerification

Cryptographic verification of execution layer state against light client consensus.

```csharp
// Create verified state service
var verifiedService = new VerifiedStateService(lightClient, evmService, proofVerifier);

// Get cryptographically verified balance
var balance = await verifiedService.GetBalanceAsync("0x...");

// Get verified storage slot
var storage = await verifiedService.GetStorageAtAsync(contractAddress, slot);

// Get verified account (balance, nonce, code hash, storage root)
var account = await verifiedService.GetAccountAsync("0x...");
```

**Transparent RPC Interception:**

```csharp
// Add verified state interceptor to Web3
var interceptor = new VerifiedStateInterceptor(verifiedService);
interceptor.FallbackOnError = true;
interceptor.FallbackTriggered += (s, e) => Console.WriteLine($"Fallback: {e.Reason}");

var web3 = new Web3(rpcClient);
web3.Client.OverridingRequestInterceptor = interceptor;

// All eth_getBalance, eth_getCode, eth_getStorageAt calls
// are now verified against the light client!
var balance = await web3.Eth.GetBalance.SendRequestAsync("0x...");
```

**Verified Methods:**
- `eth_getBalance` - Account balance with proof
- `eth_getTransactionCount` - Nonce with proof
- `eth_getCode` - Contract bytecode with proof
- `eth_getStorageAt` - Storage slot with proof
- `eth_blockNumber` - Current verified block

**Caching:**
- `VerifiedStateCache` and `VerifiedAccountState` minimize round-trips
- Configurable verification modes

Commits: [`b8305a7f`](https://github.com/Nethereum/Nethereum/commit/b8305a7f), [`f8e60d87`](https://github.com/Nethereum/Nethereum/commit/f8e60d87), [`e3b37c4a`](https://github.com/Nethereum/Nethereum/commit/e3b37c4a)

---

### Nethereum.Signer.Bls

Abstract BLS (Boneh-Lynn-Shacham) signature verification interface for Ethereum 2.0 consensus.

```csharp
public interface IBls
{
    bool VerifyAggregate(
        byte[][] publicKeys,
        byte[][] messages,
        byte[] signature
    );
}
```

- **Pluggable architecture** - Supports native and managed implementations
- **IBlsEnvironment** - Runtime implementation detection
- **NativeBls** - Wrapper with lazy initialization

Commits: [`f2b2d1da`](https://github.com/Nethereum/Nethereum/commit/f2b2d1da), [`b6dceae4`](https://github.com/Nethereum/Nethereum/commit/b6dceae4)

---

### Nethereum.Signer.Bls.Herumi

Production BLS implementation using Herumi/MCL library - high-performance native C++ bindings.

```csharp
var bls = new HerumiNativeBindings();
await bls.EnsureAvailableAsync();

// Verify aggregated sync committee signatures
var isValid = bls.VerifyAggregate(
    publicKeys: syncCommitteeKeys,
    messages: new[] { signingRoot },
    signature: aggregateSignature
);
```

**Features:**
- **BLS12-381 curve** with Ethereum domain separation
- **FastAggregateVerify** - Single message aggregated over multiple signers
- **AggregateVerify** - Multiple distinct messages with 1:1 signer mapping
- **Platform support**: Windows x64, Linux x64, Android

Commits: [`f2b2d1da`](https://github.com/Nethereum/Nethereum/commit/f2b2d1da), [`6eb04d62`](https://github.com/Nethereum/Nethereum/commit/6eb04d62), [`c4ba7790`](https://github.com/Nethereum/Nethereum/commit/c4ba7790)

---

## Enterprise Wallet Infrastructure

A complete wallet solution for web, mobile, and desktop applications with encrypted vault storage, multi-account support, and comprehensive UI components.

### Nethereum.Wallet

Core wallet services and account management infrastructure.

```csharp
// Create and unlock vault
var vaultService = new FileWalletVaultService(
    storagePath,
    new KeyStoreEncryptionStrategy()
);

await vaultService.CreateNewAsync(password);
await vaultService.UnlockAsync(password);

// Create accounts
var accountService = new CoreWalletAccountService();
var mnemonicAccount = await accountService.CreateMnemonicAccountAsync(
    mnemonic: "word1 word2 ... word12",
    index: 0,
    passphrase: null
);

// Add to vault and save
var vault = vaultService.GetCurrentVault();
vault.AddAccount(mnemonicAccount);
await vaultService.SaveAsync();
```

**Account Types:**
- `PrivateKeyWalletAccount` - Direct private key storage
- `MnemonicWalletAccount` - BIP32/BIP39 HD wallet derivation
- `ViewOnlyWalletAccount` - Read-only address monitoring
- `SmartContractWalletAccount` - Contract wallet support

**Vault Security:**
- `KeyStoreEncryptionStrategy` - Standard Ethereum keystore format (slower but proven)
- `DefaultAes256EncryptionStrategy` - Fast AES-256 encryption
- `BouncyCastleAes256EncryptionStrategy` - Alternative provider

**Network Management:**
```csharp
var chainService = new ChainManagementService();
// Add from ChainList
await chainService.AddChainFromChainListAsync(chainId);
// Add custom RPC
await chainService.AddRpcEndpointAsync(chainId, rpcUrl);
// Health check
var healthy = await chainService.TestRpcEndpointAsync(rpcUrl);
```

**DApp Integration:**
- `IDappPermissionService` - Per-origin permission management
- `NethereumWalletHostProvider` - `IEthereumHostProvider` implementation
- `NethereumWalletInterceptor` - RPC request handling

Commits: [`2a68b041`](https://github.com/Nethereum/Nethereum/commit/2a68b041), [`b048fc8c`](https://github.com/Nethereum/Nethereum/commit/b048fc8c), [`37c227fb`](https://github.com/Nethereum/Nethereum/commit/37c227fb), [`6f07c060`](https://github.com/Nethereum/Nethereum/commit/6f07c060)

---

### Nethereum.Wallet.RpcRequests

JSON-RPC handler implementation for wallet APIs - MetaMask-compatible RPC support.

```csharp
// Register all wallet RPC handlers
services.AddWalletRpcHandlers();

// Handlers implement standard Ethereum wallet APIs
public class EthSendTransactionHandler : RpcMethodHandlerBase
{
    public override string MethodName => "eth_sendTransaction";

    public override async Task<object> HandleAsync(
        RpcRequest request,
        IWalletContext context)
    {
        // Parse transaction, show prompt, sign, broadcast
    }
}
```

**Supported Methods:**
| Method | Description |
|--------|-------------|
| `eth_accounts` | List connected accounts |
| `eth_requestAccounts` | Request account connection |
| `eth_chainId` | Current chain ID |
| `eth_sendTransaction` | Sign and send transaction |
| `personal_sign` | Sign message |
| `eth_signTypedData_v4` | EIP-712 typed data |
| `wallet_switchEthereumChain` | Switch network |
| `wallet_addEthereumChain` | Add custom network |
| `wallet_getPermissions` | Query permissions |
| `wallet_requestPermissions` | Request permissions |
| `wallet_revokePermissions` | Revoke access |

Commits: [`19ffa941`](https://github.com/Nethereum/Nethereum/commit/19ffa941), [`62efc517`](https://github.com/Nethereum/Nethereum/commit/62efc517), [`f10aa4dd`](https://github.com/Nethereum/Nethereum/commit/f10aa4dd)

---

### Nethereum.Wallet.UI.Components

Framework-agnostic ViewModels using CommunityToolkit.Mvvm - shared across Blazor, MAUI, and other UI frameworks.

**ViewModels:**
- `AccountListViewModel` / `CreateAccountViewModel` - Account management
- `WalletDashboardViewModel` - Main dashboard with plugin architecture
- `HoldingsViewModel` / `EditHoldingsViewModel` - Token portfolio
- `NetworkSelectorViewModel` - Chain selection
- `ContactListViewModel` - Address book
- `DAppTransactionPromptViewModel` - Transaction approval
- `SendNativeTokenViewModel` / `TokenTransferPluginViewModel` - Token transfers

**Plugin Architecture:**
```csharp
// Register dashboard plugins
services.AddDashboardPlugin<HoldingsPluginViewModel>("Holdings");
services.AddDashboardPlugin<TransactionsPluginViewModel>("Transactions");

// Plugins implement INavigatablePlugin
public interface INavigatablePlugin
{
    string PluginName { get; }
    void NavigateTo(string destination);
}
```

**Localization:**
- Built-in English/Spanish translations
- `IComponentLocalizer<T>` pattern for all components

Commits: [`49f60568`](https://github.com/Nethereum/Nethereum/commit/49f60568), [`6f07c060`](https://github.com/Nethereum/Nethereum/commit/6f07c060), [`0308d002`](https://github.com/Nethereum/Nethereum/commit/0308d002), [`157e60fa`](https://github.com/Nethereum/Nethereum/commit/157e60fa)

---

### Nethereum.Wallet.UI.Components.Blazor

Production Blazor component library using MudBlazor for responsive, accessible interfaces.

**Components:**
```razor
<!-- Main wallet component -->
<NethereumWallet />

<!-- Account management -->
<AccountList OnAccountSelected="HandleAccountSelected" />
<AccountCard Account="@selectedAccount" />
<CreateAccount OnAccountCreated="HandleCreated" />

<!-- Holdings with tabs -->
<Holdings>
    <HoldingsAccountsTab />
    <HoldingsNetworksTab />
    <HoldingsTokensTab />
</Holdings>

<!-- Dialogs -->
<MessageSigningDialog Message="@message" OnSign="HandleSign" />
<PasswordConfirmationDialog OnConfirm="HandleUnlock" />
```

**Features:**
- Material Design via MudBlazor 8.x
- Responsive layouts for mobile/desktop
- Real-time balance updates
- Token discovery with progress visualization
- Accessible form controls

Commits: [`ea15de0c`](https://github.com/Nethereum/Nethereum/commit/ea15de0c), [`75c17c8e`](https://github.com/Nethereum/Nethereum/commit/75c17c8e), [`d6648e1e`](https://github.com/Nethereum/Nethereum/commit/d6648e1e), [`8d22c35e`](https://github.com/Nethereum/Nethereum/commit/8d22c35e)

---

### Nethereum.Wallet.UI.Components.Maui

MAUI integration for mobile (iOS/Android) and desktop (Windows/macOS) applications.

```csharp
// In MauiProgram.cs
var builder = MauiApp.CreateBuilder();
builder
    .UseMauiApp<App>()
    .ConfigureNethereumWallet(options =>
    {
        options.StoragePath = FileSystem.AppDataDirectory;
    });
```

**Target Platforms:**
- `net9.0-android` (Android 21+)
- `net9.0-ios` (iOS 15+)
- `net9.0-maccatalyst` (macOS 12+)
- `net9.0-windows10.0.19041.0` (Windows 10+)

Commits: [`a69a9b05`](https://github.com/Nethereum/Nethereum/commit/a69a9b05)

---

### Nethereum.TokenServices

Comprehensive ERC20 token management with discovery, balances, and pricing.

```csharp
// Create token service
var tokenService = new Erc20TokenService(
    balanceProvider: new MultiCallBalanceProvider(web3),
    priceProvider: new CoinGeckoPriceProvider(apiKey),
    tokenListProvider: new ResilientTokenListProvider()
);

// Discover tokens for an account
var discovery = await tokenService.DiscoverTokensAsync(
    address: "0x...",
    chainId: 1,
    progress: new Progress<DiscoveryProgress>(p =>
        Console.WriteLine($"Checking {p.Current}/{p.Total}"))
);

// Get balances with prices
var balances = await tokenService.GetBalancesWithPricesAsync(
    address: "0x...",
    chainId: 1,
    tokens: discovery.FoundTokens,
    currency: "usd"
);

// Refresh from transfer events
var refreshed = await tokenService.RefreshFromEventsAsync(
    address: "0x...",
    chainId: 1,
    fromBlock: lastCheckedBlock
);
```

**Multi-Account Support:**
```csharp
var multiService = new MultiAccountTokenService(tokenService);

// Parallel discovery across accounts and chains
var results = await multiService.DiscoverAllAsync(
    accounts: new[] { "0xAcc1", "0xAcc2" },
    chainIds: new[] { 1, 137, 42161 },
    progress: progressReporter
);
```

**Providers:**
- `MultiCallBalanceProvider` - Efficient batch balance queries
- `CoinGeckoPriceProvider` - Real-time pricing
- `ResilientTokenListProvider` - Embedded + remote token lists
- `Erc20EventScanner` - Transfer event monitoring

Commits: [`72fcd1aa`](https://github.com/Nethereum/Nethereum/commit/72fcd1aa), [`5f8a67db`](https://github.com/Nethereum/Nethereum/commit/5f8a67db), [`a233dd23`](https://github.com/Nethereum/Nethereum/commit/a233dd23), [`244ae8c9`](https://github.com/Nethereum/Nethereum/commit/244ae8c9)

---

## Hardware Wallet - Trezor Integration

Complete Trezor hardware wallet support across all platforms including mobile.

### Nethereum.Signer.Trezor (Major Upgrade)

Full upgrade supporting latest firmware, EIP-712, and multi-OS support.

```csharp
// Create Trezor manager
var manager = new NethereumTrezorManagerBroker(
    deviceFactory,
    promptHandler
);

// Get signing session
var session = await manager.GetSessionAsync();

// Sign transaction
var signedTx = await session.SignTransactionAsync(txInput);

// Sign EIP-712 typed data (NEW!)
var signature = await session.SignTypedDataV4Async(typedData);
```

**New Features:**
- Latest protobuf definitions and firmware support
- **EIP-712 typed data signing** - Sign structured data on device
- Multi-OS support (Windows, Linux, macOS)
- Improved PIN prompt handler
- Tested with legacy and new Trezor devices

Commits: [`7cb24d33`](https://github.com/Nethereum/Nethereum/commit/7cb24d33), [`d87927e2`](https://github.com/Nethereum/Nethereum/commit/d87927e2)

---

### Nethereum.Wallet.Trezor

High-level wallet account management for Trezor devices.

```csharp
// Create Trezor account service
var trezorService = new TrezorWalletAccountService(
    sessionProvider,
    discoveryService
);

// Discover device and create account
var device = await trezorService.DiscoverDeviceAsync();
var account = await trezorService.CreateAsync(
    deviceId: device.Id,
    derivationIndex: 0
);

// Add to wallet vault
vault.AddAccount(account);
await vaultService.SaveAsync();
```

Commits: [`960db8b9`](https://github.com/Nethereum/Nethereum/commit/960db8b9)

---

### Nethereum.Wallet.UI.Components.Trezor

ViewModels for Trezor UI integration.

- `TrezorAccountCreationViewModel` - Device pairing and account creation
- `TrezorAccountDetailsViewModel` - Account properties display
- `TrezorGroupDetailsViewModel` - Hardware device grouping
- Comprehensive English/Spanish localization

Commits: [`d4633613`](https://github.com/Nethereum/Nethereum/commit/d4633613)

---

### Nethereum.Wallet.UI.Components.Blazor.Trezor

Blazor components for Trezor hardware wallet UI.

Commits: [`2cfeae7c`](https://github.com/Nethereum/Nethereum/commit/2cfeae7c), [`151b6cff`](https://github.com/Nethereum/Nethereum/commit/151b6cff)

---

### Nethereum.Maui.AndroidUsb

Android USB device driver for MAUI applications - enables Trezor on Android!

```csharp
// Create USB device factory for Trezor
var usbFactory = new MauiAndroidUsbDeviceFactory(
    usbManager,
    vendorId: 0x534C,  // Trezor
    productId: 0x0001
);

// Permission handling
await UsbPermissionHelper.RequestPermissionAsync(device);

// Device implements IDevice from Device.Net
var trezorDevice = await usbFactory.GetDeviceAsync();
```

**Features:**
- `MauiAndroidUsbDevice` implementing Device.Net `IDevice`
- USB enumeration by VendorId/ProductId
- Bulk transfer read/write operations
- `UsbAttachReceiver` for device attach/detach events
- `UsbPermissionHelper` for async permission requests

Commits: [`a0d2dd0f`](https://github.com/Nethereum/Nethereum/commit/a0d2dd0f)

---

## DeFi Protocol Integrations

### Nethereum.Uniswap

Complete Uniswap protocol integration with Permit2 and UniversalRouter support.

```csharp
// Create Permit2 service
var permit2 = new Permit2Service(web3, permit2Address);

// Sign single token permit
var permitSingle = new PermitSingle
{
    Details = new PermitDetails
    {
        Token = tokenAddress,
        Amount = amount,
        Expiration = expiration
    },
    Spender = universalRouterAddress
};

var signedPermit = await permit2.GetSinglePermitWithSignatureAsync(
    permitSingle,
    signerKey
);

// Sign batch permit
var signedBatch = await permit2.GetBatchPermitWithSignatureAsync(
    permitBatch,
    signerKey
);
```

**UniversalRouter Commands:**
```csharp
// Build swap command sequence
var commands = new List<IUniversalRouterCommand>
{
    new Permit2PermitCommand(signedPermit),
    new V3SwapExactInCommand(path, recipient, amountIn, minOut),
    new PayPortionCommand(token, recipient, bips)
};

await universalRouter.ExecuteAsync(commands, deadline);
```

**Components:**
- `Permit2Service` - Single and batch permit signing
- `PermitSigner` - EIP-712 typed data signature generation
- `UniswapAddresses` - Protocol address registry by chain
- UniversalRouter command pattern

Commits: [`8eb169ec`](https://github.com/Nethereum/Nethereum/commit/8eb169ec), [`5e7262b2`](https://github.com/Nethereum/Nethereum/commit/5e7262b2), [`b439a72f`](https://github.com/Nethereum/Nethereum/commit/b439a72f)

---

### Nethereum.X402

HTTP 402 Payment Required protocol implementation (RFC 8866) - micropayments for APIs.

**Server-Side (ASP.NET Core):**
```csharp
// Add X402 middleware
app.UseX402(options =>
{
    options.PaymentRoutes = new[]
    {
        new PaymentRoute
        {
            Path = "/api/premium/*",
            Amount = 1000000, // 1 USDC (6 decimals)
            Token = usdcAddress
        }
    };
    options.FacilitatorUrl = "https://facilitator.example.com";
});
```

**Client-Side:**
```csharp
var x402Client = new X402HttpClient(
    httpClient,
    signer,
    new X402HttpClientOptions
    {
        MaxPaymentAmount = 10_000000 // Max 10 USDC
    }
);

// Automatic payment handling on 402 response
var response = await x402Client.GetAsync("https://api.example.com/premium/data");
```

**Payment Flow:**
1. Client requests protected resource
2. Server returns 402 with payment requirements
3. Client signs EIP-3009 authorization
4. Facilitator verifies and settles payment
5. Server provides resource

Commits: [`a85cb0ec`](https://github.com/Nethereum/Nethereum/commit/a85cb0ec), [`772d3bca`](https://github.com/Nethereum/Nethereum/commit/772d3bca), [`a85b0d8e`](https://github.com/Nethereum/Nethereum/commit/a85b0d8e)

---

### Nethereum.Circles

Circles protocol integration - decentralized universal basic income on Gnosis Chain.

```csharp
var hub = new HubService(web3, hubAddress);

// Register as human (requires invitation)
await hub.RegisterHumanRequestAsync(inviterAddress);

// Trust another user
await hub.TrustRequestAsync(trusteeAddress, expiryTime);

// Mint personal tokens (daily UBI)
await hub.PersonalMintRequestAsync();

// Transfer with trust path
await hub.TransferThroughRequestAsync(
    from, to, amount, flowEdges
);
```

**Contract Services:**
- `HubService` - Central protocol coordinator
- `DemurrageCirclesService` - Demurrage token operations
- `InflationaryCirclesService` - Inflationary token operations
- `NameRegistryService` - Avatar name resolution

Commits: [`fe66c281`](https://github.com/Nethereum/Nethereum/commit/fe66c281), [`86269d07`](https://github.com/Nethereum/Nethereum/commit/86269d07)

---

## Updated Libraries

### Nethereum.DataServices

**ChainList Integration (New):**
```csharp
var chainList = new ChainlistRpcApiService();
var chains = await chainList.GetAllChainsAsync();
var rpcs = await chainList.GetRpcsForChainAsync(chainId);
```

**CoinGecko Integration (New):**
```csharp
var coingecko = new CoinGeckoApiService(apiKey);
var tokenList = await coingecko.GetTokenListAsync(chainId);
var prices = await coingecko.GetPricesAsync(coinIds, currencies);
```

**Etherscan V2 (Major Upgrade):**
- Unified API supporting ALL chains
- New account methods: balance at block, multi-balance, beacon withdrawals, ERC-1155/NFT transfers
- New contract methods: creator lookup, proxy verification
- New gas tracker methods

**Sourcify V2:**
- Updated API endpoints
- Chain info, metadata, and verification responses

Commits: [`ac1728da`](https://github.com/Nethereum/Nethereum/commit/ac1728da), [`965ead82`](https://github.com/Nethereum/Nethereum/commit/965ead82), [`ffc8fc7f`](https://github.com/Nethereum/Nethereum/commit/ffc8fc7f)

---

### Nethereum.GnosisSafe

- `SafeHashes` utilities for hash computation
- Personal sign support for Safe signatures
- Gnosis Safe V signature conversion
- `SafeAccount` with contract handler configuration
- JSON serialization for Safe web app import/export

Commits: [`1b19c065`](https://github.com/Nethereum/Nethereum/commit/1b19c065), [`a62fa294`](https://github.com/Nethereum/Nethereum/commit/a62fa294)

---

### Nethereum.KeyStore

**Generic Encryption (New):**
```csharp
// Encrypt any data using keystore format
var keyStore = new KeyStoreService();
var encrypted = keyStore.EncryptAndGenerateDefaultKeyStore(
    data: mySecretData,
    password: password
);

// Decrypt
var decrypted = keyStore.DecryptKeyStore(encrypted, password);
```

Commits: [`33326297`](https://github.com/Nethereum/Nethereum/commit/33326297), [`6e931c03`](https://github.com/Nethereum/Nethereum/commit/6e931c03)

---

### Nethereum.Signer.EIP712

- Multiple hash output for signer/wallet matching (Safe compatibility)
- Enhanced `Eip712TypedDataSigner`

Commits: [`ac065a85`](https://github.com/Nethereum/Nethereum/commit/ac065a85), [`139869e3`](https://github.com/Nethereum/Nethereum/commit/139869e3)

---

### Nethereum.EIP6963WalletInterop

- Events for Error and Disconnect subscription
- Proper disconnect handling
- Clear selected host provider support

Commits: [`4fbb1123`](https://github.com/Nethereum/Nethereum/commit/4fbb1123), [`1d80396f`](https://github.com/Nethereum/Nethereum/commit/1d80396f)

---

### Nethereum.Mud

```csharp
// Change tracking for sync operations
var tracker = new InMemoryChangeTrackerTableRepository();
tracker.EnableTracking = true;

// Get condensed changes for sync
var changes = tracker.GetChanges();
foreach (var change in changes)
{
    await syncService.ApplyAsync(change);
}
tracker.ClearChanges();
```

Commits: [`767e2a7c`](https://github.com/Nethereum/Nethereum/commit/767e2a7c), [`f9f21333`](https://github.com/Nethereum/Nethereum/commit/f9f21333)

---

### Nethereum.Merkle

- Merkle Sparse Tree support

Commits: [`31e26a61`](https://github.com/Nethereum/Nethereum/commit/31e26a61)

---

## Example Projects & Demos

### Wallet Demos

| Project | Description | Path |
|---------|-------------|------|
| **Nethereum.Wallet.Blazor.Demo** | Full Blazor wallet demo | `src/demos/Nethereum.Wallet.Blazor.Demo` |
| **Nethereum.Wallet.MauiBlazor.Demo** | MAUI mobile wallet demo | `src/demos/Nethereum.Wallet.MauiBlazor.Demo` |

### X402 Payment Protocol Demos

| Project | Description | Path |
|---------|-------------|------|
| **Nethereum.X402.SimpleServer** | Basic X402 server | `src/demos/Nethereum.X402.SimpleServer` |
| **Nethereum.X402.SimpleClient** | Basic X402 client | `src/demos/Nethereum.X402.SimpleClient` |
| **Nethereum.X402.FacilitatorServer** | Payment facilitator | `src/demos/Nethereum.X402.FacilitatorServer` |

### Hardware Wallet Demos

| Project | Description | Path |
|---------|-------------|------|
| **Nethereum.Signer.Trezor.Console** | Console Trezor integration | `consoletests/Nethereum.Signer.Trezor.Console` |
| **Nethereum.Signer.Trezor.Maui** | MAUI Trezor demo with Android USB | `consoletests/Nethereum.Signer.Trezor.Maui` |

### Other Examples

| Project | Description | Path |
|---------|-------------|------|
| **MetamaskExampleBlazor.Wasm** | EIP-6963 wallet detection | `consoletests/MetamaskExampleBlazor.Wasm` |
| **NethereumReownAppKitBlazor** | Reown AppKit integration | `consoletests/NethereumReownAppKitBlazor` |

---

## Other Improvements

### External Signer Enhancements
- EIP-712 typed data signing support for all external signers
- V4 external signer protocol updates

Commits: [`139869e3`](https://github.com/Nethereum/Nethereum/commit/139869e3), [`6b7bd150`](https://github.com/Nethereum/Nethereum/commit/6b7bd150)

### Contracts & RPC
- Virtual contract message properties for JSON serialization override
- `IContractServiceConfigurableAccount` for account-based handler configuration
- Public `SendAsync` for direct RPC access
- `wallet_watchAsset` object parameter fix

Commits: [`9c85fa9e`](https://github.com/Nethereum/Nethereum/commit/9c85fa9e), [`bfb1651d`](https://github.com/Nethereum/Nethereum/commit/bfb1651d), [`56f93728`](https://github.com/Nethereum/Nethereum/commit/56f93728), [`6f375ac3`](https://github.com/Nethereum/Nethereum/commit/6f375ac3)

### Utilities
- `ViewOnlyAccount` for simplified impersonation
- `TypedDataRaw.GetChainIdFromDomain` extension
- `ChainFeature.FromAddEthereumChainParameter` conversion
- JSON hex-to-byte array utilities (Newtonsoft & STJ)
- Generic shuffler using `Random.Shared`
- Electra/Fusaka fork support

Commits: [`a3e3a0aa`](https://github.com/Nethereum/Nethereum/commit/a3e3a0aa), [`5c990a57`](https://github.com/Nethereum/Nethereum/commit/5c990a57), [`2a49e299`](https://github.com/Nethereum/Nethereum/commit/2a49e299), [`97f7c19c`](https://github.com/Nethereum/Nethereum/commit/97f7c19c), [`60ec1efa`](https://github.com/Nethereum/Nethereum/commit/60ec1efa)

### Unity
- REST utility updates
- `MultiPartAsync` support

Commits: [`b911ca4e`](https://github.com/Nethereum/Nethereum/commit/b911ca4e), [`ac1728da`](https://github.com/Nethereum/Nethereum/commit/ac1728da)

---

## Package Documentation

Comprehensive README documentation added to NuGet packages for:

**Core:** Nethereum.ABI, Nethereum.Util, Nethereum.RLP, Nethereum.Hex, Nethereum.Model, Nethereum.Merkle, Nethereum.Merkle.Patricia

**Consensus:** Nethereum.Consensus.Ssz, Nethereum.ChainStateVerification

**Signing:** Nethereum.Signer, Nethereum.Signer.EIP712

**Wallet:** Nethereum.Wallet, Nethereum.Wallet.RpcRequests, Nethereum.Wallet.UI.Components.Blazor, Nethereum.Wallet.UI.Components.Maui, Nethereum.Wallet.UI.Components.Trezor

**Other:** Nethereum.EVM, Nethereum.GnosisSafe, Nethereum.BlockchainProcessing, Nethereum.EIP6963WalletInterop, Nethereum.Web3, Nethereum.RPC, Nethereum.Contracts, Nethereum.Mud*

---

## Breaking Changes

None in this release.

---

## Full Changelog

[5.0.0...5.1.0](https://github.com/Nethereum/Nethereum/compare/5.0.0...HEAD)

---

## Contributors

- Juan Blanco (@juanfranblanco)

---

## NuGet Packages

All packages available on [NuGet](https://www.nuget.org/profiles/nethereum)

## Unity Packages

Unity releases available on the [Unity Releases](https://github.com/Nethereum/Nethereum/releases) page.
