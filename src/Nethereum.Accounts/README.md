# Nethereum.Accounts

**Nethereum.Accounts** provides account management and transaction signing capabilities for Ethereum. It enables offline transaction signing, key management, and integration with external signers (hardware wallets, browser wallets, etc.).

## Features

- **Account** - Full control with private key (offline signing)
- **ManagedAccount** - Node-managed accounts (personal API)
- **ExternalAccount** - Hardware wallets, browser extensions, custom signers
- **ViewOnlyAccount** - Read-only account access (no signing)
- **Transaction Managers** - Automatic nonce management and transaction lifecycle
- **Message Signing** - EIP-191 personal_sign and EIP-712 typed data
- **KeyStore Support** - Load accounts from encrypted JSON keystore files
- **Chain ID Support** - EIP-155 replay protection
- **Nonce Management** - In-memory and external nonce services

## Installation

```bash
dotnet add package Nethereum.Accounts
```

## Dependencies

- **Nethereum.RPC** - RPC functionality and transaction managers
- **Nethereum.Signer** - Transaction and message signing (ECDSA)
- **Nethereum.Signer.EIP712** - Typed data signing
- **Nethereum.KeyStore** - Keystore encryption/decryption (Scrypt/PBKDF2)
- **Nethereum.Util** - Utilities and unit conversion

## Quick Start

### Creating an Account

```csharp
using Nethereum.Web3;
using Nethereum.Web3.Accounts;

// From private key (hex string)
var account = new Account("0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7");

// From private key with chain ID (EIP-155)
var account = new Account("0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7", chainId: 1);

// Use with Web3
var web3 = new Web3(account, "https://mainnet.infura.io/v3/YOUR-PROJECT-ID");

// Now all transactions will be signed with this account
var balance = await web3.Eth.GetBalance.SendRequestAsync(account.Address);
Console.WriteLine($"Balance: {Web3.Convert.FromWei(balance)} ETH");
```

## Account Types

### Account (Offline Signing)

Full control account with private key. Signs transactions locally without exposing the private key to the node.

```csharp
using Nethereum.Web3;
using Nethereum.Web3.Accounts;

var account = new Account("0xYOUR_PRIVATE_KEY", chainId: 1); // Mainnet
var web3 = new Web3(account, "https://mainnet.infura.io/v3/YOUR-PROJECT-ID");

// Send Ether
var receipt = await web3.Eth.GetEtherTransferService()
    .TransferEtherAndWaitForReceiptAsync("0xRECIPIENT_ADDRESS", 0.1m); // 0.1 ETH

Console.WriteLine($"Transaction hash: {receipt.TransactionHash}");
Console.WriteLine($"Status: {(receipt.Status.Value == 1 ? "Success" : "Failed")}");
```

**Use Case:** Production applications, automated services, scripts

### ManagedAccount (Node-Managed)

Account managed by the Ethereum node. Requires node support for personal API methods.

```csharp
using Nethereum.Web3;
using Nethereum.Web3.Accounts.Managed;

var account = new ManagedAccount("0xYOUR_ADDRESS", "password");
var web3 = new Web3(account, "http://localhost:8545");

// Node unlocks account and signs transaction
var receipt = await web3.Eth.GetEtherTransferService()
    .TransferEtherAndWaitForReceiptAsync("0xRECIPIENT_ADDRESS", 0.1m);
```

**Use Case:** Local development nodes (Ganache, Hardhat) that manage accounts

### ExternalAccount (Hardware Wallets, Browser Wallets)

Integrates with external signers like Ledger, Trezor, MetaMask, WalletConnect.

```csharp
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.Signer;

// Custom external signer implementation
public class MyHardwareWalletSigner : IEthExternalSigner
{
    public Task<string> GetAddressAsync() => Task.FromResult("0x...");

    public Task<string> SignAsync(byte[] message) =>
        Task.FromResult(/* Call hardware wallet API */);

    public Task<string> SignAsync(byte[] rawHash, EthECKey.SigningVersion signingVersion) =>
        Task.FromResult(/* Call hardware wallet API */);
}

var externalSigner = new MyHardwareWalletSigner();
var account = new ExternalAccount(externalSigner, chainId: 1);
await account.InitialiseAsync();

var web3 = new Web3(account, "https://mainnet.infura.io/v3/YOUR-PROJECT-ID");
```

**Use Case:** Integrating with hardware wallets, browser wallets, HSMs

### ViewOnlyAccount (Read-Only)

No signing capabilities, useful for monitoring addresses.

```csharp
using Nethereum.Web3;
using Nethereum.Web3.Accounts;

var account = new ViewOnlyAccount("0xADDRESS_TO_MONITOR");
var web3 = new Web3(account, "https://mainnet.infura.io/v3/YOUR-PROJECT-ID");

// Can read data but cannot send transactions
var balance = await web3.Eth.GetBalance.SendRequestAsync(account.Address);
```

**Use Case:** Monitoring wallets, read-only dashboards

## Usage Examples

### Example 1: Complete Transfer with Account

