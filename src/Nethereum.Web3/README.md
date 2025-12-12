# Nethereum.Web3

Nethereum.Web3 is the main facade and entry point for interacting with Ethereum. It provides a high-level API that combines RPC methods, contract interaction, account management, and utility functions into a single, easy-to-use interface.

## Features

- **Unified API**: Single entry point for all Ethereum operations
- **Account Management**: Built-in support for accounts and transaction signing
- **Contract Interaction**: Deploy and interact with smart contracts using generated or dynamic code
- **ERC Standards**: Built-in support for ERC20, ERC721, ERC1155, ERC1271, ERC165, ERC2535, ERC6492, and EIP-3009
- **ENS Integration**: Resolve Ethereum Name Service domains
- **Unit Conversion**: Convert between Wei, Gwei, and Ether
- **Event Processing**: Filter and decode contract events
- **Transaction Management**: Send transactions and wait for receipts
- **Multi-Query Support**: Batch multiple contract queries using Multicall
- **AOT Compatible**: Works with Native AOT compilation using System.Text.Json

## Installation

```bash
dotnet add package Nethereum.Web3
```

## Dependencies

- **Nethereum.Accounts** - Account management and transaction signing
- **Nethereum.Contracts** - Contract interaction and standards implementations
- **Nethereum.RPC** - Low-level RPC methods
- **Nethereum.ABI** - ABI encoding/decoding
- **Nethereum.Util** - Utility functions and unit conversion
- **Nethereum.JsonRpc.Client** - JSON-RPC client abstraction
- **Nethereum.BlockchainProcessing** - Block/transaction/log processing services

## Key Concepts

### Web3 Facade Pattern

Web3 uses the Facade pattern to provide a unified, simplified interface to the complex subsystems of Ethereum interaction. Instead of instantiating multiple services separately, you create a single `Web3` instance that provides access to everything you need.

The main entry points are:
- `web3.Eth` - Primary Ethereum operations (transactions, blocks, contracts)
- `web3.TransactionManager` - Transaction signing and sending
- `web3.TransactionReceiptPolling` - Wait for transaction confirmations
- `Web3.Convert` - Static utility for unit conversions

### Connection Models

Web3 supports two connection models:

1. **Read-Only Mode** - Connect with just an RPC URL for queries
2. **Transaction Mode** - Connect with an `IAccount` to sign and send transactions

### Standard Services

Web3 provides built-in services for common standards:
- **ERC20** - Fungible tokens
- **ERC721** - Non-fungible tokens (NFTs)
- **ERC1155** - Multi-token standard
- **ERC1271** - Contract signature validation
- **EIP-3009** - Gasless token transfers with authorization

## Quick Start

### Basic Web3 Initialization

