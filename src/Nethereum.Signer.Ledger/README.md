# Nethereum.Signer.Ledger

Ledger hardware wallet integration for Ethereum transaction signing with Nethereum.

## Overview

Nethereum.Signer.Ledger provides **external signing capability** for Ethereum transactions and messages using **Ledger Nano S, Nano S Plus, and Nano X** hardware wallets. This allows secure transaction signing where private keys never leave the hardware device.

**Key Features:**
- Sign transactions with Ledger hardware wallet
- Support for Legacy and EIP-1559 (Type 2) transactions
- Contract deployment signing with data
- Message signing (EIP-191)
- Two derivation paths: `m/44'/60'/0'/0/x` (default) and `m/44'/60'/0'/x` (Ledger legacy)
- USB HID communication (Windows, Linux, macOS)
- Direct integration with ExternalAccount and Web3

**Use Cases:**
- Hardware wallet-based applications
- Secure custody solutions
- Multi-signature wallets with hardware signer
- DApp integrations requiring hardware wallet support
- Cold storage transaction signing

## Installation

```bash
dotnet add package Nethereum.Signer.Ledger
```

**Platform-Specific Notes:**
- **Windows**: Works with USB HID out of the box
- **Linux**: May require udev rules for USB device access
- **macOS**: Works with USB HID out of the box

## Dependencies

**External:**
- **Ledger.Net** (v4.0.0) - Ledger hardware wallet communication library (includes Device.Net for USB/HID communication)

**Nethereum:**
- **Nethereum.Accounts** - Account and transaction management (includes Nethereum.Signer)

## Quick Start

```csharp
using Nethereum.Ledger;
using Nethereum.Web3.Accounts;
using Ledger.Net;

// Initialize Ledger connection
var ledgerManagerBroker = NethereumLedgerManagerBrokerFactory.CreateWindowsHidUsb();
var ledgerManager = (LedgerManager)await ledgerManagerBroker.WaitForFirstDeviceAsync();

// Create external signer with Ledger (account index 0)
var ledgerSigner = new LedgerExternalSigner(ledgerManager, 0);

// Create external account
var account = new ExternalAccount(ledgerSigner, chainId: 1);
await account.InitialiseAsync();

// Use with Web3
var web3 = new Web3.Web3(account, "https://mainnet.infura.io/v3/YOUR-PROJECT-ID");

// Address is retrieved from Ledger
Console.WriteLine("Address: " + account.Address);
```

## Important: Ledger Device Setup

Before using this library, ensure your Ledger device:

1. Has the **Ethereum app installed** (via Ledger Live)
2. Ethereum app is **opened** on the device
3. **Contract data** setting is **ENABLED** in Ethereum app settings (required for contract interactions)
4. Device is **unlocked** with PIN
5. **Blind signing** may need to be enabled for complex transactions

## Usage Examples

### Example 1: Connect to Ledger and Get Address

```csharp
using Nethereum.Ledger;
using Nethereum.Web3.Accounts;
using Ledger.Net;

// Create device broker for Windows USB HID
var ledgerManagerBroker = NethereumLedgerManagerBrokerFactory.CreateWindowsHidUsb();

// Wait for Ledger device (user must connect and unlock)
Console.WriteLine("Connect your Ledger and open the Ethereum app...");
var ledgerManager = (LedgerManager)await ledgerManagerBroker.WaitForFirstDeviceAsync();
Console.WriteLine("Ledger connected!");

// Create external signer (account 0, default path m/44'/60'/0'/0/0)
var ledgerSigner = new LedgerExternalSigner(ledgerManager, index: 0);

// Create external account
var account = new ExternalAccount(ledgerSigner, chainId: 1);
await account.InitialiseAsync();

Console.WriteLine($"Ledger Address: {account.Address}");
// Address matches Ledger Live "Ethereum 1" account
```

### Example 2: Sign and Send Simple Transfer (Real Test Example)

