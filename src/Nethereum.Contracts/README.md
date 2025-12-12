# Nethereum.Contracts

**Nethereum.Contracts** is the core library for interacting with Ethereum smart contracts via RPC. It provides strongly-typed contract interaction, automatic ABI encoding/decoding, event filtering, and comprehensive support for deployment, function calls, and event handling.

## Features

- **Strongly-Typed Contract Interaction** - Generate C# classes from ABI
- **Contract Deployment** - Deploy contracts with constructor parameters
- **Function Calls** - Call and send contract functions
- **Event Handling** - Query and decode contract events
- **ERC Standards** - Built-in support for ERC20, ERC721, ERC1155, ERC1271, ERC1820, ERC165, ERC2535, ERC6492
- **EIP Standards** - EIP-3009 (transfer with authorization), EIP-6093 (custom errors)
- **ENS Support** - Ethereum Name Service resolution
- **Multicall** - Batch multiple contract calls
- **Query Handlers** - Simplified contract queries
- **Transaction Handlers** - Simplified contract transactions
- **AOT Compatible** - Works with Native AOT compilation

## Installation

```bash
dotnet add package Nethereum.Contracts
```

## Dependencies

**Nethereum:**
- **Nethereum.ABI** - ABI encoding/decoding
- **Nethereum.RPC** - RPC functionality
- **Nethereum.Hex** - Hex utilities
- **Nethereum.Util.Rest** - REST utilities for HTTP metadata

**External:**
- **ADRaffy.ENSNormalize** (v0.1.5) - ENS name normalization (UTS-46/ENSIP-15)

## Key Concepts

### Contract Interaction Patterns

Nethereum.Contracts supports three main patterns for interacting with smart contracts:

**1. Direct ABI Usage** - Simple, dynamic approach using ABI JSON strings
**2. Strongly-Typed DTOs** - Type-safe with IntelliSense (recommended)
**3. Code Generation** - Automated C# class generation from ABI (production recommended)

### Architecture

The library is organized around these core concepts:

- **Contract** - Represents a deployed smart contract at a specific address
- **Function** - Represents a contract function (call or transaction)
- **Event** - Represents a contract event for filtering and decoding
- **ContractHandler** - Unified interface for contract operations
- **Standard Services** - Pre-built services for ERC20, ERC721, ERC1155, etc.

### Typed vs Untyped: Which Should You Use?

Nethereum supports two fundamental approaches for contract interaction:

#### Untyped Approach (Dynamic)

**When to use:**
- Quick scripts and one-off queries
- Rapid prototyping
- Simple contracts with few functions
- When you don't need compile-time safety

**Advantages:**
- Less code (no DTO classes needed)
- Faster to write initially
- Flexible for exploration

**Disadvantages:**
- No compile-time type checking
- No IntelliSense
- Parameter order errors only caught at runtime
- Harder to maintain
- No refactoring support

**Example:**
```csharp
var abi = @"[...]";
var contract = web3.Eth.GetContract(abi, contractAddress);
var balanceFunction = contract.GetFunction("balanceOf");
var balance = await balanceFunction.CallAsync<BigInteger>("0x742d35Cc...");
// If you pass wrong type or wrong order, you only find out at runtime!
```

#### Typed Approach (DTOs)

**When to use:**
- Production applications
- Team development
- Complex contracts
- When compile-time safety matters
- Long-term maintained code

**Advantages:**
- Compile-time type checking
- IntelliSense support
- Clear parameter names
- Refactoring support
- Self-documenting code
- Catches errors before deployment

**Disadvantages:**
- More initial code (DTO classes)
- Requires understanding of attributes
- Slightly more verbose

**Example:**
```csharp
[Function("balanceOf", "uint256")]
public class BalanceOfFunction : FunctionMessage
{
    [Parameter("address", "owner", 1)]
    public string Owner { get; set; }
}

var queryHandler = web3.Eth.GetContractQueryHandler<BalanceOfFunction>();
var balance = await queryHandler.QueryAsync<BigInteger>(
    contractAddress,
    new BalanceOfFunction { Owner = "0x742d35Cc..." }
);
// Wrong type = compile error, not runtime error!
```

**Comparison Table:**

| Feature | Untyped | Typed | Code Generation |
|---------|---------|-------|-----------------|
| Compile-time safety | ✗ | ✓ | ✓ |
| IntelliSense | ✗ | ✓ | ✓ |
| Lines of code | Fewest | More | Auto-generated |
| Learning curve | Easy | Medium | Easy (after setup) |
| Maintainability | Poor | Good | Excellent |
| Best for | Scripts | Production | Production |

**Our Recommendation:**
- **Prototyping:** Start with untyped
- **Production:** Use typed DTOs or code generation
- **Large projects:** Always use code generation

