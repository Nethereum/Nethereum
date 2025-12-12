# Nethereum.Signer.Trezor

TREZOR hardware wallet integration for Ethereum transaction and message signing with Nethereum.

## Overview

Nethereum.Signer.Trezor provides **external signing capability** for Ethereum transactions and messages using **TREZOR One, TREZOR Model T, and TREZOR Safe 3** hardware wallets. This allows secure transaction signing where private keys never leave the hardware device, with full support for **EIP-712 typed data signing** and interactive user confirmations.

**Key Features:**
- Sign transactions with TREZOR hardware wallet
- Support for Legacy and EIP-1559 (Type 2) transactions
- **Full EIP-712 typed data signing** with interactive confirmation
- Message signing (EIP-191) with device confirmation
- Cross-platform support (Windows HID, Linux/macOS LibUSB)
- PIN and passphrase protection
- Custom derivation paths
- Direct integration with ExternalAccount and Web3

**Use Cases:**
- Hardware wallet-based applications
- Secure custody solutions
- Multi-signature wallets with hardware signer
- DApp integrations requiring TREZOR support
- EIP-712 signing (permits, meta-transactions, DAO voting)
- Cold storage transaction signing

## Installation

```bash
dotnet add package Nethereum.Signer.Trezor
```

**Platform-Specific Setup:**
- **Windows**: Works with USB HID out of the box
- **Linux**: Requires `libusb` and udev rules
- **macOS**: Requires `libusb` (install via Homebrew: `brew install libusb`)

## Dependencies

**External:**
- **Device.Net** (v4.3.0-beta) - Cross-platform USB device communication
- **Device.Net.LibUsb** (v4.3.0-beta) - LibUSB support for Linux/macOS
- **Hardwarewallets.Net** (v1.2.0) - Hardware wallet abstractions
- **Hid.Net** (v4.3.0-beta) - HID device support
- **Usb.Net** (v4.3.0-beta) - USB device support
- **protobuf-net** (v3.2.52) - Protocol Buffers for TREZOR messages
- **protobuf-net.Reflection** (v3.2.52) - Protocol Buffers reflection support

**Note:** TREZOR communication protocol implementation is built internally. The external Trezor.Net library is no longer used.

**Nethereum:**
- **Nethereum.Accounts** - Account and transaction management (includes Nethereum.Signer, Nethereum.Signer.EIP712)

## Quick Start

```csharp
using Nethereum.Signer.Trezor;
using Nethereum.Web3.Accounts;
using Microsoft.Extensions.Logging;

// Create logger factory
var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

// Create prompt handler for PIN/passphrase
var promptHandler = new ConsoleTrezorPromptHandler();

// Initialize TREZOR connection (Windows)
var trezorBroker = NethereumTrezorManagerBrokerFactory.Create(
    promptHandler,
    loggerFactory
);

// Wait for TREZOR device
var trezorManager = await trezorBroker.WaitForFirstDeviceAsync();

// Create external signer (account index 0)
var trezorSigner = new TrezorExternalSigner(trezorManager, index: 0);

// Create external account
var account = new ExternalAccount(trezorSigner, chainId: 1);
await account.InitialiseAsync();

// Use with Web3
var web3 = new Web3.Web3(account, "https://mainnet.infura.io/v3/YOUR-PROJECT-ID");

Console.WriteLine($"TREZOR Address: {account.Address}");
```

## Important: TREZOR Device Setup

Before using this library:

1. **Connect TREZOR** device via USB
2. **Enter PIN** when prompted (on computer keyboard, not device)
3. **Enter passphrase** if enabled (hidden wallet feature)
4. Device must remain **connected and unlocked** during signing
5. User must **physically confirm** each transaction on device screen

## Usage Examples

### Example 1: Connect to TREZOR and Get Address

