# Nethereum.Reown.AppKit.Blazor

Nethereum integration for Reown AppKit (formerly WalletConnect AppKit) in Blazor WebAssembly and Blazor Server applications.

## Overview

Nethereum.Reown.AppKit.Blazor wraps the Reown AppKit JavaScript SDK (@reown/appkit-cdn) and exposes it through Nethereum's `IEthereumHostProvider` abstraction.

**What is Reown AppKit?**

Reown AppKit (formerly WalletConnect AppKit) provides:
- Pre-built UI components for wallet connection
- WalletConnect v2 protocol support (QR code pairing for mobile wallets)
- EIP-6963 support (browser extension discovery)
- Wagmi adapter integration

**Configuration Options:**
- Network configuration (Ethereum, Optimism, Arbitrum, Base, Polygon, Avalanche, Celo, Ronin)
- Social login providers (Google, X, Discord, Farcaster, GitHub, Apple, Facebook)
- On-ramp toggle
- Token swap toggle
- Transaction history toggle
- Theme mode (light/dark) with custom CSS variables
- Web components (`<appkit-button />`, `<appkit-network-button />`)
- Coinbase preference options

## Installation

```bash
dotnet add package Nethereum.Reown.AppKit.Blazor
```

Or via Package Manager Console:

```powershell
Install-Package Nethereum.Reown.AppKit.Blazor
```

## Dependencies

**Package References:**
- Microsoft.Extensions.DependencyInjection.Abstractions 8.0.2
- Microsoft.Extensions.Options 8.0.2
- Microsoft.JSInterop 8.0.10

**Project References:**
- Nethereum.UI (provides `IEthereumHostProvider` abstraction)
- Nethereum.Web3 (Web3 API access)

**Target Framework:**
- net8.0

**External Dependencies:**
- Reown AppKit CDN: `@reown/appkit-cdn@1.4.1`
- Requires a Reown Project ID from https://cloud.reown.com/

## Quick Start

### 1. Get Reown Project ID

Sign up at https://cloud.reown.com/ and create a new project to obtain your Project ID.

### 2. Configure Services

In `Program.cs`:

```csharp
using Nethereum.Reown.AppKit.Blazor;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

var projectId = "YOUR-REOWN-PROJECT-ID";

builder.Services.AddAppKit(new AppKitConfiguration
{
    Networks = NetworkConstants.Networks.All,
    ProjectId = projectId,
    Name = "My Dapp",
    Description = "My decentralized application",
    Url = builder.HostEnvironment.BaseAddress,
    Icons = ["https://my-dapp.com/icon.png"],
    ThemeMode = ThemeModeOptions.light,
    Swaps = true,
    Onramp = true,
    Email = true
});

await builder.Build().RunAsync();
```

**From:** `consoletests/NethereumReownAppKitBlazor/Program.cs:20`

### 3. Use in Razor Components

```razor
@page "/"
@using Nethereum.Reown.AppKit.Blazor
@using Nethereum.UI
@inject IEthereumHostProvider ethereumHostProvider
@inject IAppKit appKit

<!-- Built-in AppKit UI components -->
<appkit-button />
<appkit-network-button />

@if (string.IsNullOrEmpty(Address))
{
    <button @onclick="ConnectWallet">Connect Wallet</button>
}
else
{
    <div>Connected: @Address</div>
    <div>Chain ID: @ChainId</div>
}

@code {
    private string? Address;
    private long ChainId;
    private IWeb3 web3 = default!;

    protected override async Task OnInitializedAsync()
    {
        // Subscribe to account changes
        ethereumHostProvider.SelectedAccountChanged += async (address) =>
        {
            Address = address;
            await InvokeAsync(StateHasChanged);
        };

        // Subscribe to network changes
        ethereumHostProvider.NetworkChanged += async (chainId) =>
        {
            ChainId = chainId;
            await InvokeAsync(StateHasChanged);
        };

        // Get Web3 instance with AppKit integration
        web3 = await ethereumHostProvider.GetWeb3Async();
    }

    private async Task ConnectWallet()
    {
        Address = await ethereumHostProvider.EnableProviderAsync();
    }
}
```

**From:** `consoletests/NethereumReownAppKitBlazor/Pages/Index.razor:15`

## Configuration

### AppKitConfiguration Properties

