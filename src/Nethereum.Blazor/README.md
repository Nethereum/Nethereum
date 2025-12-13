# Nethereum.Blazor

Blazor integration for Ethereum wallets with EIP-6963 multi-wallet discovery, authentication, and local storage utilities.

## Overview

Nethereum.Blazor provides essential infrastructure for building Blazor applications with Ethereum wallet integration. It includes authentication state providers, EIP-6963 wallet discovery for multi-wallet support, SIWE (Sign-In with Ethereum) authentication, and browser storage utilities.

**Key Features:**
- EthereumAuthenticationStateProvider for wallet-based authentication with Claims
- EIP-6963 wallet discovery and selection (MetaMask, Coinbase Wallet, Rabby, etc.)
- JavaScript interop for EIP-6963 protocol (NethereumEIP6963.js)
- SiweAuthenticationServerStateProvider for server-validated SIWE authentication
- LocalStorage utilities for token and data persistence
- Integration with ASP.NET Core authentication and authorization
- Automatic account and network change detection
- Support for multiple wallet providers simultaneously

## Installation

```bash
dotnet add package Nethereum.Blazor
```

Or via Package Manager Console:

```powershell
Install-Package Nethereum.Blazor
```

## Dependencies

**Package References:**
- Microsoft.AspNetCore.Components.Authorization 6.0.5
- Microsoft.AspNetCore.Components.WebAssembly 6.0.2

**Project References:**
- Nethereum.EIP6963WalletInterop
- Nethereum.Metamask

## Key Concepts

### EthereumAuthenticationStateProvider

Base authentication provider that integrates Ethereum wallet connection with Blazor's authentication system. When a wallet connects, it creates a ClaimsPrincipal with the Ethereum address as the NameIdentifier and adds the "EthereumConnected" role.

### EIP-6963 Wallet Discovery

EIP-6963 is a standard that allows web applications to discover multiple wallet extensions installed in the browser. Instead of hardcoding support for specific wallets, your app can dynamically detect and allow users to choose from all available EIP-6963 compatible wallets.

### SiweAuthenticationServerStateProvider

Advanced authentication provider that uses SIWE (Sign-In with Ethereum) protocol for server-validated authentication. It signs a message with the wallet, validates it server-side, and stores a JWT token in localStorage.

### LocalStorage Integration

Provides JavaScript interop utilities for storing authentication tokens, user preferences, and other data in the browser's localStorage.

## Quick Start

### 1. Include JavaScript Reference

In your `index.html`:

```html
<script src="_content/Nethereum.Blazor/NethereumEIP6963.js"></script>
```

### 2. Configure Services (Basic Wallet Authentication)

In `Program.cs`:

```csharp
using Nethereum.Blazor;
using Nethereum.Blazor.EIP6963WalletInterop;
using Nethereum.EIP6963WalletInterop;
using Nethereum.UI;
using Microsoft.AspNetCore.Components.Authorization;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Register EIP-6963 wallet interop
builder.Services.AddSingleton<IEIP6963WalletInterop, EIP6963WalletBlazorInterop>();
builder.Services.AddSingleton<EIP6963WalletHostProvider>();

// Register as IEthereumHostProvider
builder.Services.AddSingleton<IEthereumHostProvider>(sp =>
    sp.GetRequiredService<EIP6963WalletHostProvider>());

// Register authentication
builder.Services.AddSingleton<SelectedEthereumHostProviderService>();
builder.Services.AddScoped<AuthenticationStateProvider, EthereumAuthenticationStateProvider>();
builder.Services.AddAuthorizationCore();

await builder.Build().RunAsync();
```

### 3. Use in Blazor Components

```razor
@page "/wallet"
@inject EIP6963WalletHostProvider WalletProvider
@inject AuthenticationStateProvider AuthStateProvider

<AuthorizeView>
    <Authorized>
        <p>Connected: @context.User.Identity.Name</p>
        <p>Network: Chain ID @WalletProvider.SelectedNetworkChainId</p>
    </Authorized>
    <NotAuthorized>
        <button @onclick="ConnectWallet">Connect Wallet</button>
    </NotAuthorized>
</AuthorizeView>

@code {
    private async Task ConnectWallet()
    {
        // Discover and select a wallet (opens wallet selector)
        var wallets = await WalletProvider.GetAvailableWalletsAsync();

        // For this example, select first available wallet
        if (wallets.Length > 0)
        {
            await WalletProvider.SelectWalletAsync(wallets[0].Uuid);
            await WalletProvider.EnableProviderAsync();

            // Notify authentication system
            if (AuthStateProvider is EthereumAuthenticationStateProvider ethAuth)
            {
                await ethAuth.NotifyAuthenticationStateAsEthereumConnected();
            }
        }
    }
}
```