```csharp
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.Hex.HexTypes;

var privateKey = "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";
var account = new Account(privateKey, chainId: 1); // Mainnet
var web3 = new Web3(account, "https://mainnet.infura.io/v3/YOUR-PROJECT-ID");

Console.WriteLine($"From address: {account.Address}");

// Check balance before transfer
var balanceBefore = await web3.Eth.GetBalance.SendRequestAsync(account.Address);
Console.WriteLine($"Balance before: {Web3.Convert.FromWei(balanceBefore)} ETH");

// Send 0.1 ETH
var toAddress = "0x742d35Cc6634C0532925a3b844Bc454e4438f44e";
var amountInEther = 0.1m;

var receipt = await web3.Eth.GetEtherTransferService()
    .TransferEtherAndWaitForReceiptAsync(toAddress, amountInEther);

Console.WriteLine($"Transaction hash: {receipt.TransactionHash}");
Console.WriteLine($"Block number: {receipt.BlockNumber.Value}");
Console.WriteLine($"Gas used: {receipt.GasUsed.Value}");
Console.WriteLine($"Status: {(receipt.Status.Value == 1 ? "Success" : "Failed")}");

// Check balance after transfer
var balanceAfter = await web3.Eth.GetBalance.SendRequestAsync(account.Address);
Console.WriteLine($"Balance after: {Web3.Convert.FromWei(balanceAfter)} ETH");
```

*Based on Nethereum integration tests*

### Example 2: Creating Account from KeyStore File

```csharp
using Nethereum.Web3;
using Nethereum.Web3.Accounts;

// Load from keystore file (UTC JSON format)
var account = Account.LoadFromKeyStoreFile(
    "/path/to/UTC--2016-11-23T09-58-36Z--12890d2cce102216644c59dae5baed380d84830c",
    "password",
    chainId: 1
);

var web3 = new Web3(account, "https://mainnet.infura.io/v3/YOUR-PROJECT-ID");

Console.WriteLine($"Loaded account: {account.Address}");

// Or from JSON string
string keystoreJson = File.ReadAllText("keystore.json");
var account2 = Account.LoadFromKeyStore(keystoreJson, "password", chainId: 1);
```

The `Account` class provides static methods:
- `LoadFromKeyStoreFile(filePath, password, chainId)` - Load from file path
- `LoadFromKeyStore(json, password, chainId)` - Load from JSON string

*Based on Nethereum.Accounts source code*

### Example 3: Signing Offline Transactions

```csharp
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Hex.HexTypes;
using Nethereum.Signer;

var account = new Account("0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7", chainId: 1);

// Create transaction input
var transactionInput = new TransactionInput
{
    From = account.Address,
    To = "0x742d35Cc6634C0532925a3b844Bc454e4438f44e",
    Value = new HexBigInteger(Web3.Convert.ToWei(0.1m)),
    Nonce = new HexBigInteger(10), // Must be correct nonce
    GasPrice = new HexBigInteger(Web3.Convert.ToWei(50, UnitConversion.EthUnit.Gwei)),
    Gas = new HexBigInteger(21000)
};

// Sign transaction offline
var offlineSigner = new AccountOfflineTransactionSigner();
var signedTransaction = await offlineSigner.SignTransactionAsync(transactionInput, account);

Console.WriteLine($"Signed transaction: {signedTransaction}");

// Broadcast via any node (node doesn't see private key)
var web3 = new Web3("https://mainnet.infura.io/v3/YOUR-PROJECT-ID");
var txHash = await web3.Eth.Transactions.SendRawTransaction.SendRequestAsync(signedTransaction);
Console.WriteLine($"Transaction hash: {txHash}");
```

*Based on Nethereum integration tests*

### Example 4: Personal Sign (EIP-191)

```csharp
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.Hex.HexConvertors.Extensions;

var account = new Account("0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7");

// Sign a message
string message = "Hello, Ethereum!";
var signature = await account.AccountSigningService.PersonalSign.SendRequestAsync(message.ToHexUTF8());

Console.WriteLine($"Message: {message}");
Console.WriteLine($"Signature: {signature}");

// Verify signature
var signer = new Nethereum.Signer.EthereumMessageSigner();
var recoveredAddress = signer.EncodeUTF8AndEcRecover(message, signature);
Console.WriteLine($"Recovered address: {recoveredAddress}");
Console.WriteLine($"Verified: {recoveredAddress.Equals(account.Address, StringComparison.OrdinalIgnoreCase)}");
```

*Based on Nethereum integration tests*

### Example 5: EIP-712 Typed Data Signing

```csharp
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Signer.EIP712;

// Define EIP-712 typed data
[Struct("Person")]
public class Person
{
    [Parameter("string", "name")]
    public string Name { get; set; }

    [Parameter("address", "wallet")]
    public string Wallet { get; set; }
}

var account = new Account("0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7", chainId: 1);

var domain = new TypedData<Domain>
{
    Domain = new Domain
    {
        Name = "My Dapp",
        Version = "1",
        ChainId = 1,
        VerifyingContract = "0xCcCCccccCCCCcCCCCCCcCcCccCcCCCcCcccccccC"
    },
    PrimaryType = "Person",
    Types = MemberDescriptionFactory.GetTypesMemberDescription(typeof(Domain), typeof(Person)),
    Message = new Person
    {
        Name = "Alice",
        Wallet = "0x742d35Cc6634C0532925a3b844Bc454e4438f44e"
    }
};

// Sign typed data
var signature = await account.AccountSigningService.SignTypedDataV4.SendRequestAsync(domain);
Console.WriteLine($"EIP-712 Signature: {signature}");
```

