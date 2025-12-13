# Nethereum.Metamask.Blazor

Blazor implementation of MetaMask integration with JavaScript interop for wallet interactions and authentication.

## Overview

Nethereum.Metamask.Blazor provides the platform-specific implementation of IMetamaskInterop for Blazor applications. It bridges C# code with the MetaMask browser extension using JavaScript interop, enabling wallet connectivity, transaction signing, and authentication in Blazor apps.

**Key Features:**
- JavaScript interop implementation (MetamaskBlazorInterop) for MetaMask wallet communication
- Automatic event handling for account and network changes via callbacks
- Integration with Blazor authentication system through EthereumAuthenticationStateProvider
- Built-in JavaScript library (NethereumMetamask.js) for MetaMask extension interaction
- Support for all MetaMask RPC methods (transactions, signing, wallet operations)
- Singleton pattern with MetamaskHostProvider.Current for global access

## Installation

```bash
dotnet add package Nethereum.Metamask.Blazor
```

Or via Package Manager Console:

```powershell
Install-Package Nethereum.Metamask.Blazor
```

## Dependencies

**Package References:**
- Microsoft.AspNetCore.Components.Authorization 6.0.5
- Microsoft.AspNetCore.Components.WebAssembly 6.0.2

**Project References:**
- Nethereum.Blazor
- Nethereum.Metamask

## Architecture

### Component Stack

```
┌─────────────────────────────────────────┐
│   Blazor WebAssembly Application       │
│   (Your Components & Services)          │
└─────────────────────────────────────────┘
                  ↓
┌─────────────────────────────────────────┐
│   MetamaskBlazorInterop                 │
│   (IMetamaskInterop Implementation)     │
│   - IJSRuntime bridge                   │
│   - Event marshalling                   │
└─────────────────────────────────────────┘
                  ↓
┌─────────────────────────────────────────┐
│   NethereumMetamask.js                  │
│   (JavaScript Library)                  │
│   - window.NethereumMetamaskInterop     │
└─────────────────────────────────────────┘
                  ↓
┌─────────────────────────────────────────┐
│   MetaMask Browser Extension            │
│   (window.ethereum)                     │
└─────────────────────────────────────────┘
```

### Key Components

**MetamaskBlazorInterop**
- C# implementation of IMetamaskInterop using IJSRuntime
- Calls JavaScript functions via `_jsRuntime.InvokeAsync<T>()`
- Serializes/deserializes RPC messages using Newtonsoft.Json
- Provides [JSInvokable] callback methods for JavaScript events

**NethereumMetamask.js**
- JavaScript library providing `window.NethereumMetamaskInterop` object
- Wraps `window.ethereum` (MetaMask provider)
- Handles event listeners for accountsChanged and chainChanged
- Converts JavaScript callbacks to .NET method invocations using DotNet.invokeMethodAsync

**EthereumAuthenticationStateProvider** (from Nethereum.Blazor)
- Integrates wallet connection with Blazor authentication
- Creates ClaimsPrincipal with Ethereum address as NameIdentifier
- Adds "EthereumConnected" role when wallet is connected
- Automatically updates authentication state on account changes

## Quick Start

### 1. Add JavaScript Reference

In your `index.html` or `App.razor`, include the NethereumMetamask.js script:

```html
<script src="_content/Nethereum.Metamask.Blazor/NethereumMetamask.js"></script>
```

### 2. Configure Services

In `Program.cs`:

```csharp
using Nethereum.Metamask;
using Nethereum.Metamask.Blazor;
using Nethereum.UI;
using Microsoft.AspNetCore.Components.Authorization;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");

builder.Services.AddAuthorizationCore();
builder.Services.AddSingleton(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Register MetaMask services
builder.Services.AddSingleton<IMetamaskInterop, MetamaskBlazorInterop>();
builder.Services.AddSingleton<MetamaskHostProvider>();

// Add MetaMask as the selected Ethereum host provider
builder.Services.AddSingleton(services =>
{
    var metamaskHostProvider = services.GetService<MetamaskHostProvider>();
    var selectedHostProvider = new SelectedEthereumHostProviderService();
    selectedHostProvider.SetSelectedEthereumHostProvider(metamaskHostProvider);
    return selectedHostProvider;
});

await builder.Build().RunAsync();
```

### 3. Use in Components