## Usage Examples

### Example 1: EIP-6963 Wallet Discovery and Selection

```razor
@page "/wallet-selector"
@inject EIP6963WalletHostProvider WalletProvider
@inject IEIP6963WalletInterop WalletInterop

<h3>Select Your Wallet</h3>

@if (availableWallets == null)
{
    <p>Loading wallets...</p>
}
else if (availableWallets.Length == 0)
{
    <div class="alert alert-warning">
        <p>No EIP-6963 compatible wallets detected.</p>
        <p>Please install MetaMask, Coinbase Wallet, Rabby, or another compatible wallet.</p>
    </div>
}
else
{
    <div class="wallet-grid">
        @foreach (var wallet in availableWallets)
        {
            <div class="wallet-card" @onclick="() => SelectWallet(wallet)">
                @if (!string.IsNullOrEmpty(wallet.Icon))
                {
                    <img src="@wallet.Icon" alt="@wallet.Name" class="wallet-icon" />
                }
                <h4>@wallet.Name</h4>
                <p class="text-muted">@wallet.Rdns</p>
            </div>
        }
    </div>
}

@if (isConnected)
{
    <div class="alert alert-success mt-3">
        Connected to @selectedWalletName
    </div>
}

@code {
    private EIP6963WalletInfo[] availableWallets;
    private bool isConnected = false;
    private string selectedWalletName;

    protected override async Task OnInitializedAsync()
    {
        // Give browser time to announce wallets
        await Task.Delay(500);
        availableWallets = await WalletInterop.GetAvailableWalletsAsync();
    }

    private async Task SelectWallet(EIP6963WalletInfo wallet)
    {
        try
        {
            await WalletInterop.SelectWalletAsync(wallet.Uuid);
            var address = await WalletProvider.EnableProviderAsync();

            if (!string.IsNullOrEmpty(address))
            {
                isConnected = true;
                selectedWalletName = wallet.Name;
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error selecting wallet: {ex.Message}");
        }
    }
}
```

### Example 2: SIWE Server-Side Authentication

```csharp
// Program.cs - SIWE Authentication Setup
using Nethereum.Blazor.Siwe;
using Nethereum.Siwe;
using Nethereum.Siwe.Authentication;
using Nethereum.Siwe.Core;
using Nethereum.UI;
using Microsoft.AspNetCore.Components.Authorization;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Register SIWE services
builder.Services.AddSingleton<NethereumSiweAuthenticatorService>();
builder.Services.AddSingleton<IAccessTokenService, LocalStorageAccessTokenService>();
builder.Services.AddSingleton<ISessionStorage, SessionStorageService>();

// Register user service (implement IUserService<User>)
builder.Services.AddSingleton<IUserService<User>, MyUserService>();

// Register SIWE authentication provider
builder.Services.AddScoped<AuthenticationStateProvider, SiweAuthenticationServerStateProvider<User, SiweMessage>>();
builder.Services.AddAuthorizationCore();

await builder.Build().RunAsync();
```

```razor
@page "/siwe-login"
@inject AuthenticationStateProvider AuthStateProvider
@inject IEthereumHostProvider EthereumProvider

<AuthorizeView Roles="SiweAuthenticated">
    <Authorized>
        <h3>Welcome, @context.User.Identity.Name!</h3>
        <p>Ethereum Address: @context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value</p>
        <button @onclick="LogOut">Log Out</button>
    </Authorized>
    <NotAuthorized>
        <button @onclick="SignIn">Sign In with Ethereum</button>
    </NotAuthorized>
</AuthorizeView>

@code {
    private async Task SignIn()
    {
        if (AuthStateProvider is SiweAuthenticationServerStateProvider<User, SiweMessage> siweAuth)
        {
            try
            {
                // Connect wallet if not connected
                if (!EthereumProvider.Enabled)
                {
                    await EthereumProvider.EnableProviderAsync();
                }

                // Authenticate with SIWE (prompts wallet to sign message)
                await siweAuth.AuthenticateAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Authentication failed: {ex.Message}");
            }
        }
    }

    private async Task LogOut()
    {
        if (AuthStateProvider is SiweAuthenticationServerStateProvider<User, SiweMessage> siweAuth)
        {
            await siweAuth.LogOutUserAsync();
        }
    }
}
```