*Based on Nethereum integration tests*

### Example 6: Multiple Accounts with Web3

```csharp
using Nethereum.Web3;
using Nethereum.Web3.Accounts;

var rpcUrl = "https://mainnet.infura.io/v3/YOUR-PROJECT-ID";

// Account 1
var account1 = new Account("0x" + new string('1', 64), chainId: 1);
var web3Account1 = new Web3(account1, rpcUrl);

// Account 2
var account2 = new Account("0x" + new string('2', 64), chainId: 1);
var web3Account2 = new Web3(account2, rpcUrl);

// Send from account1 to account2
var receipt = await web3Account1.Eth.GetEtherTransferService()
    .TransferEtherAndWaitForReceiptAsync(account2.Address, 0.5m);

Console.WriteLine($"Transfer from {account1.Address} to {account2.Address}");
Console.WriteLine($"Transaction: {receipt.TransactionHash}");
```

*Based on Nethereum integration tests*

### Example 7: Custom Nonce Management

```csharp
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.RPC.NonceServices;
using Nethereum.RPC.Eth.DTOs;

var privateKey = "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";
var account = new Account(privateKey, chainId: 1);

// Custom nonce service
var client = new Nethereum.JsonRpc.Client.RpcClient(new Uri("https://mainnet.infura.io/v3/YOUR-PROJECT-ID"));
var nonceService = new InMemoryNonceService(account.Address, client);

// Assign to account
account.NonceService = nonceService;

var web3 = new Web3(account, "https://mainnet.infura.io/v3/YOUR-PROJECT-ID");

// Send multiple transactions in parallel (nonce service handles sequencing)
var task1 = web3.Eth.GetEtherTransferService()
    .TransferEtherAndWaitForReceiptAsync("0xAddress1", 0.1m);
var task2 = web3.Eth.GetEtherTransferService()
    .TransferEtherAndWaitForReceiptAsync("0xAddress2", 0.1m);
var task3 = web3.Eth.GetEtherTransferService()
    .TransferEtherAndWaitForReceiptAsync("0xAddress3", 0.1m);

await Task.WhenAll(task1, task2, task3);
Console.WriteLine("All transfers complete!");
```

*Based on Nethereum integration tests*

### Example 8: Chain-Specific Accounts

```csharp
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.Signer;

var privateKey = "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";

// Mainnet account (Chain ID = 1)
var mainnetAccount = new Account(privateKey, Chain.MainNet);
var web3Mainnet = new Web3(mainnetAccount, "https://mainnet.infura.io/v3/YOUR-PROJECT-ID");

// Sepolia testnet account (Chain ID = 11155111)
var sepoliaAccount = new Account(privateKey, Chain.Sepolia);
var web3Sepolia = new Web3(sepoliaAccount, "https://sepolia.infura.io/v3/YOUR-PROJECT-ID");

// Polygon account (Chain ID = 137)
var polygonAccount = new Account(privateKey, Chain.Polygon);
var web3Polygon = new Web3(polygonAccount, "https://polygon-rpc.com");

Console.WriteLine($"Mainnet: {mainnetAccount.Address} (Chain {mainnetAccount.ChainId})");
Console.WriteLine($"Sepolia: {sepoliaAccount.Address} (Chain {sepoliaAccount.ChainId})");
Console.WriteLine($"Polygon: {polygonAccount.Address} (Chain {polygonAccount.ChainId})");
```

The `Account` constructor supports:
- `Account(privateKey, chainId)` - Using numeric chain ID
- `Account(privateKey, Chain.MainNet)` - Using `Chain` enum

*Based on Nethereum.Accounts source code*

### Example 9: Production Transaction Workflow

```csharp
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Hex.HexTypes;

var account = new Account(Environment.GetEnvironmentVariable("PRIVATE_KEY"), chainId: 1);
var web3 = new Web3(account, "https://mainnet.infura.io/v3/YOUR-PROJECT-ID");

try
{
    // 1. Check balance
    var balance = await web3.Eth.GetBalance.SendRequestAsync(account.Address);
    if (balance.Value < Web3.Convert.ToWei(0.2m))
    {
        throw new Exception($"Insufficient balance: {Web3.Convert.FromWei(balance)} ETH");
    }

    // 2. Estimate gas
    var toAddress = "0x742d35Cc6634C0532925a3b844Bc454e4438f44e";
    var value = Web3.Convert.ToWei(0.1m);

    var gasEstimate = await web3.Eth.TransactionManager.EstimateGasAsync(
        new TransactionInput
        {
            From = account.Address,
            To = toAddress,
            Value = new HexBigInteger(value)
        }
    );

    Console.WriteLine($"Estimated gas: {gasEstimate.Value}");

    // 3. Get current gas price
    var gasPrice = await web3.Eth.GasPrice.SendRequestAsync();
    Console.WriteLine($"Gas price: {Web3.Convert.FromWei(gasPrice, UnitConversion.EthUnit.Gwei)} gwei");

    // 4. Send transaction
    var receipt = await web3.Eth.GetEtherTransferService()
        .TransferEtherAndWaitForReceiptAsync(toAddress, 0.1m);

    // 5. Verify success
    if (receipt.Status.Value != 1)
    {
        throw new Exception($"Transaction failed: {receipt.TransactionHash}");
    }

    Console.WriteLine($"Transfer successful!");
    Console.WriteLine($"   Transaction: {receipt.TransactionHash}");
    Console.WriteLine($"   Block: {receipt.BlockNumber.Value}");
    Console.WriteLine($"   Gas used: {receipt.GasUsed.Value}");

    // Calculate cost
    var gasCost = receipt.GasUsed.Value * gasPrice.Value;
    Console.WriteLine($"   Gas cost: {Web3.Convert.FromWei(gasCost)} ETH");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
```

