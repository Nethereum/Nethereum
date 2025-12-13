# Nethereum.WalletConnect

WalletConnect v2 protocol integration for connecting desktop and web applications to mobile wallet apps using QR code pairing.

## Overview

Nethereum.WalletConnect provides integration with the WalletConnect v2 protocol, enabling dApps to connect to mobile wallet applications by scanning a QR code. Once connected, the wallet handles all transaction signing and message signing requests, while blockchain queries can optionally go through a custom RPC endpoint.

**Key Features:**
- WalletConnect v2.3.0 protocol support (WalletConnectSharp.Sign)
- QR code generation for mobile wallet pairing
- NethereumWalletConnectService for connection management and wallet operations
- NethereumWalletConnectHostProvider implementing IEthereumHostProvider
- NethereumWalletConnectInterceptor for automatic request routing
- Support for personal_sign, eth_signTypedData_v4, eth_sendTransaction
- Chain switching (wallet_switchEthereumChain) and adding (wallet_addEthereumChain)
- CAIP-25 session management with eip155 namespace
- Event subscriptions for accountsChanged and chainChanged
- Optional custom RPC endpoint for queries (hybrid mode)

## Installation

```bash
dotnet add package Nethereum.WalletConnect
```

Or via Package Manager Console:

```powershell
Install-Package Nethereum.WalletConnect
```

## Dependencies

**Package References:**
- Newtonsoft.Json 13.0.3
- WalletConnect.Core 2.3.0
- WalletConnect.Sign 2.3.0

**Project References:**
- Nethereum.UI
- Nethereum.Web3

## Key Concepts

### WalletConnect Protocol Flow

1. **Initialize Client**: Create WalletConnectSignClient with ProjectId from WalletConnect Cloud
2. **Create Services**: Instantiate NethereumWalletConnectService and NethereumWalletConnectHostProvider
3. **Generate QR Code**: Call InitialiseConnectionAndGetQRUriAsync to get connection URI
4. **User Scans QR**: User scans QR code with mobile wallet app
5. **Wait for Approval**: Call WaitForConnectionApprovalAndGetSelectedAccountAsync
6. **Connected**: Session established, can now send signing requests to wallet

### CAIP-25 Namespaces

WalletConnect v2 uses CAIP-25 for multi-chain session management:
- **Required Namespace**: Must include `eip155:1` (Ethereum Mainnet) with eth_sendTransaction
- **Optional Namespace**: Additional chains and methods (personal_sign, eth_signTypedData_v4, wallet operations)
- **Chain IDs**: Format is `eip155:{chainId}` (e.g., `eip155:1` for Mainnet, `eip155:137` for Polygon)

### Request Interception

NethereumWalletConnectInterceptor automatically routes specific methods through the connected wallet:
- eth_sendTransaction
- eth_sign
- personal_sign
- eth_signTypedData_v4
- wallet_switchEthereumChain
- wallet_addEthereumChain

All other methods (e.g., eth_call, eth_getBalance) go through the configured RPC endpoint.

## Quick Start

### 1. Get WalletConnect Project ID

Sign up at https://cloud.walletconnect.com to get a free Project ID.

### 2. Initialize WalletConnect Client

```csharp
using Nethereum.WalletConnect;
using WalletConnectSharp.Sign;
using WalletConnectSharp.Sign.Models;
using WalletConnectSharp.Core;
using WalletConnectSharp.Storage;

var options = new SignClientOptions()
{
    ProjectId = "YOUR_PROJECT_ID_HERE",
    Metadata = new Metadata()
    {
        Description = "My dApp description",
        Icons = new[] { "https://myapp.com/icon.png" },
        Name = "My dApp",
        Url = "https://myapp.com"
    },
    Storage = new InMemoryStorage()
};

var client = await WalletConnectSignClient.Init(options);
```

### 3. Create Services and Generate QR Code

