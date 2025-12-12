# Nethereum.RPC

**Nethereum.RPC** is the core package providing Ethereum JSON-RPC method implementations. It contains all standard Ethereum RPC methods organized into logical categories (Eth, Net, Web3, Personal, etc.) and provides both direct RPC access and service-based APIs.

## Features

- Complete implementation of Ethereum JSON-RPC specification
- Organized API surface: Eth, Net, Web3, Personal, Shh, Debug
- Service-based architecture (EthApiService, NetApiService, etc.)
- Support for all major RPC methods:
  - Account management (eth_accounts, eth_getBalance)
  - Block operations (eth_blockNumber, eth_getBlockByNumber)
  - Transaction handling (eth_sendTransaction, eth_call, eth_estimateGas)
  - Event filtering (eth_newFilter, eth_getLogs)
  - Network info (net_version, eth_chainId)
  - EIP-1559 support (eth_maxPriorityFeePerGas, eth_feeHistory)
- Transaction managers and nonce services
- Fee suggestion services (EIP-1559)
- Works with any IClient implementation (HTTP, WebSocket, IPC)

## Installation

```bash
dotnet add package Nethereum.RPC
```

## Dependencies

- `Nethereum.JsonRpc.Client` - RPC client abstraction
- `Nethereum.Hex` - Hex encoding/decoding
- `Nethereum.Util` - Utilities
- `Nethereum.Model` - Data models
- `Nethereum.Merkle.Patricia` - Merkle proof support

## Quick Start

### Using with Web3 (Recommended)

Most users interact with RPC methods through the Web3 API, which provides a higher-level interface:

```csharp
using Nethereum.Web3;

var web3 = new Web3("https://mainnet.infura.io/v3/YOUR-PROJECT-ID");

// All RPC methods are available through web3.Eth
var blockNumber = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
var balance = await web3.Eth.GetBalance.SendRequestAsync("0x742d35Cc6634C0532925a3b844Bc454e4438f44e");
```

### Direct RPC Usage

For advanced scenarios, you can use RPC methods directly:

```csharp
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.Blocks;
using Nethereum.RPC.Eth;

var client = new RpcClient(new Uri("https://mainnet.infura.io/v3/YOUR-PROJECT-ID"));

// Direct RPC method instantiation
var ethBlockNumber = new EthBlockNumber(client);
var blockNumber = await ethBlockNumber.SendRequestAsync();

var ethGetBalance = new EthGetBalance(client);
var balance = await ethGetBalance.SendRequestAsync("0x742d35Cc6634C0532925a3b844Bc454e4438f44e");
```

## Package Organization

The package is organized by RPC method category:

```
Nethereum.RPC/
├── Eth/                      # Ethereum methods (eth_*)
│   ├── Blocks/              # Block-related methods
│   ├── Transactions/        # Transaction methods
│   ├── Filters/             # Event filtering
│   ├── Services/            # Block services
│   └── DTOs/                # Data transfer objects
├── Net/                      # Network methods (net_*)
├── Web3/                     # Web3 methods (web3_*)
├── Personal/                 # Personal methods (personal_*)
├── Shh/                      # Whisper methods (shh_*)
├── DebugNode/                # Debug methods (debug_*)
├── Chain/                    # Chain-specific methods
├── TransactionManagers/      # Transaction management
├── NonceServices/            # Nonce tracking
├── Fee1559Suggestions/       # EIP-1559 fee estimation
├── EthApiService.cs          # Service wrapper for Eth methods
├── NetApiService.cs          # Service wrapper for Net methods
└── PersonalApiService.cs     # Service wrapper for Personal methods
```

## Examples

### Example 1: Getting Block Information

```csharp
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.Blocks;
using Nethereum.RPC.Eth.DTOs;

var client = new RpcClient(new Uri("https://mainnet.infura.io/v3/YOUR-PROJECT-ID"));

// Get current block number
var ethBlockNumber = new EthBlockNumber(client);
var blockNumber = await ethBlockNumber.SendRequestAsync();
Console.WriteLine($"Current block: {blockNumber.Value}");

// Get block by number
var ethGetBlockByNumber = new EthGetBlockByNumber(client);
var block = await ethGetBlockByNumber.SendRequestAsync(
    new BlockParameter(blockNumber),
    returnFullTransactionObjects: false
);

Console.WriteLine($"Block hash: {block.BlockHash}");
Console.WriteLine($"Block timestamp: {block.Timestamp.Value}");
Console.WriteLine($"Transaction count: {block.TransactionHashes.Length}");
```