```csharp
using Nethereum.Ledger;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Hex.HexTypes;
using Ledger.Net;

// Connect to Ledger
var ledgerManagerBroker = NethereumLedgerManagerBrokerFactory.CreateWindowsHidUsb();
var ledgerManager = (LedgerManager)await ledgerManagerBroker.WaitForFirstDeviceAsync();

// Account 0 with default derivation path
var externalAccount = new ExternalAccount(
    new LedgerExternalSigner(ledgerManager, 0, legacyPath: false),
    chainId: 1
);
await externalAccount.InitialiseAsync();

// Initialize Web3 with RPC client
var web3 = new Web3(externalAccount);
web3.Client = new JsonRpc.Client.RpcClient(new Uri("https://mainnet.infura.io/v3/YOUR-PROJECT-ID"));

// Create transaction
var transactionInput = new TransactionInput
{
    From = externalAccount.Address,
    To = "0x12890d2cce102216644c59daE5baed380d848301",
    Value = new HexBigInteger(100), // 100 wei
    Gas = new HexBigInteger(21000),
    GasPrice = new HexBigInteger(20000000000), // 20 gwei
    Nonce = new HexBigInteger(1)
};

// User must approve on Ledger device
Console.WriteLine("Approve transaction on Ledger...");
var signedTransaction = await externalAccount.TransactionManager
    .SignTransactionAsync(transactionInput);

Console.WriteLine($"Signed: {signedTransaction}");

// Send to network
var txHash = await web3.Eth.Transactions.SendRawTransaction.SendRequestAsync(signedTransaction);
Console.WriteLine($"Transaction hash: {txHash}");
```

### Example 3: Sign EIP-1559 Transaction (Type 2)

```csharp
using Nethereum.Ledger;
using Nethereum.Web3.Accounts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Hex.HexTypes;
using Ledger.Net;

var ledgerManagerBroker = NethereumLedgerManagerBrokerFactory.CreateWindowsHidUsb();
var ledgerManager = (LedgerManager)await ledgerManagerBroker.WaitForFirstDeviceAsync();

var externalAccount = new ExternalAccount(
    new LedgerExternalSigner(ledgerManager, 0),
    chainId: 1
);
await externalAccount.InitialiseAsync();

// EIP-1559 transaction with MaxFeePerGas and MaxPriorityFeePerGas
var transactionInput = new TransactionInput
{
    From = externalAccount.Address,
    To = "0x742d35Cc6634C0532925a3b844Bc454e4438f44e",
    Value = new HexBigInteger(1000000000000000), // 0.001 ETH
    Gas = new HexBigInteger(21000),
    MaxFeePerGas = new HexBigInteger(50000000000), // 50 gwei
    MaxPriorityFeePerGas = new HexBigInteger(2000000000), // 2 gwei (tip)
    Nonce = new HexBigInteger(5)
};

Console.WriteLine("Approve EIP-1559 transaction on Ledger...");
var signedTx = await externalAccount.TransactionManager.SignTransactionAsync(transactionInput);

Console.WriteLine($"Signed EIP-1559 Transaction: {signedTx}");
```

### Example 4: Sign Contract Function Call (Real Test Example)

```csharp
using Nethereum.Ledger;
using Nethereum.Web3.Accounts;
using Nethereum.Contracts;
using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Numerics;
using Ledger.Net;

// ERC-20 Transfer function
[Function("transfer", "bool")]
public class TransferFunction : FunctionMessage
{
    [Parameter("address", "_to", 1)]
    public string To { get; set; }

    [Parameter("uint256", "_value", 2)]
    public BigInteger Value { get; set; }
}

// Connect to Ledger
var ledgerManagerBroker = NethereumLedgerManagerBrokerFactory.CreateWindowsHidUsb();
var ledgerManager = (LedgerManager)await ledgerManagerBroker.WaitForFirstDeviceAsync();

var externalAccount = new ExternalAccount(
    new LedgerExternalSigner(ledgerManager, 0),
    chainId: 1
);
await externalAccount.InitialiseAsync();

// Create contract function call
var transfer = new TransferFunction
{
    To = "0x12890d2cce102216644c59daE5baed380d848301",
    FromAddress = externalAccount.Address,
    Value = 1000000000000000000, // 1 token (18 decimals)
    Nonce = 1,
    GasPrice = 100,
    Gas = 100000
};

var contractAddress = "0x12890d2cce102216644c59daE5baed380d84830c";
var transactionInput = transfer.CreateTransactionInput(contractAddress);

// IMPORTANT: "Contract data" must be enabled in Ledger Ethereum app settings
Console.WriteLine("Approve contract interaction on Ledger...");
Console.WriteLine("(Ensure 'Contract data' is enabled in Ledger settings)");

var signedTransaction = await externalAccount.TransactionManager
    .SignTransactionAsync(transactionInput);

Console.WriteLine($"Signed contract call: {signedTransaction}");
```