From: [Nethereum Playground Example 1001](https://playground.nethereum.com/csharp/id/1001)

```csharp
using Nethereum.Web3;

// Connect to a public RPC endpoint
var web3 = new Web3("https://eth.drpc.org");

// Get account balance
var balance = await web3.Eth.GetBalance
    .SendRequestAsync("0x742d35Cc6634C0532925a3b844Bc454e4438f44e");

// Convert Wei to Ether
var etherBalance = Web3.Convert.FromWei(balance.Value);
Console.WriteLine($"Balance: {etherBalance} ETH");
```

### Web3 with Account (Transaction Signing)

```csharp
using Nethereum.Web3;
using Nethereum.Web3.Accounts;

// Create account from private key
var account = new Account("0xPRIVATE_KEY_HERE");

// Initialize Web3 with account
var web3 = new Web3(account, "http://localhost:8545");

// Now you can send transactions
var receipt = await web3.Eth.GetEtherTransferService()
    .TransferEtherAndWaitForReceiptAsync(
        "0x12890d2cce102216644c59daE5baed380d84830c",
        0.01m
    );

Console.WriteLine($"Transaction Hash: {receipt.TransactionHash}");
Console.WriteLine($"Block Number: {receipt.BlockNumber}");
Console.WriteLine($"Gas Used: {receipt.GasUsed}");
```

## Usage Examples

### 1. Query Block Information

From: [Nethereum Playground Example 1002](https://playground.nethereum.com/csharp/id/1002)

```csharp
var web3 = new Web3("https://eth.drpc.org");

// Get latest block with transactions
var block = await web3.Eth.Blocks.GetBlockWithTransactionsByNumber
    .SendRequestAsync(BlockParameter.CreateLatest());

Console.WriteLine($"Block Number: {block.Number}");
Console.WriteLine($"Block Hash: {block.BlockHash}");
Console.WriteLine($"Transactions: {block.Transactions.Length}");
Console.WriteLine($"Timestamp: {DateTimeOffset.FromUnixTimeSeconds((long)block.Timestamp.Value)}");
```

### 2. ERC20 Token Interactions

From: [Nethereum Playground Example 1005](https://playground.nethereum.com/csharp/id/1005)

```csharp
var web3 = new Web3("https://eth.drpc.org");

// Get ERC20 contract service (built-in support)
// Using MKR token address as example
var tokenService = web3.Eth.ERC20.GetContractService(
    "0x9f8f72aa9304c8b593d555f12ef6589cc3a579a2"
);

// Query token balance
var balance = await tokenService.BalanceOfQueryAsync(
    "0x8ee7d9235e01e6b42345120b5d270bdb763624c7"
);

// Get token metadata
var name = await tokenService.NameQueryAsync();
var symbol = await tokenService.SymbolQueryAsync();
var decimals = await tokenService.DecimalsQueryAsync();

// Convert from Wei to human-readable format
var balanceInTokens = Web3.Convert.FromWei(balance, decimals);
Console.WriteLine($"{symbol} Balance: {balanceInTokens}");
```

**Built-in ERC20 Operations:**
```csharp
// Transfer tokens (requires account)
var receipt = await tokenService.TransferRequestAndWaitForReceiptAsync(
    toAddress,
    amount
);

// Approve spending
await tokenService.ApproveRequestAndWaitForReceiptAsync(
    spenderAddress,
    allowance
);

// Check allowance
var allowance = await tokenService.AllowanceQueryAsync(
    ownerAddress,
    spenderAddress
);

// Get total supply
var totalSupply = await tokenService.TotalSupplyQueryAsync();
```

### 3. Unit Conversion Utilities

From: [Nethereum Playground Example 1014](https://playground.nethereum.com/csharp/id/1014)

```csharp
using Nethereum.Web3;

// Convert Ether to Wei
var weiAmount = Web3.Convert.ToWei(1.5m); // 1.5 Ether to Wei

// Convert Wei to Ether
var etherAmount = Web3.Convert.FromWei(1500000000000000000); // Wei to Ether

// Convert to/from Gwei (useful for gas prices)
var gweiAmount = Web3.Convert.FromWei(20000000000, UnitConversion.EthUnit.Gwei);
var weiFromGwei = Web3.Convert.ToWei(20, UnitConversion.EthUnit.Gwei);

// Custom decimal places (e.g., USDC with 6 decimals)
var usdcAmount = Web3.Convert.FromWei(1000000, 6); // 1 USDC
var usdcInWei = Web3.Convert.ToWei(1, 6); // 1 USDC to smallest unit
```

### 4. Contract Deployment

From: [Nethereum Playground Example 1006](https://playground.nethereum.com/csharp/id/1006)

```csharp
using Nethereum.Web3;
using Nethereum.Web3.Accounts;

var account = new Account("0xPRIVATE_KEY");
var web3 = new Web3(account, "http://localhost:8545");

// Using generated deployment message
var deploymentHandler = web3.Eth.GetContractDeploymentHandler<StandardTokenDeployment>();
var deploymentMessage = new StandardTokenDeployment
{
    TotalSupply = 100000,
    Name = "My Token",
    Symbol = "MTK",
    Decimals = 18
};

var receipt = await deploymentHandler.SendRequestAndWaitForReceiptAsync(deploymentMessage);
var contractAddress = receipt.ContractAddress;

Console.WriteLine($"Contract deployed at: {contractAddress}");
Console.WriteLine($"Gas used: {receipt.GasUsed}");
```

**Alternative: Deploy with ABI and Bytecode**
```csharp
// For contracts without code generation
var receipt = await web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(
    abi: contractAbi,
    bytecode: contractBytecode,
    from: account.Address,
    gas: new HexBigInteger(900000),
    constructorParams: new object[] { "TokenName", "SYMBOL", 18 }
);

Console.WriteLine($"Contract Address: {receipt.ContractAddress}");
```

**For comprehensive code generation guide, see:**
- [Nethereum Code Generation](https://docs.nethereum.com/en/latest/nethereum-code-generation/)

### 5. Smart Contract Function Calls (Read)

```csharp
var web3 = new Web3("https://eth.drpc.org");
var contractAddress = "0x...";

// Using generated function messages
var balanceHandler = web3.Eth.GetContractQueryHandler<BalanceOfFunction>();
var balanceMessage = new BalanceOfFunction
{
    Owner = "0x742d35Cc6634C0532925a3b844Bc454e4438f44e"
};

// Query and return primitive type
var balance = await balanceHandler.QueryAsync<BigInteger>(
    contractAddress,
    balanceMessage
);

// Query and deserialize to output DTO
var balanceOutput = await balanceHandler
    .QueryDeserializingToObjectAsync<BalanceOfOutputDTO>(
        balanceMessage,
        contractAddress
    );

Console.WriteLine($"Balance: {balanceOutput.Balance}");
```

**Alternative: Using Contract Handler**
```csharp
// Get contract handler for specific address
var contractHandler = web3.Eth.GetContractHandler(contractAddress);

// Query functions
var result = await contractHandler.QueryAsync<BalanceOfFunction, BigInteger>(
    new BalanceOfFunction { Owner = ownerAddress }
);
```

### 6. Smart Contract Transactions (Write)

```csharp
var account = new Account("0xPRIVATE_KEY");
var web3 = new Web3(account, "http://localhost:8545");
var contractAddress = "0x...";

// Create transaction handler
var transferHandler = web3.Eth.GetContractTransactionHandler<TransferFunction>();

// Prepare transaction message
var transfer = new TransferFunction
{
    To = "0xde0B295669a9FD93d5F28D9Ec85E40f4cb697BAe",
    TokenAmount = 100
};

// Send transaction and wait for receipt
var receipt = await transferHandler
    .SendRequestAndWaitForReceiptAsync(contractAddress, transfer);

Console.WriteLine($"Transaction successful in block: {receipt.BlockNumber}");
Console.WriteLine($"Status: {receipt.Status}"); // 1 = success, 0 = failure
```

**Gas Estimation:**
```csharp
// Estimate gas before sending
var estimatedGas = await transferHandler.EstimateGasAsync(
    contractAddress,
    transfer
);
transfer.Gas = estimatedGas.Value;

var receipt = await transferHandler
    .SendRequestAndWaitForReceiptAsync(contractAddress, transfer);
```

### 7. Event Filtering and Decoding

From: [Nethereum Playground Example 1008](https://playground.nethereum.com/csharp/id/1008)

```csharp
var web3 = new Web3("https://eth.drpc.org");
var contractAddress = "0x...";

// Get event handler
var transferEvent = web3.Eth.GetEvent<TransferEventDTO>(contractAddress);

// Create filter for all Transfer events
var filterAll = transferEvent.CreateFilterInput(
    fromBlock: new BlockParameter(12000000),
    toBlock: new BlockParameter(12000100)
);

// Get all historical events
var allEvents = await transferEvent.GetAllChangesAsync(filterAll);

foreach (var evt in allEvents)
{
    Console.WriteLine($"From: {evt.Event.From}");
    Console.WriteLine($"To: {evt.Event.To}");
    Console.WriteLine($"Value: {evt.Event.Value}");
    Console.WriteLine($"Block: {evt.Log.BlockNumber}");
    Console.WriteLine($"TxHash: {evt.Log.TransactionHash}");
}
```

**Decode Events from Receipt:**
```csharp
// Decode events directly from transaction receipt
var receipt = await web3.Eth.Transactions.GetTransactionReceipt
    .SendRequestAsync(txHash);

var transferEvents = receipt.DecodeAllEvents<TransferEventDTO>();

foreach (var evt in transferEvents)
{
    Console.WriteLine($"Transfer: {evt.Event.From} -> {evt.Event.To}: {evt.Event.Value}");
}
```

**Filter Events by Indexed Parameters:**
```csharp
// Filter by sender (first indexed parameter)
var filterBySender = transferEvent.CreateFilterInput(
    fromBlock: BlockParameter.CreateEarliest(),
    toBlock: BlockParameter.CreateLatest(),
    filterTopic1: new object[] { senderAddress }
);

// Filter by recipient (second indexed parameter)
var filterByRecipient = transferEvent.CreateFilterInput(
    fromBlock: BlockParameter.CreateEarliest(),
    toBlock: BlockParameter.CreateLatest(),
    filterTopic2: new object[] { recipientAddress }
);

// Multiple recipients (OR condition)
var filterMultiple = transferEvent.CreateFilterInput(
    fromBlock: BlockParameter.CreateEarliest(),
    toBlock: BlockParameter.CreateLatest(),
    filterTopic2: new object[] { recipient1, recipient2 }
);

var events = await transferEvent.GetAllChangesAsync(filterBySender);
```

### 8. ENS (Ethereum Name Service)

From: [Nethereum Playground Example 1055](https://playground.nethereum.com/csharp/id/1055)

```csharp
var web3 = new Web3("https://eth.drpc.org");
var ensService = web3.Eth.GetEnsService();

// Resolve ENS name to address
var address = await ensService.ResolveAddressAsync("nick.eth");
Console.WriteLine($"Address: {address}");

// Reverse resolve (address to ENS name)
var name = await ensService.ReverseResolveAsync(
    "0xd1220a0cf47c7b9be7a2e6ba89f429762e7b9adb"
);
Console.WriteLine($"ENS Name: {name}");

// Resolve text records
var url = await ensService.ResolveTextAsync("nethereum.eth", TextDataKey.url);
var email = await ensService.ResolveTextAsync("nethereum.eth", TextDataKey.email);
var avatar = await ensService.ResolveTextAsync("nethereum.eth", TextDataKey.avatar);

Console.WriteLine($"URL: {url}");
Console.WriteLine($"Email: {email}");
```

**ENS supports emojis and special characters:**
```csharp
// Emoji domains work natively
var address = await ensService.ResolveAddressAsync("💩💩💩.eth");

// ENS names are automatically normalized
var normalizedName = new EnsUtil().Normalise("Foo.eth"); // Returns "foo.eth"
```

### 9. Transaction Receipt Polling

From: [Nethereum Playground Example 1003](https://playground.nethereum.com/csharp/id/1003)

```csharp
var account = new Account("0xPRIVATE_KEY");
var web3 = new Web3(account, "http://localhost:8545");

// Send Ether transfer
var ethSenderService = web3.Eth.GetEtherTransferService();
var transactionHash = await ethSenderService
    .TransferEtherAsync("0x12890d2cce102216644c59daE5baed380d84830c", 0.01m);

Console.WriteLine($"Transaction Hash: {transactionHash}");

// Poll for receipt (with optional cancellation token)
var cancellationToken = new CancellationTokenSource().Token;
var receipt = await web3.TransactionReceiptPolling
    .PollForReceiptAsync(transactionHash, cancellationToken);

if (receipt != null)
{
    Console.WriteLine($"Transaction Receipt: {receipt.TransactionHash}");
    Console.WriteLine($"Block Number: {receipt.BlockNumber}");
    Console.WriteLine($"Gas Used: {receipt.GasUsed}");
    Console.WriteLine($"Status: {receipt.Status}"); // 1 = success, 0 = failure
}
```

### 10. Custom Error Handling

Smart contracts can throw custom errors (Solidity custom errors). Nethereum can decode these errors:

```csharp
var account = new Account("0xPRIVATE_KEY");
var web3 = new Web3(account, "http://localhost:8545");

var contract = web3.Eth.GetContract(contractAbi, contractAddress);
var transferFunction = contract.GetFunction("transfer");

try
{
    // This will throw a custom error
    await transferFunction.SendTransactionAndWaitForReceiptAsync(
        web3.TransactionManager.Account.Address,
        gas: null,
        value: null,
        functionInput: new object[] { toAddress, 100 }
    );
}
catch (SmartContractCustomErrorRevertException error)
{
    // Check if it's our specific error
    if (error.IsCustomErrorFor<InsufficientBalanceError>())
    {
        // Decode the error with parameters
        var insufficientBalance = error.DecodeError<InsufficientBalanceError>();

        Console.WriteLine($"Insufficient Balance!");
        Console.WriteLine($"Required: {insufficientBalance.Required}");
        Console.WriteLine($"Available: {insufficientBalance.Available}");
    }
}
```

**Define Custom Error:**
```csharp
[Error("InsufficientBalance")]
public class InsufficientBalanceError
{
    [Parameter("uint256", "available", 1)]
    public BigInteger Available { get; set; }

    [Parameter("uint256", "required", 2)]
    public BigInteger Required { get; set; }
}
```

## Advanced Topics

### Using Web3 with Different RPC Clients

```csharp
using Nethereum.Web3;
using Nethereum.JsonRpc.SystemTextJsonRpcClient;
using Nethereum.JsonRpc.WebSocketClient;

// HTTP client (default) - uses Newtonsoft.Json
var web3Http = new Web3("https://eth.drpc.org");

// AOT-compatible SimpleRpcClient - uses System.Text.Json
var simpleClient = new SimpleRpcClient("https://eth.drpc.org");
var web3Simple = new Web3(simpleClient);

// WebSocket client for real-time data
var wsClient = new WebSocketClient("wss://eth.drpc.org");
var web3Ws = new Web3(wsClient);
```

### AOT (Native Ahead-of-Time) Compilation

```csharp
// Enable System.Text.Json for AOT compatibility
Nethereum.ABI.ABIDeserialisation.AbiDeserializationSettings.UseSystemTextJson = true;

// Configure signature algorithm for AOT
Nethereum.Signer.EthECKey.SignRecoverable = false;

// Now use Web3 normally with AOT-compatible client
var client = new SimpleRpcClient("https://eth.drpc.org");
var web3 = new Web3(client);
```

### Working with Multiple Accounts

```csharp
var account1 = new Account("0xPRIVATE_KEY_1");
var account2 = new Account("0xPRIVATE_KEY_2");

var web3_1 = new Web3(account1, "http://localhost:8545");
var web3_2 = new Web3(account2, "http://localhost:8545");

// Or switch accounts dynamically
var web3 = new Web3("http://localhost:8545");
web3.TransactionManager.Account = account1; // Use account1
await DoSomeWork(web3);

web3.TransactionManager.Account = account2; // Switch to account2
await DoSomeMoreWork(web3);
```

### Contract Handler Pattern

```csharp
// For frequently accessed contracts, use contract handler
var contractAddress = "0x...";
var contractHandler = web3.Eth.GetContractHandler(contractAddress);

// Query
var balance = await contractHandler.QueryAsync<BalanceOfFunction, BigInteger>(
    new BalanceOfFunction { Owner = ownerAddress }
);

// Transaction
var receipt = await contractHandler.SendRequestAndWaitForReceiptAsync(
    new TransferFunction { To = toAddress, TokenAmount = 100 }
);

// Estimate gas
var estimatedGas = await contractHandler.EstimateGasAsync(
    new TransferFunction { To = toAddress, TokenAmount = 100 }
);
```

### Historical State Queries

```csharp
var contractHandler = web3.Eth.GetContractHandler(contractAddress);

// Query current state
var currentBalance = await contractHandler.QueryAsync<BalanceOfFunction, BigInteger>(
    new BalanceOfFunction { Owner = ownerAddress }
);

// Query historical state at specific block
var historicalBalance = await contractHandler.QueryAsync<BalanceOfFunction, BigInteger>(
    new BalanceOfFunction { Owner = ownerAddress },
    new BlockParameter(12345678) // Block number
);

Console.WriteLine($"Balance at block 12345678: {historicalBalance}");
Console.WriteLine($"Current balance: {currentBalance}");
```

### Multi-Query with Multicall

```csharp
// Batch multiple contract queries into a single RPC call
var multiQueryHandler = web3.Eth.GetMultiQueryHandler();

// Define multiple queries
var query1 = new BalanceOfFunction { Owner = address1 };
var query2 = new BalanceOfFunction { Owner = address2 };
var query3 = new BalanceOfFunction { Owner = address3 };

// Execute all queries in one call
var results = await multiQueryHandler.MultiCallAsync(
    new MultiCallInput<BalanceOfFunction, BigInteger>(contractAddress, query1),
    new MultiCallInput<BalanceOfFunction, BigInteger>(contractAddress, query2),
    new MultiCallInput<BalanceOfFunction, BigInteger>(contractAddress, query3)
);

Console.WriteLine($"Balance 1: {results[0]}");
Console.WriteLine($"Balance 2: {results[1]}");
Console.WriteLine($"Balance 3: {results[2]}");
```

## Common Patterns

### Deployment + Interaction Pattern

```csharp
var account = new Account("0xPRIVATE_KEY");
var web3 = new Web3(account, "http://localhost:8545");

// 1. Deploy contract
var deploymentHandler = web3.Eth.GetContractDeploymentHandler<MyContractDeployment>();
var deployment = new MyContractDeployment { InitialValue = 100 };
var receipt = await deploymentHandler.SendRequestAndWaitForReceiptAsync(deployment);
var contractAddress = receipt.ContractAddress;

// 2. Get contract handler
var contractHandler = web3.Eth.GetContractHandler(contractAddress);

// 3. Call functions
var value = await contractHandler.QueryAsync<GetValueFunction, int>();
await contractHandler.SendRequestAndWaitForReceiptAsync(
    new SetValueFunction { NewValue = 200 }
);
```

### Multi-Contract Interaction

```csharp
var web3 = new Web3("https://eth.drpc.org");

// Work with multiple contracts
var usdcService = web3.Eth.ERC20.GetContractService(
    "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48" // USDC
);
var uniService = web3.Eth.ERC20.GetContractService(
    "0x1f9840a85d5aF5bf1D1762F925BDADdC4201F984" // UNI
);

var usdcBalance = await usdcService.BalanceOfQueryAsync(myAddress);
var uniBalance = await uniService.BalanceOfQueryAsync(myAddress);

Console.WriteLine($"USDC: {Web3.Convert.FromWei(usdcBalance, 6)}");
Console.WriteLine($"UNI: {Web3.Convert.FromWei(uniBalance, 18)}");
```

### Event Monitoring Pattern

```csharp
var web3 = new Web3("https://eth.drpc.org");
var contractAddress = "0x...";

// Get event handler
var transferEvent = web3.Eth.GetEvent<TransferEventDTO>(contractAddress);

// Create and save filter
var filterId = await transferEvent.CreateFilterAsync(
    transferEvent.CreateFilterInput(fromBlock: BlockParameter.CreateLatest())
);

// Poll for new events periodically
while (true)
{
    var newEvents = await transferEvent.GetFilterChangesAsync(filterId);

    foreach (var evt in newEvents)
    {
        Console.WriteLine($"New transfer: {evt.Event.From} -> {evt.Event.To}: {evt.Event.Value}");
    }

    await Task.Delay(5000); // Wait 5 seconds
}
```

## API Reference

### Main Entry Points

- `web3.Eth` (IEthApiContractService) - Ethereum-related methods (transactions, blocks, contracts)
- `web3.TransactionManager` (ITransactionManager) - Transaction signing and management
- `web3.TransactionReceiptPolling` (ITransactionReceiptService) - Wait for transaction receipts
- `web3.Processing` (IBlockchainProcessingService) - Block/transaction/log processing
- `web3.Net` (INetApiService) - Network information
- `web3.Personal` (IPersonalApiService) - Personal RPC methods
- `web3.Debug` (IDebugApiService) - Debug RPC methods
- `web3.FeeSuggestion` (FeeSuggestionService) - Fee estimation
- `Web3.Convert` (UnitConversion) - Static utility for unit conversions (Wei/Gwei/Ether)

### Contract Methods

- `web3.Eth.GetContractHandler(address)` - Get contract handler for address
- `web3.Eth.GetContract(abi, address)` - Get dynamic contract instance
- `web3.Eth.DeployContract` - Deploy contract with ABI and bytecode
- `web3.Eth.GetContractDeploymentHandler<T>()` - Get typed deployment handler
- `web3.Eth.GetContractTransactionHandler<T>()` - Get typed transaction handler
- `web3.Eth.GetContractQueryHandler<T>()` - Get typed query handler
- `web3.Eth.GetEvent<T>()` - Get event handler (no address specified)
- `web3.Eth.GetEvent<T>(address)` - Get event handler for specific contract
- `web3.Eth.GetContractTransactionErrorReason` - Decode transaction error reasons

### Standard Services

- `web3.Eth.ERC20.GetContractService(address)` - ERC20 token service
- `web3.Eth.ERC721.GetContractService(address)` - ERC721 NFT service
- `web3.Eth.ERC1155.GetContractService(address)` - ERC1155 multi-token service
- `web3.Eth.ERC1271` - ERC1271 contract signature validation service
- `web3.Eth.ERC165` - ERC165 interface detection service
- `web3.Eth.ERC2535Diamond` - ERC2535 Diamond standard service
- `web3.Eth.ERC6492` - ERC6492 pre-deployed contract signature validation
- `web3.Eth.EIP3009` - EIP-3009 transfer with authorization (USDC)
- `web3.Eth.GetEnsService()` - ENS resolver service
- `web3.Eth.GetEnsEthTlsService()` - ENS .eth TLD service
- `web3.Eth.GetEtherTransferService()` - Simple Ether transfer service
- `web3.Eth.ProofOfHumanity` - Proof of Humanity registry service
- `web3.Eth.Create2DeterministicDeploymentProxyService` - CREATE2 deployment service

### Multi-Query Methods

- `web3.Eth.GetMultiQueryHandler(multiCallAddress)` - Multicall contract-based batching
- `web3.Eth.GetMultiQueryBatchRpcHandler()` - RPC batch request handler

### Static Utilities

- `Web3.Convert.ToWei(amount, unit)` - Convert to Wei
- `Web3.Convert.FromWei(amount, unit)` - Convert from Wei
- `Web3.IsChecksumAddress(address)` - Validate EIP-55 checksum address
- `Web3.ToChecksumAddress(address)` - Convert to EIP-55 checksum address
- `Web3.ToValid20ByteAddress(address)` - Normalize to 20-byte address
- `Web3.Sha3(value)` - Calculate Keccak-256 hash
- `Web3.GetAddressFromPrivateKey(privateKey)` - Derive address from private key

### Advanced Services

- `web3.GetEIP7022SponsorAuthorisation()` - EIP-7022 sponsor authorization service

## Related Packages

### Core Dependencies

- **Nethereum.Accounts** - Account management and transaction signing
- **Nethereum.Contracts** - Contract interaction, code generation, and all standard implementations
- **Nethereum.RPC** - Low-level RPC method implementations
- **Nethereum.ABI** - ABI encoding/decoding
- **Nethereum.Util** - Utility functions and unit conversion
- **Nethereum.JsonRpc.Client** - JSON-RPC client abstraction
- **Nethereum.BlockchainProcessing** - Block/transaction/log processing

### Optional Enhancements

- **Nethereum.JsonRpc.SystemTextJsonRpcClient** - AOT-compatible JSON-RPC client
- **Nethereum.JsonRpc.WebSocketClient** - WebSocket client for real-time subscriptions
- **Nethereum.Signer** - Transaction and message signing
- **Nethereum.KeyStore** - Encrypted keystore management
- **Nethereum.HDWallet** - HD wallet (BIP32/BIP39) support

### Client-Specific Extensions

- **Nethereum.Geth** - Geth-specific RPC methods
- **Nethereum.Parity** - Parity-specific RPC methods
- **Nethereum.Besu** - Hyperledger Besu RPC methods
- **Nethereum.Quorum** - Quorum private transaction support

## Playground Examples

Runnable examples available at [Nethereum Playground](https://playground.nethereum.com/):

**Basic Operations:**
- [Example 1001](https://playground.nethereum.com/csharp/id/1001) - Query Ether account balance
- [Example 1002](https://playground.nethereum.com/csharp/id/1002) - Get block, transaction, and receipt
- [Example 1014](https://playground.nethereum.com/csharp/id/1014) - Unit conversion between Ether and Wei

**Transactions:**
- [Example 1003](https://playground.nethereum.com/csharp/id/1003) - Transfer Ether to an account
- [Example 1059](https://playground.nethereum.com/csharp/id/1059) - Send transactions using transaction manager
- [Example 1061](https://playground.nethereum.com/csharp/id/1061) - Transaction replacement

**Smart Contracts:**
- [Example 1005](https://playground.nethereum.com/csharp/id/1005) - Query ERC20 token balance
- [Example 1006](https://playground.nethereum.com/csharp/id/1006) - Smart contract deployment
- [Example 1007](https://playground.nethereum.com/csharp/id/1007) - Deployment, querying, transactions, gas

**Events:**
- [Example 1008](https://playground.nethereum.com/csharp/id/1008) - Events (end-to-end introduction)
- [Example 1009](https://playground.nethereum.com/csharp/id/1009) - Retrieving events from chain

**ENS:**
- [Example 1055](https://playground.nethereum.com/csharp/id/1055) - Resolve ENS address
- [Example 1056](https://playground.nethereum.com/csharp/id/1056) - Resolve ENS URL

## Comprehensive Guides

For in-depth documentation:
- [Nethereum Documentation](https://docs.nethereum.com/)
- [Code Generation Guide](https://docs.nethereum.com/en/latest/nethereum-code-generation/)
- [Account Management Guide](../Nethereum.Accounts/README.md)
- [Contract Interaction Guide](../Nethereum.Contracts/README.md)

## Additional Resources

- **GitHub**: [Nethereum Repository](https://github.com/Nethereum/Nethereum)
- **Playground**: [Try Nethereum Online](https://playground.nethereum.com/)
- **Discord**: [Community Chat](https://discord.gg/jQPrR58FxX)
- **Stack Overflow**: [nethereum tag](https://stackoverflow.com/questions/tagged/nethereum)

## Important Notes

### Transaction Status

Always check `receipt.Status` after waiting for receipt:
- `Status = 1` (or `0x1`) - Transaction succeeded
- `Status = 0` (or `0x0`) - Transaction failed (reverted)

### Gas Price Strategies

Web3 supports multiple gas price strategies:
- **Legacy**: Single `GasPrice` field
- **EIP-1559**: `MaxFeePerGas` and `MaxPriorityFeePerGas` fields
- Use `web3.FeeSuggestion` service for optimal fee estimation

### Account Security

Never hardcode private keys in production code:
- Use environment variables
- Use key management services (Azure Key Vault, AWS KMS)
- Use hardware wallets (Ledger, Trezor) via Nethereum.Signer.* packages

### RPC Endpoint Selection

- **Public Nodes** (Infura, Alchemy, etc.) - Rate-limited, good for development
- **Local Nodes** - Full control, no rate limits, requires infrastructure
- **Archive Nodes** - Required for historical state queries beyond recent blocks
