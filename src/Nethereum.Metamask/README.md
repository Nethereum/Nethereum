# Nethereum.Metamask

Core MetaMask integration abstractions for Nethereum. Provides platform-agnostic interfaces and request interceptors for integrating MetaMask wallet into .NET applications.

## Overview

Nethereum.Metamask is the foundation package for MetaMask integration. It defines the core abstractions (`IMetamaskInterop`) and request interceptor logic needed to route Ethereum RPC calls through the MetaMask browser extension. Platform-specific implementations (Blazor, desktop, mobile) build on top of these abstractions.

**Key Features:**
- `IMetamaskInterop` interface for platform-specific implementations
- `MetamaskHostProvider` implementing `IEthereumHostProvider`
- `MetamaskInterceptor` for routing RPC requests through MetaMask
- Support for all MetaMask-specific methods (personal_sign, eth_signTypedData_v4, wallet_*)
- Automatic account injection into transactions
- Two operating modes: all requests or signing-only

**Use Cases:**
- Building Blazor dApps with MetaMask
- Creating custom MetaMask integrations for other platforms
- Routing transaction signing through MetaMask while using custom RPC
- Implementing wallet connection flows

## Installation

```bash
dotnet add package Nethereum.Metamask
```

**Note:** This is the core abstraction package. For Blazor applications, use **Nethereum.Metamask.Blazor** which provides the JavaScript interop implementation.

## Dependencies

- Nethereum.UI
- Nethereum.Web3

## Architecture

### Platform Implementations

```
┌─────────────────────┐  ┌──────────────────┐  ┌─────────────────┐
│ Nethereum.Metamask  │  │ Nethereum.Unity  │  │ Desktop/Mobile  │
│      .Blazor        │  │   .Metamask      │  │ Implementations │
│                     │  │                  │  │                 │
│ MetamaskBlazer-     │  │ Unity-specific   │  │ Platform-       │
│ Interop (JS)        │  │ implementation   │  │ specific impl   │
└─────────────────────┘  └──────────────────┘  └─────────────────┘
         │                       │                      │
         └───────────────────────┴──────────────────────┘
                                 │
                    implements IMetamaskInterop
                                 │
                                 ▼
┌─────────────────────────────────────────────────────────────┐
│              Nethereum.Metamask (This Package)               │
│                     Core Abstractions                        │
│                                                              │
│  ┌──────────────────────┐      ┌────────────────────────┐  │
│  │ IMetamaskInterop     │      │ MetamaskHostProvider   │  │
│  │ (Platform Interface) │      │ (Wallet Provider)      │  │
│  └──────────────────────┘      └────────────────────────┘  │
│                                                              │
│  ┌──────────────────────┐      ┌────────────────────────┐  │
│  │ MetamaskInterceptor  │      │ MetamaskRpcRequest-    │  │
│  │ (Request Router)     │      │ Message                │  │
│  └──────────────────────┘      └────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
                                 │
                                 ▼
┌─────────────────────────────────────────────────────────────┐
│                     Nethereum.Web3                           │
│                  (Ethereum Interaction)                      │
└─────────────────────────────────────────────────────────────┘
```

### IMetamaskInterop

Platform-specific interface that must be implemented by each platform (Blazor, Unity, desktop):

```csharp
public interface IMetamaskInterop
{
    Task<string> EnableEthereumAsync();
    Task<bool> CheckMetamaskAvailability();
    Task<string> GetSelectedAddress();
    Task<RpcResponseMessage> SendAsync(RpcRequestMessage rpcRequestMessage);
    Task<RpcResponseMessage> SendTransactionAsync(MetamaskRpcRequestMessage rpcRequestMessage);
    Task<string> SignAsync(string utf8Hex);
}
```

### MetamaskHostProvider

Implements `IEthereumHostProvider` for MetaMask:

```csharp
public class MetamaskHostProvider : IEthereumHostProvider
{
    public string Name { get; } // "Metamask"
    public bool Available { get; }
    public string SelectedAccount { get; }
    public long SelectedNetworkChainId { get; }
    public bool Enabled { get; }

    // Events
    event Func<string, Task> SelectedAccountChanged;
    event Func<long, Task> NetworkChanged;
    event Func<bool, Task> AvailabilityChanged;
    event Func<bool, Task> EnabledChanged;

    // Methods
    Task<bool> CheckProviderAvailabilityAsync();
    Task<string> EnableProviderAsync();
    Task<IWeb3> GetWeb3Async();
    Task<string> SignMessageAsync(string message);
}
```

### MetamaskInterceptor

Intercepts Web3 requests and routes appropriate methods through MetaMask:

```csharp
public class MetamaskInterceptor : RequestInterceptor
{
    // Methods routed through MetaMask
    public static List<string> SigningWalletTransactionsMethods { get; } = new List<string>
    {
        "eth_sendTransaction",
        "eth_signTransaction",
        "eth_sign",
        "personal_sign",
        "eth_signTypedData",
        "eth_signTypedData_v3",
        "eth_signTypedData_v4",
        "wallet_watchAsset",
        "wallet_addEthereumChain",
        "wallet_switchEthereumChain"
    };
}
```

## Usage Examples

### Example 1: Basic Setup (Requires Platform Implementation)

```csharp
using Nethereum.Metamask;
using Nethereum.UI;

// Platform-specific interop implementation
// In Blazor: MetamaskBlazorInterop
// In other platforms: implement IMetamaskInterop
IMetamaskInterop metamaskInterop = GetPlatformSpecificInterop();

// Create MetaMask provider
var metamaskProvider = new MetamaskHostProvider(metamaskInterop);

// Check if MetaMask is available
bool isAvailable = await metamaskProvider.CheckProviderAvailabilityAsync();
if (!isAvailable)
{
    Console.WriteLine("MetaMask extension not detected");
    return;
}

// Request connection
string account = await metamaskProvider.EnableProviderAsync();
Console.WriteLine($"Connected to MetaMask: {account}");

// Get Web3 instance
var web3 = await metamaskProvider.GetWeb3Async();

// All transactions will be signed by MetaMask
var balance = await web3.Eth.GetBalance.SendRequestAsync(account);
Console.WriteLine($"Balance: {Web3.Convert.FromWei(balance)} ETH");
```

### Example 2: Listening to Account Changes

```csharp
using Nethereum.Metamask;

var metamaskProvider = new MetamaskHostProvider(metamaskInterop);

// Subscribe to account changes
metamaskProvider.SelectedAccountChanged += async (newAccount) =>
{
    Console.WriteLine($"Account changed to: {newAccount}");

    // Update UI, reload balances, etc.
    var web3 = await metamaskProvider.GetWeb3Async();
    var balance = await web3.Eth.GetBalance.SendRequestAsync(newAccount);
    Console.WriteLine($"New balance: {Web3.Convert.FromWei(balance)} ETH");
};

// Subscribe to network changes
metamaskProvider.NetworkChanged += async (chainId) =>
{
    Console.WriteLine($"Network changed to chain ID: {chainId}");

    if (chainId == 1)
        Console.WriteLine("Connected to Ethereum Mainnet");
    else if (chainId == 137)
        Console.WriteLine("Connected to Polygon");
    else
        Console.WriteLine($"Connected to unknown network: {chainId}");
};

// Enable provider
await metamaskProvider.EnableProviderAsync();
```

### Example 3: Sending Transactions Through MetaMask

```csharp
using Nethereum.Metamask;
using Nethereum.Web3;
using Nethereum.Hex.HexTypes;

var metamaskProvider = new MetamaskHostProvider(metamaskInterop);
await metamaskProvider.EnableProviderAsync();

var web3 = await metamaskProvider.GetWeb3Async();

// Send ETH (MetaMask will prompt for signature)
var toAddress = "0x742d35Cc6634C0532925a3b844Bc9e7595f0bEb";
var amountInWei = Web3.Convert.ToWei(0.1);

var txHash = await web3.Eth.GetEtherTransferService()
    .TransferEtherAndWaitForReceiptAsync(toAddress, 0.1m);

Console.WriteLine($"Transaction sent: {txHash}");
```

