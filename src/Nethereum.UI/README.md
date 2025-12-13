# Nethereum.UI

Common UI services and abstractions for building Ethereum frontend applications with Nethereum.

## Overview

Nethereum.UI provides infrastructure components for building Ethereum-enabled user interfaces across web, desktop, and mobile platforms. It defines standard abstractions for wallet providers, authentication, and input validation that work consistently across Blazor, MAUI, Avalonia, Unity, and other .NET UI frameworks.

**Key Features:**
- Wallet provider abstraction (`IEthereumHostProvider`)
- Provider selection service with change notifications
- Sign-In with Ethereum (SIWE) authentication
- FluentValidation rules for Ethereum addresses and data
- Built-in provider implementation for testing/development
- Event-driven account and network change tracking

**Use Cases:**
- Building multi-wallet support in dApps
- Implementing Sign-In with Ethereum authentication
- Validating user input (addresses, private keys, hex data)
- Managing wallet connections across UI frameworks
- Creating wallet selection interfaces

## Installation

```bash
dotnet add package Nethereum.UI
```

## Dependencies

- **FluentValidation** (9.3.0) - Input validation framework
- Nethereum.Siwe.Core
- Nethereum.Siwe

## Core Components

### IEthereumHostProvider

Standard interface for Ethereum wallet providers (MetaMask, WalletConnect, built-in wallets, etc.):

```csharp
public interface IEthereumHostProvider
{
    // Provider metadata
    string Name { get; }
    bool Available { get; }
    bool Enabled { get; }

    // Selected account/network
    string SelectedAccount { get; }
    long SelectedNetworkChainId { get; }

    // Multi-wallet support
    bool MultipleWalletsProvider { get; }
    bool MultipleWalletSelected { get; }

    // Change events
    event Func<string, Task> SelectedAccountChanged;
    event Func<long, Task> NetworkChanged;
    event Func<bool, Task> AvailabilityChanged;
    event Func<bool, Task> EnabledChanged;

    // Provider operations
    Task<bool> CheckProviderAvailabilityAsync();
    Task<IWeb3> GetWeb3Async();
    Task<string> EnableProviderAsync();
    Task<string> GetProviderSelectedAccountAsync();
    Task<string> SignMessageAsync(string message);
}
```

### SelectedEthereumHostProviderService

Service for managing the currently selected wallet provider:

```csharp
public class SelectedEthereumHostProviderService
{
    public IEthereumHostProvider SelectedHost { get; }
    public event Func<IEthereumHostProvider, Task> SelectedHostProviderChanged;

    Task SetSelectedEthereumHostProvider(IEthereumHostProvider provider);
    Task ClearSelectedEthereumHostProvider();
}
```

### NethereumSiweAuthenticatorService

SIWE authentication service integrating with wallet providers:

```csharp
public class NethereumSiweAuthenticatorService
{
    public NethereumSiweAuthenticatorService(
        SelectedEthereumHostProviderService selectedEthereumHostProviderService,
        ISessionStorage sessionStorage);

    string GenerateNewSiweMessage(SiweMessage siweMessage);
    Task<SiweMessage> AuthenticateAsync(SiweMessage siweMessage);
    void LogOut(SiweMessage siweMessage);
}
```

## Usage Examples

### Example 1: Using Built-In Provider for Testing

```csharp
using Nethereum.UI;
using Nethereum.Web3.Accounts;

// Create built-in provider with private key
var provider = new NethereumHostProvider();

// Set RPC URL and detect chain ID
await provider.SetUrl("https://mainnet.infura.io/v3/YOUR-PROJECT-ID");

// Set account
var account = new Account("0xYOUR_PRIVATE_KEY");
provider.SetSelectedAccount(account);

// Get Web3 instance
var web3 = await provider.GetWeb3Async();
var balance = await web3.Eth.GetBalance.SendRequestAsync(provider.SelectedAccount);
```

### Example 2: Provider Selection Service

```csharp
using Nethereum.UI;

// Create selection service
var selectionService = new SelectedEthereumHostProviderService();

// Subscribe to provider changes
selectionService.SelectedHostProviderChanged += async (provider) =>
{
    if (provider != null)
    {
        Console.WriteLine($"Provider changed to: {provider.Name}");
        Console.WriteLine($"Account: {provider.SelectedAccount}");
        Console.WriteLine($"Chain ID: {provider.SelectedNetworkChainId}");
    }
    else
    {
        Console.WriteLine("Provider disconnected");
    }
};

// Set provider
await selectionService.SetSelectedEthereumHostProvider(metamaskProvider);

// Later: switch providers
await selectionService.SetSelectedEthereumHostProvider(walletConnectProvider);

// Disconnect
await selectionService.ClearSelectedEthereumHostProvider();
```