```csharp
using Nethereum.WalletConnect;
using QRCoder;

var walletConnectService = new NethereumWalletConnectService(client);
var walletConnectHostProvider = new NethereumWalletConnectHostProvider(walletConnectService);

// Subscribe to events
walletConnectHostProvider.SelectedAccountChanged += async (address) =>
{
    Console.WriteLine($"Account changed: {address}");
};

walletConnectHostProvider.NetworkChanged += async (chainId) =>
{
    Console.WriteLine($"Network changed: {chainId}");
};

// Get connection URI and generate QR code
var connectionOptions = NethereumWalletConnectService.GetDefaultConnectOptions();
var connectionUri = await walletConnectService.InitialiseConnectionAndGetQRUriAsync(connectionOptions);

// Generate QR code for display
QRCodeGenerator qrGenerator = new QRCodeGenerator();
QRCodeData qrCodeData = qrGenerator.CreateQrCode(connectionUri, QRCodeGenerator.ECCLevel.Q);
PngByteQRCode qRCode = new PngByteQRCode(qrCodeData);
byte[] qrCodeBytes = qRCode.GetGraphic(20);

// Display qrCodeBytes to user (convert to image)
Console.WriteLine($"Scan this QR code with your wallet: {connectionUri}");

// Wait for user to approve connection in wallet
var selectedAddress = await walletConnectService.WaitForConnectionApprovalAndGetSelectedAccountAsync();
Console.WriteLine($"Connected to: {selectedAddress}");
```

### 4. Use Web3 with WalletConnect

```csharp
var web3 = await walletConnectHostProvider.GetWeb3Async();

// Send transaction (goes through wallet for signing)
var receipt = await web3.Eth.GetEtherTransferService()
    .TransferEtherAndWaitForReceiptAsync("0xRecipientAddress", 0.1m);

// Query balance (goes through RPC endpoint if configured, or throws if not)
var balance = await web3.Eth.GetBalance.SendRequestAsync("0xAddress");
```

## Usage Examples

### Example 1: Complete Blazor WalletConnect Integration

Based on the official Nethereum WalletConnect Blazor console test.