```csharp
public class AppKitConfiguration
{
    // Required
    public required IEnumerable<Network> Networks { get; init; }
    public required string ProjectId { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Url { get; init; }
    public required string[] Icons { get; init; }

    // Network
    public Network? DefaultNetwork { get; init; }

    // Theme
    public ThemeModeOptions? ThemeMode { get; set; } // dark, light, or null (system default)
    public ThemeVariables? ThemeVariables { get; init; }

    // Wallet Configuration
    public AllWalletsOptions AllWallets { get; init; } = AllWalletsOptions.SHOW; // SHOW, HIDE, ONLY_MOBILE
    public string[]? FeaturedWalletIds { get; init; }
    public string[]? IncludeWalletIds { get; init; }
    public string[]? ExcludeWalletIds { get; init; }

    // Coinbase
    public CoinbasePreferenceOptions CoinbasePreference { get; set; } = CoinbasePreferenceOptions.all;
    // Options: all, smartWalletOnly, eoaOnly

    // Legal
    public string? TermsConditionsUrl { get; init; }
    public string? PrivacyPolicyUrl { get; init; }
    public bool LegalCheckbox { get; init; } = false;

    // Provider Configuration
    public bool? DisableAppend { get; init; }
    public bool? EnableWallets { get; init; }
    public bool? EnableEIP6963 { get; init; }
    public bool? EnableCoinbase { get; init; }
    public bool? EnableInjected { get; init; }

    // Debug
    public bool Debug { get; init; } = false;

    // Features
    public bool Swaps { get; init; } = true;
    public bool Onramp { get; init; } = true;
    public bool Email { get; init; } = true;
    public bool EmailShowWallets { get; init; } = true;
    public HashSet<SocialOptions>? Socials { get; init; }
    public bool History { get; init; } = true;
    public bool Analytics { get; init; } = true;
}
```

**From:** `src/Nethereum.Reown.AppKit.Blazor/AppKitConfiguration.cs:37`

### Social Login Options

```csharp
public enum SocialOptions
{
    google,
    x,           // Twitter/X
    discord,
    farcaster,
    github,
    apple,
    facebook
}
```

**Default:** All 7 social options are enabled by default.

**From:** `src/Nethereum.Reown.AppKit.Blazor/AppKitConfiguration.cs:14`

### Theme Customization

```csharp
public struct ThemeVariables
{
    public string W3mFontFamily { get; init; }
    public string W3mAccent { get; init; }
    public string W3mColorMix { get; init; }
    public int W3mColorMixStrength { get; init; }
    public string W3mFontSizeMaster { get; init; }
    public string W3mBorderRadiusMaster { get; init; }
    public int W3mZIndex { get; init; }
}
```

**Example:**

```csharp
builder.Services.AddAppKit(new AppKitConfiguration
{
    // ... other config
    ThemeMode = ThemeModeOptions.dark,
    ThemeVariables = new ThemeVariables
    {
        W3mFontFamily = "Roboto, sans-serif",
        W3mAccent = "#FF6B00",
        W3mColorMix = "#FF6B00",
        W3mColorMixStrength = 20,
        W3mBorderRadiusMaster = "8px",
        W3mZIndex = 9999
    }
});
```

**From:** `src/Nethereum.Reown.AppKit.Blazor/AppKitConfiguration.cs:87`

## Network Configuration

### Pre-configured Networks

Nethereum.Reown.AppKit.Blazor includes 10 pre-configured networks:

```csharp
public static class NetworkConstants
{
    public static class Networks
    {
        public static readonly Network Ethereum;       // Chain ID: 1
        public static readonly Network Optimism;       // Chain ID: 10
        public static readonly Network Ronin;          // Chain ID: 2020
        public static readonly Network RoninSaigon;    // Chain ID: 2021 (testnet)
        public static readonly Network Base;           // Chain ID: 8453
        public static readonly Network Arbitrum;       // Chain ID: 42161
        public static readonly Network Celo;           // Chain ID: 42220
        public static readonly Network CeloAlfajores;  // Chain ID: 44787 (testnet)
        public static readonly Network Polygon;        // Chain ID: 137
        public static readonly Network Avalanche;      // Chain ID: 43114

        public static readonly IReadOnlyCollection<Network> All;
    }
}
```

**Usage:**

```csharp
// Use all networks
Networks = NetworkConstants.Networks.All

// Use specific networks
Networks = new[]
{
    NetworkConstants.Networks.Ethereum,
    NetworkConstants.Networks.Optimism,
    NetworkConstants.Networks.Base
}
```

**From:** `src/Nethereum.Reown.AppKit.Blazor/Network.cs:37`

### Custom Network Configuration

