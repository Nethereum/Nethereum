# Nethereum.EIP6963WalletInterop

Core abstraction layer for implementing EIP-6963 Multi-Wallet Discovery Standard across multiple platforms.

## Overview

Nethereum.EIP6963WalletInterop provides the foundational interfaces and base classes for integrating with browser wallet extensions using the EIP-6963 standard. This package enables applications to discover and interact with multiple installed wallet extensions simultaneously, rather than being limited to a single provider at `window.ethereum`.

**What is EIP-6963?**

EIP-6963 is an Ethereum Improvement Proposal that standardizes how wallet providers announce themselves to dApps. Before EIP-6963, applications could only access one wallet at a time via `window.ethereum`, causing conflicts when multiple wallets were installed. EIP-6963 solves this by having each wallet broadcast an `eip6963:announceProvider` event containing metadata (name, icon, UUID, reverse DNS) and its provider instance.

**Key Benefits:**
- Multi-wallet discovery - discover all installed browser wallet extensions
- Wallet metadata - name, icon, UUID, and reverse DNS identifier
- Platform abstraction - implement once, use across Blazor, Unity, desktop applications
- Standard interfaces - IEIP6963WalletInterop and IEthereumHostProvider
- Request interception - automatic routing of signing requests through selected wallet
- Event subscriptions - account and network change notifications
- Type-safe RPC - EIP6963RpcRequestMessage includes "from" address field

**Platform Implementations:**
- **Nethereum.Blazor** - Blazor WebAssembly and Server via IJSRuntime
- **Nethereum.Unity.EIP6963** - Unity3D WebGL builds via jslib

## Installation

```bash
dotnet add package Nethereum.EIP6963WalletInterop
```

Or via Package Manager Console:

```powershell
Install-Package Nethereum.EIP6963WalletInterop
```

## Dependencies

**Project References:**
- Nethereum.UI
- Nethereum.Web3

**Target Frameworks:**
- netstandard2.0
- net472
- net6.0
- net7.0
- net8.0

## Architecture

```
                     ┌──────────────────────────────┐
                     │  Your Application            │
                     │  (Blazor, Unity, Desktop)    │
                     └──────────────────────────────┘
                                  │
                                  │ uses
                                  ▼
┌────────────────────────────────────────────────────────────┐
│         EIP6963WalletHostProvider                          │
│         implements IEthereumHostProvider                   │
│                                                            │
│  - GetWeb3Async()                                         │
│  - EnableProviderAsync()                                  │
│  - GetAvailableWalletsAsync()                             │
│  - SelectWalletAsync(uuid)                                │
│  - Events: SelectedAccountChanged, NetworkChanged         │
└────────────────────────────────────────────────────────────┘
                                  │
                    ┌─────────────┴─────────────┐
                    │                           │
                    ▼                           ▼
    ┌───────────────────────────┐   ┌──────────────────────┐
    │ EIP6963WalletInterceptor  │   │ IEIP6963WalletInterop│
    │  (Request Routing)        │   │  (Platform Bridge)   │
    │                           │   │                      │
    │ - Intercepts RPC calls    │   │ - EnableEthereumAsync│
    │ - Routes signing to wallet│   │ - SendAsync          │
    │ - Auto-fills "from"       │   │ - GetAvailableWallets│
    └───────────────────────────┘   │ - SelectWalletAsync  │
                                    └──────────────────────┘
                                                │
                                Platform-specific implementation
                                                │
                        ┌───────────────────────┴──────────────────┐
                        │                                          │
                        ▼                                          ▼
            ┌────────────────────────┐              ┌──────────────────────┐
            │ EIP6963WalletBlazer-   │              │ EIP6963WebglInterop  │
            │ Interop (Blazor)       │              │ (Unity WebGL)        │
            │                        │              │                      │
            │ Uses IJSRuntime to     │              │ Uses DllImport for   │
            │ call JavaScript        │              │ jslib calls          │
            └────────────────────────┘              └──────────────────────┘
                        │                                          │
                        └───────────────────┬──────────────────────┘
                                            │
                                            ▼
                            ┌───────────────────────────┐
                            │  Browser JavaScript       │
                            │  NethereumEIP6963Interop  │
                            │                           │
                            │  - Listen for EIP-6963    │
                            │    announceProvider events│
                            │  - Store wallet metadata  │
                            │  - Route requests to      │
                            │    selected wallet        │
                            └───────────────────────────┘
                                            │
                                            ▼
                        ┌───────────────────────────────────┐
                        │  Browser Wallet Extensions        │
                        │  (MetaMask, Coinbase, Rabby, etc.)│
                        └───────────────────────────────────┘
```