### Example 3: Listening to Account Changes

```csharp
using Nethereum.UI;

var provider = new NethereumHostProvider();

// Subscribe to account changes
provider.SelectedAccountChanged += async (address) =>
{
    Console.WriteLine($"Account changed to: {address}");

    // Update UI, reload balances, etc.
    var web3 = await provider.GetWeb3Async();
    var balance = await web3.Eth.GetBalance.SendRequestAsync(address);
    Console.WriteLine($"New account balance: {Web3.Convert.FromWei(balance)} ETH");
};

// Subscribe to network changes
provider.NetworkChanged += async (chainId) =>
{
    Console.WriteLine($"Network changed to chain ID: {chainId}");

    // Update UI, warn user, etc.
    if (chainId != 1)
    {
        Console.WriteLine("Warning: Not connected to Ethereum mainnet");
    }
};

// Set account (triggers event)
provider.SetSelectedAccount("0xPRIVATE_KEY");

// Change network (triggers event)
await provider.SetUrl("https://polygon-rpc.com");
```

### Example 4: SIWE Authentication Flow

```csharp
using Nethereum.UI;
using Nethereum.Siwe.Core;

// Setup services
var providerService = new SelectedEthereumHostProviderService();
var authService = new NethereumSiweAuthenticatorService(
    providerService,
    sessionStorage);

// Set provider (MetaMask, WalletConnect, etc.)
await providerService.SetSelectedEthereumHostProvider(metamaskProvider);

// Create SIWE message
var siweMessage = new SiweMessage
{
    Domain = "example.com",
    Address = providerService.SelectedHost.SelectedAccount,
    Uri = "https://example.com/login",
    Version = "1",
    ChainId = providerService.SelectedHost.SelectedNetworkChainId,
    Nonce = Guid.NewGuid().ToString(),
    IssuedAt = DateTime.UtcNow.ToString("o")
};

try
{
    // Authenticate (prompts wallet to sign)
    var authenticatedMessage = await authService.AuthenticateAsync(siweMessage);

    Console.WriteLine("Authentication successful!");
    Console.WriteLine($"Authenticated address: {authenticatedMessage.Address}");

    // User is now authenticated
}
catch (Exception ex)
{
    Console.WriteLine($"Authentication failed: {ex.Message}");
}

// Later: log out
authService.LogOut(siweMessage);
```

### Example 5: FluentValidation for Ethereum Data

```csharp
using FluentValidation;
using Nethereum.UI.Validation;

public class TransferRequest
{
    public string ToAddress { get; set; }
    public string Amount { get; set; }
    public string PrivateKey { get; set; }
}

public class TransferRequestValidator : AbstractValidator<TransferRequest>
{
    public TransferRequestValidator()
    {
        RuleFor(x => x.ToAddress)
            .NotEmpty()
            .IsEthereumAddress(); // Custom Ethereum address validation

        RuleFor(x => x.Amount)
            .NotEmpty()
            .Must(BeValidNumber)
            .WithMessage("Invalid amount");

        RuleFor(x => x.PrivateKey)
            .IsEthereumPrivateKey(); // Validates hex format and length (66 chars)
    }

    private bool BeValidNumber(string value)
    {
        return decimal.TryParse(value, out _);
    }
}

// Usage
var validator = new TransferRequestValidator();
var request = new TransferRequest
{
    ToAddress = "0x742d35Cc6634C0532925a3b844Bc9e7595f0bEb",
    Amount = "0.5",
    PrivateKey = "0x..." // 64 hex chars
};

var result = validator.Validate(request);
if (!result.IsValid)
{
    foreach (var error in result.Errors)
    {
        Console.WriteLine($"{error.PropertyName}: {error.ErrorMessage}");
    }
}
```

### Example 6: Data Annotation Validation (.NET Core 3.1+)