From: [Nethereum Playground Example 1007](https://playground.nethereum.com/csharp/id/1007) (typed), [Example 1045](https://playground.nethereum.com/csharp/id/1045) (untyped)

## Quick Start

### Interacting with ERC20 Token

From: [Nethereum Playground Example 1005](https://playground.nethereum.com/csharp/id/1005)

```csharp
using Nethereum.Web3;
using Nethereum.Contracts.Standards.ERC20;

var web3 = new Web3("https://mainnet.infura.io/v3/YOUR-PROJECT-ID");

// ERC20 service for any token
var tokenAddress = "0x9f8f72aa9304c8b593d555f12ef6589cc3a579a2"; // MKR token
var erc20Service = web3.Eth.ERC20.GetContractService(tokenAddress);

// Get token details
var name = await erc20Service.NameQueryAsync();
var symbol = await erc20Service.SymbolQueryAsync();
var decimals = await erc20Service.DecimalsQueryAsync();
var totalSupply = await erc20Service.TotalSupplyQueryAsync();

Console.WriteLine($"Token: {name} ({symbol})");
Console.WriteLine($"Decimals: {decimals}");
Console.WriteLine($"Total Supply: {totalSupply}");

// Get balance
var holderAddress = "0x742d35Cc6634C0532925a3b844Bc454e4438f44e";
var balance = await erc20Service.BalanceOfQueryAsync(holderAddress);
Console.WriteLine($"Balance: {balance}");
```

### Deploying and Interacting with Custom Contract

From: [Nethereum Playground Example 1006](https://playground.nethereum.com/csharp/id/1006)

#### Typed Deployment (Recommended)

The typed approach uses `ContractDeploymentMessage` for compile-time safety:

```csharp
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.Contracts;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts.CQS;

// Define deployment message with constructor parameters
public class StandardTokenDeployment : ContractDeploymentMessage
{
    public static string BYTECODE = "0x60606040526040516020806106f5833981016040528080519060200190919050505b80600160005060003373ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060005081905550806000600050819055505b506106868061006f6000396000f360606040523615610074576000357c010000000000000000000000000000000000000000000000000000000090048063095ea7b31461008157806318160ddd146100b657806323b872dd146100d957806370a0823114610117578063a9059cbb14610143578063dd62ed3e1461017857610074565b61007f5b610002565b565b005b6100a060048080359060200190919080359060200190919050506101ad565b6040518082815260200191505060405180910390f35b6100c36004805050610674565b6040518082815260200191505060405180910390f35b6101016004808035906020019091908035906020019091908035906020019091905050610281565b6040518082815260200191505060405180910390f35b61012d600480803590602001909190505061048d565b6040518082815260200191505060405180910390f35b61016260048080359060200190919080359060200190919050506104cb565b6040518082815260200191505060405180910390f35b610197600480803590602001909190803590602001909190505061060b565b6040518082815260200191505060405180910390f35b600081600260005060003373ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060005060008573ffffffffffffffffffffffffffffffffffffffff168152602001908152602001600020600050819055508273ffffffffffffffffffffffffffffffffffffffff163373ffffffffffffffffffffffffffffffffffffffff167f8c5be1e5ebec7d5bd14f71427d1e84f3dd0314c0f7b2291e5b200ac8c7c3b925846040518082815260200191505060405180910390a36001905061027b565b92915050565b600081600160005060008673ffffffffffffffffffffffffffffffffffffffff168152602001908152602001600020600050541015801561031b575081600260005060008673ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060005060003373ffffffffffffffffffffffffffffffffffffffff1681526020019081526020016000206000505410155b80156103275750600082115b1561047c5781600160005060008573ffffffffffffffffffffffffffffffffffffffff1681526020019081526020016000206000828282505401925050819055508273ffffffffffffffffffffffffffffffffffffffff168473ffffffffffffffffffffffffffffffffffffffff167fddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef846040518082815260200191505060405180910390a381600160005060008673ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060008282825054039250508190555081600260005060008673ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060005060003373ffffffffffffffffffffffffffffffffffffffff1681526020019081526020016000206000828282505403925050819055506001905061048656610485565b60009050610486565b5b9392505050565b6000600160005060008373ffffffffffffffffffffffffffffffffffffffff1681526020019081526020016000206000505490506104c6565b919050565b600081600160005060003373ffffffffffffffffffffffffffffffffffffffff168152602001908152602001600020600050541015801561050c5750600082115b156105fb5781600160005060003373ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060008282825054039250508190555081600160005060008573ffffffffffffffffffffffffffffffffffffffff1681526020019081526020016000206000828282505401925050819055508273ffffffffffffffffffffffffffffffffffffffff163373ffffffffffffffffffffffffffffffffffffffff167fddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef846040518082815260200191505060405180910390a36001905061060556610604565b60009050610605565b5b92915050565b6000600260005060008473ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060005060008373ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060005054905061066e565b92915050565b60006000600050549050610683565b9056";

    public StandardTokenDeployment() : base(BYTECODE)
    {
    }

    [Parameter("uint256", "totalSupply")]
    public BigInteger TotalSupply { get; set; }
}

var privateKey = "0x7580e7fb49df1c861f0050fae31c2224c6aba908e116b8da44ee8cd927b990b0";
var chainId = 444444444500;
var account = new Account(privateKey, chainId);
var web3 = new Web3(account, "http://testchain.nethereum.com:8545");

// Create deployment message with constructor parameters
var deploymentMessage = new StandardTokenDeployment
{
    TotalSupply = 100000
};

// Deploy using typed handler
var deploymentHandler = web3.Eth.GetContractDeploymentHandler<StandardTokenDeployment>();
var transactionReceipt = await deploymentHandler.SendRequestAndWaitForReceiptAsync(deploymentMessage);
var contractAddress = transactionReceipt.ContractAddress;

Console.WriteLine($"Contract deployed at: {contractAddress}");
```

**Why use typed deployment:**
- Constructor parameters are strongly-typed properties
- Compile-time validation of parameter types and order
- IntelliSense support for deployment parameters
- Automatic gas estimation, nonce management
- Self-documenting deployment code

#### Untyped Deployment (Alternative)

For quick scripts or when you don't want to define deployment classes:

```csharp
var abi = @"[{'inputs':[{'name':'totalSupply','type':'uint256'}],'stateMutability':'nonpayable','type':'constructor'}...]";
var bytecode = "0x608060405234801561001057600080fd5b50...";

// Deploy with constructor parameters as params
var receipt = await web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(
    abi,
    bytecode,
    account.Address,
    new Nethereum.Hex.HexTypes.HexBigInteger(3000000), // gas
    null, // gas price
    null, // value
    100000 // constructor parameter: totalSupply
);

var contractAddress = receipt.ContractAddress;
```

**Trade-offs:**
- Faster to write for simple deployments
- No compile-time checking of constructor parameters
- Easy to pass wrong parameter type or order
- Less maintainable for complex constructors
```

## Contract Definition Patterns

### Pattern 1: Using Contract ABI Directly

Simple approach for quick interactions:

```csharp
var abi = @"[{'inputs':[],'name':'totalSupply','outputs':[{'type':'uint256'}],'type':'function'}]";
var contract = web3.Eth.GetContract(abi, contractAddress);

var function = contract.GetFunction("totalSupply");
var totalSupply = await function.CallAsync<BigInteger>();
```

### Pattern 2: Strongly-Typed DTOs (Recommended)

Define C# classes for type safety and IntelliSense:

```csharp
using Nethereum.Contracts;
using Nethereum.ABI.FunctionEncoding.Attributes;

// Function message
[Function("balanceOf", "uint256")]
public class BalanceOfFunction : FunctionMessage
{
    [Parameter("address", "_owner", 1)]
    public string Owner { get; set; }
}

// Usage
var balanceOfFunction = contract.GetFunction<BalanceOfFunction>();
var balance = await balanceOfFunction.CallAsync<BigInteger>(new BalanceOfFunction
{
    Owner = "0x742d35Cc6634C0532925a3b844Bc454e4438f44e"
});
```

### Extension Methods for FunctionMessage

Nethereum provides powerful extension methods to simplify common operations with FunctionMessages:

```csharp
using Nethereum.Contracts.Extensions;

// 1. Create CallInput for eth_call (read-only)
var balanceOfFunction = new BalanceOfFunction { Owner = senderAddress };
var callInput = balanceOfFunction.CreateCallInput(contractAddress);
// Use for EVM simulation, Nethereum.EVM, etc.

// 2. Create TransactionInput for transactions
var transferFunction = new TransferFunction { To = receiver, TokenAmount = 1000 };
var transactionInput = transferFunction.CreateTransactionInput(contractAddress);
// Complete TransactionInput ready to sign

// 3. Get encoded function call data
var callData = transferFunction.GetCallData();
Console.WriteLine($"Encoded data: {callData.ToHex()}");
// Perfect for analyzing transactions, debugging ABI encoding

// 4. Decode transaction input to FunctionMessage
var txn = await web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync("0x...");
var decodedTransfer = new TransferFunction().DecodeTransaction(txn);
Console.WriteLine($"Decoded to: {decodedTransfer.To}, amount: {decodedTransfer.TokenAmount}");

// 5. Check if transaction matches function signature
if (txn.IsTransactionForFunctionMessage<TransferFunction>())
{
    var transfer = new TransferFunction().DecodeTransaction(txn);
    Console.WriteLine($"Transfer to {transfer.To}");
}
```

**Common Use Cases:**

- **EVM Simulation:** Use `CreateCallInput()` to simulate contract calls locally
- **Transaction Analysis:** Use `DecodeTransaction()` to parse historical transactions
- **Signature Verification:** Use `IsTransactionForFunctionMessage<T>()` to filter transactions
- **Data Extraction:** Use `GetCallData()` to get raw encoded function data

From: [Nethereum Playground Example 1063](https://playground.nethereum.com/csharp/id/1063), [Example 1075](https://playground.nethereum.com/csharp/id/1075), [Example 1079](https://playground.nethereum.com/csharp/id/1079)

### Pattern 3: Code Generation (Production Recommended)

For production applications, use the **Nethereum Code Generator** to automatically create strongly-typed C# classes from your contract ABI. This eliminates manual DTO creation, reduces errors, and provides the most type-safe contract interaction pattern.

**Installation:**
```bash
dotnet tool install -g Nethereum.Generator.Console
```

**Generate from ABI:**
```bash
Nethereum.Generator.Console generate from-abi \
    -abi MyContract.abi.json \
    -o Generated \
    -n MyApp.Contracts
```

**Generate from Project (Truffle/Hardhat):**
```bash
Nethereum.Generator.Console generate from-project \
    -p ./contracts \
    -o ./src/Generated \
    -n MyApp.Contracts
```

#### What Gets Generated

The code generator creates a complete **ContractService** class that provides:

1. **Deployment Messages** - Typed constructor parameters
2. **Function Messages** - All function DTOs with parameters
3. **Event DTOs** - All event definitions
4. **ContractService** - High-level wrapper with typed methods

**Key Generated Methods:**

```csharp
public class StandardTokenService
{
    // Static deployment method - deploys and returns service instance
    public static Task<StandardTokenService> DeployContractAndGetServiceAsync(
        Web3 web3,
        StandardTokenDeployment deployment
    );

    // Typed query methods - for view/pure functions
    public Task<BigInteger> BalanceOfQueryAsync(string owner);
    public Task<BigInteger> TotalSupplyQueryAsync();
    public Task<BigInteger> AllowanceQueryAsync(string owner, string spender);

    // Typed transaction methods - for state-changing functions
    public Task<TransactionReceipt> TransferRequestAndWaitForReceiptAsync(TransferFunction function);
    public Task<TransactionReceipt> ApproveRequestAndWaitForReceiptAsync(ApproveFunction function);

    // Event access
    public Event<TransferEventDTO> GetTransferEvent();
}
```

#### Complete Example: Manual vs Generated

**Before (Manual Pattern):**

```csharp
// Define deployment message manually
public class StandardTokenDeployment : ContractDeploymentMessage
{
    public static string BYTECODE = "0x608060...";
    public StandardTokenDeployment() : base(BYTECODE) { }

    [Parameter("uint256", "totalSupply")]
    public BigInteger TotalSupply { get; set; }
}

// Define function messages manually
[Function("balanceOf", "uint256")]
public class BalanceOfFunction : FunctionMessage
{
    [Parameter("address", "owner", 1)]
    public string Owner { get; set; }
}

[Function("transfer", "bool")]
public class TransferFunction : FunctionMessage
{
    [Parameter("address", "to", 1)]
    public string To { get; set; }

    [Parameter("uint256", "value", 2)]
    public BigInteger Value { get; set; }
}

// Deploy manually
var deploymentHandler = web3.Eth.GetContractDeploymentHandler<StandardTokenDeployment>();
var receipt = await deploymentHandler.SendRequestAndWaitForReceiptAsync(new StandardTokenDeployment
{
    TotalSupply = 100000
});
var contractAddress = receipt.ContractAddress;

// Query manually
var balanceOfHandler = web3.Eth.GetContractQueryHandler<BalanceOfFunction>();
var balance = await balanceOfHandler.QueryAsync<BigInteger>(
    contractAddress,
    new BalanceOfFunction { Owner = address }
);

// Transaction manually
var transferHandler = web3.Eth.GetContractTransactionHandler<TransferFunction>();
var transferReceipt = await transferHandler.SendRequestAndWaitForReceiptAsync(
    contractAddress,
    new TransferFunction { To = receiver, Value = 1000 }
);
```

**After (Generated Pattern):**

```csharp
using MyApp.Contracts.StandardToken.Service;
using MyApp.Contracts.StandardToken.ContractDefinition;

// Deploy and get service in ONE call
var tokenService = await StandardTokenService.DeployContractAndGetServiceAsync(
    web3,
    new StandardTokenDeployment { TotalSupply = 100000 }
);

// Query with simple typed method
var balance = await tokenService.BalanceOfQueryAsync(address);

// Transaction with simple typed method
var transferReceipt = await tokenService.TransferRequestAndWaitForReceiptAsync(
    new TransferFunction { To = receiver, Value = 1000 }
);

// Access events through service
var transferEvent = tokenService.GetTransferEvent();
var filter = transferEvent.CreateFilterInput(fromAddress: address);
var logs = await transferEvent.GetAllChangesAsync(filter);
```

**Comparison:**

| Aspect | Manual | Generated |
|--------|--------|-----------|
| **Lines of code** | ~80 lines | ~15 lines |
| **Deployment** | 5 steps | 1 step |
| **Query** | 4 steps | 1 step |
| **Transaction** | 4 steps | 1 step |
| **Type safety** | Manual DTOs | Auto-generated DTOs |
| **Refactoring** | Manual updates | Regenerate from ABI |
| **IntelliSense** | Full | Full |
| **Maintainability** | High effort | Low effort |

**Why Use Code Generation:**

1. **Eliminates boilerplate** - No manual DTO creation
2. **Single source of truth** - ABI is the only source
3. **Compile-time safety** - All parameters typed
4. **Easy refactoring** - Regenerate when contract changes
5. **IntelliSense support** - All methods discoverable
6. **Less error-prone** - No manual attribute decoration
7. **Faster development** - Write business logic, not infrastructure

**For comprehensive code generation documentation, see:**
[Nethereum Code Generation Guide](https://docs.nethereum.com/en/latest/nethereum-code-generation/)

## Usage Examples

### Example 1: ERC20 Token Transfer

```csharp
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.Contracts;
using Nethereum.ABI.FunctionEncoding.Attributes;

var account = new Account("0xPRIVATE_KEY", chainId: 1);
var web3 = new Web3(account, "https://mainnet.infura.io/v3/YOUR-PROJECT-ID");

// Transfer function DTO
[Function("transfer", "bool")]
public class TransferFunction : FunctionMessage
{
    [Parameter("address", "to", 1)]
    public string To { get; set; }

    [Parameter("uint256", "amount", 2)]
    public BigInteger Amount { get; set; }
}

// Get ERC20 contract
var tokenAddress = "0x...";
var contract = web3.Eth.GetContract("[...abi...]", tokenAddress);

// Send transfer transaction
var transferHandler = web3.Eth.GetContractTransactionHandler<TransferFunction>();
var receipt = await transferHandler.SendRequestAndWaitForReceiptAsync(
    tokenAddress,
    new TransferFunction
    {
        To = "0xRECIPIENT",
        Amount = 1000000000000000000, // 1 token (18 decimals)
        Gas = 100000
    }
);

Console.WriteLine($"Transfer transaction: {receipt.TransactionHash}");
Console.WriteLine($"Status: {(receipt.Status.Value == 1 ? "Success" : "Failed")}");
```

### Example 1a: Complex Return Types (Multiple Values)

From: [Nethereum Playground Example 1007](https://playground.nethereum.com/csharp/id/1007)

For functions that return multiple values or complex objects, use `QueryDeserializingToObjectAsync` with a `FunctionOutputDTO`:

```csharp
using Nethereum.Web3;
using Nethereum.Contracts;
using Nethereum.ABI.FunctionEncoding.Attributes;

// Function that returns a single value
[Function("balanceOf", "uint256")]
public class BalanceOfFunction : FunctionMessage
{
    [Parameter("address", "owner", 1)]
    public string Owner { get; set; }
}

// Output DTO for deserializing the return value
[FunctionOutput]
public class BalanceOfOutputDTO : IFunctionOutputDTO
{
    [Parameter("uint256", "balance", 1)]
    public BigInteger Balance { get; set; }
}

var web3 = new Web3("https://mainnet.infura.io/v3/YOUR-PROJECT-ID");
var contractAddress = "0x...";

var balanceHandler = web3.Eth.GetContractQueryHandler<BalanceOfFunction>();
var balanceMessage = new BalanceOfFunction { Owner = "0xOWNER" };

// Query and deserialize to output DTO
var balanceOutput = await balanceHandler.QueryDeserializingToObjectAsync<BalanceOfOutputDTO>(
    balanceMessage,
    contractAddress
);

Console.WriteLine($"Balance: {balanceOutput.Balance}");

// Query at specific block number (historical state)
var balanceAtBlock = await balanceHandler.QueryDeserializingToObjectAsync<BalanceOfOutputDTO>(
    balanceMessage,
    contractAddress,
    new Nethereum.RPC.Eth.DTOs.BlockParameter(15000000)
);
```

**For functions with multiple return values:**

```csharp
[FunctionOutput]
public class TokenInfoOutputDTO : IFunctionOutputDTO
{
    [Parameter("string", "name", 1)]
    public string Name { get; set; }

    [Parameter("string", "symbol", 2)]
    public string Symbol { get; set; }

    [Parameter("uint8", "decimals", 3)]
    public byte Decimals { get; set; }
}

// Use same QueryDeserializingToObjectAsync pattern
var tokenInfo = await handler.QueryDeserializingToObjectAsync<TokenInfoOutputDTO>(
    functionMessage,
    contractAddress
);
```

### Example 1b: Transaction Customization

From: [Nethereum Playground Example 1007](https://playground.nethereum.com/csharp/id/1007)

All `FunctionMessage` and `ContractDeploymentMessage` classes support transaction customization through properties:

```csharp
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.Contracts;
using Nethereum.Util;

var account = new Account("0xPRIVATE_KEY", chainId: 1);
var web3 = new Web3(account, "https://mainnet.infura.io/v3/YOUR-PROJECT-ID");

var transferFunction = new TransferFunction
{
    To = "0xRECIPIENT",
    Amount = 1000,

    // Customize gas limit
    Gas = 100000,

    // Customize gas price (in Wei, convert from Gwei)
    GasPrice = Web3.Convert.ToWei(25, UnitConversion.EthUnit.Gwei),

    // Customize nonce (usually auto-calculated)
    Nonce = 5,

    // Send Ether along with function call
    AmountToSend = Web3.Convert.ToWei(0.1, UnitConversion.EthUnit.Ether)
};

var transferHandler = web3.Eth.GetContractTransactionHandler<TransferFunction>();
var receipt = await transferHandler.SendRequestAndWaitForReceiptAsync(
    contractAddress,
    transferFunction
);
```

**Estimating Gas:**

```csharp
var transferFunction = new TransferFunction { To = receiver, Amount = 1000 };
var transferHandler = web3.Eth.GetContractTransactionHandler<TransferFunction>();

// Estimate gas for this specific transaction
var estimatedGas = await transferHandler.EstimateGasAsync(contractAddress, transferFunction);
transferFunction.Gas = estimatedGas.Value;

// Now send with accurate gas estimate
var receipt = await transferHandler.SendRequestAndWaitForReceiptAsync(
    contractAddress,
    transferFunction
);
```

**When to customize:**

- **Gas:** Estimate first, then set manually if needed for complex transactions
- **GasPrice:** Set higher for faster confirmation, lower to save costs
- **Nonce:** Usually auto-managed, set manually only for offline signing or parallel transactions
- **AmountToSend:** When function is payable and requires Ether

### Example 1c: Offline Transaction Signing

From: [Nethereum Playground Example 1007](https://playground.nethereum.com/csharp/id/1007)

For offline signing, set all transaction parameters and use `SignTransactionAsync`:

```csharp
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.Contracts;
using Nethereum.Util;

var account = new Account("0xPRIVATE_KEY", chainId: 1);
var web3 = new Web3(account);  // No RPC endpoint needed for signing

var transferFunction = new TransferFunction
{
    To = "0xRECIPIENT",
    Amount = 1000,

    // MUST set all values for offline signing
    Nonce = 2,
    Gas = 21000,
    GasPrice = Web3.Convert.ToWei(25, UnitConversion.EthUnit.Gwei)
};

var transferHandler = web3.Eth.GetContractTransactionHandler<TransferFunction>();

// Sign transaction offline
var signedTransaction = await transferHandler.SignTransactionAsync(
    contractAddress,
    transferFunction
);

Console.WriteLine($"Signed transaction: {signedTransaction}");

// Later, broadcast signed transaction to network
web3 = new Web3("https://mainnet.infura.io/v3/YOUR-PROJECT-ID");
var txHash = await web3.Eth.Transactions.SendRawTransaction.SendRequestAsync(signedTransaction);
Console.WriteLine($"Transaction hash: {txHash}");
```

**Important for offline signing:**
- Nonce must be known in advance (query online first, then sign offline)
- Gas and GasPrice must be estimated or set manually
- ChainId must be correct for the target network
- Signed transaction is a hex string that can be stored or transmitted

### Example 2: Querying Contract Events

From: [Nethereum Playground Example 1008](https://playground.nethereum.com/csharp/id/1008)

```csharp
using Nethereum.Web3;
using Nethereum.Contracts;
using Nethereum.ABI.FunctionEncoding.Attributes;

// Transfer event DTO
[Event("Transfer")]
public class TransferEventDTO : IEventDTO
{
    [Parameter("address", "from", 1, true)]
    public string From { get; set; }

    [Parameter("address", "to", 2, true)]
    public string To { get; set; }

    [Parameter("uint256", "value", 3, false)]
    public BigInteger Value { get; set; }
}

var web3 = new Web3("https://mainnet.infura.io/v3/YOUR-PROJECT-ID");
var tokenAddress = "0x6B175474E89094C44Da98b954EedeAC495271d0F"; // DAI

// Get contract
var contract = web3.Eth.GetContract("[...abi...]", tokenAddress);

// Get transfer event
var transferEvent = contract.GetEvent<TransferEventDTO>();

// Create filter for specific address
var filterInput = transferEvent.CreateFilterInput(
    fromAddress: "0x742d35Cc6634C0532925a3b844Bc454e4438f44e",
    fromBlock: new Nethereum.RPC.Eth.DTOs.BlockParameter(18000000),
    toBlock: new Nethereum.RPC.Eth.DTOs.BlockParameter(18000100)
);

// Get all transfer events
var logs = await transferEvent.GetAllChangesAsync(filterInput);

foreach (var log in logs)
{
    Console.WriteLine($"Transfer from {log.Event.From} to {log.Event.To}");
    Console.WriteLine($"Amount: {Web3.Convert.FromWei(log.Event.Value)}");
    Console.WriteLine($"Block: {log.Log.BlockNumber}");
    Console.WriteLine($"Tx: {log.Log.TransactionHash}");
    Console.WriteLine();
}
```

### Example 3: Decoding Events from Transaction Receipt

```csharp
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.Contracts;

var account = new Account("0xPRIVATE_KEY");
var web3 = new Web3(account, "https://mainnet.infura.io/v3/YOUR-PROJECT-ID");

// Send transaction
var tokenAddress = "0x...";
var contract = web3.Eth.GetContract("[...abi...]", tokenAddress);
var transferFunction = contract.GetFunction("transfer");

var receipt = await transferFunction.SendTransactionAndWaitForReceiptAsync(
    account.Address,
    new Nethereum.Hex.HexTypes.HexBigInteger(100000),
    null,
    "0xRECIPIENT",
    1000000
);

// Decode all events from receipt
var transferEvents = receipt.DecodeAllEvents<TransferEventDTO>();

foreach (var transferEvent in transferEvents)
{
    Console.WriteLine($"Decoded Transfer Event:");
    Console.WriteLine($"  From: {transferEvent.Event.From}");
    Console.WriteLine($"  To: {transferEvent.Event.To}");
    Console.WriteLine($"  Value: {transferEvent.Event.Value}");
}
```

### Example 3a: Advanced Event Filtering

From: [Nethereum Playground Example 1008](https://playground.nethereum.com/csharp/id/1008)

Nethereum provides powerful event filtering capabilities using indexed parameters:

```csharp
using Nethereum.Web3;
using Nethereum.Contracts;
using Nethereum.ABI.FunctionEncoding.Attributes;

[Event("Transfer")]
public class TransferEventDTO : IEventDTO
{
    [Parameter("address", "_from", 1, true)]  // indexed
    public string From { get; set; }

    [Parameter("address", "_to", 2, true)]    // indexed
    public string To { get; set; }

    [Parameter("uint256", "_value", 3, false)] // not indexed
    public BigInteger Value { get; set; }
}

var web3 = new Web3("https://mainnet.infura.io/v3/YOUR-PROJECT-ID");
var tokenAddress = "0x...";
var transferEvent = web3.Eth.GetEvent<TransferEventDTO>(tokenAddress);

// 1. Filter by specific sender AND receiver
var filterBoth = transferEvent.CreateFilterInput(
    fromAddress: "0xSENDER",
    toAddress: "0xRECEIVER"
);
var resultsBoth = await transferEvent.GetAllChangesAsync(filterBoth);

// 2. Filter by receiver only (skip sender with null)
// Note: Must use array format to pass null for first parameter
var filterReceiverOnly = transferEvent.CreateFilterInput(
    null,  // Any sender
    new[] { "0xRECEIVER" }
);
var resultsReceiverOnly = await transferEvent.GetAllChangesAsync(filterReceiverOnly);

// 3. Filter with OR logic (multiple receivers)
// Use array to match ANY of the specified addresses
var filterMultipleReceivers = transferEvent.CreateFilterInput(
    null,  // Any sender
    new[] { "0xRECEIVER1", "0xRECEIVER2", "0xRECEIVER3" }
);
var resultsMultiple = await transferEvent.GetAllChangesAsync(filterMultipleReceivers);

// 4. Incremental updates with GetFilterChangesAsync
// Create a filter that tracks changes since last check
var filterId = await transferEvent.CreateFilterAsync(filterReceiverOnly);

// First call returns no results (no changes since filter creation)
var changes1 = await transferEvent.GetFilterChangesAsync(filterId);
Console.WriteLine($"Changes: {changes1.Count}"); // 0

// After new transactions occur...
await Task.Delay(10000); // Wait for new blocks

// Second call returns only NEW events since last check
var changes2 = await transferEvent.GetFilterChangesAsync(filterId);
Console.WriteLine($"New events: {changes2.Count}");

// Third call returns only events since second check
var changes3 = await transferEvent.GetFilterChangesAsync(filterId);
```

**Cross-Contract Event Filtering:**

To monitor events across ALL contracts with the same signature (e.g., all ERC20 transfers):

```csharp
// Create event handler WITHOUT contract address
var transferEventAnyContract = web3.Eth.GetEvent<TransferEventDTO>();

// Filter for transfers to specific address across ALL token contracts
var filterAllContracts = transferEventAnyContract.CreateFilterInput(
    null,  // Any sender
    new[] { "0xRECEIVER" }  // Specific receiver
);

var allTransfers = await transferEventAnyContract.GetAllChangesAsync(filterAllContracts);

foreach (var transfer in allTransfers)
{
    Console.WriteLine($"Token Contract: {transfer.Log.Address}");
    Console.WriteLine($"From: {transfer.Event.From}");
    Console.WriteLine($"To: {transfer.Event.To}");
    Console.WriteLine($"Value: {transfer.Event.Value}");
}
```

**Key Concepts:**

- **Indexed Parameters:** Only indexed parameters (marked `true` in `[Parameter]`) can be used in filters
- **Null for Skip:** Pass `null` to ignore a parameter position in the filter
- **Array for OR Logic:** Pass array of values to match ANY of them (OR operation)
- **Filter Order:** Filter parameter order must match event parameter order
- **GetFilterChangesAsync:** Returns only NEW events since last check (efficient for polling)
- **GetAllChangesAsync:** Returns ALL matching events in block range (can be expensive)
- **Cross-Contract:** Omit contract address to filter events across all contracts

### Example 4: Handling Custom Errors

```csharp
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.Contracts;
using Nethereum.Contracts.Exceptions;

// Custom error DTO
[Error("InsufficientBalance")]
public class InsufficientBalanceError : IErrorDTO
{
    [Parameter("uint256", "available", 1)]
    public BigInteger Available { get; set; }

    [Parameter("uint256", "required", 2)]
    public BigInteger Required { get; set; }
}

var account = new Account("0xPRIVATE_KEY");
var web3 = new Web3(account, "https://mainnet.infura.io/v3/YOUR-PROJECT-ID");

try
{
    // Attempt transfer with insufficient balance
    var contract = web3.Eth.GetContract("[...abi...]", "0x...");
    var transferFunction = contract.GetFunction("transfer");

    var receipt = await transferFunction.SendTransactionAndWaitForReceiptAsync(
        account.Address,
        new Nethereum.Hex.HexTypes.HexBigInteger(100000),
        null,
        "0xRECIPIENT",
        1000000000000000000000 // More than balance
    );
}
catch (SmartContractCustomErrorRevertException ex)
{
    // Decode custom error
    var error = ex.DecodeError<InsufficientBalanceError>();
    if (error != null)
    {
        Console.WriteLine($"Insufficient Balance!");
        Console.WriteLine($"Available: {error.Available}");
        Console.WriteLine($"Required: {error.Required}");
    }
}
```

### Example 5: Contract Deployment with Verification

```csharp
using Nethereum.Web3;
using Nethereum.Web3.Accounts;

var account = new Account("0xPRIVATE_KEY");
var web3 = new Web3(account, "http://localhost:8545");

// Contract source
var abi = @"[...]";
var bytecode = "0x...";

// Deploy
Console.WriteLine("Deploying contract...");
var receipt = await web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(
    abi,
    bytecode,
    account.Address,
    new Nethereum.Hex.HexTypes.HexBigInteger(3000000),
    null,
    null,
    1000000 // constructor param
);

if (receipt.Status.Value != 1)
{
    throw new Exception("Contract deployment failed");
}

var contractAddress = receipt.ContractAddress;
Console.WriteLine($"Contract deployed: {contractAddress}");
Console.WriteLine($"Gas used: {receipt.GasUsed.Value}");

// Verify deployment by calling a function
var contract = web3.Eth.GetContract(abi, contractAddress);
var totalSupplyFunction = contract.GetFunction("totalSupply");
var totalSupply = await totalSupplyFunction.CallAsync<BigInteger>();

Console.WriteLine($"Verified total supply: {totalSupply}");
```

### Example 6: ENS Name Resolution

From: [Nethereum Playground Example 1055](https://playground.nethereum.com/csharp/id/1055)

```csharp
using Nethereum.Web3;
using Nethereum.Contracts.Standards.ENS;

var web3 = new Web3("https://mainnet.infura.io/v3/YOUR-PROJECT-ID");

// Get ENS service
var ensService = web3.Eth.GetEnsService();

// Resolve ENS name to address
var address = await ensService.ResolveAddressAsync("vitalik.eth");
Console.WriteLine($"vitalik.eth resolves to: {address}");

// Reverse lookup
var ensName = await ensService.ReverseResolveAsync(address);
Console.WriteLine($"{address} reverse resolves to: {ensName}");

// Resolve text records
var url = await ensService.ResolveTextAsync("vitalik.eth", ENSTextRecordKey.Url);
var avatar = await ensService.ResolveTextAsync("vitalik.eth", ENSTextRecordKey.Avatar);
```

### Example 7: Multicall - Batch Contract Calls

From: [Nethereum Playground Example 1066](https://playground.nethereum.com/csharp/id/1066)

```csharp
using Nethereum.Web3;
using Nethereum.Contracts.QueryHandlers.MultiCall;

var web3 = new Web3("https://mainnet.infura.io/v3/YOUR-PROJECT-ID");

// Multiple tokens to query
var tokens = new[]
{
    "0x6B175474E89094C44Da98b954EedeAC495271d0F", // DAI
    "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48", // USDC
    "0xdAC17F958D2ee523a2206206994597C13D831ec7"  // USDT
};

var holder = "0x742d35Cc6634C0532925a3b844Bc454e4438f44e";

// Get multicall handler
var multiQueryHandler = web3.Eth.GetMultiQueryHandler();

// Create calls for each token
var calls = new List<IMulticallInputOutput>();
foreach (var tokenAddress in tokens)
{
    var erc20 = web3.Eth.ERC20.GetContractService(tokenAddress);
    var balanceQuery = new BalanceOfFunction { Owner = holder };

    calls.Add(new MulticallInputOutput<BalanceOfFunction, BigInteger>(
        balanceQuery,
        tokenAddress
    ));
}

// Execute all calls in single RPC request
var results = await multiQueryHandler.MultiCallAsync(calls.ToArray());

for (int i = 0; i < tokens.Length; i++)
{
    var balance = ((MulticallInputOutput<BalanceOfFunction, BigInteger>)results[i]).Output;
    Console.WriteLine($"Token {tokens[i]}: {Web3.Convert.FromWei(balance, 18)}");
}
```

### Example 8: Estimating Gas for Contract Call

```csharp
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.RPC.Eth.DTOs;

var account = new Account("0xPRIVATE_KEY");
var web3 = new Web3(account, "https://mainnet.infura.io/v3/YOUR-PROJECT-ID");

var contract = web3.Eth.GetContract("[...abi...]", "0x...");
var transferFunction = contract.GetFunction("transfer");

// Estimate gas
var gasEstimate = await transferFunction.EstimateGasAsync(
    account.Address,
    null,
    null,
    "0xRECIPIENT",
    1000000
);

Console.WriteLine($"Estimated gas: {gasEstimate.Value}");

// Add 10% buffer
var gasLimit = gasEstimate.Value * 110 / 100;

// Send with custom gas limit
var receipt = await transferFunction.SendTransactionAndWaitForReceiptAsync(
    account.Address,
    new Nethereum.Hex.HexTypes.HexBigInteger(gasLimit),
    null,
    null,
    "0xRECIPIENT",
    1000000
);
```

### Example 9: Query Handler Pattern (Simplified)

```csharp
using Nethereum.Web3;
using Nethereum.Contracts;
using Nethereum.ABI.FunctionEncoding.Attributes;

// Query DTO
[Function("balanceOf", "uint256")]
public class BalanceOfFunction : FunctionMessage
{
    [Parameter("address", "owner", 1)]
    public string Owner { get; set; }
}

var web3 = new Web3("https://mainnet.infura.io/v3/YOUR-PROJECT-ID");
var tokenAddress = "0x...";

// Query handler simplifies read operations
var queryHandler = web3.Eth.GetContractQueryHandler<BalanceOfFunction>();
var balance = await queryHandler.QueryAsync<BigInteger>(
    tokenAddress,
    new BalanceOfFunction { Owner = "0x742d35Cc6634C0532925a3b844Bc454e4438f44e" }
);

Console.WriteLine($"Balance: {balance}");
```

### Example 10: AOT-Compatible Contract Usage

```csharp
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.JsonRpc.SystemTextJsonRpcClient;
using Nethereum.Contracts;

// CRITICAL: Enable System.Text.Json for AOT compatibility
Nethereum.ABI.ABIDeserialisation.AbiDeserializationSettings.UseSystemTextJson = true;

var account = new Account("0xPRIVATE_KEY", chainId: 1);
var client = new SimpleRpcClient("https://mainnet.infura.io/v3/YOUR-PROJECT-ID");
var web3 = new Web3(account, client);

// Deploy contract (AOT-compatible)
var abi = "[...]";
var bytecode = "0x...";

var receipt = await web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(
    abi,
    bytecode,
    account.Address,
    new Nethereum.Hex.HexTypes.HexBigInteger(3000000)
);

var contractAddress = receipt.ContractAddress;

// Get contract and call function (AOT-compatible)
var contract = web3.Eth.GetContract(abi, contractAddress);
var function = contract.GetFunction("totalSupply");
var supply = await function.CallAsync<BigInteger>();

Console.WriteLine($"Total supply: {supply}");

// Decode events (AOT-compatible)
var events = receipt.DecodeAllEvents<TransferEventDTO>();
foreach (var evt in events)
{
    Console.WriteLine($"Transfer: {evt.Event.From} → {evt.Event.To}");
}
```

## Contract Standards

### ERC20 Token Standard

From: [Nethereum Playground Example 1005](https://playground.nethereum.com/csharp/id/1005)

```csharp
var erc20 = web3.Eth.ERC20.GetContractService(tokenAddress);

// Read operations
var name = await erc20.NameQueryAsync();
var symbol = await erc20.SymbolQueryAsync();
var decimals = await erc20.DecimalsQueryAsync();
var totalSupply = await erc20.TotalSupplyQueryAsync();
var balance = await erc20.BalanceOfQueryAsync(holderAddress);
var allowance = await erc20.AllowanceQueryAsync(owner, spender);

// Write operations (requires account)
var approveReceipt = await erc20.ApproveRequestAndWaitForReceiptAsync(spender, amount);
var transferReceipt = await erc20.TransferRequestAndWaitForReceiptAsync(to, amount);
var transferFromReceipt = await erc20.TransferFromRequestAndWaitForReceiptAsync(from, to, amount);
```

### ERC721 NFT Standard

From: [Nethereum Playground Example 1067](https://playground.nethereum.com/csharp/id/1067)

```csharp
var erc721 = web3.Eth.ERC721.GetContractService(nftAddress);

// Query operations
var name = await erc721.NameQueryAsync();
var symbol = await erc721.SymbolQueryAsync();
var owner = await erc721.OwnerOfQueryAsync(tokenId);
var balance = await erc721.BalanceOfQueryAsync(address);
var tokenURI = await erc721.TokenURIQueryAsync(tokenId);
var approved = await erc721.GetApprovedQueryAsync(tokenId);

// Transfer operations
var transferReceipt = await erc721.TransferFromRequestAndWaitForReceiptAsync(from, to, tokenId);
var approveReceipt = await erc721.ApproveRequestAndWaitForReceiptAsync(to, tokenId);
var setApprovalForAllReceipt = await erc721.SetApprovalForAllRequestAndWaitForReceiptAsync(operator, approved);
```

### ERC1155 Multi-Token Standard

```csharp
var erc1155 = web3.Eth.ERC1155.GetContractService(contractAddress);

// Query single balance
var balance = await erc1155.BalanceOfQueryAsync(owner, tokenId);

// Query multiple balances
var balances = await erc1155.BalanceOfBatchQueryAsync(
    new[] { owner1, owner2 },
    new[] { tokenId1, tokenId2 }
);

// Transfer single token
var transferReceipt = await erc1155.SafeTransferFromRequestAndWaitForReceiptAsync(
    from,
    to,
    tokenId,
    amount,
    data
);

// Transfer multiple tokens
var batchTransferReceipt = await erc1155.SafeBatchTransferFromRequestAndWaitForReceiptAsync(
    from,
    to,
    new[] { tokenId1, tokenId2 },
    new[] { amount1, amount2 },
    data
);
```

### EIP-3009: Transfer With Authorization (USDC)

```csharp
using Nethereum.Contracts.Standards.EIP3009;

var eip3009Service = web3.Eth.EIP3009.GetContractService(usdcAddress);

// Create authorization for gasless transfer
var authorization = await eip3009Service.CreateTransferWithAuthorizationAsync(
    from: fromAddress,
    to: toAddress,
    value: amount,
    validAfter: 0,
    validBefore: uint.MaxValue,
    nonce: nonceBytes
);

// Sign the authorization
var signature = await account.SignTypedDataV4Async(authorization);

// Execute the transfer (can be done by anyone, not just the sender)
var receipt = await eip3009Service.TransferWithAuthorizationRequestAndWaitForReceiptAsync(
    from: authorization.From,
    to: authorization.To,
    value: authorization.Value,
    validAfter: authorization.ValidAfter,
    validBefore: authorization.ValidBefore,
    nonce: authorization.Nonce,
    v: signature.V,
    r: signature.R,
    s: signature.S
);
```

## Best Practices

1. **Use Strongly-Typed DTOs**: Better IntelliSense, compile-time safety
   ```csharp
   // Good
   var function = contract.GetFunction<TransferFunction>();

   // Less maintainable
   var function = contract.GetFunction("transfer");
   ```

2. **Use Code Generation for Production**: Automate DTO creation
   ```bash
   Nethereum.Generator.Console generate from-abi -abi MyContract.abi.json
   ```

3. **Estimate Gas Before Sending**:
   ```csharp
   var gasEstimate = await function.EstimateGasAsync(...);
   var gasLimit = gasEstimate.Value * 110 / 100; // 10% buffer
   ```

4. **Always Check Transaction Status**:
   ```csharp
   if (receipt.Status.Value != 1)
   {
       throw new Exception("Transaction failed");
   }
   ```

5. **Decode Events from Receipts**:
   ```csharp
   var events = receipt.DecodeAllEvents<TransferEventDTO>();
   ```

6. **Use ERC Standard Services**: Don't reimplement standards
   ```csharp
   var erc20 = web3.Eth.ERC20.GetContractService(tokenAddress);
   ```

7. **Handle Custom Errors Properly**:
   ```csharp
   catch (SmartContractCustomErrorRevertException ex)
   {
       var error = ex.DecodeError<YourErrorDTO>();
   }
   ```

8. **Use Multicall for Batch Queries**: Reduce RPC calls
   ```csharp
   var multiQueryHandler = web3.Eth.GetMultiQueryHandler();
   ```

9. **Enable AOT Compatibility When Needed**:
   ```csharp
   // CRITICAL: Enable System.Text.Json for AOT
   AbiDeserializationSettings.UseSystemTextJson = true;
   ```

10. **Index Event Parameters Correctly**:
    ```csharp
    [Parameter("address", "from", 1, true)] // indexed=true
    [Parameter("uint256", "value", 3, false)] // indexed=false
    ```

## Error Handling

```csharp
using Nethereum.Contracts.Exceptions;
using Nethereum.JsonRpc.Client;

try
{
    var receipt = await function.SendTransactionAndWaitForReceiptAsync(...);

    if (receipt.Status.Value != 1)
    {
        Console.WriteLine("Transaction reverted");
    }
}
catch (SmartContractCustomErrorRevertException ex)
{
    Console.WriteLine($"Custom error: {ex.Message}");
    var decoded = ex.DecodeError<YourErrorDTO>();
    // Handle specific custom error
}
catch (SmartContractRevertException ex)
{
    Console.WriteLine($"Revert reason: {ex.RevertMessage}");
}
catch (RpcResponseException ex)
{
    Console.WriteLine($"RPC error: {ex.Message}");
}
```

## API Reference

### Core Classes

- **Contract** - Represents a deployed contract
  - `GetFunction(name)` / `GetFunction<T>()` - Get function by name or type
  - `GetEvent(name)` / `GetEvent<T>()` - Get event by name or type
  - `GetError(name)` / `FindError(encodedData)` - Get custom error

- **Function** / **Function<T>** - Represents a contract function
  - `CallAsync<TReturn>(params)` - Call function (read-only)
  - `SendTransactionAsync(from, gas, value, params)` - Send transaction
  - `SendTransactionAndWaitForReceiptAsync(...)` - Send and wait for receipt
  - `EstimateGasAsync(from, gas, value, params)` - Estimate gas cost

- **Event** / **Event<T>** - Represents a contract event
  - `CreateFilterInput(...)` - Create event filter
  - `GetAllChangesAsync(filterInput)` - Get historical events
  - `CreateFilterAsync()` - Create persistent filter on node
  - `GetFilterChangesAsync(filterId)` - Get new events since last check

- **ContractHandler** - Unified interface for contract operations
  - `QueryAsync<TFunction, TReturn>(function)` - Query contract state
  - `SendRequestAndWaitForReceiptAsync<TFunction>(function)` - Send transaction
  - `EstimateGasAsync<TFunction>(function)` - Estimate gas

### Handler Classes

- **IContractQueryHandler<TFunction>** - Simplified query operations
- **IContractTransactionHandler<TFunction>** - Simplified transaction operations
- **IContractDeploymentHandler<TDeployment>** - Simplified deployment operations

### Standard Services

- **ERC20Service** - Complete ERC20 token interaction
- **ERC721Service** - Complete ERC721 NFT interaction
- **ERC1155Service** - Complete ERC1155 multi-token interaction
- **ERC1271Service** - Contract signature validation
- **ERC165SupportsInterfaceService** - Interface detection
- **ERC2535DiamondService** - Diamond proxy standard
- **ERC6492Service** - Pre-deployed contract signature validation
- **EIP3009Service** - Transfer with authorization (USDC/stablecoins)
- **ENSService** - Ethereum Name Service resolution
- **ProofOfHumanityService** - Proof of Humanity registry

## Playground Examples

Live, runnable examples:

**Smart Contracts:**
- [Example 1005](https://playground.nethereum.com/csharp/id/1005) - Query ERC20 token balance
- [Example 1006](https://playground.nethereum.com/csharp/id/1006) - Smart contract deployment
- [Example 1007](https://playground.nethereum.com/csharp/id/1007) - Deployment, queries, transactions, gas

**Events:**
- [Example 1008](https://playground.nethereum.com/csharp/id/1008) - Events (end-to-end introduction)
- [Example 1009](https://playground.nethereum.com/csharp/id/1009) - Retrieving events from chain
- [Example 1060](https://playground.nethereum.com/csharp/id/1060) - Decode events from transaction receipt

**ERC721 NFTs:**
- [Example 1067](https://playground.nethereum.com/csharp/id/1067) - Query ERC721 balance, owner, transfers
- [Example 1048](https://playground.nethereum.com/csharp/id/1048) - Query ERC721 smart contract
- [Example 1068](https://playground.nethereum.com/csharp/id/1068) - Query ERC721 using human-readable ABI

**ERC20 Multicall:**
- [Example 1066](https://playground.nethereum.com/csharp/id/1066) - Query multiple ERC20 balances using multicall

**ENS:**
- [Example 1055](https://playground.nethereum.com/csharp/id/1055) - Resolve ENS address
- [Example 1056](https://playground.nethereum.com/csharp/id/1056) - Resolve ENS URL

**Advanced:**
- [Example 1012](https://playground.nethereum.com/csharp/id/1012) - Working with structs
- [Example 1070](https://playground.nethereum.com/csharp/id/1070) - JSON input/output with complex structs
- [Example 1075](https://playground.nethereum.com/csharp/id/1075) - Decode function from transaction input

## Related Packages

- **Nethereum.ABI** - ABI encoding/decoding (used by this package)
- **Nethereum.Web3** - High-level API that includes contracts
- **Nethereum.Accounts** - Account management for signing transactions
- **Nethereum.RPC** - Low-level RPC methods
- **Nethereum.Generator.Console** - Code generation tool

## Comprehensive Guides

For in-depth documentation:
- [Nethereum Code Generation](https://docs.nethereum.com/en/latest/nethereum-code-generation/)
- [Nethereum Documentation](https://docs.nethereum.com/)
- [ERC Standards](https://eips.ethereum.org/erc)
- [Solidity ABI Specification](https://docs.soliditylang.org/en/latest/abi-spec.html)

## Important Notes

### Event Parameter Indexing

Indexed parameters (up to 3) go into event topics for filtering:
```csharp
[Parameter("address", "from", 1, true)] // indexed - can filter
[Parameter("uint256", "value", 3, false)] // not indexed - cannot filter
```

### Gas Estimation Buffer

Always add a buffer to gas estimates (network conditions vary):
```csharp
var gasLimit = gasEstimate.Value * 110 / 100; // 10% buffer recommended
```

### Transaction Receipt Status

Status field indicates success/failure:
- `Status = 1` or `0x1` - Transaction succeeded
- `Status = 0` or `0x0` - Transaction failed (reverted)

### ABI Compatibility

Nethereum supports:
- Solidity ABI JSON format
- Human-readable ABI format
- Minimal ABI (function signatures only)

### Code Generation Benefits

For production applications, code generation is strongly recommended:
- Eliminates manual DTO creation
- Provides compile-time type safety
- Reduces errors from manual ABI transcription
- Simplifies contract updates
- Generates complete service wrappers