## Key Concepts

### EIP-6963 Discovery Flow

1. **Application Loads**: JavaScript listens for `eip6963:announceProvider` events
2. **Request Providers**: Dispatches `eip6963:requestProvider` event
3. **Wallets Announce**: Each installed wallet broadcasts its metadata via `eip6963:announceProvider`
4. **Store Providers**: Application stores all discovered wallet providers
5. **User Selection**: User selects which wallet to use from discovered list
6. **Enable Connection**: Call `EnableProviderAsync()` to request account access
7. **Transaction Signing**: All signing requests route through selected wallet provider

### IEIP6963WalletInterop Interface

Platform-specific implementations must implement this interface to bridge C# code to JavaScript:

```csharp
public interface IEIP6963WalletInterop
{
    // Connection Management
    ValueTask<string> EnableEthereumAsync();
    ValueTask<bool> CheckAvailabilityAsync();
    ValueTask<string> GetSelectedAddress();

    // RPC Communication
    ValueTask<RpcResponseMessage> SendAsync(RpcRequestMessage rpcRequestMessage);
    ValueTask<RpcResponseMessage> SendTransactionAsync(EIP6963RpcRequestMessage rpcRequestMessage);
    ValueTask<string> SignAsync(string utf8Hex);

    // Multi-Wallet Discovery
    ValueTask<EIP6963WalletInfo[]> GetAvailableWalletsAsync();
    ValueTask SelectWalletAsync(string walletId);
    ValueTask<string> GetWalletIconAsync(string walletId);
}
```

### EIP6963WalletInfo

Wallet metadata returned by the discovery process:

```csharp
public class EIP6963WalletInfo
{
    public string Uuid { get; set; }    // Unique identifier (e.g., "c436f8d0-...")
    public string Name { get; set; }    // Human-readable name (e.g., "MetaMask")
    public string Icon { get; set; }    // Base64 data URI or HTTPS URL
    public string Rdns { get; set; }    // Reverse DNS (e.g., "io.metamask")
}
```

### Request Interception

EIP6963WalletInterceptor automatically routes specific methods through the wallet:

**Always Intercepted (Wallet-Only Methods):**
- eth_sendTransaction
- eth_signTransaction
- eth_sign
- personal_sign
- eth_signTypedData
- eth_signTypedData_v3
- eth_signTypedData_v4
- wallet_watchAsset
- wallet_addEthereumChain
- wallet_switchEthereumChain

**Optional Interception:**
- eth_call (fills "from" if not provided)
- eth_estimateGas (fills "from" if not provided)

All other methods can optionally pass through an RPC endpoint if configured.

### MultipleWalletsProvider Support

The host provider exposes `MultipleWalletsProvider` property:

```csharp
public bool MultipleWalletsProvider => true;  // EIP-6963 supports multiple wallets
public bool MultipleWalletSelected { get; private set; }  // Set after SelectWalletAsync
```

This distinguishes EIP-6963 providers from single-wallet providers like MetaMask extension.

## Quick Start

### For Platform Implementers

If you're creating a new platform integration (e.g., MAUI, Avalonia), implement `IEIP6963WalletInterop`:

```csharp
using Nethereum.EIP6963WalletInterop;
using Nethereum.JsonRpc.Client.RpcMessages;

public class MyPlatformEIP6963Interop : IEIP6963WalletInterop
{
    public async ValueTask<EIP6963WalletInfo[]> GetAvailableWalletsAsync()
    {
        // Call platform-specific JavaScript bridge
        var walletsJson = await CallJavaScriptAsync("NethereumEIP6963Interop.getAvailableWallets");
        return JsonConvert.DeserializeObject<EIP6963WalletInfo[]>(walletsJson);
    }

    public async ValueTask SelectWalletAsync(string walletId)
    {
        await CallJavaScriptAsync("NethereumEIP6963Interop.selectWallet", walletId);
    }

    public async ValueTask<string> EnableEthereumAsync()
    {
        return await CallJavaScriptAsync("NethereumEIP6963Interop.enableEthereum");
    }

    public async ValueTask<RpcResponseMessage> SendAsync(RpcRequestMessage rpcRequestMessage)
    {
        var json = JsonConvert.SerializeObject(rpcRequestMessage);
        var responseJson = await CallJavaScriptAsync("NethereumEIP6963Interop.request", json);
        return JsonConvert.DeserializeObject<RpcResponseMessage>(responseJson);
    }

    // Implement remaining interface methods...
}
```