```csharp
using Nethereum.Signer.Trezor;
using Nethereum.Signer.Trezor.Abstractions;
using Nethereum.Web3.Accounts;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

// Implement prompt handler for PIN/passphrase
public class ConsoleTrezorPromptHandler : ITrezorPromptHandler
{
    public Task<string> GetPinAsync()
    {
        Console.WriteLine("Enter PIN (use numpad layout 789/456/123):");
        return Task.FromResult(Console.ReadLine());
    }

    public Task<string> GetPassphraseAsync()
    {
        Console.WriteLine("Enter passphrase (leave empty if none):");
        return Task.FromResult(Console.ReadLine());
    }
}

// Create connection
var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var promptHandler = new ConsoleTrezorPromptHandler();

var trezorBroker = NethereumTrezorManagerBrokerFactory.Create(
    promptHandler,
    loggerFactory,
    pollInterval: 2000
);

Console.WriteLine("Waiting for TREZOR device...");
var trezorManager = await trezorBroker.WaitForFirstDeviceAsync();
Console.WriteLine("TREZOR connected!");

// Create signer for account 0 (m/44'/60'/0'/0/0)
var trezorSigner = new TrezorExternalSigner(
    trezorManager,
    index: 0
);

var account = new ExternalAccount(trezorSigner, chainId: 1);
await account.InitialiseAsync();

Console.WriteLine($"Address: {account.Address}");
// Matches TREZOR Suite "Ethereum #1" account
```

### Example 2: Sign and Send Transaction

```csharp
using Nethereum.Signer.Trezor;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Hex.HexTypes;

// Connect to TREZOR (see Example 1)
var trezorBroker = NethereumTrezorManagerBrokerFactory.Create(promptHandler, loggerFactory);
var trezorManager = await trezorBroker.WaitForFirstDeviceAsync();

var externalAccount = new ExternalAccount(
    new TrezorExternalSigner(trezorManager, index: 0),
    chainId: 1
);
await externalAccount.InitialiseAsync();

// Create Web3 instance
var web3 = new Web3(externalAccount, "https://mainnet.infura.io/v3/YOUR-PROJECT-ID");

// Create transaction
var transactionInput = new TransactionInput
{
    From = externalAccount.Address,
    To = "0x742d35Cc6634C0532925a3b844Bc454e4438f44e",
    Value = new HexBigInteger(1000000000000000), // 0.001 ETH
    Gas = new HexBigInteger(21000),
    GasPrice = new HexBigInteger(20000000000) // 20 gwei
};

// User must approve on TREZOR device
Console.WriteLine("Approve transaction on TREZOR device...");
var receipt = await web3.Eth.TransactionManager
    .SendTransactionAndWaitForReceiptAsync(transactionInput);

Console.WriteLine($"Transaction mined! Hash: {receipt.TransactionHash}");
```

### Example 3: Sign EIP-1559 Transaction

```csharp
using Nethereum.Signer.Trezor;
using Nethereum.Web3.Accounts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Hex.HexTypes;

var trezorBroker = NethereumTrezorManagerBrokerFactory.Create(promptHandler, loggerFactory);
var trezorManager = await trezorBroker.WaitForFirstDeviceAsync();

var externalAccount = new ExternalAccount(
    new TrezorExternalSigner(trezorManager, index: 0),
    chainId: 1
);
await externalAccount.InitialiseAsync();

// EIP-1559 transaction with priority fee
var transactionInput = new TransactionInput
{
    From = externalAccount.Address,
    To = "0x742d35Cc6634C0532925a3b844Bc454e4438f44e",
    Value = new HexBigInteger(1000000000000000), // 0.001 ETH
    Gas = new HexBigInteger(21000),
    MaxFeePerGas = new HexBigInteger(50000000000), // 50 gwei
    MaxPriorityFeePerGas = new HexBigInteger(2000000000) // 2 gwei (tip)
};

Console.WriteLine("Approve EIP-1559 transaction on TREZOR...");
var signedTx = await externalAccount.TransactionManager
    .SignTransactionAsync(transactionInput);

Console.WriteLine($"Signed EIP-1559 TX: {signedTx}");
```

### Example 4: Sign Contract Function Call