### Example 4: Signing Messages with MetaMask

```csharp
using Nethereum.Metamask;
using Nethereum.Signer;

var metamaskProvider = new MetamaskHostProvider(metamaskInterop);
await metamaskProvider.EnableProviderAsync();

// Sign message (MetaMask popup will appear)
string message = "Sign this message to authenticate";
string signature = await metamaskProvider.SignMessageAsync(message);

Console.WriteLine($"Signature: {signature}");

// Verify signature
var signer = new EthereumMessageSigner();
var recoveredAddress = signer.EncodeUTF8AndEcRecover(message, signature);

Console.WriteLine($"Recovered address: {recoveredAddress}");
Console.WriteLine($"Matches selected account: {recoveredAddress.Equals(metamaskProvider.SelectedAccount, StringComparison.OrdinalIgnoreCase)}");
```

### Example 5: Hybrid Mode - MetaMask for Signing, Custom RPC for Queries

```csharp
using Nethereum.Metamask;
using Nethereum.JsonRpc.Client;
using Nethereum.Web3;

// Custom RPC client for queries (faster, no MetaMask popups)
var customRpcClient = new RpcClient(new Uri("https://mainnet.infura.io/v3/YOUR-PROJECT-ID"));

// MetaMask provider with custom RPC client
// useOnlySigningWalletTransactionMethods = true means:
// - Queries go through custom RPC
// - Signatures go through MetaMask
var metamaskProvider = new MetamaskHostProvider(
    metamaskInterop,
    client: customRpcClient,
    useOnlySigningWalletTransactionMethods: true);

await metamaskProvider.EnableProviderAsync();

var web3 = await metamaskProvider.GetWeb3Async();

// This query goes through Infura (fast, no MetaMask popup)
var blockNumber = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
Console.WriteLine($"Current block: {blockNumber.Value}");

// This transaction goes through MetaMask (user signature required)
var txHash = await web3.Eth.GetEtherTransferService()
    .TransferEtherAsync("0x742d35Cc6634C0532925a3b844Bc9e7595f0bEb", 0.01m);

Console.WriteLine($"Transaction sent via MetaMask: {txHash}");
```

### Example 6: Typed Data Signing (EIP-712)

```csharp
using Nethereum.Metamask;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Signer.EIP712;

var metamaskProvider = new MetamaskHostProvider(metamaskInterop);
await metamaskProvider.EnableProviderAsync();

var web3 = await metamaskProvider.GetWeb3Async();

// Define typed data
var typedData = new
{
    types = new
    {
        EIP712Domain = new[]
        {
            new { name = "name", type = "string" },
            new { name = "version", type = "string" },
            new { name = "chainId", type = "uint256" }
        },
        Person = new[]
        {
            new { name = "name", type = "string" },
            new { name = "wallet", type = "address" }
        }
    },
    primaryType = "Person",
    domain = new
    {
        name = "MyDApp",
        version = "1",
        chainId = 1
    },
    message = new
    {
        name = "Alice",
        wallet = "0x742d35Cc6634C0532925a3b844Bc9e7595f0bEb"
    }
};

// Sign typed data (MetaMask will show structured data)
var signature = await web3.Eth.AccountSigning.SignTypedDataV4.SendRequestAsync(
    System.Text.Json.JsonSerializer.Serialize(typedData));

Console.WriteLine($"EIP-712 Signature: {signature}");
```

### Example 7: Adding Token to MetaMask (wallet_watchAsset)

```csharp
using Nethereum.Metamask;
using Nethereum.Web3;

var metamaskProvider = new MetamaskHostProvider(metamaskInterop);
await metamaskProvider.EnableProviderAsync();

var web3 = await metamaskProvider.GetWeb3Async();

// Prepare token details
var tokenDetails = new
{
    type = "ERC20",
    options = new
    {
        address = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48", // USDC
        symbol = "USDC",
        decimals = 6,
        image = "https://example.com/usdc.png"
    }
};

// Request to add token to MetaMask
// Note: This requires using the interceptor
var result = await web3.Client.SendRequestAsync<bool>(
    "wallet_watchAsset",
    null,
    tokenDetails);

if (result)
{
    Console.WriteLine("Token added to MetaMask successfully");
}
```