```csharp
using System.ComponentModel.DataAnnotations;
using Nethereum.UI.Validation.Attributes;

public class WalletConnectionForm
{
    [Required]
    [EthereumAddress]
    public string WalletAddress { get; set; }

    [Required]
    [Url]
    public string RpcUrl { get; set; }
}

// Usage in ASP.NET Core / Blazor
var form = new WalletConnectionForm
{
    WalletAddress = "0x742d35Cc6634C0532925a3b844Bc9e7595f0bEb",
    RpcUrl = "https://mainnet.infura.io/v3/YOUR-PROJECT-ID"
};

var context = new ValidationContext(form);
var results = new List<ValidationResult>();

if (Validator.TryValidateObject(form, context, results, true))
{
    Console.WriteLine("Form is valid");
}
else
{
    foreach (var error in results)
    {
        Console.WriteLine(error.ErrorMessage);
    }
}
```

### Example 7: Custom Provider Implementation

```csharp
using Nethereum.UI;
using System.Threading.Tasks;

public class MyCustomWalletProvider : IEthereumHostProvider
{
    public string Name => "My Custom Wallet";
    public bool Available { get; private set; }
    public string SelectedAccount { get; private set; }
    public long SelectedNetworkChainId { get; private set; }
    public bool Enabled => Available && SelectedAccount != null;
    public bool MultipleWalletsProvider => false;
    public bool MultipleWalletSelected => false;

    public event Func<string, Task> SelectedAccountChanged;
    public event Func<long, Task> NetworkChanged;
    public event Func<bool, Task> AvailabilityChanged;
    public event Func<bool, Task> EnabledChanged;

    public async Task<bool> CheckProviderAvailabilityAsync()
    {
        // Check if your wallet extension/app is available
        Available = await CheckIfWalletIsInstalled();
        await AvailabilityChanged?.Invoke(Available);
        return Available;
    }

    public async Task<string> EnableProviderAsync()
    {
        // Request user permission to connect
        SelectedAccount = await RequestAccountAccess();
        await SelectedAccountChanged?.Invoke(SelectedAccount);
        await EnabledChanged?.Invoke(true);
        return SelectedAccount;
    }

    public Task<IWeb3> GetWeb3Async()
    {
        // Return Web3 instance connected to your wallet
        return Task.FromResult(myWalletWeb3Instance);
    }

    public Task<string> GetProviderSelectedAccountAsync()
    {
        return Task.FromResult(SelectedAccount);
    }

    public async Task<string> SignMessageAsync(string message)
    {
        // Call your wallet's signing method
        return await myWallet.SignMessageAsync(message);
    }

    private async Task<bool> CheckIfWalletIsInstalled() { /* ... */ }
    private async Task<string> RequestAccountAccess() { /* ... */ }
}
```

### Example 8: Hex and URI Validation

```csharp
using FluentValidation;
using Nethereum.UI.Validation;

public class ContractInteractionForm
{
    public string ContractAddress { get; set; }
    public string FunctionData { get; set; }
    public string RpcEndpoint { get; set; }
}

public class ContractInteractionValidator : AbstractValidator<ContractInteractionForm>
{
    public ContractInteractionValidator()
    {
        RuleFor(x => x.ContractAddress)
            .IsEthereumAddress()
            .WithMessage("Please provide a valid Ethereum contract address");

        RuleFor(x => x.FunctionData)
            .IsHex()
            .WithMessage("Function data must be valid hexadecimal with 0x prefix");

        RuleFor(x => x.RpcEndpoint)
            .IsUri()
            .WithMessage("Please provide a valid RPC endpoint URL");
    }
}
```

## Available Validation Rules

### FluentValidation Extensions

```csharp
using Nethereum.UI.Validation;

// Ethereum address validation
RuleFor(x => x.Address).IsEthereumAddress();

// Hexadecimal data validation (must have 0x prefix)
RuleFor(x => x.Data).IsHex();

// Private key validation (0x + 64 hex chars = 66 total)
RuleFor(x => x.PrivateKey).IsEthereumPrivateKey();

// URI validation
RuleFor(x => x.Url).IsUri();
```

### Data Annotation Attributes (.NET Core 3.1+)

```csharp
using Nethereum.UI.Validation.Attributes;

[EthereumAddress]
public string Address { get; set; }

[Hex]
public string Data { get; set; }
```

## API Reference

### IEthereumHostProvider

```csharp
public interface IEthereumHostProvider
{
    // Properties
    string Name { get; }
    bool Available { get; }
    string SelectedAccount { get; }
    long SelectedNetworkChainId { get; }
    bool Enabled { get; }
    bool MultipleWalletsProvider { get; }
    bool MultipleWalletSelected { get; }

    // Events
    event Func<string, Task> SelectedAccountChanged;
    event Func<long, Task> NetworkChanged;
    event Func<bool, Task> AvailabilityChanged;
    event Func<bool, Task> EnabledChanged;

    // Methods
    Task<bool> CheckProviderAvailabilityAsync();
    Task<IWeb3> GetWeb3Async();
    Task<string> EnableProviderAsync();
    Task<string> GetProviderSelectedAccountAsync();
    Task<string> SignMessageAsync(string message);
}
```