```razor
@page "/"
@using Nethereum.WalletConnect
@using QRCoder
@using WalletConnectSharp.Sign
@using WalletConnectSharp.Sign.Models
@using WalletConnectSharp.Core
@using WalletConnectSharp.Storage

@if (walletConnectConnectedSession == null)
{
    <button @onclick="InitAsync">Connect Wallet</button>
    @if (!string.IsNullOrEmpty(QRByte))
    {
        <img src="@QRByte" Width="400" />
    }
}
else
{
    <div>
        <p>Address: @Address</p>
        <p>ChainId: @ChainId</p>
        <button @onclick="PersonalSignAsync">Personal Sign</button>
        <button @onclick="SignTypedDataAsync">Sign Typed Data</button>
        <button @onclick="SwitchChainAsync">Switch Chain</button>
        <button @onclick="AddEthereumChainAsync">Add Chain</button>
        <p>Response: @Response</p>
        <p>Recovered Account: @RecoveredAccount</p>
    </div>
}

@code {
    WalletConnectSignClient client;
    public string QRByte = "";
    NethereumWalletConnectService walletConnectService;
    WalletConnectConnectedSession walletConnectConnectedSession;
    NethereumWalletConnectHostProvider walletConnectHostProvider;
    public string Response;
    public string Address;
    public string ChainId;
    public string RecoveredAccount;

    public async Task InitAsync()
    {
        try
        {
            var options = new SignClientOptions()
            {
                ProjectId = "97d8fb2db9753c13645fd37d6920b2cc",
                Metadata = new Metadata()
                {
                    Description = "An example project to showcase WalletConnectSharpv2",
                    Icons = new[] { "https://walletconnect.com/meta/favicon.ico" },
                    Name = "WC Example",
                    Url = "https://walletconnect.com"
                },
                Storage = new InMemoryStorage()
            };

            var connectionOptions = NethereumWalletConnectService.GetDefaultConnectOptions();

            if (client == null)
            {
                client = await WalletConnectSignClient.Init(options);
            }

            walletConnectService = new NethereumWalletConnectService(client);
            // Initialize host provider immediately to hook up events
            walletConnectHostProvider = new NethereumWalletConnectHostProvider(walletConnectService);

            walletConnectHostProvider.SelectedAccountChanged += async (address) =>
            {
                Address = address;
                await InvokeAsync(StateHasChanged);
            };

            walletConnectHostProvider.NetworkChanged += async (chainId) =>
            {
                ChainId = chainId.ToString();
                await InvokeAsync(StateHasChanged);
            };

            var connectionUri = await walletConnectService.InitialiseConnectionAndGetQRUriAsync(connectionOptions);

            if (!string.IsNullOrEmpty(connectionUri))
            {
                // Generate QR code
                using MemoryStream ms = new();
                QRCodeGenerator qrCodeGenerate = new();
                QRCodeData qrCodeData = qrCodeGenerate.CreateQrCode(connectionUri, QRCodeGenerator.ECCLevel.Q);
                PngByteQRCode qRCode = new PngByteQRCode(qrCodeData);
                byte[] qrCodeBytes = qRCode.GetGraphic(20);
                string base64 = Convert.ToBase64String(qrCodeBytes);
                QRByte = string.Format("data:image/png;base64,{0}", base64);
                await InvokeAsync(StateHasChanged);

                // Wait for connection approval
                var selectedAddress = await walletConnectService.WaitForConnectionApprovalAndGetSelectedAccountAsync();
                walletConnectConnectedSession = walletConnectService.GetWalletConnectConnectedSession();
            }
        }
        catch (Exception ex)
        {
            Response = ex.Message;
            Console.WriteLine(ex.ToString());
        }
    }

    public async Task PersonalSignAsync()
    {
        try
        {
            var web3 = await walletConnectHostProvider.GetWeb3Async();
            var response = await web3.Eth.AccountSigning.PersonalSign.SendRequestAsync(
                new HexUTF8String("Hello World"));
            Response = response;
        }
        catch (Exception ex)
        {
            Response = ex.Message;
        }
    }

    public async Task SignTypedDataAsync()
    {
        try
        {
            var web3 = await walletConnectHostProvider.GetWeb3Async();
            var typedData = GetMailTypedDefinition();

            var mail = new Mail
            {
                From = new Person
                {
                    Name = "Cow",
                    Wallets = new List<string> { "0xCD2a3d9F938E13CD947Ec05AbC7FE734Df8DD826" }
                },
                To = new List<Person> { new Person { Name = "Bob" } },
                Contents = "Hello, Bob!"
            };

            typedData.Domain.ChainId = 1;
            typedData.SetMessage(mail);

            Response = await web3.Eth.AccountSigning.SignTypedDataV4.SendRequestAsync(typedData.ToJson());
            RecoveredAccount = new Eip712TypedDataSigner().RecoverFromSignatureV4(typedData, Response);
        }
        catch (Exception ex)
        {
            Response = ex.Message();
        }
    }

    public async Task SwitchChainAsync()
    {
        try
        {
            var web3 = await walletConnectHostProvider.GetWeb3Async();
            var response = await web3.Eth.HostWallet.SwitchEthereumChain.SendRequestAsync(
                new SwitchEthereumChainParameter() { ChainId = 1.ToHexBigInteger() });
            Response = response;
        }
        catch (Exception ex)
        {
            Response = ex.Message;
        }
    }

    public async Task AddEthereumChainAsync()
    {
        try
        {
            var web3 = await walletConnectHostProvider.GetWeb3Async();
            var chainFeature = ChainDefaultFeaturesServicesRepository.GetDefaultChainFeature(
                Nethereum.Signer.Chain.Optimism);
            var addParameter = chainFeature.ToAddEthereumChainParameter();
            var response = await web3.Eth.HostWallet.AddEthereumChain.SendRequestAsync(addParameter);
            Response = response;
        }
        catch (Exception ex)
        {
            Response = ex.Message;
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
            Types = MemberDescriptionFactory.GetTypesMemberDescription(
                typeof(Domain), typeof(Group), typeof(Mail), typeof(Person)),
            PrimaryType = nameof(Mail),
        };
    }
}
```

### Example 2: Custom Chain Configuration

```csharp
using Nethereum.WalletConnect;

// Connect to specific chains (Mainnet + Polygon)
var connectionOptions = NethereumWalletConnectService.GetDefaultConnectOptions(1, 137);

var connectionUri = await walletConnectService.InitialiseConnectionAndGetQRUriAsync(connectionOptions);
```

### Example 3: Hybrid Mode with Custom RPC Endpoint

```csharp
using Nethereum.WalletConnect;

// Create host provider with custom RPC endpoint for queries
var walletConnectHostProvider = new NethereumWalletConnectHostProvider(
    walletConnectService,
    "https://mainnet.infura.io/v3/YOUR-PROJECT-ID");

var web3 = await walletConnectHostProvider.GetWeb3Async();

// Queries go through Infura
var balance = await web3.Eth.GetBalance.SendRequestAsync("0xAddress");

// Signing goes through WalletConnect
var signature = await web3.Eth.AccountSigning.PersonalSign.SendRequestAsync(
    new HexUTF8String("Sign this message"));
```