### For Application Developers

Use the platform-specific implementation (Blazor example):

```csharp
using Nethereum.Blazor.EIP6963WalletInterop;
using Nethereum.EIP6963WalletInterop;

// In your Blazor component or service
@inject IJSRuntime JSRuntime

private EIP6963WalletHostProvider walletProvider;
private EIP6963WalletInfo[] availableWallets;

protected override async Task OnInitializedAsync()
{
    // Create platform-specific interop
    var walletInterop = new EIP6963WalletBlazorInterop(JSRuntime);

    // Create host provider
    walletProvider = new EIP6963WalletHostProvider(walletInterop);

    // Discover available wallets
    availableWallets = await walletProvider.GetAvailableWalletsAsync();
}

private async Task ConnectWallet(string walletUuid)
{
    // Select wallet
    await walletProvider.SelectWalletAsync(walletUuid);

    // Enable and get account
    var account = await walletProvider.EnableProviderAsync();

    // Get Web3 instance
    var web3 = await walletProvider.GetWeb3Async();

    // Use web3...
    var balance = await web3.Eth.GetBalance.SendRequestAsync(account);
}
```

## Usage Examples

### Example 1: Complete Wallet Discovery and Selection

```csharp
using Nethereum.EIP6963WalletInterop;
using System;
using System.Threading.Tasks;

public class WalletManager
{
    private readonly EIP6963WalletHostProvider _provider;
    private IEIP6963WalletInterop _walletInterop;

    public WalletManager(IEIP6963WalletInterop walletInterop)
    {
        _walletInterop = walletInterop;
        _provider = new EIP6963WalletHostProvider(walletInterop);

        // Subscribe to events
        _provider.SelectedAccountChanged += OnAccountChanged;
        _provider.NetworkChanged += OnNetworkChanged;
        _provider.AvailabilityChanged += OnAvailabilityChanged;
    }

    public async Task<EIP6963WalletInfo[]> DiscoverWalletsAsync()
    {
        await Task.Delay(100); // Wait for wallet announcements
        var wallets = await _provider.GetAvailableWalletsAsync();

        Console.WriteLine($"Found {wallets.Length} wallets:");
        foreach (var wallet in wallets)
        {
            Console.WriteLine($"  - {wallet.Name} ({wallet.Rdns})");
            Console.WriteLine($"    UUID: {wallet.Uuid}");
            Console.WriteLine($"    Icon: {wallet.Icon.Substring(0, 50)}...");
        }

        return wallets;
    }

    public async Task<string> ConnectToWalletAsync(string walletUuid)
    {
        // Select the wallet
        await _provider.SelectWalletAsync(walletUuid);

        // Enable and request accounts
        var selectedAccount = await _provider.EnableProviderAsync();

        Console.WriteLine($"Connected to: {selectedAccount}");
        Console.WriteLine($"Chain ID: {_provider.SelectedNetworkChainId}");

        return selectedAccount;
    }

    private async Task OnAccountChanged(string newAccount)
    {
        Console.WriteLine($"Account changed to: {newAccount}");
    }

    private async Task OnNetworkChanged(long newChainId)
    {
        Console.WriteLine($"Network changed to: {newChainId}");
    }

    private async Task OnAvailabilityChanged(bool available)
    {
        Console.WriteLine($"Wallet availability changed: {available}");
    }
}
```

### Example 2: Using Web3 with Selected Wallet

```csharp
using Nethereum.EIP6963WalletInterop;
using Nethereum.Web3;
using Nethereum.Hex.HexTypes;

public async Task SendTransactionAsync(string walletUuid, string toAddress, decimal amount)
{
    // Setup (assuming walletInterop already created)
    var provider = new EIP6963WalletHostProvider(walletInterop);

    // Select and connect
    await provider.SelectWalletAsync(walletUuid);
    var account = await provider.EnableProviderAsync();

    // Get Web3 with interceptor configured
    var web3 = await provider.GetWeb3Async();

    // Send transaction - automatically routed through wallet for signing
    var txInput = new Nethereum.RPC.Eth.DTOs.TransactionInput
    {
        From = account,  // Will be set automatically by interceptor
        To = toAddress,
        Value = new HexBigInteger(Web3.Convert.ToWei(amount))
    };

    var txHash = await web3.Eth.TransactionManager.SendTransactionAsync(txInput);
    Console.WriteLine($"Transaction sent: {txHash}");

    // Wait for receipt
    var receipt = await web3.Eth.TransactionManager
        .TransactionReceiptService
        .PollForReceiptAsync(txHash);

    Console.WriteLine($"Transaction mined in block: {receipt.BlockNumber}");
}
```