```csharp
using Nethereum.Signer.Trezor;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.Contracts;
using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Numerics;

// ERC-20 Transfer function
[Function("transfer", "bool")]
public class TransferFunction : FunctionMessage
{
    [Parameter("address", "_to", 1)]
    public string To { get; set; }

    [Parameter("uint256", "_value", 2)]
    public BigInteger Value { get; set; }
}

// Connect to TREZOR
var trezorBroker = NethereumTrezorManagerBrokerFactory.Create(promptHandler, loggerFactory);
var trezorManager = await trezorBroker.WaitForFirstDeviceAsync();

var externalAccount = new ExternalAccount(
    new TrezorExternalSigner(trezorManager, index: 0),
    chainId: 1
);
await externalAccount.InitialiseAsync();

var web3 = new Web3(externalAccount, "https://mainnet.infura.io/v3/YOUR-PROJECT-ID");

// Create transfer function
var transfer = new TransferFunction
{
    To = "0x742d35Cc6634C0532925a3b844Bc454e4438f44e",
    Value = BigInteger.Parse("1000000000000000000"), // 1 token
    FromAddress = externalAccount.Address
};

// Get contract
var tokenAddress = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48"; // USDC
var contract = web3.Eth.GetContract(
    "[{\"constant\":false,\"inputs\":[{\"name\":\"_to\",\"type\":\"address\"},{\"name\":\"_value\",\"type\":\"uint256\"}],\"name\":\"transfer\",\"outputs\":[{\"name\":\"\",\"type\":\"bool\"}],\"type\":\"function\"}]",
    tokenAddress
);

// User approves contract data on TREZOR
Console.WriteLine("Approve contract call on TREZOR...");
var transferHandler = contract.GetFunction("transfer");
var receipt = await transferHandler.SendTransactionAndWaitForReceiptAsync(
    externalAccount.Address,
    null,
    null,
    transfer.To,
    transfer.Value
);

Console.WriteLine($"Transfer complete! Hash: {receipt.TransactionHash}");
```

### Example 5: Sign EIP-712 Typed Data (ERC-2612 Permit)

```csharp
using Nethereum.Signer.Trezor;
using Nethereum.Signer.EIP712;
using Nethereum.ABI.EIP712;
using Nethereum.Hex.HexConvertors.Extensions;
using System.Numerics;

// ERC-2612 Permit domain and message
public class EIP712Domain
{
    public string Name { get; set; }
    public string Version { get; set; }
    public BigInteger ChainId { get; set; }
    public string VerifyingContract { get; set; }
}

public class Permit
{
    public string Owner { get; set; }
    public string Spender { get; set; }
    public BigInteger Value { get; set; }
    public BigInteger Nonce { get; set; }
    public BigInteger Deadline { get; set; }
}

// Connect to TREZOR
var trezorBroker = NethereumTrezorManagerBrokerFactory.Create(promptHandler, loggerFactory);
var trezorManager = await trezorBroker.WaitForFirstDeviceAsync();

var trezorSigner = new TrezorExternalSigner(trezorManager, index: 0);
await trezorSigner.GetAddressAsync();

// Create typed data
var typedData = new TypedData<EIP712Domain>
{
    Domain = new EIP712Domain
    {
        Name = "USD Coin",
        Version = "2",
        ChainId = 1,
        VerifyingContract = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48"
    },
    Types = new Dictionary<string, MemberDescription[]>
    {
        ["EIP712Domain"] = new[]
        {
            new MemberDescription { Name = "name", Type = "string" },
            new MemberDescription { Name = "version", Type = "string" },
            new MemberDescription { Name = "chainId", Type = "uint256" },
            new MemberDescription { Name = "verifyingContract", Type = "address" }
        },
        ["Permit"] = new[]
        {
            new MemberDescription { Name = "owner", Type = "address" },
            new MemberDescription { Name = "spender", Type = "address" },
            new MemberDescription { Name = "value", Type = "uint256" },
            new MemberDescription { Name = "nonce", Type = "uint256" },
            new MemberDescription { Name = "deadline", Type = "uint256" }
        }
    },
    PrimaryType = "Permit",
    Message = new[]
    {
        new MemberValue { TypeName = "address", Value = await trezorSigner.GetAddressAsync() },
        new MemberValue { TypeName = "address", Value = "0x1234567890123456789012345678901234567890" },
        new MemberValue { TypeName = "uint256", Value = new BigInteger(1000000000000) },
        new MemberValue { TypeName = "uint256", Value = BigInteger.Zero },
        new MemberValue { TypeName = "uint256", Value = new BigInteger(1735689600) }
    }
};

// TREZOR shows full typed data on device for user confirmation
Console.WriteLine("Review and approve typed data on TREZOR...");
var signature = await trezorSigner.SignTypedDataAsync(typedData);

Console.WriteLine($"EIP-712 Signature: {signature.CreateStringSignature()}");
// User sees: "Permit owner to spender" with all details on device
```