### Example 4: Direct Service Usage (Without Host Provider)

```csharp
using Nethereum.WalletConnect;
using Nethereum.Web3;

var walletConnectService = new NethereumWalletConnectService(client);
var connectionUri = await walletConnectService.InitialiseConnectionAndGetQRUriAsync();
var address = await walletConnectService.WaitForConnectionApprovalAndGetSelectedAccountAsync();

// Use Web3 with interceptor
var web3 = new Web3();
web3.Client.OverridingRequestInterceptor = new NethereumWalletConnectInterceptor(walletConnectService);

// Send transaction
var txHash = await walletConnectService.SendTransactionAsync(new TransactionInput
{
    To = "0xRecipient",
    Value = new HexBigInteger(Web3.Web3.Convert.ToWei(0.1m))
});
```

### Example 5: Handling Account and Network Changes

```csharp
using Nethereum.WalletConnect;

var walletConnectHostProvider = new NethereumWalletConnectHostProvider(walletConnectService);

walletConnectHostProvider.SelectedAccountChanged += async (newAddress) =>
{
    Console.WriteLine($"User switched to account: {newAddress}");
    // Update UI, reload balances, etc.
};

walletConnectHostProvider.NetworkChanged += async (newChainId) =>
{
    Console.WriteLine($"User switched to chain ID: {newChainId}");
    // Update UI to reflect new network
};
```

### Example 6: Converting Chain IDs to EIP155 Format

```csharp
using Nethereum.WalletConnect;

// Convert long chain ID to eip155 format
string eip155ChainId = NethereumWalletConnectService.GetEIP155ChainId(1);
// Returns: "eip155:1"

// Convert eip155 format to long
long chainId = NethereumWalletConnectService.GetChainIdFromEip155("eip155:137");
// Returns: 137

// Convert multiple chain IDs
string[] eip155ChainIds = NethereumWalletConnectService.GetEIP155ChainIds(1, 137, 10);
// Returns: ["eip155:1", "eip155:137", "eip155:10"]
```

## API Reference

### NethereumWalletConnectService

Core service for WalletConnect session management and wallet operations.

```csharp
public class NethereumWalletConnectService : INethereumWalletConnectService
{
    // Constructor
    public NethereumWalletConnectService(ISignClient walletConnectClient);

    // Properties
    public ISignClient WalletConnectClient { get; }
    public string SelectedChainId { get; protected set; }
    public string SelectedAccount { get; protected set; }

    // Connection Management
    public Task<string> InitialiseConnectionAndGetQRUriAsync(
        ConnectOptions connectionOptions = null);
    public Task<string> WaitForConnectionApprovalAndGetSelectedAccountAsync();
    public WalletConnectConnectedSession GetWalletConnectConnectedSession();

    // Signing Operations
    public Task<string> PersonalSignAsync(string hexUtf8);
    public Task<string> SignAsync(string hexUtf8);
    public Task<string> SignTypedDataAsync(string hexUtf8);
    public Task<string> SignTypedDataV4Async(string hexUtf8);

    // Transaction Operations
    public Task<string> SendTransactionAsync(TransactionInput transaction);
    public Task<string> SignTransactionAsync(TransactionInput transaction);

    // Wallet Operations
    public Task<string> SwitchEthereumChainAsync(SwitchEthereumChainParameter chainId);
    public Task<string> AddEthereumChainAsync(AddEthereumChainParameter addEthereumChainParameter);

    // Static Helpers
    public static ConnectOptions GetDefaultConnectOptions();
    public static ConnectOptions GetDefaultConnectOptions(params long[] optionalEIP155chainIds);
    public static ConnectOptions GetDefaultConnectOptions(params string[] optionalEIP155chainIds);
    public static string GetEIP155ChainId(long chainId);
    public static long GetChainIdFromEip155(string chainId);
    public static string[] GetEIP155ChainIds(params long[] chainIds);

    // Constants
    public const string MAINNET = "eip155:1";
    public static readonly string[] DEFAULT_CHAINS = { MAINNET };
}
```

### NethereumWalletConnectHostProvider