```razor
@page "/wallet"
@inject MetamaskHostProvider MetamaskProvider
@inject AuthenticationStateProvider AuthStateProvider

<AuthorizeView>
    <Authorized>
        <p>Connected: @context.User.Identity.Name</p>
        <button @onclick="Disconnect">Disconnect</button>
    </Authorized>
    <NotAuthorizing>
        <button @onclick="Connect">Connect Wallet</button>
    </NotAuthorizing>
</AuthorizeView>

@code {
    private async Task Connect()
    {
        await MetamaskProvider.EnableProviderAsync();

        // Notify authentication system
        if (AuthStateProvider is EthereumAuthenticationStateProvider ethAuthProvider)
        {
            await ethAuthProvider.NotifyAuthenticationStateAsEthereumConnected();
        }
    }

    private async Task Disconnect()
    {
        // MetaMask doesn't support disconnect, but we can clear local state
        if (AuthStateProvider is EthereumAuthenticationStateProvider ethAuthProvider)
        {
            await ethAuthProvider.NotifyAuthenticationStateAsEthereumDisconnected();
        }
    }
}
```

## Usage Examples

### Example 1: Basic MetaMask Setup

```csharp
using Nethereum.Metamask;
using Nethereum.Metamask.Blazor;
using Microsoft.JSInterop;

public class MetamaskService
{
    private readonly MetamaskBlazorInterop _metamaskInterop;
    private readonly MetamaskHostProvider _hostProvider;

    public MetamaskService(IJSRuntime jsRuntime)
    {
        _metamaskInterop = new MetamaskBlazorInterop(jsRuntime);
        _hostProvider = new MetamaskHostProvider(_metamaskInterop);
    }

    public async Task<bool> CheckAvailabilityAsync()
    {
        return await _metamaskInterop.CheckMetamaskAvailability();
    }

    public async Task<string> ConnectAsync()
    {
        return await _hostProvider.EnableProviderAsync();
    }

    public async Task<IWeb3> GetWeb3Async()
    {
        return await _hostProvider.GetWeb3Async();
    }
}
```

### Example 2: Handling Account Changes

```razor
@page "/account-watcher"
@inject MetamaskHostProvider MetamaskProvider
@implements IDisposable

<h3>Current Account: @currentAccount</h3>
<p>Network Chain ID: @chainId</p>

@code {
    private string currentAccount = "Not connected";
    private long chainId = 0;

    protected override async Task OnInitializedAsync()
    {
        // Subscribe to events
        MetamaskProvider.SelectedAccountChanged += OnAccountChanged;
        MetamaskProvider.NetworkChanged += OnNetworkChanged;

        // Get current values
        if (MetamaskProvider.Available && MetamaskProvider.Enabled)
        {
            currentAccount = MetamaskProvider.SelectedAccount;
            chainId = MetamaskProvider.SelectedNetworkChainId;
        }
    }

    private async Task OnAccountChanged(string newAccount)
    {
        currentAccount = newAccount ?? "Not connected";
        StateHasChanged();
    }

    private async Task OnNetworkChanged(long newChainId)
    {
        chainId = newChainId;
        StateHasChanged();
    }

    public void Dispose()
    {
        MetamaskProvider.SelectedAccountChanged -= OnAccountChanged;
        MetamaskProvider.NetworkChanged -= OnNetworkChanged;
    }
}
```

### Example 3: Sending Transactions

```csharp
using Nethereum.Metamask;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;

public class TransactionService
{
    private readonly MetamaskHostProvider _provider;

    public TransactionService(MetamaskHostProvider provider)
    {
        _provider = provider;
    }

    public async Task<string> SendEtherAsync(string toAddress, decimal etherAmount)
    {
        var web3 = await _provider.GetWeb3Async();
        var fromAddress = _provider.SelectedAccount;

        var transactionInput = new TransactionInput
        {
            From = fromAddress,
            To = toAddress,
            Value = new HexBigInteger(Web3.Web3.Convert.ToWei(etherAmount))
        };

        // This will prompt MetaMask for user confirmation
        var txHash = await web3.Eth.TransactionManager.SendTransactionAsync(transactionInput);
        return txHash;
    }
}
```

### Example 4: Message Signing with MetaMask