*Based on Nethereum integration tests*

### Example 10: AOT-Compatible Account Usage

```csharp
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.JsonRpc.SystemTextJsonRpcClient;

// Enable System.Text.Json for AOT compatibility
Nethereum.ABI.ABIDeserialisation.AbiDeserializationSettings.UseSystemTextJson = true;

// Create account
var privateKey = "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";
var account = new Account(privateKey, chainId: 1);

// Use SimpleRpcClient for AOT
var client = new SimpleRpcClient("https://mainnet.infura.io/v3/YOUR-PROJECT-ID");
var web3 = new Web3(account, client);

// All operations are AOT-compatible
var balance = await web3.Eth.GetBalance.SendRequestAsync(account.Address);
Console.WriteLine($"Balance: {Web3.Convert.FromWei(balance)} ETH");

// Send transaction
var receipt = await web3.Eth.GetEtherTransferService()
    .TransferEtherAndWaitForReceiptAsync("0xRECIPIENT", 0.01m);

Console.WriteLine($"Transaction: {receipt.TransactionHash}");
```

*Based on Nethereum AOT integration tests*

## Transaction Managers

Each account type has an associated transaction manager that handles nonce management and transaction lifecycle:

### AccountSignerTransactionManager

Used by `Account` for offline transaction signing:

```csharp
var account = new Account(privateKey, chainId: 1);

// Transaction manager is automatically created
var txManager = account.TransactionManager;

// Manual transaction with custom parameters
var receipt = await txManager.SendTransactionAndWaitForReceiptAsync(
    new TransactionInput
    {
        From = account.Address,
        To = "0xTO_ADDRESS",
        Value = new HexBigInteger(Web3.Convert.ToWei(0.1m)),
        Gas = new HexBigInteger(21000),
        GasPrice = new HexBigInteger(Web3.Convert.ToWei(50, UnitConversion.EthUnit.Gwei))
    }
);
```

**Key Methods:**
- `SendTransactionAsync(TransactionInput)` - Sign and send transaction
- `SignTransactionAsync(TransactionInput)` - Sign transaction without sending
- `GetNonceAsync(TransactionInput)` - Get next nonce for account
- `SignAuthorisationAsync(Authorisation)` - Sign EIP-7702 authorization

### ManagedAccountTransactionManager

Used by `ManagedAccount` for node-managed signing. The node must support `personal_` methods.

### ExternalAccountSignerTransactionManager

Used by `ExternalAccount` for external signer integration (hardware wallets, browser wallets).

### ViewOnlyAccountTransactionManager

Used by `ViewOnlyAccount`. Throws exception if signing is attempted.

## Nonce Management

Nonce management is critical for sending multiple transactions from the same account. Each Ethereum transaction must have a unique, sequential nonce to ensure proper ordering and prevent replay attacks.

### The Nonce Problem

When sending multiple transactions concurrently from the same account, **nonce racing** occurs:

```csharp
// ❌ PROBLEM: All three transactions will get the same nonce!
var task1 = web3.Eth.SendTransactionAsync(...); // Gets nonce: 5
var task2 = web3.Eth.SendTransactionAsync(...); // Gets nonce: 5 (race condition!)
var task3 = web3.Eth.SendTransactionAsync(...); // Gets nonce: 5 (race condition!)

await Task.WhenAll(task1, task2, task3);
// Result: Only one transaction succeeds, two fail with "nonce too low"
```

**Why This Happens:**
1. All three transactions query `eth_getTransactionCount` at nearly the same time
2. The blockchain hasn't mined the first transaction yet
3. All queries return the same nonce value (e.g., 5)
4. All three transactions try to use nonce 5
5. Only the first one to reach the mempool succeeds

### Solution: InMemoryNonceService

Nethereum provides `InMemoryNonceService` to solve nonce racing and ensure **consecutive nonces**:

```csharp
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.RPC.NonceServices;

var privateKey = "0xPRIVATE_KEY";
var account = new Account(privateKey, chainId: 1);

// Create nonce service
var client = new Nethereum.JsonRpc.Client.RpcClient(new Uri("https://mainnet.infura.io/v3/YOUR-PROJECT-ID"));
account.NonceService = new InMemoryNonceService(account.Address, client);

var web3 = new Web3(account, "https://mainnet.infura.io/v3/YOUR-PROJECT-ID");

// ✅ SOLUTION: Nonce service ensures consecutive nonces (5, 6, 7)
var task1 = web3.Eth.GetEtherTransferService().TransferEtherAndWaitForReceiptAsync("0xAddress1", 0.1m);
var task2 = web3.Eth.GetEtherTransferService().TransferEtherAndWaitForReceiptAsync("0xAddress2", 0.1m);
var task3 = web3.Eth.GetEtherTransferService().TransferEtherAndWaitForReceiptAsync("0xAddress3", 0.1m);

await Task.WhenAll(task1, task2, task3);
// Result: All three transactions succeed with nonces 5, 6, 7
```

### How InMemoryNonceService Works

The `InMemoryNonceService` uses a semaphore and local nonce tracking to guarantee consecutive nonces:

```csharp
public class InMemoryNonceService : INonceService
{
    public BigInteger CurrentNonce { get; set; } = -1;
    private SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);
    public bool UseLatestTransactionsOnly { get; set; } = false;

    public async Task<HexBigInteger> GetNextNonceAsync()
    {
        await _semaphoreSlim.WaitAsync(); // 🔒 Lock to prevent concurrent access
        try
        {
            var blockParameter = UseLatestTransactionsOnly
                ? BlockParameter.CreateLatest()   // Only confirmed transactions
                : BlockParameter.CreatePending(); // Including pending transactions

            var nonce = await ethGetTransactionCount.SendRequestAsync(_account, blockParameter);

            // 🎯 Key Logic: Ensure consecutive nonces
            if (nonce.Value <= CurrentNonce)
            {
                // Blockchain hasn't caught up yet, increment locally
                CurrentNonce = CurrentNonce + 1;
                nonce = new HexBigInteger(CurrentNonce);
            }
            else
            {
                // Blockchain has progressed, use its nonce
                CurrentNonce = nonce.Value;
            }

            return nonce;
        }
        finally
        {
            _semaphoreSlim.Release(); // 🔓 Unlock
        }
    }
}
```

**Key Features:**

1. **Semaphore Protection** - Prevents race conditions with thread-safe locking
2. **Consecutive Nonce Guarantee** - Automatically increments if blockchain returns same/lower nonce
3. **In-Memory Tracking** - Tracks `CurrentNonce` to ensure sequential nonces
4. **Configurable Block Parameter** - Choose between `pending` (default) or `latest` transactions

### Pending vs Latest Block Parameter

```csharp
var nonceService = new InMemoryNonceService(account.Address, client);

// Default: Use pending transactions (includes unconfirmed transactions)
nonceService.UseLatestTransactionsOnly = false;
// Query: eth_getTransactionCount(address, "pending")
// Use this when: Sending many transactions rapidly

// Alternative: Use latest confirmed transactions only
nonceService.UseLatestTransactionsOnly = true;
// Query: eth_getTransactionCount(address, "latest")
// Use this when: You want to ignore pending transactions (rare)
```

**When to use `pending` (default):**
- ✅ Sending multiple transactions in quick succession
- ✅ High-throughput scenarios
- ✅ Most production applications

**When to use `latest`:**
- ⚠️ You want to replace a pending transaction (manually managing nonces)
- ⚠️ Advanced nonce manipulation scenarios

### Why Nonce Management is Pluggable

The `INonceService` interface allows custom nonce management strategies:

```csharp
public interface INonceService
{
    IClient Client { get; set; }
    bool UseLatestTransactionsOnly { get; set; }

    Task<HexBigInteger> GetNextNonceAsync();
    Task ResetNonceAsync();
}
```

**Why Pluggable?**

1. **Different Storage Strategies**
   - `InMemoryNonceService` - Local in-memory tracking (default)
   - Custom database-backed service - Shared nonce across multiple app instances
   - Redis-backed service - Distributed nonce management

2. **Custom Business Logic**
   - Nonce reservations for batch processing
   - Priority transaction handling
   - Gap detection and recovery

3. **Testing and Mocking**
   - Mock nonce service for unit tests
   - Deterministic nonce sequences for integration tests

### Custom Nonce Service Example

```csharp
using Nethereum.RPC.NonceServices;
using Nethereum.Hex.HexTypes;

// Custom database-backed nonce service
public class DatabaseNonceService : INonceService
{
    private readonly string _address;
    private readonly IMyDatabase _database;

    public IClient Client { get; set; }
    public bool UseLatestTransactionsOnly { get; set; }

    public DatabaseNonceService(string address, IClient client, IMyDatabase database)
    {
        _address = address;
        Client = client;
        _database = database;
    }

    public async Task<HexBigInteger> GetNextNonceAsync()
    {
        // 🔒 Database-level locking for distributed systems
        await _database.AcquireLockAsync($"nonce_{_address}");

        try
        {
            // Get nonce from database (shared across app instances)
            var currentNonce = await _database.GetNonceAsync(_address);

            // Get blockchain nonce
            var ethGetTransactionCount = new EthGetTransactionCount(Client);
            var blockchainNonce = await ethGetTransactionCount.SendRequestAsync(
                _address,
                BlockParameter.CreatePending()
            );

            // Use higher of the two
            var nextNonce = BigInteger.Max(currentNonce + 1, blockchainNonce.Value);

            // Update database
            await _database.SetNonceAsync(_address, nextNonce);

            return new HexBigInteger(nextNonce);
        }
        finally
        {
            await _database.ReleaseLockAsync($"nonce_{_address}");
        }
    }

    public async Task ResetNonceAsync()
    {
        await _database.DeleteNonceAsync(_address);
    }
}

// Usage with custom service
var dbNonceService = new DatabaseNonceService(account.Address, client, myDatabase);
account.NonceService = dbNonceService;
```