### Example 3: LocalStorage Utilities

```csharp
using Nethereum.Blazor.Storage;
using Microsoft.JSInterop;

public class UserPreferencesService
{
    private readonly LocalStorageHelper _localStorage;

    public UserPreferencesService(IJSRuntime jsRuntime)
    {
        _localStorage = new LocalStorageHelper(jsRuntime);
    }

    public async Task SaveThemeAsync(string theme)
    {
        await _localStorage.SetItemAsync("user_theme", theme);
    }

    public async Task<string> GetThemeAsync()
    {
        var theme = await _localStorage.GetItemAsync("user_theme");
        return theme ?? "dark"; // default
    }

    public async Task RemoveThemeAsync()
    {
        await _localStorage.RemoveItemAsync("user_theme");
    }
}
```

### Example 4: Account and Network Change Handling

```razor
@page "/wallet-monitor"
@inject EIP6963WalletHostProvider WalletProvider
@implements IDisposable

<h3>Wallet Status Monitor</h3>

<div class="status-panel">
    <p><strong>Connected:</strong> @(isConnected ? "Yes" : "No")</p>
    <p><strong>Address:</strong> @currentAddress</p>
    <p><strong>Network:</strong> @GetNetworkName(currentChainId)</p>
    <p><strong>Last Change:</strong> @lastChangeTime</p>
</div>

<div class="change-log">
    <h4>Change Log</h4>
    @foreach (var log in changeLogs)
    {
        <p>@log</p>
    }
</div>

@code {
    private bool isConnected = false;
    private string currentAddress = "Not connected";
    private long currentChainId = 0;
    private DateTime lastChangeTime;
    private List<string> changeLogs = new();

    protected override void OnInitialized()
    {
        WalletProvider.SelectedAccountChanged += OnAccountChanged;
        WalletProvider.NetworkChanged += OnNetworkChanged;

        // Initialize current values
        isConnected = WalletProvider.Enabled;
        currentAddress = WalletProvider.SelectedAccount ?? "Not connected";
        currentChainId = WalletProvider.SelectedNetworkChainId;
    }

    private async Task OnAccountChanged(string newAddress)
    {
        lastChangeTime = DateTime.Now;
        changeLogs.Insert(0, $"[{lastChangeTime:HH:mm:ss}] Account changed: {newAddress}");

        currentAddress = newAddress ?? "Disconnected";
        isConnected = !string.IsNullOrEmpty(newAddress);

        StateHasChanged();
    }

    private async Task OnNetworkChanged(long newChainId)
    {
        lastChangeTime = DateTime.Now;
        changeLogs.Insert(0, $"[{lastChangeTime:HH:mm:ss}] Network changed: {GetNetworkName(newChainId)}");

        currentChainId = newChainId;

        StateHasChanged();
    }

    private string GetNetworkName(long chainId) => chainId switch
    {
        1 => "Ethereum Mainnet",
        5 => "Goerli",
        11155111 => "Sepolia",
        137 => "Polygon",
        _ => $"Chain ID {chainId}"
    };

    public void Dispose()
    {
        WalletProvider.SelectedAccountChanged -= OnAccountChanged;
        WalletProvider.NetworkChanged -= OnNetworkChanged;
    }
}
```

### Example 5: Protected Routes with Role-Based Authorization

```razor
@page "/admin"
@attribute [Authorize(Roles = "EthereumConnected,Admin")]
@inject AuthenticationStateProvider AuthStateProvider

<h3>Admin Dashboard</h3>

<AuthorizeView Roles="Admin">
    <Authorized>
        <p>Welcome, administrator!</p>
        <p>Your Ethereum address: @context.User.Identity.Name</p>

        <h4>Your Claims:</h4>
        <ul>
            @foreach (var claim in context.User.Claims)
            {
                <li>@claim.Type: @claim.Value</li>
            }
        </ul>
    </Authorized>
    <NotAuthorized>
        <p>You need administrator privileges to access this page.</p>
    </NotAuthorized>
</AuthorizeView>
```