```csharp
using Nethereum.Metamask;
using Nethereum.Signer;
using Nethereum.Util;

public class SigningService
{
    private readonly MetamaskHostProvider _provider;

    public SigningService(MetamaskHostProvider provider)
    {
        _provider = provider;
    }

    public async Task<string> SignMessageAsync(string message)
    {
        // Sign message through MetaMask (personal_sign)
        var signature = await _provider.SignMessageAsync(message);
        return signature;
    }

    public async Task<bool> VerifySignatureAsync(string message, string signature, string expectedAddress)
    {
        var signer = new EthereumMessageSigner();
        var recoveredAddress = signer.EncodeUTF8AndEcRecover(message, signature);

        return recoveredAddress.Equals(expectedAddress, StringComparison.OrdinalIgnoreCase);
    }
}
```

### Example 5: Authentication with Custom Claims

```csharp
using Microsoft.AspNetCore.Components.Authorization;
using Nethereum.Blazor;
using Nethereum.UI;
using System.Security.Claims;

public class CustomEthereumAuthStateProvider : EthereumAuthenticationStateProvider
{
    public CustomEthereumAuthStateProvider(SelectedEthereumHostProviderService selectedHostProviderService)
        : base(selectedHostProviderService)
    {
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var state = await base.GetAuthenticationStateAsync();

        if (state.User.Identity.IsAuthenticated)
        {
            var address = state.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            // Add custom claims based on address
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, address),
                new Claim(ClaimTypes.Role, "EthereumConnected"),
                new Claim("ChainId", EthereumHostProvider.SelectedNetworkChainId.ToString())
            };

            // Add admin role for specific addresses
            if (address == "0xYourAdminAddress")
            {
                claims.Add(new Claim(ClaimTypes.Role, "Admin"));
            }

            var identity = new ClaimsIdentity(claims, "ethereumConnection");
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }

        return state;
    }
}
```

### Example 6: Direct JavaScript Interop Usage

```csharp
using Nethereum.Metamask.Blazor;
using Nethereum.JsonRpc.Client.RpcMessages;
using Microsoft.JSInterop;

public class DirectInteropService
{
    private readonly MetamaskBlazorInterop _interop;

    public DirectInteropService(IJSRuntime jsRuntime)
    {
        _interop = new MetamaskBlazorInterop(jsRuntime);
    }

    public async Task<string> GetChainIdAsync()
    {
        var request = new RpcRequestMessage(1, "eth_chainId");
        var response = await _interop.SendAsync(request);

        if (response.HasError)
        {
            throw new Exception($"RPC Error: {response.Error.Message}");
        }

        return response.Result.ToString();
    }

    public async Task<string[]> GetAccountsAsync()
    {
        var selectedAddress = await _interop.GetSelectedAddress();
        return new[] { selectedAddress };
    }

    public async Task<string> EnableAsync()
    {
        return await _interop.EnableEthereumAsync();
    }
}
```

### Example 7: Comprehensive MetaMask Integration Component

Full-featured component demonstrating authentication, signing, chain switching, and blockchain queries.