### Resetting Nonces

If transactions fail or you need to resynchronize with the blockchain:

```csharp
// Reset nonce service to query blockchain again
await account.NonceService.ResetNonceAsync();

// Next transaction will query eth_getTransactionCount fresh
var receipt = await web3.Eth.GetEtherTransferService()
    .TransferEtherAndWaitForReceiptAsync(toAddress, amount);
```

### Production Patterns

#### Pattern 1: High-Throughput Transaction Sender

```csharp
var account = new Account(privateKey, chainId: 1);
account.NonceService = new InMemoryNonceService(account.Address, client);

var web3 = new Web3(account, rpcUrl);

// Send 100 transactions concurrently
var tasks = Enumerable.Range(0, 100).Select(async i =>
{
    try
    {
        var receipt = await web3.Eth.GetEtherTransferService()
            .TransferEtherAndWaitForReceiptAsync($"0xRecipient{i}", 0.001m);

        Console.WriteLine($"Transaction {i}: {receipt.TransactionHash}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Transaction {i} failed: {ex.Message}");
    }
});

await Task.WhenAll(tasks);
```

#### Pattern 2: Retry with Fresh Nonce

```csharp
async Task<TransactionReceipt> SendWithRetryAsync()
{
    int maxRetries = 3;
    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            if (attempt > 1)
            {
                // Reset nonce on retry
                await account.NonceService.ResetNonceAsync();
            }

            var receipt = await web3.Eth.GetEtherTransferService()
                .TransferEtherAndWaitForReceiptAsync(toAddress, amount);

            return receipt;
        }
        catch (Exception ex) when (ex.Message.Contains("nonce"))
        {
            if (attempt == maxRetries) throw;
            Console.WriteLine($"Nonce error on attempt {attempt}, retrying...");
            await Task.Delay(1000 * attempt); // Exponential backoff
        }
    }

    throw new Exception("Failed after retries");
}
```

#### Pattern 3: Manual Nonce Control (Advanced)

```csharp
// For advanced scenarios, you can set nonce manually
var txInput = new TransactionInput
{
    From = account.Address,
    To = toAddress,
    Value = new HexBigInteger(Web3.Convert.ToWei(0.1m)),
    Nonce = new HexBigInteger(42) // ⚠️ Manually specified nonce
};

var signedTx = await web3.Eth.TransactionManager.SignTransactionAsync(txInput);
var txHash = await web3.Eth.Transactions.SendRawTransaction.SendRequestAsync(signedTx);
```

### Common Nonce Issues and Solutions

| Issue | Symptom | Solution |
|-------|---------|----------|
| **Nonce too low** | `replacement transaction underpriced` | Reset nonce: `await account.NonceService.ResetNonceAsync()` |
| **Nonce too high** | Transaction stuck pending | Reset nonce or wait for previous transactions to confirm |
| **Nonce gap** | Multiple transactions stuck | Send missing nonce transaction or reset nonce |
| **Race condition** | Some transactions fail with same nonce | Use `InMemoryNonceService` |
| **Distributed apps** | Multiple instances send conflicting nonces | Implement custom database-backed nonce service |

### When You Don't Need Nonce Management

Nonce management is **automatic** if you're sending transactions sequentially:

```csharp
// ✅ No nonce service needed - sequential execution
var receipt1 = await web3.Eth.GetEtherTransferService().TransferEtherAndWaitForReceiptAsync(addr1, 0.1m);
var receipt2 = await web3.Eth.GetEtherTransferService().TransferEtherAndWaitForReceiptAsync(addr2, 0.1m);
var receipt3 = await web3.Eth.GetEtherTransferService().TransferEtherAndWaitForReceiptAsync(addr3, 0.1m);
// Each transaction waits for previous to confirm before querying nonce
```

You **need** nonce management when:
- ✅ Sending transactions concurrently (`Task.WhenAll`)
- ✅ High-throughput applications (many transactions per second)
- ✅ Distributed systems (multiple app instances using same account)
- ✅ Batch transaction sending

## Message Signing

### Personal Sign (EIP-191)

```csharp
var account = new Account(privateKey);

// Sign UTF-8 message
string message = "Sign this message";
var signature = await account.AccountSigningService.PersonalSign.SendRequestAsync(
    System.Text.Encoding.UTF8.GetBytes(message).ToHex(true)
);

// Verify
var signer = new Nethereum.Signer.EthereumMessageSigner();
var recoveredAddress = signer.EncodeUTF8AndEcRecover(message, signature);
```