### Example 6: Sign Personal Message

```csharp
using Nethereum.Signer.Trezor;
using Nethereum.Signer;
using Nethereum.Hex.HexConvertors.Extensions;
using System.Text;

var trezorBroker = NethereumTrezorManagerBrokerFactory.Create(promptHandler, loggerFactory);
var trezorManager = await trezorBroker.WaitForFirstDeviceAsync();

var trezorSigner = new TrezorExternalSigner(trezorManager, index: 0);
var address = await trezorSigner.GetAddressAsync();

// Message to sign
string message = "Sign this message to authenticate with our dApp!";
byte[] messageBytes = Encoding.UTF8.GetBytes(message);

// TREZOR displays message on device for confirmation
Console.WriteLine("Review and approve message on TREZOR...");
var signature = await trezorSigner.SignEthereumMessageAsync(messageBytes);

Console.WriteLine($"Message: {message}");
Console.WriteLine($"Signature: {signature.CreateStringSignature()}");

// Verify signature
var messageSigner = new EthereumMessageSigner();
var recoveredAddress = messageSigner.EncodeUTF8AndEcRecover(message, signature.CreateStringSignature());

Console.WriteLine($"Signer address: {address}");
Console.WriteLine($"Recovered address: {recoveredAddress}");
Console.WriteLine($"Match: {address.Equals(recoveredAddress, StringComparison.OrdinalIgnoreCase)}");
```

### Example 7: Multiple Accounts from Same TREZOR

```csharp
using Nethereum.Signer.Trezor;
using Nethereum.Web3.Accounts;

var trezorBroker = NethereumTrezorManagerBrokerFactory.Create(promptHandler, loggerFactory);
var trezorManager = await trezorBroker.WaitForFirstDeviceAsync();

// Account 0 (Ethereum #1 in TREZOR Suite)
var account0 = new ExternalAccount(
    new TrezorExternalSigner(trezorManager, index: 0),
    chainId: 1
);
await account0.InitialiseAsync();
Console.WriteLine($"Account 0: {account0.Address}");

// Account 1 (Ethereum #2 in TREZOR Suite)
var account1 = new ExternalAccount(
    new TrezorExternalSigner(trezorManager, index: 1),
    chainId: 1
);
await account1.InitialiseAsync();
Console.WriteLine($"Account 1: {account1.Address}");

// Account 5 (Ethereum #6 in TREZOR Suite)
var account5 = new ExternalAccount(
    new TrezorExternalSigner(trezorManager, index: 5),
    chainId: 1
);
await account5.InitialiseAsync();
Console.WriteLine($"Account 5: {account5.Address}");
```

### Example 8: Custom Derivation Path

```csharp
using Nethereum.Signer.Trezor;
using Nethereum.Web3.Accounts;

var trezorBroker = NethereumTrezorManagerBrokerFactory.Create(promptHandler, loggerFactory);
var trezorManager = await trezorBroker.WaitForFirstDeviceAsync();

// Custom path for account 1 instead of 0
string customPath = "m/44'/60'/1'/0"; // Will derive m/44'/60'/1'/0/3
var trezorSigner = new TrezorExternalSigner(
    trezorManager,
    customPath: customPath,
    index: 3
);

var account = new ExternalAccount(trezorSigner, chainId: 1);
await account.InitialiseAsync();

Console.WriteLine($"Custom path address: {account.Address}");
```