```razor
@page "/"
@using Nethereum.ABI.EIP712
@using Nethereum.ABI.FunctionEncoding.Attributes
@implements IDisposable
@inject IJSRuntime jsRuntime
@inject SelectedEthereumHostProviderService selectedHostProviderService
@inject NavigationManager _navigationManager
@using Nethereum.Hex.HexTypes
@using Microsoft.AspNetCore.Components.Authorization
@using System.Security.Claims
@using Nethereum.RPC.HostWallet
@using Nethereum.Signer
@using Nethereum.Signer.EIP712

<AuthorizeView Roles="EthereumConnected">
    <Authorized>
        <div class="card m-1">
            <div class="card-body">
                <div class="row">
                    <label class="col-sm-3 col-form-label-lg">Selected Account:</label>
                    <div class="col-sm-6">
                        @SelectedAccount
                        <small class="form-text text-muted">The selected account is bound to the host (ie Metamask) on change</small>
                    </div>
                </div>
                <div class="row">
                    <label class="col-sm-3 col-form-label-lg">Selected Account from Claims Principal</label>
                    <div class="col-sm-6">
                        @context?.User?.FindFirst(c => c.Type.Contains(ClaimTypes.NameIdentifier))?.Value
                        <small class="form-text text-muted">The selected account is bound to the claims principal</small>
                    </div>
                </div>
            </div>

            <div class="card-body">
                <div class="row">
                    <label class="col-sm-3 col-form-label-lg">Selected Network ChainId:</label>
                    <div class="col-sm-6">
                        @SelectedChainId
                        <small class="form-text text-muted">The selected chain Id</small>
                    </div>
                </div>
            </div>
        </div>

        <div class="card m-1">
            <div class="card-body">
                <div class="row">
                    <label class="col-sm-3 col-form-label-lg">Block hash of block number 0:</label>
                    <div class="col-sm-6">
                        <button @onclick="@GetBlockHashAsync">Get BlockHash</button>
                        <div>@BlockHash</div>
                        <small class="form-text text-muted">With Metamask calls are redirected to its configured node (i.e http://localhost:8545)</small>
                    </div>
                </div>
            </div>
        </div>

        <div class="card m-1">
            <div class="card-body">
                <div class="row">
                    <label class="col-sm-3 col-form-label-lg">Sign Typed Data V4:</label>
                    <div class="col-sm-6">
                        <button @onclick="@SignV4">Sign</button>
                        <div>@RecoveredAccount</div>
                        <small class="form-text text-muted">Converts Typed data to Json and sends it to Metamask to sign it, then uses Nethereum to recover the address</small>
                    </div>
                </div>
            </div>
        </div>

        <div class="card m-1">
            <div class="card-body">
                <div class="row">
                    <label class="col-sm-3 col-form-label-lg">Change Chain</label>
                    <div class="col-sm-6">
                        <button @onclick="@ChangeChainToMainnet">Change Chain To Mainnet</button>
                        <small class="form-text text-muted">Changes the chain to Mainnet</small>
                    </div>
                </div>
            </div>
        </div>

        <div class="card m-1">
            <div class="card-body">
                <div class="row">
                    <label class="col-sm-3 col-form-label-lg">Sign Message</label>
                    <div class="col-sm-6">
                        <button @onclick="@SignAMessage">Sign</button>
                        <div>@RecoveredAccountMessage</div>
                        <small class="form-text text-muted">Signs using personal_sign and recovers using Nethereum</small>
                    </div>
                </div>
            </div>
        </div>

        <div class="card m-1">
            <div class="card-body">
                <div class="row">
                    <label class="col-sm-3 col-form-label-lg">Add Chain</label>
                    <div class="col-sm-6">
                        <button @onclick="@AddChain">Add Chain (Optimism)</button>
                        <small class="form-text text-muted">Adds a new chain (Optimism) to Metamask</small>
                    </div>
                </div>
            </div>
        </div>
    </Authorized>
    <NotAuthorized>
        <div>
            Please connect to Ethereum!
        </div>
    </NotAuthorized>
</AuthorizeView>

@code {
    [CascadingParameter]
    public Task<AuthenticationState> AuthenticationState { get; set; }

    bool EthereumAvailable { get; set; }
    string SelectedAccount { get; set; }
    long SelectedChainId { get; set; }
    string BlockHash { get; set; }
    string RecoveredAccount { get; set; }
    string RecoveredAccountMessage { get; set; }
    IEthereumHostProvider _ethereumHostProvider;

    protected override void OnInitialized()
    {
        _ethereumHostProvider = selectedHostProviderService.SelectedHost;
        _ethereumHostProvider.SelectedAccountChanged += HostProvider_SelectedAccountChanged;
        _ethereumHostProvider.NetworkChanged += HostProvider_NetworkChanged;
        _ethereumHostProvider.EnabledChanged += HostProviderOnEnabledChanged;
    }

    public void Dispose()
    {
        _ethereumHostProvider.SelectedAccountChanged -= HostProvider_SelectedAccountChanged;
        _ethereumHostProvider.NetworkChanged -= HostProvider_NetworkChanged;
        _ethereumHostProvider.EnabledChanged -= HostProviderOnEnabledChanged;
    }

    protected override async Task OnInitializedAsync()
    {
        EthereumAvailable = await _ethereumHostProvider.CheckProviderAvailabilityAsync();
        if (EthereumAvailable)
        {
            SelectedAccount = await _ethereumHostProvider.GetProviderSelectedAccountAsync();
            await GetChainId();
        }
    }

    private async Task HostProviderOnEnabledChanged(bool enabled)
    {
        if (enabled)
        {
            await GetChainId();
            this.StateHasChanged();
        }
    }

    private async Task GetChainId()
    {
        var web3 = await _ethereumHostProvider.GetWeb3Async();
        var chainId = await web3.Eth.ChainId.SendRequestAsync();
        SelectedChainId = (long)chainId.Value;
    }

    private async Task HostProvider_SelectedAccountChanged(string account)
    {
        SelectedAccount = account;
        await GetChainId();
        this.StateHasChanged();
    }

    private async Task HostProvider_NetworkChanged(long chainId)
    {
        SelectedChainId = chainId;
        this.StateHasChanged();
    }

    protected async Task GetBlockHashAsync()
    {
        var web3 = await _ethereumHostProvider.GetWeb3Async();
        var block = await web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(new HexBigInteger(1));
        BlockHash = block.BlockHash;
    }

    protected async Task SignAMessage()
    {
        RecoveredAccountMessage = "";
        var web3 = await _ethereumHostProvider.GetWeb3Async();
        var signature = await web3.Eth.AccountSigning.PersonalSign.SendRequestAsync(new HexUTF8String("Hello"));
        RecoveredAccountMessage = new EthereumMessageSigner().EncodeUTF8AndEcRecover("Hello", signature);
    }

    protected async Task AddChain()
    {
        var web3 = await _ethereumHostProvider.GetWeb3Async();
        var optimismChain = new AddEthereumChainParameter()
        {
            ChainId = new HexBigInteger(10),
            ChainName = "Optimism",
            NativeCurrency = new NativeCurrency()
            {
                Decimals = 18,
                Name = "ETH",
                Symbol = "ETH"
            },
            RpcUrls = new List<string> { "https://mainnet.optimism.io", "https://rpc.ankr.com/optimism" },
            BlockExplorerUrls = new List<string> { "https://optimistic.etherscan.io/" },
        };
        try
        {
            var result = await web3.Eth.HostWallet.AddEthereumChain.SendRequestAsync(optimismChain);
        }
        catch (Exception ex)
        {
            // Handle error
        }
    }

    protected async Task ChangeChainToMainnet()
    {
        try
        {
            var web3 = await _ethereumHostProvider.GetWeb3Async();
            var result = await web3.Eth.HostWallet.SwitchEthereumChain.SendRequestAsync(
                new SwitchEthereumChainParameter() { ChainId = new HexBigInteger(1) });
        }
        catch (Exception ex)
        {
            // Handle error
        }
    }

    protected async Task SignV4()
    {
        RecoveredAccount = "";
        var web3 = await _ethereumHostProvider.GetWeb3Async();
        var chainId = await web3.Eth.ChainId.SendRequestAsync();
        if (chainId.Value == 1)
        {
            var typedData = GetMailTypedDefinition();
            var mail = new Mail
            {
                From = new Person
                {
                    Name = "Cow",
                    Wallets = new List<string> { "0xCD2a3d9F938E13CD947Ec05AbC7FE734Df8DD826", "0xDeaDbeefdEAdbeefdEadbEEFdeadbeEFdEaDbeeF" }
                },
                To = new List<Person>
                {
                    new Person
                    {
                        Name = "Bob",
                        Wallets = new List<string> { "0xbBbBBBBbbBBBbbbBbbBbbbbBBbBbbbbBbBbbBBbB", "0xB0BdaBea57B0BDABeA57b0bdABEA57b0BDabEa57", "0xB0B0b0b0b0b0B000000000000000000000000000" }
                    }
                },
                Contents = "Hello, Bob!"
            };

            typedData.Domain.ChainId = 1;
            typedData.SetMessage(mail);

            var signature = await web3.Eth.AccountSigning.SignTypedDataV4.SendRequestAsync(typedData.ToJson());
            RecoveredAccount = new Eip712TypedDataSigner().RecoverFromSignatureV4(typedData, signature);
        }
        else
        {
            RecoveredAccount = "Chain Id is not 1, please change your chain to mainnet";
        }
    }

    public TypedData<Domain> GetMailTypedDefinition()
    {
        return new TypedData<Domain>
        {
            Domain = new Domain
            {
                Name = "Ether Mail",
                Version = "1",
                ChainId = 1,
                VerifyingContract = "0xCcCCccccCCCCcCCCCCCcCcCccCcCCCcCcccccccC"
            },
            Types = MemberDescriptionFactory.GetTypesMemberDescription(typeof(Domain), typeof(Group), typeof(Mail), typeof(Person)),
            PrimaryType = nameof(Mail),
        };
    }
}
```