### EIP-712 Typed Data

```csharp
// Sign structured typed data
var signature = await account.AccountSigningService.SignTypedDataV4.SendRequestAsync(typedData);
```

## Best Practices

1. **Never Hardcode Private Keys**: Use environment variables or secure vaults
   ```csharp
   // Good
   var privateKey = Environment.GetEnvironmentVariable("PRIVATE_KEY");
   var account = new Account(privateKey, chainId: 1);

   // Bad
   var account = new Account("0x123456...", chainId: 1); // Hardcoded!
   ```

2. **Always Specify Chain ID**: Prevents replay attacks (EIP-155)
   ```csharp
   var account = new Account(privateKey, chainId: 1); // Mainnet
   ```

3. **Use Appropriate Account Type**:
   - **Production/Automation**: `Account` (offline signing)
   - **Local Development**: `ManagedAccount`
   - **Hardware Wallets**: `ExternalAccount`
   - **Read-Only**: `ViewOnlyAccount`

4. **Handle Nonce for Concurrent Transactions**:
   ```csharp
   account.NonceService = new InMemoryNonceService(account.Address, client);
   ```

5. **Verify Transactions**:
   ```csharp
   if (receipt.Status.Value != 1)
   {
       throw new Exception("Transaction failed");
   }
   ```

6. **Estimate Gas Before Sending**:
   ```csharp
   var gasEstimate = await web3.Eth.TransactionManager.EstimateGasAsync(txInput);
   ```

7. **Monitor Balance**:
   ```csharp
   var balance = await web3.Eth.GetBalance.SendRequestAsync(account.Address);
   if (balance.Value < requiredAmount)
   {
       throw new InsufficientFundsException();
   }
   ```

8. **Secure KeyStore Files**: Encrypt private keys when storing
   ```csharp
   var keyStoreService = new Nethereum.KeyStore.KeyStoreService();
   var json = keyStoreService.EncryptAndGenerateDefaultKeyStoreAsJson(
       password,
       privateKeyBytes,
       address
   );
   File.WriteAllText("keystore.json", json);
   ```

## Security Considerations

1. **Private Key Storage**:
   - Never commit private keys to version control
   - Use environment variables, Azure Key Vault, AWS KMS, or HSMs
   - Encrypt keystore files with strong passwords

2. **Chain ID**:
   - Always specify chain ID to prevent replay attacks
   - Verify chain ID matches the network you're using

3. **Transaction Signing**:
   - Prefer `Account` (offline signing) over `ManagedAccount` in production
   - Never send private keys over the network

4. **External Signers**:
   - Validate addresses returned by external signers
   - Handle signer errors gracefully

## Error Handling

```csharp
using Nethereum.JsonRpc.Client;

try
{
    var receipt = await web3.Eth.GetEtherTransferService()
        .TransferEtherAndWaitForReceiptAsync(toAddress, amount);

    if (receipt.Status.Value != 1)
    {
        Console.WriteLine($"Transaction reverted: {receipt.TransactionHash}");
    }
}
catch (RpcResponseException ex)
{
    Console.WriteLine($"RPC Error: {ex.RpcError.Message}");
    Console.WriteLine($"Code: {ex.RpcError.Code}");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
```

## API Reference

### Account Class

**Constructors:**
- `Account(string privateKey, BigInteger? chainId = null)` - Create from hex private key
- `Account(byte[] privateKey, BigInteger? chainId = null)` - Create from byte array
- `Account(EthECKey key, BigInteger? chainId = null)` - Create from EthECKey
- `Account(string privateKey, Chain chain)` - Create with Chain enum
- `Account(byte[] privateKey, Chain chain)` - Create with Chain enum
- `Account(EthECKey key, Chain chain)` - Create with Chain enum

**Static Methods:**
- `LoadFromKeyStoreFile(string filePath, string password, BigInteger? chainId = null)` - Load from UTC JSON file
- `LoadFromKeyStore(string json, string password, BigInteger? chainId = null)` - Load from JSON string

**Properties:**
- `string Address` - Ethereum address
- `string PrivateKey` - Private key (hex)
- `string PublicKey` - Public key (hex)
- `BigInteger? ChainId` - Chain ID for EIP-155
- `ITransactionManager TransactionManager` - Transaction manager instance
- `INonceService NonceService` - Nonce service (lazy-initialized as InMemoryNonceService)
- `IAccountSigningService AccountSigningService` - Signing service

### ManagedAccount Class

**Constructor:**
- `ManagedAccount(string accountAddress, string password)` - Create managed account

**Properties:**
- `string Address` - Ethereum address
- `string Password` - Password for node
- `ITransactionManager TransactionManager` - ManagedAccountTransactionManager
- `INonceService NonceService` - Optional nonce service
- `IAccountSigningService AccountSigningService` - Signing service

### ExternalAccount Class

**Constructors:**
- `ExternalAccount(IEthExternalSigner externalSigner, BigInteger? chainId = null)` - Create external account
- `ExternalAccount(string address, IEthExternalSigner externalSigner, BigInteger? chainId = null)` - With pre-known address