### Example 5: Deploy Contract with Ledger (Real Test Example)

```csharp
using Nethereum.Ledger;
using Nethereum.Web3.Accounts;
using Nethereum.Contracts;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Ledger.Net;

// Contract deployment message
public class StandardTokenDeployment : ContractDeploymentMessage
{
    public static string BYTECODE = "0x606060405260405160208061..."; // Full bytecode

    public StandardTokenDeployment() : base(BYTECODE) { }

    [Parameter("uint256", "totalSupply")]
    public int TotalSupply { get; set; }
}

// Connect to Ledger
var ledgerManagerBroker = NethereumLedgerManagerBrokerFactory.CreateWindowsHidUsb();
var ledgerManager = (LedgerManager)await ledgerManagerBroker.WaitForFirstDeviceAsync();

var externalAccount = new ExternalAccount(
    new LedgerExternalSigner(ledgerManager, 0),
    chainId: 1
);
await externalAccount.InitialiseAsync();

// Create deployment transaction
var deployment = new StandardTokenDeployment
{
    FromAddress = externalAccount.Address,
    TotalSupply = 1000000,
    Nonce = 1,
    GasPrice = 20000000000,
    Gas = 3000000
};

var transactionInput = deployment.CreateTransactionInput();

// CRITICAL: Must enable "Contract data" and possibly "Blind signing" on Ledger
Console.WriteLine("Approve contract deployment on Ledger...");
Console.WriteLine("This may require 'Blind signing' enabled for large bytecode");

externalAccount.TransactionManager.UseLegacyAsDefault = true; // Use legacy for deployment
var signedDeployment = await externalAccount.TransactionManager
    .SignTransactionAsync(transactionInput);

Console.WriteLine($"Signed deployment: {signedDeployment}");
```

### Example 6: Use Legacy Derivation Path (Ledger Live Pre-2022)

```csharp
using Nethereum.Ledger;
using Nethereum.Web3.Accounts;
using Ledger.Net;

var ledgerManagerBroker = NethereumLedgerManagerBrokerFactory.CreateWindowsHidUsb();
var ledgerManager = (LedgerManager)await ledgerManagerBroker.WaitForFirstDeviceAsync();

// Legacy path: m/44'/60'/0'/x (old Ledger Live format)
var ledgerSignerLegacy = new LedgerExternalSigner(
    ledgerManager,
    index: 0,
    legacyPath: true // Use m/44'/60'/0'/0 instead of m/44'/60'/0'/0/0
);

var account = new ExternalAccount(ledgerSignerLegacy, chainId: 1);
await account.InitialiseAsync();

Console.WriteLine($"Legacy path address: {account.Address}");
// This address matches old Ledger Live / MEW Ledger mode
```

### Example 7: Multiple Accounts from Same Ledger

```csharp
using Nethereum.Ledger;
using Nethereum.Web3.Accounts;
using Ledger.Net;

var ledgerManagerBroker = NethereumLedgerManagerBrokerFactory.CreateWindowsHidUsb();
var ledgerManager = (LedgerManager)await ledgerManagerBroker.WaitForFirstDeviceAsync();

// Account 0 (Ethereum 1 in Ledger Live)
var account0 = new ExternalAccount(
    new LedgerExternalSigner(ledgerManager, 0),
    chainId: 1
);
await account0.InitialiseAsync();
Console.WriteLine($"Account 0: {account0.Address}");

// Account 1 (Ethereum 2 in Ledger Live)
var account1 = new ExternalAccount(
    new LedgerExternalSigner(ledgerManager, 1),
    chainId: 1
);
await account1.InitialiseAsync();
Console.WriteLine($"Account 1: {account1.Address}");

// Account 5 (Ethereum 6 in Ledger Live)
var account5 = new ExternalAccount(
    new LedgerExternalSigner(ledgerManager, 5),
    chainId: 1
);
await account5.InitialiseAsync();
Console.WriteLine($"Account 5: {account5.Address}");
```