**Output:**
```
Current block: 18500000
Block hash: 0x1234...
Block timestamp: 1697000000
Transaction count: 150
```

### Example 2: Checking Account Balance

```csharp
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Eth.DTOs;

var client = new RpcClient(new Uri("https://mainnet.infura.io/v3/YOUR-PROJECT-ID"));
var ethGetBalance = new EthGetBalance(client);

// Get balance at latest block
var balance = await ethGetBalance.SendRequestAsync(
    "0x742d35Cc6634C0532925a3b844Bc454e4438f44e",
    BlockParameter.CreateLatest()
);

// Convert from Wei to Ether
var etherBalance = Web3.Convert.FromWei(balance.Value);
Console.WriteLine($"Balance: {etherBalance} ETH");

// Get balance at specific block
var blockNumber = new BlockParameter(18000000);
var historicalBalance = await ethGetBalance.SendRequestAsync(
    "0x742d35Cc6634C0532925a3b844Bc454e4438f44e",
    blockNumber
);
Console.WriteLine($"Historical balance: {Web3.Convert.FromWei(historicalBalance.Value)} ETH");
```

**From:** `tests/Nethereum.RPC.IntegrationTests/Testers/EthGetBalanceTester.cs:24`

### Example 3: Calling Smart Contract Methods (eth_call)

```csharp
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.Transactions;
using Nethereum.RPC.Eth.DTOs;

var client = new RpcClient(new Uri("https://mainnet.infura.io/v3/YOUR-PROJECT-ID"));

// Example: Call a contract method (e.g., balanceOf)
var ethCall = new EthCall(client);

var callInput = new CallInput
{
    To = "0x32eb97b8ad202b072fd9066c03878892426320ed",
    From = "0x12890D2cce102216644c59daE5baed380d84830c",
    // Function signature + encoded parameters
    Data = "0xc6888fa10000000000000000000000000000000000000000000000000000000000000045"
};

var result = await ethCall.SendRequestAsync(callInput, BlockParameter.CreateLatest());
Console.WriteLine($"Call result: {result}");

// For typed contract interactions, use Nethereum.Contracts instead
```

**From:** `tests/Nethereum.RPC.IntegrationTests/Testers/EthCallTester.cs:18`

### Example 4: Estimating Gas

```csharp
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.Transactions;
using Nethereum.RPC.Eth.DTOs;

var client = new RpcClient(new Uri("https://mainnet.infura.io/v3/YOUR-PROJECT-ID"));
var ethEstimateGas = new EthEstimateGas(client);

var transactionInput = new CallInput
{
    From = "0x12890D2cce102216644c59daE5baed380d84830c",
    To = "0x742d35Cc6634C0532925a3b844Bc454e4438f44e",
    Value = new Nethereum.Hex.HexTypes.HexBigInteger(1000000000000000000), // 1 ETH
};

var gasEstimate = await ethEstimateGas.SendRequestAsync(transactionInput);
Console.WriteLine($"Estimated gas: {gasEstimate.Value}");

// Use the estimate with a buffer for actual transaction
var gasLimit = gasEstimate.Value * 110 / 100; // 10% buffer
Console.WriteLine($"Recommended gas limit: {gasLimit}");
```

### Example 5: Getting Chain ID

```csharp
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;

var client = new RpcClient(new Uri("https://mainnet.infura.io/v3/YOUR-PROJECT-ID"));
var ethChainId = new EthChainId(client);

var chainId = await ethChainId.SendRequestAsync();
Console.WriteLine($"Chain ID: {chainId.Value}");

// Common chain IDs:
// 1 = Ethereum Mainnet
// 5 = Goerli
// 11155111 = Sepolia
// 137 = Polygon
// 42161 = Arbitrum
```

**From:** `tests/Nethereum.RPC.IntegrationTests/Testers/EthChainIdTester.cs:18`

### Example 6: Working with Different Clients (WebSocket, IPC)