**Methods:**
- `Task InitialiseAsync()` - Initialize account (fetches address from signer)
- `void InitialiseDefaultTransactionManager(IClient client)` - Initialize transaction manager

**Properties:**
- `IEthExternalSigner ExternalSigner` - External signer implementation
- `string Address` - Ethereum address
- `BigInteger? ChainId` - Chain ID
- `ITransactionManager TransactionManager` - ExternalAccountSignerTransactionManager
- `INonceService NonceService` - Optional nonce service
- `IAccountSigningService AccountSigningService` - Signing service

### ViewOnlyAccount Class

**Constructor:**
- `ViewOnlyAccount(string accountAddress)` - Create view-only account

**Properties:**
- `string Address` - Ethereum address to monitor
- `ITransactionManager TransactionManager` - ViewOnlyAccountTransactionManager (throws on sign)
- `INonceService NonceService` - Optional nonce service
- `IAccountSigningService AccountSigningService` - No signing service

### AccountSignerTransactionManager Class

**Constructor:**
- `AccountSignerTransactionManager(IClient rpcClient, Account account, BigInteger? overridingAccountChainId = null)` - Create transaction manager

**Key Methods:**
- `Task<string> SendTransactionAsync(TransactionInput transactionInput)` - Sign and send
- `Task<string> SignTransactionAsync(TransactionInput transaction)` - Sign only (doesn't send)
- `string SignTransaction(TransactionInput transaction)` - Synchronous signing
- `Task<HexBigInteger> GetNonceAsync(TransactionInput transaction)` - Get next nonce
- `Task<Authorisation> SignAuthorisationAsync(Authorisation authorisation)` - Sign EIP-7702 authorization

## Related Packages

### Used By (Consumers)
- **Nethereum.Web3** - Uses accounts for transaction signing and management
- **Nethereum.HdWallet** - Creates Account instances from HD wallet derivation

### Dependencies
- **Nethereum.RPC** - Provides `IAccount` interface and nonce services
- **Nethereum.Signer** - Provides `EthECKey` and transaction signing
- **Nethereum.Signer.EIP712** - Provides typed data signing
- **Nethereum.KeyStore** - Provides keystore encryption/decryption

### See Also
- [Nethereum.Web3](../Nethereum.Web3/README.md) - High-level Web3 API
- [Nethereum.HdWallet](../Nethereum.HdWallet/README.md) - HD Wallet implementation
- [Nethereum.Signer](../Nethereum.Signer/README.md) - Transaction and message signing

## Playground Examples

Live examples you can run in the browser:

**Chain IDs and Replay Protection:**
- [Chain ID Usage](https://playground.nethereum.com/csharp/id/1020) - How to use Chain IDs to prevent replay attacks (EIP-155)

**HD Wallets:**
- [HD Wallet Introduction](https://playground.nethereum.com/csharp/id/1043) - Creating HD wallets with BIP32 standard
- [Deriving Accounts](https://playground.nethereum.com/csharp/id/1041) - Derive multiple accounts from seed words
- [Generating Mnemonics](https://playground.nethereum.com/csharp/id/1042) - Generate 12-word seed phrases

**KeyStore:**
- [Create Scrypt KeyStore](https://playground.nethereum.com/csharp/id/1021) - Create keystore with custom Scrypt params

## Supported Chains

Common chain IDs available in the `Chain` enum:

| Network | Chain ID | Enum |
|---------|----------|------|
| Ethereum Mainnet | 1 | `Chain.MainNet` |
| Sepolia | 11155111 | `Chain.Sepolia` |
| Polygon | 137 | `Chain.Polygon` |
| Binance Smart Chain | 56 | `Chain.BSC` |
| Arbitrum One | 42161 | `Chain.Arbitrum` |
| Optimism | 10 | `Chain.Optimism` |

## Additional Resources

- [EIP-155: Simple replay attack protection](https://eips.ethereum.org/EIPS/eip-155)
- [EIP-191: Signed Data Standard](https://eips.ethereum.org/EIPS/eip-191)
- [EIP-712: Typed structured data hashing and signing](https://eips.ethereum.org/EIPS/eip-712)
- [EIP-7702: Set EOA account code](https://eips.ethereum.org/EIPS/eip-7702)
- [Nethereum Documentation](http://docs.nethereum.com)
- [BIP-32: Hierarchical Deterministic Wallets](https://github.com/bitcoin/bips/blob/master/bip-0032.mediawiki)

## Important Notes

- **Chain ID is crucial**: Always specify chain ID in production to prevent replay attacks across different networks
- **Nonce racing**: Use `InMemoryNonceService` when sending concurrent transactions
- **Private key security**: Never hardcode or commit private keys. Use environment variables or secure key management systems
- **KeyStore passwords**: Use strong passwords for keystore encryption (high Scrypt N parameter increases security but also computation time)
- **Gas estimation**: Always estimate gas before sending transactions to avoid out-of-gas errors
- **Transaction verification**: Check `receipt.Status.Value == 1` to ensure transaction success