### Example 3: Personal Sign with EIP-6963

```csharp
using Nethereum.EIP6963WalletInterop;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;

public async Task<string> SignMessageAsync(string walletUuid, string message)
{
    var provider = new EIP6963WalletHostProvider(walletInterop);

    await provider.SelectWalletAsync(walletUuid);
    var account = await provider.EnableProviderAsync();

    // Sign message (routes through wallet)
    var signature = await provider.SignMessageAsync(message);

    // Verify signature
    var signer = new EthereumMessageSigner();
    var recoveredAddress = signer.EncodeUTF8AndEcRecover(message, signature);

    if (recoveredAddress.ToLower() == account.ToLower())
    {
        Console.WriteLine("Signature verified!");
    }

    return signature;
}
```

### Example 4: EIP-712 Typed Data Signing

```csharp
using Nethereum.EIP6963WalletInterop;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Signer.EIP712;

[Struct("Person")]
public class Person
{
    [Parameter("string", "name", 1)]
    public string Name { get; set; }

    [Parameter("address", "wallet", 2)]
    public string Wallet { get; set; }
}

[Struct("Mail")]
public class Mail
{
    [Parameter("tuple", "from", 1)]
    public Person From { get; set; }

    [Parameter("tuple", "to", 2)]
    public Person To { get; set; }

    [Parameter("string", "contents", 3)]
    public string Contents { get; set; }
}

public async Task<string> SignTypedDataAsync(string walletUuid)
{
    var provider = new EIP6963WalletHostProvider(walletInterop);
    await provider.SelectWalletAsync(walletUuid);
    await provider.EnableProviderAsync();

    var web3 = await provider.GetWeb3Async();

    // Create typed data
    var typedData = new TypedData<Domain>
    {
        Domain = new Domain
        {
            Name = "Ether Mail",
            Version = "1",
            ChainId = 1,
            VerifyingContract = "0xCcCCccccCCCCcCCCCCCcCcCccCcCCCcCcccccccC"
        },
        Types = MemberDescriptionFactory.GetTypesMemberDescription(
            typeof(Domain), typeof(Mail), typeof(Person)),
        PrimaryType = nameof(Mail)
    };

    var mail = new Mail
    {
        From = new Person { Name = "Alice", Wallet = "0x..." },
        To = new Person { Name = "Bob", Wallet = "0x..." },
        Contents = "Hello Bob!"
    };

    typedData.SetMessage(mail);

    // Sign (routes through wallet)
    var signature = await web3.Eth.AccountSigning.SignTypedDataV4
        .SendRequestAsync(typedData.ToJson());

    return signature;
}
```

### Example 5: Switching Chains via Wallet

```csharp
using Nethereum.EIP6963WalletInterop;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Hex.HexTypes;

public async Task SwitchToPolygonAsync(string walletUuid)
{
    var provider = new EIP6963WalletHostProvider(walletInterop);
    await provider.SelectWalletAsync(walletUuid);
    await provider.EnableProviderAsync();

    var web3 = await provider.GetWeb3Async();

    try
    {
        // Try to switch to Polygon (chain ID 137)
        await web3.Eth.HostWallet.SwitchEthereumChain.SendRequestAsync(
            new SwitchEthereumChainParameter
            {
                ChainId = new HexBigInteger(137)
            });
    }
    catch
    {
        // Chain not added yet, add it
        await web3.Eth.HostWallet.AddEthereumChain.SendRequestAsync(
            new AddEthereumChainParameter
            {
                ChainId = new HexBigInteger(137),
                ChainName = "Polygon Mainnet",
                RpcUrls = new[] { "https://polygon-rpc.com" },
                NativeCurrency = new NativeCurrency
                {
                    Name = "MATIC",
                    Symbol = "MATIC",
                    Decimals = 18
                },
                BlockExplorerUrls = new[] { "https://polygonscan.com" }
            });
    }
}
```

### Example 6: Hybrid Mode with Custom RPC