Implementation of IEthereumHostProvider for WalletConnect.

```csharp
public class NethereumWalletConnectHostProvider : IEthereumHostProvider
{
    // Constructors
    public NethereumWalletConnectHostProvider(
        NethereumWalletConnectService walletConnectService,
        IClient client = null);

    public NethereumWalletConnectHostProvider(
        NethereumWalletConnectService walletConnectService,
        string url,
        AuthenticationHeaderValue authHeaderValue = null,
        JsonSerializerSettings jsonSerializerSettings = null,
        HttpClientHandler httpClientHandler = null,
        ILogger log = null);

    // Properties
    public static NethereumWalletConnectHostProvider Current { get; }
    public string Name { get; } // "WalletConnect"
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
    public Task<IWeb3> GetWeb3Async();
    public Task<string> EnableProviderAsync();
    public Task<string> GetProviderSelectedAccountAsync();
    public Task<string> SignMessageAsync(string message);
    public Task ChangeSelectedAccountAsync(string selectedAccount);
    public Task ChangeSelectedNetworkAsync(long chainId);
}
```

### NethereumWalletConnectInterceptor

Request interceptor for routing methods through WalletConnect.

```csharp
public class NethereumWalletConnectInterceptor : RequestInterceptor
{
    // Constructor
    public NethereumWalletConnectInterceptor(INethereumWalletConnectService walletConnectService);
    public NethereumWalletConnectInterceptor(WalletConnectSignClient walletConnectSignClient);

    // Properties
    public static List<string> SigningWalletTransactionsMethods { get; protected set; }
    public string SelectedAccount { get; internal set; }

    // Intercepted Methods (automatically routed through wallet)
    // - eth_sendTransaction
    // - eth_sign
    // - personal_sign
    // - eth_signTypedData_v4
    // - wallet_switchEthereumChain
    // - wallet_addEthereumChain
}
```

### WalletConnectConnectedSession

Session information for active WalletConnect connection.

```csharp
public class WalletConnectConnectedSession
{
    public SessionStruct Session { get; set; }
    public string Address { get; set; }
    public string ChainId { get; set; }
}
```

## Important Notes

### WalletConnect Cloud Project ID Required

You must sign up for a free Project ID at https://cloud.walletconnect.com. This is required for the WalletConnect relay network.

### Event Subscription Timing

Create NethereumWalletConnectHostProvider IMMEDIATELY after creating the service to ensure event subscriptions are set up before connection approval:

```csharp
walletConnectService = new NethereumWalletConnectService(client);
// Create provider immediately - DO NOT DELAY
walletConnectHostProvider = new NethereumWalletConnectHostProvider(walletConnectService);
```

### Connection Flow is Async

The connection flow requires user interaction:

1. Display QR code to user
2. User scans with mobile wallet
3. User approves connection in wallet
4. WaitForConnectionApprovalAndGetSelectedAccountAsync completes

Do not block the UI thread during this process.

### RPC Endpoint Optional

If you don't provide a custom RPC endpoint, only signing methods will work. Queries (eth_call, eth_getBalance, etc.) will fail. For a complete dApp experience, provide an RPC endpoint:

```csharp
var provider = new NethereumWalletConnectHostProvider(
    walletConnectService,
    "https://mainnet.infura.io/v3/YOUR-KEY");
```

### Chain ID Format

WalletConnect uses CAIP-25 format for chain IDs: `eip155:{chainId}`

Use the helper methods to convert between formats:
- `GetEIP155ChainId(1)` → `"eip155:1"`
- `GetChainIdFromEip155("eip155:137")` → `137`

### Storage Considerations

The example uses `InMemoryStorage()` which loses session on app restart. For production, consider implementing persistent storage to restore sessions.

### Protocol Compatibility

WalletConnect v2 protocol is supported. Any mobile wallet application that implements the WalletConnect v2 protocol specification can connect via QR code pairing.

## Related Packages

### Dependencies
- **Nethereum.UI** - IEthereumHostProvider interface
- **Nethereum.Web3** - Web3 client and transaction management
- **WalletConnect.Core** - WalletConnect v2 core functionality
- **WalletConnect.Sign** - WalletConnect v2 sign protocol

### Similar Packages
- **Nethereum.Metamask** - Browser extension wallet integration
- **Nethereum.EIP6963WalletInterop** - EIP-6963 multi-wallet discovery