```csharp
using Nethereum.JsonRpc.WebSocketClient;
using Nethereum.JsonRpc.IpcClient;
using Nethereum.RPC.Eth.Blocks;
using Nethereum.Web3;
using Nethereum.Web3.Accounts.Managed;

// WebSocket client
var clientWs = new WebSocketClient("ws://127.0.0.1:8546");
var web3Ws = new Web3(clientWs);
var blockNumberWs = await web3Ws.Eth.Blocks.GetBlockNumber.SendRequestAsync();
Console.WriteLine($"Block number (WebSocket): {blockNumberWs.Value}");

// IPC client (Unix socket or named pipe)
var clientIpc = new IpcClient("jsonrpc.ipc");
var web3Ipc = new Web3(clientIpc);
var blockNumberIpc = await web3Ipc.Eth.Blocks.GetBlockNumber.SendRequestAsync();
Console.WriteLine($"Block number (IPC): {blockNumberIpc.Value}");

// With managed account
var account = new ManagedAccount("0x12890d2cce102216644c59daE5baed380d84830c", "password");
var web3WithAccount = new Web3(account, clientWs);
```

**From:** `consoletests/Nethereum.Parity.Reactive.ConsoleTest/Program.cs:20`

### Example 7: Building RPC Requests for Custom Use Cases

```csharp
using Nethereum.RPC.Eth;
using Nethereum.RPC.Eth.DTOs;

// Build request without sending it (useful for batching or custom handling)
var ethGetBalance = new EthGetBalance();
var request = ethGetBalance.BuildRequest(
    "0x12890d2cce102216644c59daE5baed380d84830c",
    BlockParameter.CreateLatest()
);

Console.WriteLine($"Method: {request.Method}");
Console.WriteLine($"Params: {string.Join(", ", request.RawParameters)}");

// You can now send this request through any client
// Or batch multiple requests together
var ethBlockNumber = new EthBlockNumber();
var request2 = ethBlockNumber.BuildRequest();

// Both requests can be sent in a batch
```

**From:** `consoletests/Nethereum.Parity.Reactive.ConsoleTest/Program.cs:36`

### Example 8: Network Information

```csharp
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Net;

var client = new RpcClient(new Uri("https://mainnet.infura.io/v3/YOUR-PROJECT-ID"));

// Get network version (chain ID as string)
var netVersion = new NetVersion(client);
var version = await netVersion.SendRequestAsync();
Console.WriteLine($"Network version: {version}");

// Check if node is listening for network connections
var netListening = new NetListening(client);
var isListening = await netListening.SendRequestAsync();
Console.WriteLine($"Node listening: {isListening}");

// Get peer count
var netPeerCount = new NetPeerCount(client);
var peerCount = await netPeerCount.SendRequestAsync();
Console.WriteLine($"Peer count: {peerCount.Value}");
```

### Example 9: Transaction Receipt

```csharp
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.Transactions;
using Nethereum.RPC.Eth.DTOs;

var client = new RpcClient(new Uri("https://mainnet.infura.io/v3/YOUR-PROJECT-ID"));
var ethGetTransactionReceipt = new EthGetTransactionReceipt(client);

var txHash = "0x1234..."; // Your transaction hash
var receipt = await ethGetTransactionReceipt.SendRequestAsync(txHash);

if (receipt != null)
{
    Console.WriteLine($"Transaction status: {(receipt.Status.Value == 1 ? "Success" : "Failed")}");
    Console.WriteLine($"Block number: {receipt.BlockNumber.Value}");
    Console.WriteLine($"Gas used: {receipt.GasUsed.Value}");
    Console.WriteLine($"Contract address: {receipt.ContractAddress}"); // If contract creation

    // Decode events (logs)
    foreach (var log in receipt.Logs)
    {
        Console.WriteLine($"Log address: {log.Address}");
        Console.WriteLine($"Log topics: {string.Join(", ", log.Topics)}");
    }
}
else
{
    Console.WriteLine("Transaction not found or pending");
}
```

### Example 10: EIP-1559 Gas Fee Estimation