### Example 8: Custom Derivation Path

```csharp
using Nethereum.Ledger;
using Nethereum.Web3.Accounts;
using Ledger.Net;

var ledgerManagerBroker = NethereumLedgerManagerBrokerFactory.CreateWindowsHidUsb();
var ledgerManager = (LedgerManager)await ledgerManagerBroker.WaitForFirstDeviceAsync();

// Custom BIP44 path (e.g., for testnet or custom derivation)
string customPath = "m/44'/60'/1'/0"; // Account 1 instead of 0
var ledgerSigner = new LedgerExternalSigner(
    ledgerManager,
    index: 3, // Will derive m/44'/60'/1'/0/3
    customPath: customPath
);

var account = new ExternalAccount(ledgerSigner, chainId: 1);
await account.InitialiseAsync();

Console.WriteLine($"Custom path address: {account.Address}");
```

### Example 9: Verify Signature Matches Standard Account (Real Test Example)

```csharp
using Nethereum.Ledger;
using Nethereum.Web3.Accounts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Hex.HexTypes;
using Ledger.Net;

// Known test account
var addressFrom = "0x07dCc60Ec5179f30ba30a2Ec25B683d5C5276025";
var privateKey = "0x32b28a67ec29294be914a356a9f439cf1fd8c56a400d897f75f46694fb06a9c9";

// Sign with regular account
var regularAccount = new Web3.Accounts.Account(privateKey, chainId: 1);
var transactionInput = new TransactionInput
{
    From = addressFrom,
    To = "0x12890d2cce102216644c59daE5baed380d848301",
    Value = new HexBigInteger(100),
    Gas = new HexBigInteger(21000),
    GasPrice = new HexBigInteger(20000000000),
    Nonce = new HexBigInteger(1)
};

var regularSignature = await regularAccount.TransactionManager
    .SignTransactionAsync(transactionInput);

// Sign with Ledger (assuming same account is on Ledger index 0)
var ledgerManagerBroker = NethereumLedgerManagerBrokerFactory.CreateWindowsHidUsb();
var ledgerManager = (LedgerManager)await ledgerManagerBroker.WaitForFirstDeviceAsync();

var ledgerAccount = new ExternalAccount(
    new LedgerExternalSigner(ledgerManager, 0),
    chainId: 1
);
await ledgerAccount.InitialiseAsync();

var ledgerSignature = await ledgerAccount.TransactionManager
    .SignTransactionAsync(transactionInput);

// Signatures should match
Console.WriteLine($"Regular: {regularSignature}");
Console.WriteLine($"Ledger:  {ledgerSignature}");
Console.WriteLine($"Match: {regularSignature == ledgerSignature}");
```

## API Reference

### LedgerExternalSigner

External signer implementation for Ledger hardware wallets.

```csharp
public class LedgerExternalSigner : EthExternalSignerBase
{
    // Constructors
    public LedgerExternalSigner(LedgerManager ledgerManager, uint index, bool legacyPath = false);
    public LedgerExternalSigner(LedgerManager ledgerManager, uint index, string customPath);

    // Properties
    public LedgerManager LedgerManager { get; }
    public byte[] CurrentPublicKey { get; set; }
    public override bool CalculatesV { get; protected set; } = false;
    public override ExternalSignerTransactionFormat ExternalSignerTransactionFormat { get; } = ExternalSignerTransactionFormat.RLP;
    public override bool Supported1559 { get; } = true;

    // Methods
    public byte[] GetPath();
    protected override Task<byte[]> GetPublicKeyAsync();
    protected override Task<ECDSASignature> SignExternallyAsync(byte[] hash);
    public override Task SignAsync(LegacyTransaction transaction);
    public override Task SignAsync(LegacyTransactionChainId transaction);
    public override Task SignAsync(Transaction1559 transaction);
}
```

### NethereumLedgerManagerBrokerFactory

Factory for creating Ledger device connections.

```csharp
public class NethereumLedgerManagerBrokerFactory
{
    public static LedgerManagerBroker CreateWindowsHidUsb();
}
```

### NethereumLedgerManagerFactory

Factory for creating Ledger manager instances.