### Example 6: Custom Authentication State Provider with Custom Claims

```csharp
using Nethereum.Blazor;
using Nethereum.UI;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

public class CustomEthereumAuthProvider : EthereumAuthenticationStateProvider
{
    private readonly IUserRoleService _roleService;

    public CustomEthereumAuthProvider(
        SelectedEthereumHostProviderService selectedHostProviderService,
        IUserRoleService roleService) : base(selectedHostProviderService)
    {
        _roleService = roleService;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var baseState = await base.GetAuthenticationStateAsync();

        if (baseState.User.Identity.IsAuthenticated)
        {
            var address = baseState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            // Add custom claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, address),
                new Claim(ClaimTypes.Role, "EthereumConnected")
            };

            // Fetch user-specific roles from your service
            var userRoles = await _roleService.GetRolesAsync(address);
            foreach (var role in userRoles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            // Add custom claims
            claims.Add(new Claim("ChainId", EthereumHostProvider.SelectedNetworkChainId.ToString()));
            claims.Add(new Claim("ConnectedAt", DateTime.UtcNow.ToString("o")));

            var identity = new ClaimsIdentity(claims, "ethereumConnection");
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }

        return baseState;
    }
}
```

### Example 7: Wallet Connection Component with EIP-6963

```razor
@page "/connect"
@inject IEIP6963WalletInterop WalletInterop
@inject EIP6963WalletHostProvider WalletProvider
@inject AuthenticationStateProvider AuthStateProvider

<div class="connect-wallet-container">
    @if (!isConnected)
    {
        <h3>Connect Your Wallet</h3>

        @if (isLoading)
        {
            <p>Detecting wallets...</p>
        }
        else if (wallets == null || wallets.Length == 0)
        {
            <div class="alert alert-info">
                <h4>No Wallets Found</h4>
                <p>Please install a compatible wallet extension:</p>
                <ul>
                    <li><a href="https://metamask.io" target="_blank">MetaMask</a></li>
                    <li><a href="https://www.coinbase.com/wallet" target="_blank">Coinbase Wallet</a></li>
                    <li><a href="https://rabby.io" target="_blank">Rabby</a></li>
                </ul>
            </div>
        }
        else
        {
            <div class="wallet-options">
                @foreach (var wallet in wallets)
                {
                    <button class="wallet-button" @onclick="() => ConnectWallet(wallet)">
                        @if (!string.IsNullOrEmpty(wallet.Icon))
                        {
                            <img src="@wallet.Icon" alt="@wallet.Name" />
                        }
                        <span>@wallet.Name</span>
                    </button>
                }
            </div>
        }
    }
    else
    {
        <div class="connected-info">
            <h4>Connected</h4>
            <p><strong>Wallet:</strong> @connectedWalletName</p>
            <p><strong>Address:</strong> @FormatAddress(WalletProvider.SelectedAccount)</p>
            <p><strong>Network:</strong> @WalletProvider.SelectedNetworkChainId</p>
            <button class="btn btn-danger" @onclick="Disconnect">Disconnect</button>
        </div>
    }
</div>

@code {
    private bool isLoading = true;
    private bool isConnected = false;
    private EIP6963WalletInfo[] wallets;
    private string connectedWalletName;

    protected override async Task OnInitializedAsync()
    {
        // Wait for wallets to be announced
        await Task.Delay(500);
        wallets = await WalletInterop.GetAvailableWalletsAsync();
        isLoading = false;
    }

    private async Task ConnectWallet(EIP6963WalletInfo wallet)
    {
        try
        {
            await WalletInterop.SelectWalletAsync(wallet.Uuid);
            var address = await WalletProvider.EnableProviderAsync();

            if (!string.IsNullOrEmpty(address))
            {
                isConnected = true;
                connectedWalletName = wallet.Name;

                // Update authentication state
                if (AuthStateProvider is EthereumAuthenticationStateProvider ethAuth)
                {
                    await ethAuth.NotifyAuthenticationStateAsEthereumConnected();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Connection error: {ex.Message}");
        }
    }

    private async Task Disconnect()
    {
        if (AuthStateProvider is EthereumAuthenticationStateProvider ethAuth)
        {
            await ethAuth.NotifyAuthenticationStateAsEthereumDisconnected();
        }

        isConnected = false;
        connectedWalletName = null;
    }

    private string FormatAddress(string address)
    {
        if (string.IsNullOrEmpty(address) || address.Length < 10)
            return address;
        return $"{address.Substring(0, 6)}...{address.Substring(address.Length - 4)}";
    }
}
```