```csharp
using Nethereum.EIP6963WalletInterop;
using Nethereum.JsonRpc.Client;

public async Task UseHybridModeAsync(string walletUuid)
{
    // Provide custom RPC client for queries
    var rpcClient = new RpcClient(new Uri("https://mainnet.infura.io/v3/YOUR-KEY"));

    // Create provider with custom client
    var provider = new EIP6963WalletHostProvider(walletInterop, rpcClient);

    await provider.SelectWalletAsync(walletUuid);
    await provider.EnableProviderAsync();

    var web3 = await provider.GetWeb3Async();

    // eth_call goes through Infura (faster, no wallet popup)
    var balance = await web3.Eth.GetBalance.SendRequestAsync("0x...");

    // eth_sendTransaction goes through wallet (requires signature)
    var txHash = await web3.Eth.GetEtherTransferService()
        .TransferEtherAsync("0x...", 0.1m);
}
```

## API Reference

### EIP6963WalletHostProvider

Implementation of IEthereumHostProvider for EIP-6963 wallets.

```csharp
public class EIP6963WalletHostProvider : IEthereumHostProvider
{
    // Constructors
    public EIP6963WalletHostProvider(
        IEIP6963WalletInterop walletInterop,
        IClient client = null,
        bool useOnlySigningWalletTransactionMethods = false);

    // Properties
    public static EIP6963WalletHostProvider Current { get; }
    public string Name { get; }  // "EIP6963 Standard"
    public bool MultipleWalletsProvider { get; }  // true
    public bool MultipleWalletSelected { get; }
    public bool Available { get; }
    public string SelectedAccount { get; }
    public long SelectedNetworkChainId { get; }
    public bool Enabled { get; }
    public IClient Client { get; }

    // Events
    public event Func<string, Task> SelectedAccountChanged;
    public event Func<long, Task> NetworkChanged;
    public event Func<bool, Task> AvailabilityChanged;
    public event Func<bool, Task> EnabledChanged;

    // Multi-Wallet Methods
    public Task<EIP6963WalletInfo[]> GetAvailableWalletsAsync();
    public Task SelectWalletAsync(string walletuuid);

    // IEthereumHostProvider Methods
    public Task<bool> CheckProviderAvailabilityAsync();
    public Task<IWeb3> GetWeb3Async();
    public Task<string> EnableProviderAsync();
    public Task<string> GetProviderSelectedAccountAsync();
    public Task<string> SignMessageAsync(string message);

    // Change Notification Methods
    public Task ChangeSelectedAccountAsync(string selectedAccount);
    public Task ChangeSelectedNetworkAsync(long chainId);
    public Task ChangeWalletAvailableAsync(bool available);
    public Task ChangeWalletEnabledAsync(bool enabled);
}
```

### EIP6963WalletInterceptor

Request interceptor for routing methods through the wallet.

```csharp
public class EIP6963WalletInterceptor : RequestInterceptor
{
    // Constructor
    public EIP6963WalletInterceptor(
        IEIP6963WalletInterop walletInterop,
        bool useOnlySigningWalletTransactionMethods = false);

    // Properties
    public static List<string> SigningWalletTransactionsMethods { get; }
    public string SelectedAccount { get; set; }

    // Methods Automatically Intercepted:
    // - eth_sendTransaction (fills "from" with SelectedAccount)
    // - eth_signTransaction
    // - eth_sign
    // - personal_sign (reorders params: [message, account])
    // - eth_signTypedData / v3 / v4 (reorders params: [account, data])
    // - wallet_watchAsset
    // - wallet_addEthereumChain
    // - wallet_switchEthereumChain
    // - eth_call (fills "from" if not provided)
    // - eth_estimateGas (fills "from" if not provided)
}
```

### EIP6963RpcRequestMessage

Extended RPC request message with "from" field.

```csharp
public class EIP6963RpcRequestMessage : RpcRequestMessage
{
    public EIP6963RpcRequestMessage(
        object id,
        string method,
        string from,
        params object[] parameterList);

    public string From { get; }
}
```

### EIP6963WalletInfo

Wallet metadata structure.

```csharp
public class EIP6963WalletInfo
{
    public string Uuid { get; set; }    // Unique identifier
    public string Name { get; set; }    // Display name
    public string Icon { get; set; }    // Data URI or HTTPS URL
    public string Rdns { get; set; }    // Reverse DNS (e.g., "io.metamask")
}
```

## JavaScript Integration

### Browser-Side Implementation