### NethereumHostProvider

Built-in provider for testing and development:

```csharp
public class NethereumHostProvider : IEthereumHostProvider
{
    void SetSelectedAccount(string privateKey);
    void SetSelectedAccount(Account account);
    Task<bool> SetUrl(string url);
}
```

### SelectedEthereumHostProviderService

```csharp
public class SelectedEthereumHostProviderService
{
    public IEthereumHostProvider SelectedHost { get; }
    public event Func<IEthereumHostProvider, Task> SelectedHostProviderChanged;

    public Task SetSelectedEthereumHostProvider(IEthereumHostProvider provider);
    public Task ClearSelectedEthereumHostProvider();
}
```

### NethereumSiweAuthenticatorService

```csharp
public class NethereumSiweAuthenticatorService
{
    public NethereumSiweAuthenticatorService(
        SelectedEthereumHostProviderService selectedEthereumHostProviderService,
        ISessionStorage sessionStorage);

    public string GenerateNewSiweMessage(SiweMessage siweMessage);
    public Task<SiweMessage> AuthenticateAsync(SiweMessage siweMessage);
    public void LogOut(SiweMessage siweMessage);
}
```

## Important Notes

### Provider Lifecycle

The `IEthereumHostProvider` interface follows this lifecycle:

1. **Check Availability**: `CheckProviderAvailabilityAsync()` - Check if wallet is installed/available
2. **Enable Provider**: `EnableProviderAsync()` - Request user permission to connect
3. **Get Account**: Provider sets `SelectedAccount` and fires `SelectedAccountChanged` event
4. **Get Web3**: `GetWeb3Async()` returns configured Web3 instance
5. **Sign Messages**: `SignMessageAsync()` for SIWE and other signature requests

### Event Handling

All provider change events are async:

```csharp
provider.SelectedAccountChanged += async (account) =>
{
    // Async operations allowed
    await UpdateUserInterface(account);
};
```

### Multi-Wallet Support

Some providers (like WalletConnect with multiple sessions) support multiple simultaneous wallet connections:

```csharp
if (provider.MultipleWalletsProvider)
{
    Console.WriteLine("This provider supports multiple wallets");

    if (provider.MultipleWalletSelected)
    {
        Console.WriteLine("Multiple wallets are currently connected");
    }
}
```

### Validation Framework Support

Nethereum.UI provides validation through two approaches:

1. **FluentValidation** - Full framework support across all .NET versions
2. **Data Annotations** - Available in .NET Core 3.1+ only (conditional compilation)

Choose based on your target framework and preferences.

### SIWE Authentication

SIWE authentication requires:
- Selected wallet provider with signing capability
- Session storage implementation (`ISessionStorage`)
- Valid SIWE message with all required fields

## Related Packages

### Implementations

- **Nethereum.Metamask** - MetaMask provider implementation
- **Nethereum.Metamask.Blazor** - Blazor-specific MetaMask provider
- **Nethereum.WalletConnect** - WalletConnect provider
- **Nethereum.Reown.AppKit.Blazor** - Reown AppKit provider for Blazor
- **Nethereum.EIP6963WalletInterop** - Multi-wallet discovery (EIP-6963)

### UI Frameworks

- **Nethereum.Blazor** - Blazor components and services
- **Nethereum.Wallet.UI.Components** - Shared UI components
- **Nethereum.Wallet.UI.Components.Blazor** - Blazor wallet UI
- **Nethereum.Wallet.UI.Components.Avalonia** - Avalonia wallet UI
- **Nethereum.Wallet.UI.Components.Maui** - MAUI wallet UI

### Dependencies

- **Nethereum.Siwe** - Sign-In with Ethereum
- **Nethereum.Siwe.Core** - SIWE core types
- **FluentValidation** - Validation framework

## Additional Resources

- [Sign-In with Ethereum (EIP-4361)](https://eips.ethereum.org/EIPS/eip-4361)
- [EIP-6963 Multi Injected Provider Discovery](https://eips.ethereum.org/EIPS/eip-6963)
- [FluentValidation Documentation](https://docs.fluentvalidation.net/)
- [Nethereum Documentation](http://docs.nethereum.com/)