```csharp
public class Network
{
    public required long Id { get; init; }
    public required string Name { get; init; }
    public required Currency NativeCurrency { get; init; }
    public BlockExplorers? BlockExplorers { get; init; }
    public required RpcUrls RpcUrls { get; init; }
    public bool Testnet { get; init; }
}

public record Currency(string Name, string Symbol, int Decimals);
public record RpcUrls(RpcUrl Default);
public record RpcUrl(string[] Http, string? WebSocket);
public record BlockExplorers(BlockExplorer Default);
public record BlockExplorer(string Name, string Url);
```

**Example:**

```csharp
var customNetwork = new Network
{
    Id = 11155111,
    Name = "Sepolia",
    NativeCurrency = new Currency("Sepolia Ether", "ETH", 18),
    RpcUrls = new RpcUrls(new RpcUrl(
        ["https://rpc.sepolia.org"],
        "wss://rpc.sepolia.org"
    )),
    BlockExplorers = new BlockExplorers(new BlockExplorer(
        "Etherscan",
        "https://sepolia.etherscan.io"
    )),
    Testnet = true
};
```

**From:** `src/Nethereum.Reown.AppKit.Blazor/Network.cs:5`

## UI Components

### Built-in Web Components

Reown AppKit provides pre-built web components that can be used directly in Razor templates:

```razor
<!-- Wallet connection button -->
<appkit-button />

<!-- Network selector button -->
<appkit-network-button />
```

These components are automatically styled based on your theme configuration and handle all wallet connection UI interactions.

**From:** `consoletests/NethereumReownAppKitBlazor/Pages/Index.razor:21`

### IAppKit Interface

For programmatic control over the AppKit modal:

```csharp
@inject IAppKit appKit

@code {
    private void OpenModal()
    {
        appKit.Open();    // Open the wallet connection modal
    }

    private void CloseModal()
    {
        appKit.Close();   // Close the modal
    }

    private void DisconnectWallet()
    {
        appKit.Disconnect();  // Disconnect current wallet
    }
}
```

**From:** `src/Nethereum.Reown.AppKit.Blazor/IAppKit.cs:3`

## IEthereumHostProvider Integration

AppKit implements `IEthereumHostProvider`, enabling standard Nethereum integration patterns:

### Account and Network Events

```csharp
@inject IEthereumHostProvider ethereumHostProvider

@code {
    protected override async Task OnInitializedAsync()
    {
        // Subscribe to account changes
        ethereumHostProvider.SelectedAccountChanged += async (address) =>
        {
            Console.WriteLine($"Account changed: {address}");
            await InvokeAsync(StateHasChanged);
        };

        // Subscribe to network changes
        ethereumHostProvider.NetworkChanged += async (chainId) =>
        {
            Console.WriteLine($"Network changed: {chainId}");
            await InvokeAsync(StateHasChanged);
        };

        // Check availability
        bool available = await ethereumHostProvider.CheckProviderAvailabilityAsync();

        // Get current account
        string? account = await ethereumHostProvider.GetProviderSelectedAccountAsync();
    }
}
```

**From:** `consoletests/NethereumReownAppKitBlazor/Pages/Index.razor:56`

### Web3 Integration

```csharp
@code {
    private IWeb3 web3 = default!;

    protected override async Task OnInitializedAsync()
    {
        // Get Web3 instance with AppKit request interception
        web3 = await ethereumHostProvider.GetWeb3Async();

        // All RPC calls requiring wallet interaction are automatically routed through AppKit
        var blockNumber = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
    }
}
```

**From:** `src/Nethereum.Reown.AppKit.Blazor/AppKitHostProvider.cs:47`

## Examples

### Example 1: Sign Typed Data (EIP-712)

```csharp
@code {
    private async Task SignTypedDataAsync()
    {
        var typedData = new TypedData<Domain>
        {
            Domain = new Domain
            {
                Name = "Ether Mail",
                Version = "1",
                ChainId = ChainId,
                VerifyingContract = "0xCcCCccccCCCCcCCCCCCcCcCccCcCCCcCcccccccC"
            },
            Types = MemberDescriptionFactory.GetTypesMemberDescription(
                typeof(Domain), typeof(Mail), typeof(Person)
            ),
            PrimaryType = nameof(Mail)
        };

        var mail = new Mail
        {
            From = new Person { Name = "Alice", Wallets = ["0x1234..."] },
            To = [new Person { Name = "Bob", Wallets = ["0x5678..."] }],
            Contents = "Hello, Bob!"
        };

        typedData.SetMessage(mail);

        string signature = await web3.Eth.AccountSigning.SignTypedDataV4.SendRequestAsync(
            typedData.ToJson()
        );

        // Verify signature
        string recoveredAddress = new Eip712TypedDataSigner()
            .RecoverFromSignatureV4(typedData, signature);
    }
}
```