Platform implementations should include JavaScript similar to:

```javascript
window.NethereumEIP6963Interop = {
    ethereumProviders: [],
    selectedEthereumProvider: null,

    init: function() {
        // Listen for wallet announcements
        window.addEventListener("eip6963:announceProvider", (event) => {
            const provider = event.detail;
            if (!this.ethereumProviders.some(p => p.info.uuid === provider.info.uuid)) {
                this.ethereumProviders.push(provider);
            }
        });

        // Request wallets to announce themselves
        window.dispatchEvent(new Event("eip6963:requestProvider"));
    },

    getAvailableWallets: function() {
        return this.ethereumProviders.map(provider => ({
            name: provider.info.name,
            uuid: provider.info.uuid,
            icon: provider.info.icon,
            rdns: provider.info.rdns
        }));
    },

    selectWallet: async function(uuid) {
        const provider = this.ethereumProviders.find(p => p.info.uuid === uuid);
        if (provider) {
            this.selectedEthereumProvider = provider.provider;
            await this.enableEthereum();
        }
    },

    enableEthereum: async function() {
        // Setup event listeners
        this.selectedEthereumProvider.on("accountsChanged", (accounts) => {
            // Call back to C#
            DotNet.invokeMethodAsync('YourAssembly', 'EIP6963SelectedAccountChanged', accounts[0]);
        });

        this.selectedEthereumProvider.on("chainChanged", (chainId) => {
            DotNet.invokeMethodAsync('YourAssembly', 'EIP6963SelectedNetworkChanged', chainId);
        });
    },

    request: async function(message) {
        const parsedMessage = JSON.parse(message);
        const response = await this.selectedEthereumProvider.request(parsedMessage);
        return JSON.stringify({ jsonrpc: "2.0", result: response, id: parsedMessage.id });
    }
};

window.addEventListener("load", () => {
    window.NethereumEIP6963Interop.init();
});
```

## Important Notes

### Wait for Wallet Announcements

EIP-6963 wallet announcements are asynchronous. Add a small delay after page load before calling `GetAvailableWalletsAsync()`:

```csharp
await Task.Delay(100);  // Allow time for wallets to announce
var wallets = await provider.GetAvailableWalletsAsync();
```

### Event Subscription Pattern

Platform implementations should use `[JSInvokable]` static methods to receive JavaScript callbacks:

```csharp
[JSInvokable()]
public static async Task EIP6963SelectedAccountChanged(string selectedAccount)
{
    await EIP6963WalletHostProvider.Current.ChangeSelectedAccountAsync(selectedAccount);
}
```

### UseOnlySigningWalletTransactionMethods Parameter

Controls whether all requests go through the wallet or only signing methods:

```csharp
// All requests through wallet
var provider = new EIP6963WalletHostProvider(walletInterop, useOnlySigningWalletTransactionMethods: false);

// Only signing requests through wallet (requires custom RPC client)
var provider = new EIP6963WalletHostProvider(walletInterop, rpcClient, useOnlySigningWalletTransactionMethods: true);
```

### Wallet Icon Handling

Wallet icons are typically base64-encoded data URIs:

```
data:image/svg+xml;base64,PHN2ZyB3aWR0aD0iMzAiIGhlaWdodD0iMzAiIHZpZXdCb3g9IjAgMCAz...
```

Display directly in `<img>` tags without additional processing.

### Reverse DNS (RDNS) Identifier

The RDNS field provides a stable identifier for wallets:

```csharp
io.metamask         // MetaMask
com.coinbase.wallet // Coinbase Wallet
io.rabby            // Rabby
```

Use this for consistent wallet identification across sessions.

## Related Packages

### Platform Implementations
- **Nethereum.Blazor** - EIP6963WalletBlazorInterop for Blazor applications
- **Nethereum.Unity.EIP6963** - EIP6963WebglInterop for Unity WebGL builds

### Dependencies
- **Nethereum.UI** - IEthereumHostProvider interface and SIWE authentication
- **Nethereum.Web3** - Web3 client, transaction management, and RPC types

### Alternative Wallet Integration
- **Nethereum.Metamask** - MetaMask-specific integration (legacy window.ethereum approach)
- **Nethereum.WalletConnect** - Mobile wallet connectivity via QR code pairing

## EIP-6963 Standard

For the complete EIP-6963 specification, see:
- https://eips.ethereum.org/EIPS/eip-6963
- https://eip6963.org/