### Example 9: Cross-Platform Setup (Linux/macOS with LibUSB)

```csharp
using Nethereum.Signer.Trezor;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var promptHandler = new ConsoleTrezorPromptHandler();

// Auto-detect platform and use appropriate device factory
var trezorBroker = NethereumTrezorManagerBrokerFactory.CreateDefault(
    promptHandler,
    loggerFactory,
    platformProviders: new NethereumTrezorManagerBrokerFactory.PlatformDeviceFactoryProviders
    {
        WindowsProvider = new WindowsHidUsbDeviceFactoryProvider(),
        LinuxProvider = new LibUsbDeviceFactoryProvider(),
        MacProvider = new LibUsbDeviceFactoryProvider()
    }
);

Console.WriteLine($"Platform: {RuntimeInformation.OSDescription}");
Console.WriteLine("Waiting for TREZOR device...");

var trezorManager = await trezorBroker.WaitForFirstDeviceAsync();
Console.WriteLine("TREZOR connected!");

// Linux users: Ensure udev rules are configured
// Create file: /etc/udev/rules.d/51-trezor.rules
// Content: SUBSYSTEM=="usb", ATTR{idVendor}=="534c", MODE="0660", GROUP="plugdev"
// Run: sudo udevadm control --reload-rules && sudo udevadm trigger
```

## API Reference

### TrezorExternalSigner

External signer implementation for TREZOR hardware wallets.

```csharp
public class TrezorExternalSigner : EthExternalSignerBase
{
    // Constructors
    public TrezorExternalSigner(TrezorManagerBase<MessageType> trezorManager, uint index, string knownAddress = null, ILogger<TrezorExternalSigner> logger = null);
    public TrezorExternalSigner(TrezorManagerBase<MessageType> trezorManager, string customPath, uint index, string knownAddress = null, ILogger<TrezorExternalSigner> logger = null);

    // Properties
    public TrezorManagerBase<MessageType> TrezorManager { get; }
    public override bool CalculatesV { get; } = true;
    public override ExternalSignerTransactionFormat ExternalSignerTransactionFormat { get; } = ExternalSignerTransactionFormat.Transaction;
    public override bool Supported1559 { get; } = true;

    // Methods
    public override Task<string> GetAddressAsync();
    public Task<string> RefreshAddressFromDeviceAsync();
    public override Task<EthECDSASignature> SignEthereumMessageAsync(byte[] rawBytes);
    public override Task<EthECDSASignature> SignTypedDataAsync<TDomain>(TypedData<TDomain> typedData);
    public Task<EthECDSASignature> SignTypedDataAsync<TDomain>(TypedData<TDomain> typedData, byte[] encodedNetwork);
    public Task<EthECDSASignature> SignTypedDataHashAsync(byte[] domainSeparatorHash, byte[] messageHash, byte[] encodedNetwork, byte[] typedDataHash);
    public override Task SignAsync(LegacyTransactionChainId transaction);
    public override Task SignAsync(Transaction1559 transaction);
    public uint[] GetPath();
}
```

### NethereumTrezorManagerBrokerFactory

Factory for creating TREZOR device connections.

```csharp
public class NethereumTrezorManagerBrokerFactory
{
    public static NethereumTrezorManagerBroker Create(ITrezorPromptHandler promptHandler, ILoggerFactory loggerFactory, int? pollInterval = 2000);
    public static NethereumTrezorManagerBroker CreateWindowsHidUsb(EnterPinArgs enterPinCallback, EnterPinArgs enterPassPhrase, ILoggerFactory loggerFactory, int? pollInterval = 2000);
    public static NethereumTrezorManagerBroker CreateDefault(ITrezorPromptHandler promptHandler, ILoggerFactory loggerFactory, PlatformDeviceFactoryProviders platformProviders = null, int? pollInterval = 2000);
}
```

### ITrezorPromptHandler

Interface for handling PIN and passphrase prompts.

```csharp
public interface ITrezorPromptHandler
{
    Task<string> GetPinAsync();
    Task<string> GetPassphraseAsync();
}
```