**From:** `consoletests/NethereumReownAppKitBlazor/Pages/Index.razor:76`

### Example 2: Personal Sign

```csharp
using Nethereum.Hex.HexTypes;

@code {
    private async Task PersonalSignAsync()
    {
        var message = new HexUTF8String("Hello World");
        string signature = await web3.Eth.AccountSigning.PersonalSign.SendRequestAsync(message);

        Console.WriteLine($"Signature: {signature}");
    }
}
```

**From:** `consoletests/NethereumReownAppKitBlazor/Pages/Index.razor:149`

### Example 3: Switch Network

```csharp
using Nethereum.RPC.HostWallet;
using Nethereum.Hex.HexTypes;

@code {
    private async Task SwitchToEthereumMainnet()
    {
        var response = await web3.Eth.HostWallet.SwitchEthereumChain.SendRequestAsync(
            new SwitchEthereumChainParameter
            {
                ChainId = 1.ToHexBigInteger()
            }
        );
    }
}
```

**From:** `consoletests/NethereumReownAppKitBlazor/Pages/Index.razor:116`

### Example 4: Add Custom Network

```csharp
using Nethereum.RPC.Chain;
using Nethereum.RPC.HostWallet;

@code {
    private async Task AddOptimismNetwork()
    {
        var chainFeature = ChainDefaultFeaturesServicesRepository.GetDefaultChainFeature(
            Nethereum.Signer.Chain.Optimism
        );

        var addParameter = chainFeature.ToAddEthereumChainParameter();

        string? response = await web3.Eth.HostWallet.AddEthereumChain.SendRequestAsync(
            addParameter
        );
    }
}
```

**From:** `consoletests/NethereumReownAppKitBlazor/Pages/Index.razor:164`

### Example 5: Get Block Number

```csharp
@code {
    private async Task GetBlockNumberAsync()
    {
        var blockNumber = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
        Console.WriteLine($"Current block: {blockNumber.Value}");
    }
}
```

**From:** `consoletests/NethereumReownAppKitBlazor/Pages/Index.razor:135`

## Architecture

### Request Interception

AppKit uses `AppKitInterceptor` to route Nethereum RPC requests through the Wagmi adapter:

```
┌─────────────────────────────────────────┐
│  Your Application                       │
│  (Nethereum Web3 API calls)             │
└─────────────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────┐
│  AppKitInterceptor                      │
│  (Routes requests to Wagmi)             │
└─────────────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────┐
│  Wagmi Core (via JavaScript Interop)    │
│  - sendTransaction                      │
│  - signMessage                          │
│  - signTypedData                        │
│  - call                                 │
│  - getBalance                           │
│  - estimateGas                          │
└─────────────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────┐
│  Connected Wallet Provider              │
│  (Browser extension, mobile wallet,     │
│   social login, etc.)                   │
└─────────────────────────────────────────┘
```

**Supported RPC Methods:**
- `eth_sendTransaction` → `WagmiCore.sendTransaction`
- `eth_signTypedData_v4` → `WagmiCore.signTypedData`
- `personal_sign` → `WagmiCore.signMessage`
- `eth_call` → `WagmiCore.call`
- `eth_getBalance` → `WagmiCore.getBalance`
- `eth_estimateGas` → `WagmiCore.estimateGas`
- `eth_chainId` → `WagmiCore.getChainId`
- `eth_getTransactionReceipt` → `WagmiCore.getTransactionReceipt`

**From:** `src/Nethereum.Reown.AppKit.Blazor/wwwroot/js/index.js:193`

### JavaScript Interop

The package uses JavaScript interop to communicate with the Reown AppKit CDN:

```javascript
import { createAppKit, WagmiAdapter, WagmiCore }
  from 'https://cdn.jsdelivr.net/npm/@reown/appkit-cdn@1.4.1/dist/appkit.min.js'
```

**Key JavaScript Functions:**
- `InitializeAsync(configJson)` - Initialize AppKit with configuration
- `EnableProviderAsync()` - Open modal and request wallet connection
- `WatchAccount(callback)` - Subscribe to account changes
- `WatchChainId(callback)` - Subscribe to network changes
- `GetAccount()` - Get current account state
- `SignMessageAsync(message)` - Sign personal message
- `SendTransactionAsync(id, method, params)` - Execute Wagmi method