```csharp
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Eth.DTOs;

var client = new RpcClient(new Uri("https://mainnet.infura.io/v3/YOUR-PROJECT-ID"));

// Get max priority fee per gas (tip to miner)
var ethMaxPriorityFeePerGas = new EthMaxPriorityFeePerGas(client);
var maxPriorityFee = await ethMaxPriorityFeePerGas.SendRequestAsync();
Console.WriteLine($"Max priority fee: {maxPriorityFee.Value} wei");

// Get base fee from latest block
var ethGetBlockByNumber = new EthGetBlockByNumber(client);
var block = await ethGetBlockByNumber.SendRequestAsync(
    BlockParameter.CreateLatest(),
    false
);
var baseFee = block.BaseFeePerGas;
Console.WriteLine($"Base fee: {baseFee.Value} wei");

// Calculate max fee per gas (base fee + priority fee + buffer)
var maxFeePerGas = baseFee.Value + (maxPriorityFee.Value * 2);
Console.WriteLine($"Recommended max fee: {maxFeePerGas} wei");

// Use in EIP-1559 transaction
var tx1559 = new TransactionInput
{
    From = "0x12890D2cce102216644c59daE5baed380d84830c",
    To = "0x742d35Cc6634C0532925a3b844Bc454e4438f44e",
    Value = new Nethereum.Hex.HexTypes.HexBigInteger(1000000000000000000),
    MaxPriorityFeePerGas = maxPriorityFee,
    MaxFeePerGas = new Nethereum.Hex.HexTypes.HexBigInteger(maxFeePerGas),
    Type = new Nethereum.Hex.HexTypes.HexBigInteger(2) // EIP-1559 transaction type
};
```

## API Service Classes

The package provides service classes that group related RPC methods:

### EthApiService

Provides access to all Ethereum RPC methods:

```csharp
using Nethereum.JsonRpc.Client;
using Nethereum.RPC;

var client = new RpcClient(new Uri("https://mainnet.infura.io/v3/YOUR-PROJECT-ID"));
var ethApi = new EthApiService(client);

// Access any Eth method through the service
var blockNumber = await ethApi.Blocks.GetBlockNumber.SendRequestAsync();
var balance = await ethApi.GetBalance.SendRequestAsync("0x742d35Cc6634C0532925a3b844Bc454e4438f44e");
var gasPrice = await ethApi.GasPrice.SendRequestAsync();
```

### NetApiService

Provides access to network RPC methods:

```csharp
var netApi = new NetApiService(client);

var version = await netApi.Version.SendRequestAsync();
var peerCount = await netApi.PeerCount.SendRequestAsync();
var isListening = await netApi.Listening.SendRequestAsync();
```

### PersonalApiService

Provides access to personal/account RPC methods:

```csharp
var personalApi = new PersonalApiService(client);

// Note: Most nodes disable personal API methods for security
var accounts = await personalApi.ListAccounts.SendRequestAsync();
```

## Block Parameters

Many RPC methods accept a `BlockParameter` to specify which block to query:

```csharp
using Nethereum.RPC.Eth.DTOs;

// Latest block
var latest = BlockParameter.CreateLatest();

// Earliest block (genesis)
var earliest = BlockParameter.CreateEarliest();

// Pending block
var pending = BlockParameter.CreatePending();

// Specific block number
var specific = new BlockParameter(18000000);

// Use with any method that accepts block parameter
var balance = await ethGetBalance.SendRequestAsync(address, latest);
```

## Transaction Types

The package supports all Ethereum transaction types:

```csharp
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Hex.HexTypes;

// Legacy transaction (Type 0)
var legacyTx = new TransactionInput
{
    From = "0x...",
    To = "0x...",
    Value = new HexBigInteger(1000000000000000000),
    Gas = new HexBigInteger(21000),
    GasPrice = new HexBigInteger(20000000000) // 20 gwei
};

// EIP-2930 transaction (Type 1) - with access list
var eip2930Tx = new TransactionInput
{
    From = "0x...",
    To = "0x...",
    Type = new HexBigInteger(1),
    AccessList = new AccessListItem[]
    {
        new AccessListItem
        {
            Address = "0x...",
            StorageKeys = new[] { "0x..." }
        }
    }
};

// EIP-1559 transaction (Type 2) - with dynamic fees
var eip1559Tx = new TransactionInput
{
    From = "0x...",
    To = "0x...",
    Type = new HexBigInteger(2),
    MaxPriorityFeePerGas = new HexBigInteger(2000000000), // 2 gwei tip
    MaxFeePerGas = new HexBigInteger(50000000000) // 50 gwei max
};
```

## DTOs (Data Transfer Objects)