### Example 8: Token Storage with LocalStorageAccessTokenService

```csharp
using Nethereum.Blazor.Siwe;
using Nethereum.Siwe.Authentication;
using Microsoft.JSInterop;

// Register in Program.cs
builder.Services.AddSingleton<IAccessTokenService, LocalStorageAccessTokenService>();

// Usage in a service
public class AuthenticationService
{
    private readonly IAccessTokenService _tokenService;

    public AuthenticationService(IAccessTokenService tokenService)
    {
        _tokenService = tokenService;
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var token = await _tokenService.GetAccessTokenAsync();
        return !string.IsNullOrEmpty(token);
    }

    public async Task StoreTokenAsync(string token)
    {
        await _tokenService.SetAccessTokenAsync(token);
    }

    public async Task ClearTokenAsync()
    {
        await _tokenService.RemoveAccessTokenAsync();
    }
}
```

## API Reference

### EthereumAuthenticationStateProvider

Base authentication provider for Ethereum wallet connection.

```csharp
public class EthereumAuthenticationStateProvider : AuthenticationStateProvider, IDisposable
{
    // Constructor
    public EthereumAuthenticationStateProvider(
        SelectedEthereumHostProviderService selectedHostProviderService);

    // Properties
    protected IEthereumHostProvider EthereumHostProvider { get; set; }
    protected SelectedEthereumHostProviderService SelectedHostProviderService { get; }

    // Methods
    public override Task<AuthenticationState> GetAuthenticationStateAsync();
    public void NotifyStateHasChanged();
    public Task NotifyAuthenticationStateAsEthereumConnected();
    public Task NotifyAuthenticationStateAsEthereumConnected(string currentAddress);
    public Task NotifyAuthenticationStateAsEthereumDisconnected();
    public void Dispose();
}
```

**Claims Created:**
- ClaimTypes.NameIdentifier: Ethereum address
- ClaimTypes.Role: "EthereumConnected"

### EIP6963WalletBlazorInterop

Blazor implementation of EIP-6963 wallet discovery protocol.

```csharp
public class EIP6963WalletBlazorInterop : IEIP6963WalletInterop
{
    // Constructor
    public EIP6963WalletBlazorInterop(IJSRuntime jsRuntime);

    // Properties
    public JsonSerializerSettings JsonSerializerSettings { get; set; }

    // Wallet Discovery
    public ValueTask<EIP6963WalletInfo[]> GetAvailableWalletsAsync();
    public ValueTask SelectWalletAsync(string walletId);
    public ValueTask<string> GetWalletIconAsync(string walletId);

    // Standard Wallet Operations
    public ValueTask<string> EnableEthereumAsync();
    public ValueTask<bool> CheckAvailabilityAsync();
    public ValueTask<RpcResponseMessage> SendAsync(RpcRequestMessage rpcRequestMessage);
    public ValueTask<string> SignAsync(string utf8Hex);
    public ValueTask<string> GetSelectedAddress();

    // JavaScript Invokable Callbacks
    [JSInvokable]
    public static Task EIP6963AvailableChanged(bool available);

    [JSInvokable]
    public static Task EIP6963SelectedAccountChanged(string selectedAccount);

    [JSInvokable]
    public static Task EIP6963SelectedNetworkChanged(string chainId);
}
```

### SiweAuthenticationServerStateProvider

SIWE-based authentication with server validation.

```csharp
public class SiweAuthenticationServerStateProvider<TUser, TSiweMessage>
    : EthereumAuthenticationStateProvider
    where TUser : User
    where TSiweMessage : SiweMessage, new()
{
    // Constructor
    public SiweAuthenticationServerStateProvider(
        NethereumSiweAuthenticatorService nethereumSiweAuthenticatorService,
        IAccessTokenService accessTokenService,
        SelectedEthereumHostProviderService selectedHostProviderService,
        IUserService<TUser> userService,
        ISessionStorage sessionStorage);

    // Methods
    public override Task<AuthenticationState> GetAuthenticationStateAsync();
    public Task AuthenticateAsync(string address = null);
    public Task MarkUserAsAuthenticated();
    public Task LogOutUserAsync();
    public Task<TUser> GetUserAsync();
}
```

