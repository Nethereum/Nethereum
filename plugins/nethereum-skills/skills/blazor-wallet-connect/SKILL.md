---
name: blazor-wallet-connect
description: Connect browser wallets in Blazor using EIP-6963, MetaMask, or WalletConnect/Reown (.NET/C#). Use this skill when the user asks about connecting wallets in Blazor, EIP-6963, MetaMask Blazor integration, WalletConnect, Reown AppKit, or browser wallet discovery in a Blazor web app.
user-invocable: true
---

# Blazor Wallet Connection

Connect browser wallets in Blazor using EIP-6963 multi-wallet discovery (recommended), MetaMask, or WalletConnect/Reown.

## Option 1: EIP-6963 Multi-Wallet Discovery (Recommended)

EIP-6963 discovers all installed browser wallets. Users choose which to connect. This is the modern replacement for `window.ethereum`.

NuGet: `Nethereum.Blazor`

### Setup

Add the JS interop script to your `index.html` or `_Host.cshtml`:

```html
<script src="_content/Nethereum.Blazor/NethereumEIP6963.js"></script>
```

Register services:

```csharp
builder.Services.AddSingleton<IEthereumHostProvider, EIP6963HostProvider>();
builder.Services.AddSingleton<AuthenticationStateProvider, EthereumAuthenticationStateProvider>();
```

### Use the Component

```razor
@using Nethereum.Blazor
@inject IEthereumHostProvider EthereumHostProvider

<EIP6963Wallet OnWalletConnected="HandleWalletConnected" />

@code {
    private async Task HandleWalletConnected(string address)
    {
        var web3 = await EthereumHostProvider.GetWeb3Async();
        var balance = await web3.Eth.GetBalance.SendRequestAsync(address);
    }
}
```

## Option 2: MetaMask Direct

NuGet: `Nethereum.Metamask.Blazor`

### Setup (Program.cs)

```csharp
builder.Services.AddSingleton<IMetamaskInterop, MetamaskBlazorInterop>();
builder.Services.AddSingleton<MetamaskHostProvider>();
builder.Services.AddSingleton(services =>
{
    var metamaskHostProvider = services.GetService<MetamaskHostProvider>();
    var selectedHostProvider = new SelectedEthereumHostProviderService();
    selectedHostProvider.SetSelectedEthereumHostProvider(metamaskHostProvider);
    return selectedHostProvider;
});
builder.Services.AddSingleton<AuthenticationStateProvider, EthereumAuthenticationStateProvider>();
```

### Connect and Use

```razor
@inject MetamaskHostProvider MetamaskHostProvider

<button @onclick="ConnectAsync">Connect MetaMask</button>
<p>Address: @Address</p>

@code {
    private string? Address;

    protected override Task OnInitializedAsync()
    {
        MetamaskHostProvider.SelectedAccountChanged += async (address) =>
        {
            Address = address;
            await InvokeAsync(StateHasChanged);
        };
        return Task.CompletedTask;
    }

    private async Task ConnectAsync()
    {
        await MetamaskHostProvider.EnableProviderAsync();
        Address = await MetamaskHostProvider.GetProviderSelectedAccountAsync();
        var web3 = await MetamaskHostProvider.GetWeb3Async();
    }
}
```

Full example: `consoletests/MetamaskExampleBlazor.Wasm/`

## Option 3: WalletConnect / Reown AppKit

NuGet: `Nethereum.Reown.AppKit.Blazor`

### Setup (Program.cs)

```csharp
builder.Services.AddAppKit(new()
{
    ProjectId = "YOUR_REOWN_PROJECT_ID",
    Name = "My dApp",
    Networks = NetworkConstants.Networks.All,
    Url = builder.HostEnvironment.BaseAddress,
});
```

Get a project ID from https://cloud.reown.com/sign-in

### Use the AppKit Button

```razor
@using Nethereum.Reown.AppKit.Blazor
@inject IEthereumHostProvider EthereumHostProvider

<appkit-button />
<appkit-network-button />

@if (!string.IsNullOrEmpty(Address))
{
    <p>Connected: @Address on chain @ChainId</p>
}

@code {
    private string? Address;
    private long? ChainId;

    protected override async Task OnInitializedAsync()
    {
        EthereumHostProvider.SelectedAccountChanged += async (address) =>
        {
            Address = address;
            await InvokeAsync(StateHasChanged);
        };

        EthereumHostProvider.NetworkChanged += async (chainId) =>
        {
            ChainId = chainId;
            await InvokeAsync(StateHasChanged);
        };
    }
}
```

Full example: `consoletests/NethereumReownAppKitBlazor/`

## Getting Web3 After Connection

All providers implement `IEthereumHostProvider`. Once connected:

```csharp
var web3 = await ethereumHostProvider.GetWeb3Async();
var blockNumber = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
var balance = await web3.Eth.GetBalance.SendRequestAsync(address);
```

## Event Handling

All providers raise events for account and network changes:

```csharp
ethereumHostProvider.SelectedAccountChanged += async (address) => { ... };
ethereumHostProvider.NetworkChanged += async (chainId) => { ... };
ethereumHostProvider.EnabledChanged += async (enabled) => { ... };
```

## Switching and Adding Chains

```csharp
await web3.Eth.HostWallet.SwitchEthereumChain.SendRequestAsync(
    new SwitchEthereumChainParameter { ChainId = 10.ToHexBigInteger() });

var chainFeature = ChainDefaultFeaturesServicesRepository.GetDefaultChainFeature(Chain.Optimism);
await web3.Eth.HostWallet.AddEthereumChain.SendRequestAsync(chainFeature.ToAddEthereumChainParameter());
```

## AuthorizeView Integration

All providers work with Blazor's `<AuthorizeView>`:

```razor
<AuthorizeView Roles="EthereumConnected">
    <Authorized>
        <p>Connected as @context.User.Identity?.Name</p>
    </Authorized>
    <NotAuthorized>
        <button @onclick="ConnectAsync">Connect Wallet</button>
    </NotAuthorized>
</AuthorizeView>
```

For full SIWE authentication with JWT tokens, see the `blazor-authentication` skill.

For full documentation, see: https://docs.nethereum.com/docs/blazor-dapp-integration/overview