The package includes comprehensive DTOs for all RPC requests and responses:

- `BlockWithTransactions` / `Block` - Block information
- `Transaction` - Transaction details
- `TransactionReceipt` - Transaction receipt with logs
- `FilterInput` - Event filter configuration
- `Log` - Event log entry
- `CallInput` / `TransactionInput` - Transaction/call input
- `SyncStatus` - Node sync status
- `AccessListItem` - EIP-2930 access list

All DTOs are in the `Nethereum.RPC.Eth.DTOs` namespace.

## Error Handling

```csharp
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;

var client = new RpcClient(new Uri("https://mainnet.infura.io/v3/YOUR-PROJECT-ID"));
var ethGetBalance = new EthGetBalance(client);

try
{
    var balance = await ethGetBalance.SendRequestAsync(
        "0xinvalid-address",
        BlockParameter.CreateLatest()
    );
}
catch (RpcResponseException ex)
{
    Console.WriteLine($"RPC Error: {ex.Message}");
    Console.WriteLine($"RPC Error Code: {ex.RpcError?.Code}");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
```

## Best Practices

1. **Use Web3 for Most Scenarios**: Unless you need direct RPC access, use the Web3 API which provides a more convenient interface

2. **Reuse Client Instances**: Create one client and reuse it across multiple RPC method instances

3. **Handle Null Responses**: Some methods return null (e.g., pending transactions, receipts for pending txs)

4. **Use BlockParameter Appropriately**:
   - `Latest` - Most recent block (may reorganize)
   - `Finalized` - Finalized block (after merge, more stable)
   - Specific number - For historical queries

5. **Batch Requests When Possible**: For multiple independent queries, use batch requests to reduce round trips

6. **Choose the Right Client**:
   - HTTP for simple queries
   - WebSocket for subscriptions and frequent updates
   - IPC for local node connections (lowest latency)

7. **EIP-1559 Gas Estimation**: Always query `eth_maxPriorityFeePerGas` and base fee from latest block for accurate EIP-1559 transactions

8. **Error Handling**: Always handle `RpcResponseException` for RPC-level errors (invalid params, execution reverted, etc.)

## Advanced Usage

### Transaction Managers

The package includes transaction manager infrastructure for handling nonce tracking and transaction lifecycle:

```csharp
using Nethereum.RPC.TransactionManagers;
using Nethereum.RPC.NonceServices;

// Custom nonce management
var nonceService = new InMemoryNonceService(address, client);
var currentNonce = await nonceService.GetNextNonceAsync();
```

### Fee Suggestion Services

For dynamic gas fee estimation:

```csharp
using Nethereum.RPC.Fee1559Suggestions;

var feeSuggestionService = new Fee1559SuggestionService(client);
var suggestion = await feeSuggestionService.SuggestFeeAsync();

Console.WriteLine($"Suggested base fee: {suggestion.BaseFee}");
Console.WriteLine($"Suggested max priority fee: {suggestion.MaxPriorityFeePerGas}");
Console.WriteLine($"Suggested max fee: {suggestion.MaxFeePerGas}");
```

## Related Packages

- **Nethereum.Web3** - High-level Web3 API (builds on this package)
- **Nethereum.Contracts** - Smart contract interaction (uses RPC methods)
- **Nethereum.JsonRpc.Client** - HTTP client implementation
- **Nethereum.JsonRpc.WebSocketClient** - WebSocket client
- **Nethereum.JsonRpc.IpcClient** - IPC client
- **Nethereum.RPC.Reactive** - Reactive extensions for RPC (polling, subscriptions)

## Testing

The package includes comprehensive integration tests demonstrating all RPC methods:

```
tests/Nethereum.RPC.IntegrationTests/Testers/
├── EthGetBalanceTester.cs
├── EthBlockNumberTester.cs
├── EthCallTester.cs
├── EthChainIdTester.cs
├── EthSendTransactionTester.cs
└── ... (50+ test classes)
```

## Additional Resources

- [Ethereum JSON-RPC Specification](https://ethereum.org/en/developers/docs/apis/json-rpc/)
- [Nethereum Documentation](http://docs.nethereum.com)
- [EIP-1559 Documentation](https://eips.ethereum.org/EIPS/eip-1559)
- [EIP-2930 Access Lists](https://eips.ethereum.org/EIPS/eip-2930)