**From:** `src/Nethereum.Reown.AppKit.Blazor/wwwroot/js/index.js:1`

## Blazor Server vs WebAssembly

This package supports both Blazor Server and Blazor WebAssembly:

**Blazor WebAssembly:**
```csharp
var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.Services.AddAppKit(config);
```

**Blazor Server:**
```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAppKit(config);
```

The JavaScript interop (`IJSRuntime`) works in both hosting models.

## Advanced Configuration

### Disable Email Login

```csharp
builder.Services.AddAppKit(new AppKitConfiguration
{
    // ... other config
    Email = false,
    Socials = new HashSet<SocialOptions> { SocialOptions.google, SocialOptions.github }
});
```

### Coinbase Smart Wallet Only

```csharp
builder.Services.AddAppKit(new AppKitConfiguration
{
    // ... other config
    CoinbasePreference = CoinbasePreferenceOptions.smartWalletOnly
});
```

### Hide All Wallets (Social Login Only)

```csharp
builder.Services.AddAppKit(new AppKitConfiguration
{
    // ... other config
    AllWallets = AllWalletsOptions.HIDE,
    Email = true,
    Socials = new HashSet<SocialOptions>
    {
        SocialOptions.google,
        SocialOptions.discord
    }
});
```

### Featured Wallets

```csharp
builder.Services.AddAppKit(new AppKitConfiguration
{
    // ... other config
    FeaturedWalletIds = new[]
    {
        "c57ca95b47569778a828d19178114f4db188b89b763c899ba0be274e97267d96", // MetaMask
        "4622a2b2d6af1c9844944291e5e7351a6aa24cd7b23099efac1b2fd875da31a0"  // Trust Wallet
    }
});
```

Wallet IDs can be found in the WalletConnect Explorer: https://walletconnect.com/explorer

### Legal Compliance

```csharp
builder.Services.AddAppKit(new AppKitConfiguration
{
    // ... other config
    TermsConditionsUrl = "https://my-dapp.com/terms",
    PrivacyPolicyUrl = "https://my-dapp.com/privacy",
    LegalCheckbox = true  // Require users to accept T&C before connecting
});
```

## Troubleshooting

### AppKit Modal Not Opening

**Issue:** Calling `EnableProviderAsync()` doesn't show the modal.

**Solution:** Ensure JavaScript interop is initialized:
```csharp
protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (firstRender)
    {
        await Task.Delay(100); // Wait for JS initialization
        await ethereumHostProvider.CheckProviderAvailabilityAsync();
    }
}
```

### Account Not Detected After Connection

**Issue:** `SelectedAccount` is null after connecting.

**Solution:** AppKit uses async account detection. Subscribe to `SelectedAccountChanged` event:
```csharp
ethereumHostProvider.SelectedAccountChanged += async (address) =>
{
    if (!string.IsNullOrEmpty(address))
    {
        // Account detected
    }
};
```

### RPC Calls Failing with "User Rejected"

**Issue:** User canceled the wallet transaction prompt.

**Solution:** Wrap calls in try-catch:
```csharp
try
{
    var signature = await web3.Eth.AccountSigning.PersonalSign.SendRequestAsync(message);
}
catch (Exception ex)
{
    if (ex.Message.Contains("User rejected"))
    {
        // Handle user rejection
    }
}
```

### Debug Mode

Enable debug logging to troubleshoot JavaScript interop issues:

```csharp
builder.Services.AddAppKit(new AppKitConfiguration
{
    // ... other config
    Debug = true  // Logs AppKit events to browser console
});
```

## Related Packages

- **Nethereum.UI** - `IEthereumHostProvider` abstraction
- **Nethereum.Web3** - Web3 API for Ethereum interactions
- **Nethereum.Blazor** - EIP-6963 multi-wallet support for Blazor
- **Nethereum.Metamask.Blazor** - MetaMask-specific integration
- **Nethereum.WalletConnect** - Low-level WalletConnect v2 protocol implementation

## Additional Resources

- [Reown AppKit Documentation](https://docs.reown.com/appkit/overview)
- [Get Reown Project ID](https://cloud.reown.com/)
- [Wagmi Documentation](https://wagmi.sh/)
- [WalletConnect Explorer](https://walletconnect.com/explorer) - Find wallet IDs
- [Nethereum Documentation](http://docs.nethereum.com)

## License

MIT License - see LICENSE file for details