## Important Notes

### PIN Entry

```
TREZOR PIN is entered using numpad layout:

7 8 9
4 5 6
1 2 3

Device shows positions, you enter numbers on computer.
Example: Device shows [• • •], you type "789" for top row.
```

### Passphrase (Hidden Wallet)

```csharp
// Same seed + different passphrase = different addresses
// Passphrase acts as 25th word (like Ledger/HDWallet)

// No passphrase = standard wallet
// "secret123" = hidden wallet #1
// "different" = hidden wallet #2
```

Enables plausible deniability - can reveal standard wallet under duress.

### Transaction Types Supported

| Type | Supported | Notes |
|------|-----------|-------|
| Legacy | Yes | EIP-155 with chain ID (requires chain ID, no raw Legacy) |
| EIP-1559 (Type 2) | Yes | MaxFeePerGas, MaxPriorityFeePerGas |
| EIP-2930 (Type 1) | No | Access lists not supported |
| EIP-7702 (Type 4) | No | Not yet implemented |

### EIP-712 Signing

TREZOR provides **interactive EIP-712 signing** - the device displays:
- Domain information (name, version, contract address)
- Full message structure with field names and values
- User reviews ALL data before signing

This is more secure than "blind signing" hash-only approaches.

### Device Compatibility

Supports all TREZOR devices with latest firmware. All Ethereum features (transactions, EIP-712, message signing) work across all models.

### Platform Support

| Platform | Connection Method | Setup Required |
|----------|-------------------|----------------|
| **Windows** | USB HID | None (plug and play) |
| **Linux** | LibUSB | udev rules required |
| **macOS** | LibUSB | `brew install libusb` |
| **Android** | Custom provider | Platform-specific implementation |

### Error Handling

```csharp
using hw.trezor.messages.common;

try
{
    var signature = await externalAccount.TransactionManager
        .SignTransactionAsync(transactionInput);
}
catch (FailureException<Failure> ex)
{
    // User rejected on device, or device error
    Console.WriteLine($"TREZOR error: {ex.Failure?.Message}");
}
catch (Exception ex)
{
    Console.WriteLine($"Signing error: {ex.Message}");
}
```

Common errors:
- Device not connected
- User rejected transaction
- PIN incorrect
- Passphrase incorrect
- Device timeout

### Logging

```csharp
using Microsoft.Extensions.Logging;

var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.SetMinimumLevel(LogLevel.Debug);
    builder.AddConsole();
});

// Detailed logs for debugging TREZOR communication
var trezorBroker = NethereumTrezorManagerBrokerFactory.Create(
    promptHandler,
    loggerFactory
);
```

## Related Packages

### Used By (Consumers)
- DApp backends requiring TREZOR support
- Custody solutions
- Multi-signature wallets
- Hardware wallet integrations

### Dependencies
- **Nethereum.Accounts** - Account management
- **Nethereum.Signer.EIP712** - EIP-712 typed data
- **Nethereum.Web3** - Web3 integration
- **Device.Net** / **Hardwarewallets.Net** - USB device communication
- **protobuf-net** - Protocol Buffers serialization

### Alternatives
- **Nethereum.Signer.Ledger** - Ledger hardware wallet integration
- **Nethereum.HDWallet** - Software HD wallets

## Additional Resources

- [TREZOR Documentation](https://docs.trezor.io/)
- [TREZOR Firmware](https://github.com/trezor/trezor-firmware)
- [EIP-712 Specification](https://eips.ethereum.org/EIPS/eip-712)
- [BIP44 Derivation Paths](https://github.com/bitcoin/bips/blob/master/bip-0044.mediawiki)
- [Nethereum Documentation](http://docs.nethereum.com/)

## Security Considerations

- Private keys **never leave** the TREZOR device
- All signing happens on the secure element chip
- Users must physically confirm each transaction on device screen
- Device displays full transaction details for verification
- **EIP-712 data is displayed in human-readable format** on device
- No software can extract private keys from device
- Passphrase adds extra security layer (25th word)