## API Reference

### MetamaskBlazorInterop

Blazor WebAssembly implementation of IMetamaskInterop using IJSRuntime.

```csharp
public class MetamaskBlazorInterop : IMetamaskInterop
{
    // Constructor
    public MetamaskBlazorInterop(IJSRuntime jsRuntime);

    // Properties
    public JsonSerializerSettings JsonSerializerSettings { get; set; }

    // IMetamaskInterop Implementation
    public ValueTask<string> EnableEthereumAsync();
    public ValueTask<bool> CheckMetamaskAvailability();
    public ValueTask<RpcResponseMessage> SendAsync(RpcRequestMessage rpcRequestMessage);
    public ValueTask<RpcResponseMessage> SendTransactionAsync(MetamaskRpcRequestMessage rpcRequestMessage);
    public ValueTask<string> SignAsync(string utf8Hex);
    public ValueTask<string> GetSelectedAddress();

    // JavaScript Invokable Callbacks
    [JSInvokable]
    public static Task MetamaskAvailableChanged(bool available);

    [JSInvokable]
    public static Task SelectedAccountChanged(string selectedAccount);

    [JSInvokable]
    public static Task SelectedNetworkChanged(string chainId);
}
```

### JavaScript API (window.NethereumMetamaskInterop)

The JavaScript library exposes the following functions:

```javascript
window.NethereumMetamaskInterop = {
    // Enable MetaMask and request account access
    EnableEthereum: async () => Promise<string>

    // Check if MetaMask is available
    IsMetamaskAvailable: () => boolean

    // Get current accounts
    GetAddresses: async () => Promise<string>  // JSON-serialized RpcResponseMessage

    // Send generic RPC request
    Request: async (message: string) => Promise<string>  // JSON-serialized RPC messages

    // Sign message with personal_sign
    Sign: async (utf8HexMsg: string) => Promise<string>  // JSON-serialized RpcResponseMessage
}
```

### Event Flow

1. **MetaMask Extension Events** → JavaScript event listeners
2. **JavaScript** → Calls .NET via `DotNet.invokeMethodAsync('Nethereum.Metamask.Blazor', methodName, args)`
3. **[JSInvokable] Methods** → Update MetamaskHostProvider.Current state
4. **MetamaskHostProvider** → Raises C# events (SelectedAccountChanged, NetworkChanged)
5. **Your Application** → Subscribes to C# events and updates UI

## Important Notes

### Script Reference Required

You MUST include the JavaScript library in your HTML:

```html
<script src="_content/Nethereum.Metamask.Blazor/NethereumMetamask.js"></script>
```

Without this script, IJSRuntime calls will fail with "function not found" errors.

### Singleton Pattern

MetamaskHostProvider.Current is a static singleton accessed by JavaScript callbacks:

```csharp
[JSInvokable]
public static async Task SelectedAccountChanged(string selectedAccount)
{
    await MetamaskHostProvider.Current.ChangeSelectedAccountAsync(selectedAccount);
}
```

You should register MetamaskHostProvider as a singleton in DI to ensure the same instance is used.

### JSON Serialization

Uses Newtonsoft.Json for all serialization with DefaultJsonSerializerSettings:

```csharp
JsonSerializerSettings = DefaultJsonSerializerSettingsFactory.BuildDefaultJsonSerializerSettings();
```

This ensures compatibility with Nethereum's RPC message formats.

### Error Handling

All RPC errors are converted to RpcResponseException:

```csharp
if (response.HasError)
{
    throw new RpcResponseException(
        new RpcError(response.Error.Code, response.Error.Message, response.Error.Data)
    );
}
```

Wrap await calls in try-catch to handle user rejections and RPC errors.

### MetaMask Disconnect

MetaMask does not provide a programmatic disconnect method. Users must disconnect manually through the MetaMask extension UI. You can clear local authentication state but cannot revoke MetaMask's connection.

### Browser Support

Requires a browser with:
- MetaMask extension installed
- Access to window.ethereum provider

Works with both Blazor WebAssembly and Blazor Server. The JavaScript interop communicates with the MetaMask extension running in the user's browser.

### Authentication Integration

EthereumAuthenticationStateProvider (from Nethereum.Blazor) creates:
- **ClaimTypes.NameIdentifier**: Ethereum address
- **ClaimTypes.Role**: "EthereumConnected"

Use `[Authorize(Roles = "EthereumConnected")]` to protect components.

## Related Packages

### Dependencies
- **Nethereum.Metamask** - Core abstraction interfaces
- **Nethereum.Blazor** - EthereumAuthenticationStateProvider and common Blazor utilities

### Used By
- Applications using Blazor WebAssembly with MetaMask wallet integration

### Similar Packages
- **Nethereum.WalletConnect** - WalletConnect protocol implementation
- **Nethereum.EIP6963WalletInterop** - EIP-6963 multi-wallet discovery