```csharp
public class NethereumLedgerManagerFactory : ILedgerManagerFactory
{
    public IManagesLedger GetNewLedgerManager(IDevice ledgerHidDevice, ICoinUtility coinUtility, ErrorPromptDelegate errorPrompt);
}
```

## Important Notes

### Ledger Device Settings

```
CRITICAL: Before using, configure Ledger Ethereum app:

1. Open Ethereum app on device
2. Go to Settings (gear icon)
3. Enable "Contract data" (required for smart contracts)
4. Enable "Blind signing" (required for complex contracts/large data)
5. Enable "Debug data" (optional, for development)
```

Without these settings enabled, transactions may be rejected by the device.

### Derivation Paths

| Path | Format | Used By | Ledger Live Name |
|------|--------|---------|------------------|
| **Default** | `m/44'/60'/0'/0/x` | Current standard | "Ethereum 1", "Ethereum 2", etc. |
| **Legacy** | `m/44'/60'/0'/x` | Old Ledger Live (pre-2022) | Legacy accounts |

**Note:** If addresses don't match Ledger Live, you may need to use `legacyPath: true`.

### Transaction Types Supported

| Type | Supported | Notes |
|------|-----------|-------|
| Legacy | Yes | EIP-155 with chain ID |
| EIP-1559 (Type 2) | Yes | MaxFeePerGas, MaxPriorityFeePerGas |
| EIP-2930 (Type 1) | No | Not implemented |
| EIP-7702 (Type 4) | No | Not implemented |

### User Experience Considerations

```csharp
// Always inform user of required actions
Console.WriteLine("Please connect your Ledger device");
Console.WriteLine("1. Connect via USB");
Console.WriteLine("2. Enter PIN");
Console.WriteLine("3. Open Ethereum app");
Console.WriteLine("4. Keep device unlocked during signing");

var ledgerManager = await ledgerManagerBroker.WaitForFirstDeviceAsync();

Console.WriteLine("Ledger connected! Approve transaction on device...");
```

Users must:
1. Physically approve each transaction on device
2. Review transaction details on device screen
3. Keep device connected during entire signing process

### Error Handling

```csharp
using Ledger.Net.Exceptions;

try
{
    var signature = await externalAccount.TransactionManager
        .SignTransactionAsync(transactionInput);
}
catch (LedgerException ex)
{
    // Device not connected, app not open, or user rejected
    Console.WriteLine($"Ledger error: {ex.Message}");
}
catch (Exception ex)
{
    Console.WriteLine($"Signing error: {ex.Message}");
}
```

Common errors:
- Device not connected
- Ethereum app not opened
- User rejected transaction
- Contract data not enabled
- Transaction too large for device screen

### Chain ID Support

```csharp
// Ledger supports all chain IDs
var mainnetAccount = new ExternalAccount(ledgerSigner, chainId: 1);      // Ethereum
var polygonAccount = new ExternalAccount(ledgerSigner, chainId: 137);    // Polygon
var avalancheAccount = new ExternalAccount(ledgerSigner, chainId: 43114); // Avalanche
```

**Note:** For chainId > 109, Ledger hardware has specific handling. This is automatically managed by the library.

## Related Packages

### Used By (Consumers)
- DApp backends requiring hardware wallet support
- Custody solutions
- Multi-signature wallets
- Hardware wallet integrations

### Dependencies
- **Nethereum.Accounts** - Account management
- **Nethereum.Web3** - Web3 integration
- **Ledger.Net** - Ledger device communication

### Alternatives
- **Nethereum.Signer.Trezor** - TREZOR hardware wallet integration
- **Nethereum.HDWallet** - Software HD wallets

## Additional Resources

- [Ledger Ethereum App](https://github.com/LedgerHQ/app-ethereum)
- [Ledger Developer Portal](https://developers.ledger.com/)
- [BIP44 Derivation Paths](https://github.com/bitcoin/bips/blob/master/bip-0044.mediawiki)
- [Nethereum Documentation](http://docs.nethereum.com/)

## Security Considerations

- Private keys **never leave** the Ledger device
- All signing happens on the secure element chip
- Users must physically confirm each transaction
- Device screen shows transaction details for verification
- No software can extract private keys from device