### Example 8: Switching Networks in MetaMask

```csharp
using Nethereum.Metamask;
using Nethereum.Web3;
using Nethereum.Hex.HexTypes;

var metamaskProvider = new MetamaskHostProvider(metamaskInterop);
await metamaskProvider.EnableProviderAsync();

var web3 = await metamaskProvider.GetWeb3Async();

// Switch to Polygon network
var switchParams = new
{
    chainId = "0x89" // Polygon chain ID in hex (137 in decimal)
};

try
{
    await web3.Client.SendRequestAsync<object>(
        "wallet_switchEthereumChain",
        null,
        new[] { switchParams });

    Console.WriteLine("Switched to Polygon network");
}
catch (RpcResponseException ex) when (ex.RpcError.Code == 4902)
{
    // Network not added to MetaMask, add it first
    var addChainParams = new
    {
        chainId = "0x89",
        chainName = "Polygon Mainnet",
        nativeCurrency = new
        {
            name = "MATIC",
            symbol = "MATIC",
            decimals = 18
        },
        rpcUrls = new[] { "https://polygon-rpc.com/" },
        blockExplorerUrls = new[] { "https://polygonscan.com/" }
    };

    await web3.Client.SendRequestAsync<object>(
        "wallet_addEthereumChain",
        null,
        new[] { addChainParams });

    Console.WriteLine("Added and switched to Polygon network");
}
```

## API Reference

### MetamaskHostProvider

```csharp
public class MetamaskHostProvider : IEthereumHostProvider
{
    // Constructor
    public MetamaskHostProvider(
        IMetamaskInterop metamaskInterop,
        IClient client = null,
        bool useOnlySigningWalletTransactionMethods = false);

    // Properties
    public static MetamaskHostProvider Current { get; }
    public string Name { get; }
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

    // Methods
    public Task<bool> CheckProviderAvailabilityAsync();
    public Task<string> EnableProviderAsync();
    public Task<IWeb3> GetWeb3Async();
    public Task<string> GetProviderSelectedAccountAsync();
    public Task<string> SignMessageAsync(string message);

    // State change methods (called by platform layer)
    public Task ChangeSelectedAccountAsync(string selectedAccount);
    public Task ChangeSelectedNetworkAsync(long chainId);
    public Task ChangeMetamaskAvailableAsync(bool available);
    public Task ChangeMetamaskEnabledAsync(bool enabled);
}
```

### IMetamaskInterop

```csharp
public interface IMetamaskInterop
{
    Task<string> EnableEthereumAsync();
    Task<bool> CheckMetamaskAvailability();
    Task<string> GetSelectedAddress();
    Task<RpcResponseMessage> SendAsync(RpcRequestMessage rpcRequestMessage);
    Task<RpcResponseMessage> SendTransactionAsync(MetamaskRpcRequestMessage rpcRequestMessage);
    Task<string> SignAsync(string utf8Hex);
}
```

### MetamaskInterceptor

```csharp
public class MetamaskInterceptor : RequestInterceptor
{
    public MetamaskInterceptor(
        IMetamaskInterop metamaskInterop,
        bool useOnlySigningWalletTransactionMethods = false);

    public static List<string> SigningWalletTransactionsMethods { get; }
    public string SelectedAccount { get; set; }
}
```

### MetamaskRpcRequestMessage

```csharp
public class MetamaskRpcRequestMessage : RpcRequestMessage
{
    public MetamaskRpcRequestMessage(
        object id,
        string method,
        string from,
        params object[] parameterList);

    public string From { get; }
}
```

## Important Notes

### Platform-Specific Implementation Required

This package provides **abstractions only**. You must implement `IMetamaskInterop` for your platform:

- **Blazor**: Use **Nethereum.Metamask.Blazor** (provides JavaScript interop)
- **Desktop/Mobile**: Implement `IMetamaskInterop` with embedded browser control
- **Unity**: Implement `IMetamaskInterop` with WebGL or mobile wallet integration

### Two Operating Modes

```csharp
// Mode 1: All requests through MetaMask (default)
var provider = new MetamaskHostProvider(interop);
// Queries and transactions both use MetaMask
// Slower but everything goes through wallet

// Mode 2: Signing only through MetaMask (hybrid)
var provider = new MetamaskHostProvider(
    interop,
    client: customRpcClient,
    useOnlySigningWalletTransactionMethods: true);
// Queries use custom RPC (faster)
// Transactions use MetaMask (secure)
```

### Automatically Handled Methods

The `MetamaskInterceptor` automatically routes these methods through MetaMask:

- `eth_sendTransaction` - Sends transactions (user approval required)
- `eth_signTransaction` - Signs transactions without sending
- `eth_sign` - Personal message signing
- `personal_sign` - EIP-191 personal message signing
- `eth_signTypedData` - Typed data signing (EIP-712)
- `eth_signTypedData_v3` - Typed data signing v3
- `eth_signTypedData_v4` - Typed data signing v4 (recommended)
- `wallet_watchAsset` - Add token to MetaMask
- `wallet_addEthereumChain` - Add custom network
- `wallet_switchEthereumChain` - Switch network

All other methods (queries like `eth_call`, `eth_getBalance`, etc.) use the configured RPC client.

### Account Injection

The interceptor automatically injects the selected MetaMask account into transactions:

```csharp
var web3 = await provider.GetWeb3Async();

// No need to specify 'from' - automatically uses MetaMask account
await web3.Eth.GetEtherTransferService()
    .TransferEtherAsync(toAddress, amount);
```

### Static Current Property

For convenience, the last created `MetamaskHostProvider` is available via:

```csharp
var provider = new MetamaskHostProvider(interop);
// Later, anywhere in code:
var currentProvider = MetamaskHostProvider.Current;
```

### Error Handling

MetaMask interactions can fail for various reasons:

```csharp
try
{
    var txHash = await web3.Eth.GetEtherTransferService()
        .TransferEtherAsync(toAddress, amount);
}
catch (RpcResponseException ex)
{
    // User rejected transaction
    if (ex.RpcError.Code == 4001)
    {
        Console.WriteLine("User rejected the transaction");
    }
    // Insufficient funds
    else if (ex.RpcError.Message.Contains("insufficient funds"))
    {
        Console.WriteLine("Insufficient funds");
    }
    else
    {
        Console.WriteLine($"Transaction failed: {ex.Message}");
    }
}
```

### ValueTask Support

On .NET Core 3.1+, `IMetamaskInterop` uses `ValueTask<T>` for better performance. On earlier frameworks, it uses `Task<T>`.

## Related Packages

### Platform Implementations

- **Nethereum.Metamask.Blazor** - Blazor WebAssembly implementation with JavaScript interop

### Dependencies

- **Nethereum.UI** - Ethereum host provider abstractions
- **Nethereum.Web3** - Web3 client and RPC functionality

### Related UI Packages

- **Nethereum.Blazor** - Blazor components and services
- **Nethereum.EIP6963WalletInterop** - Multi-wallet discovery
- **Nethereum.WalletConnect** - WalletConnect integration
- **Nethereum.Reown.AppKit.Blazor** - Reown AppKit integration

## Additional Resources

- [MetaMask Developer Documentation](https://docs.metamask.io/)
- [MetaMask JSON-RPC API](https://docs.metamask.io/wallet/reference/json-rpc-api/)
- [EIP-712: Typed structured data hashing](https://eips.ethereum.org/EIPS/eip-712)
- [EIP-1193: Ethereum Provider JavaScript API](https://eips.ethereum.org/EIPS/eip-1193)
- [Nethereum Documentation](http://docs.nethereum.com/)