**Claims Created:**
- ClaimTypes.Name: Username from user service
- ClaimTypes.NameIdentifier: Ethereum address
- ClaimTypes.Role: "EthereumConnected"
- ClaimTypes.Role: "SiweAuthenticated"

### LocalStorageAccessTokenService

JWT token storage in browser localStorage.

```csharp
public class LocalStorageAccessTokenService : IAccessTokenService
{
    public const string JWTTokenName = "jwt_token";

    // Constructor
    public LocalStorageAccessTokenService(IJSRuntime jsRuntime);

    // Methods
    public Task<string> GetAccessTokenAsync();
    public Task SetAccessTokenAsync(string tokenValue);
    public Task RemoveAccessTokenAsync();
}
```

### LocalStorageHelper

Generic localStorage utility wrapper.

```csharp
public class LocalStorageHelper
{
    // Constructor
    public LocalStorageHelper(IJSRuntime jsRuntime);

    // Methods
    public Task<string?> GetItemAsync(string key);
    public Task SetItemAsync(string key, string value);
    public Task RemoveItemAsync(string key);
}
```

### JavaScript API (window.NethereumEIP6963Interop)

```javascript
window.NethereumEIP6963Interop = {
    // Initialization
    init: function()

    // Wallet Discovery
    isAvailable: function() => boolean
    getAvailableWallets: function() => WalletInfo[]
    selectWallet: async function(uuid: string) => void
    getWalletIcon: function(walletUuid: string) => string

    // Wallet Operations
    enableEthereum: async function() => string
    request: async function(message: string) => Promise<string>
    sign: async function(utf8HexMsg: string) => Promise<string>
    getSelectedOrRequestAddress: async function() => Promise<string>
}
```

## Important Notes

### EIP-6963 Wallet Discovery

EIP-6963 uses browser events to discover wallets:

1. On load, JavaScript dispatches `eip6963:requestProvider` event
2. Wallet extensions respond with `eip6963:announceProvider` events
3. Each wallet provides: name, uuid, icon (data URI), rdns

**Supported Wallets:**
- MetaMask
- Coinbase Wallet
- Rabby
- Trust Wallet
- Any EIP-6963 compatible wallet

### Script Reference Required

You MUST include the JavaScript library:

```html
<script src="_content/Nethereum.Blazor/NethereumEIP6963.js"></script>
```

### Authentication Flow

**Basic Wallet Authentication:**
1. User selects wallet from EIP-6963 discovery
2. Wallet prompts for connection approval
3. EthereumAuthenticationStateProvider creates ClaimsPrincipal
4. User gains "EthereumConnected" role

**SIWE Authentication:**
1. User connects wallet
2. Server generates SIWE message
3. User signs message in wallet
4. Server validates signature and creates JWT
5. JWT stored in localStorage
6. User gains "EthereumConnected" and "SiweAuthenticated" roles

### Token Storage Security

LocalStorageAccessTokenService stores tokens in browser localStorage with key "jwt_token".

**Security Considerations:**
- localStorage is vulnerable to XSS attacks
- Only store tokens, never private keys
- Implement token expiration
- Use HTTPS only
- Consider httpOnly cookies for production

### Account and Network Changes

The JavaScript library automatically sets up event listeners for:
- `accountsChanged`: When user switches accounts in wallet
- `chainChanged`: When user switches networks

These events update the provider state and trigger C# callbacks.

### Browser Support

Requires:
- Browser with localStorage support
- EIP-6963 compatible wallet extension(s) installed

Works with both Blazor WebAssembly and Blazor Server.

## Related Packages

### Dependencies
- **Nethereum.EIP6963WalletInterop** - Core EIP-6963 interfaces and types
- **Nethereum.Metamask** - MetaMask-specific interfaces (for compatibility)

### Dependent Packages
- **Nethereum.Metamask.Blazor** - Specific MetaMask implementation (uses EthereumAuthenticationStateProvider)

### Related Packages
- **Nethereum.UI** - Base wallet provider interfaces
- **Nethereum.Siwe** - SIWE message building and parsing
- **Nethereum.Siwe.Core** - Core SIWE authentication interfaces

## License

This package is part of the Nethereum project and follows the same MIT license.
