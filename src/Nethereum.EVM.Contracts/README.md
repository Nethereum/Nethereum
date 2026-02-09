# Nethereum.EVM.Contracts

**Nethereum.EVM.Contracts** provides high-level contract simulators built on top of Nethereum.EVM for testing and analyzing smart contract behavior without broadcasting transactions.

## Overview

This package offers specialized contract simulators that leverage the EVM execution engine to:
- Simulate contract function calls with real blockchain state
- Track state changes across transactions
- Reverse-engineer contract storage layouts
- Validate expected behavior before deployment
- Test contract interactions locally

**Status**: Production - suitable for testing, debugging, simulation, and analysis.

## Installation

```bash
dotnet add package Nethereum.EVM.Contracts
```

## Features

### ERC20 Contract Simulation

Complete ERC20 token operation simulation with:
- **Transfer simulation** with before/after balance tracking
- **Balance queries** using EVM simulation
- **Storage slot discovery** for mapping-based balances
- **Event log capture** from simulated transfers
- **State verification** comparing EVM storage with function calls

## Core Components

### ERC20ContractSimulator

High-level simulator for ERC20 token contracts. Located in `ERC20/ERC20ContractSimulator.cs:19-197`.

**Constructor:**
```csharp
public ERC20ContractSimulator(
    IWeb3 web3,
    BigInteger chainId,
    string contractAddress,
    byte[] code = null
)
```

**Properties:**
- `Web3` - Web3 instance for RPC access
- `ChainId` - Network chain ID
- `ContractAddress` - ERC20 token contract address

### TransferSimulationResult

Result from `SimulateTransferAndBalanceStateAsync`. Located in `ERC20ContractSimulator.cs:35-47`.

**Properties:**
- `BalanceSenderBefore` - Sender balance before transfer
- `BalanceSenderStorageAfter` - Sender balance in storage after transfer
- `BalanceSenderAfter` - Sender balance from balanceOf after transfer
- `BalanceReceiverBefore` - Receiver balance before transfer
- `BalanceReceiverStorageAfter` - Receiver balance in storage after transfer
- `BalanceReceiverAfter` - Receiver balance from balanceOf after transfer
- `TransferLogs` - Event logs emitted during transfer

## Usage Examples

### Example 1: Simulate ERC20 Transfer

```csharp
using Nethereum.EVM.Contracts.ERC20;
using Nethereum.Web3;

var web3 = new Web3("https://mainnet.infura.io/v3/YOUR_KEY");

// USDC contract on mainnet
var usdcAddress = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48";
var simulator = new ERC20ContractSimulator(web3, chainId: 1, usdcAddress);

// Simulate transfer from address1 to address2
var senderAddress = "0x0000000000000000000000000000000000000001";
var receiverAddress = "0x0000000000000000000000000000000000000025";
var amount = 100; // 100 USDC (6 decimals)

var result = await simulator.SimulateTransferAndBalanceStateAsync(
    senderAddress,
    receiverAddress,
    amount
);

Console.WriteLine($"Sender balance before: {result.BalanceSenderBefore}");
Console.WriteLine($"Sender balance after: {result.BalanceSenderAfter}");
Console.WriteLine($"Receiver balance before: {result.BalanceReceiverBefore}");
Console.WriteLine($"Receiver balance after: {result.BalanceReceiverAfter}");

// Verify balance changes
Assert.Equal(result.BalanceSenderAfter, result.BalanceSenderBefore - amount);
Assert.Equal(result.BalanceReceiverAfter, result.BalanceReceiverBefore + amount);

// Check storage consistency
Assert.Equal(result.BalanceSenderStorageAfter, result.BalanceSenderAfter);
Assert.Equal(result.BalanceReceiverStorageAfter, result.BalanceReceiverAfter);

// Inspect transfer logs
foreach (var log in result.TransferLogs)
{
    Console.WriteLine($"Transfer event: {log.Topics[0]}");
}
```

From test: `Erc20EVMContractSimulatorAndStorage.cs:54-67`

### Example 2: Simulate Balance Query

```csharp
using Nethereum.EVM.Contracts.ERC20;
using Nethereum.EVM.BlockchainState;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;

var web3 = new Web3("https://mainnet.infura.io/v3/YOUR_KEY");
var tokenAddress = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48"; // USDC

var simulator = new ERC20ContractSimulator(web3, chainId: 1, tokenAddress);

// Get current block
var blockNumber = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();

// Create state service
var nodeDataService = new RpcNodeDataService(
    web3.Eth,
    new BlockParameter(blockNumber)
);
var stateService = new ExecutionStateService(nodeDataService);

// Simulate balance query
var ownerAddress = "0x0000000000000000000000000000000000000001";
var balance = await simulator.SimulateGetBalanceAsync(ownerAddress, stateService);

Console.WriteLine($"Balance of {ownerAddress}: {balance}");
```

From method: `ERC20ContractSimulator.cs:117-133`

### Example 3: Discover Storage Slot for Balances

This powerful feature reverse-engineers where ERC20 balances are stored by simulating `balanceOf` and comparing storage values.

```csharp
using Nethereum.EVM.Contracts.ERC20;
using Nethereum.Web3;

var web3 = new Web3("https://mainnet.infura.io/v3/YOUR_KEY");

// USDC contract
var usdcAddress = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48";
var simulator = new ERC20ContractSimulator(web3, chainId: 1, usdcAddress);

// Find an address with a non-zero balance
var addressWithBalance = "0x0000000000000000000000000000000000000001";

// Calculate the storage slot where balances are stored
// Tests up to 100 slots by default
var balanceSlot = await simulator.CalculateMappingBalanceSlotAsync(
    addressWithBalance,
    numberOfSlotsToTry: 100
);

Console.WriteLine($"Balance mapping is at storage slot: {balanceSlot}");
// Output: Balance mapping is at storage slot: 9 (for USDC)
```

From test: `Erc20EVMContractSimulatorAndStorage.cs:34-50`

### Example 4: Direct Transfer Simulation with Custom State

```csharp
using Nethereum.EVM.Contracts.ERC20;
using Nethereum.EVM.BlockchainState;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using System.Numerics;

var web3 = new Web3("https://mainnet.infura.io/v3/YOUR_KEY");
var tokenAddress = "0xYourTokenAddress";

var simulator = new ERC20ContractSimulator(web3, chainId: 1, tokenAddress);

// Setup state service
var blockNumber = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
var nodeDataService = new RpcNodeDataService(web3.Eth, new BlockParameter(blockNumber));
var stateService = new ExecutionStateService(nodeDataService);

// Simulate transfer
var senderAddress = "0xSenderAddress";
var receiverAddress = "0xReceiverAddress";
var amount = BigInteger.Parse("1000000"); // 1 token (6 decimals)

var programResult = await simulator.SimulateTransferAsync(
    senderAddress,
    receiverAddress,
    amount,
    stateService
);

if (programResult.IsRevert)
{
    Console.WriteLine($"Transfer would revert: {programResult.GetRevertMessage()}");
}
else
{
    Console.WriteLine("Transfer simulation successful");
    Console.WriteLine($"Logs generated: {programResult.Logs.Count}");

    // Inspect Transfer event
    foreach (var log in programResult.Logs)
    {
        if (log.Topics.Length > 0)
        {
            Console.WriteLine($"Event signature: {log.Topics[0]}");
        }
    }
}
```

From method: `ERC20ContractSimulator.cs:96-115`

### Example 5: Validate Storage Layout

Compare direct storage reads with contract function calls to validate storage layout:

```csharp
using Nethereum.Contracts.ContractStorage;
using Nethereum.Web3;

var web3 = new Web3("https://mainnet.infura.io/v3/YOUR_KEY");
var tokenAddress = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48"; // USDC

// Get balance via smart contract call
var erc20Service = web3.Eth.ERC20.GetContractService(tokenAddress);
var address = "0x0000000000000000000000000000000000000001";
var balanceFromContract = await erc20Service.BalanceOfQueryAsync(address);

// Get balance from direct storage read (slot 9 for USDC)
var balanceFromStorage = await erc20Service.GetBalanceFromStorageAsync(address, slot: 9);

// Verify they match
Assert.Equal(balanceFromContract, balanceFromStorage);

Console.WriteLine($"Balance from contract call: {balanceFromContract}");
Console.WriteLine($"Balance from storage: {balanceFromStorage}");
Console.WriteLine("Storage layout validated!");
```

From test: `Erc20EVMContractSimulatorAndStorage.cs:72-80`

## How It Works

### Storage Slot Discovery Algorithm

The `CalculateMappingBalanceSlotAsync` method discovers where balances are stored by: (Located in `ERC20ContractSimulator.cs:136-193`)

1. **Execute balanceOf via EVM** - Simulates the `balanceOf(address)` call to get expected balance
2. **Capture storage accesses** - Tracks all storage slots read during execution
3. **Compare values** - Finds storage values matching the returned balance
4. **Calculate slot** - For each match, tries slot positions 0-N to calculate mapping key
5. **Validate** - Confirms the slot by recalculating `keccak256(address || slot)`

**Example calculation:**
```csharp
// For mapping(address => uint256) balances at slot 9:
// Storage key = keccak256(leftPad32(address) + leftPad32(9))

var storageKey = StorageUtil.CalculateMappingAddressStorageKeyAsBigInteger(
    address,
    slot: 9
);
```

### Transfer Simulation Flow

The `SimulateTransferAndBalanceStateAsync` method: (Located in `ERC20ContractSimulator.cs:59-94`)

1. **Query initial balances** - Gets sender and receiver balances via RPC
2. **Calculate storage slot** - Discovers balance mapping slot (if not known)
3. **Setup EVM state** - Creates ExecutionStateService with RPC data source
4. **Simulate transfer** - Executes transfer function via EVM simulator
5. **Capture logs** - Records Transfer events from execution
6. **Query final balances** - Gets updated balances via simulated balanceOf calls
7. **Read storage** - Reads balance storage slots directly
8. **Compare results** - Validates function results match storage values

## Advanced Usage

### Custom Contract Code

Provide pre-fetched contract bytecode to avoid RPC calls:

```csharp
var web3 = new Web3("https://mainnet.infura.io/v3/YOUR_KEY");
var contractAddress = "0xYourContract";

// Fetch bytecode once
var bytecode = await web3.Eth.GetCode.SendRequestAsync(contractAddress);

// Reuse bytecode for multiple simulations
var simulator = new ERC20ContractSimulator(
    web3,
    chainId: 1,
    contractAddress,
    code: bytecode.HexToByteArray()
);

// Multiple simulations without refetching code
var result1 = await simulator.SimulateTransferAndBalanceStateAsync(addr1, addr2, 100);
var result2 = await simulator.SimulateTransferAndBalanceStateAsync(addr3, addr4, 200);
```

### Historical Block Simulation

Simulate at a specific block height:

```csharp
using Nethereum.Hex.HexTypes;

var web3 = new Web3("https://mainnet.infura.io/v3/YOUR_KEY");
var simulator = new ERC20ContractSimulator(web3, 1, tokenAddress);

// Simulate at specific block
var historicalBlock = new HexBigInteger(18_000_000);
var slot = await simulator.CalculateMappingBalanceSlotAsync(
    address,
    numberOfSlotsToTry: 1000,
    blockNumber: historicalBlock
);
```

From method: `ERC20ContractSimulator.cs:136-143`

### Gas Estimation Comparison

Compare actual gas with simulated gas:

```csharp
using Nethereum.Contracts.Standards.ERC20.ContractDefinition;

var web3 = new Web3("https://mainnet.infura.io/v3/YOUR_KEY");
var contractAddress = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48";

// Estimate gas via RPC
var contractHandler = web3.Eth.GetContractHandler(contractAddress);
var balanceOfFunction = new BalanceOfFunction { Owner = address };
var gasEstimate = await contractHandler.EstimateGasAsync(balanceOfFunction);

Console.WriteLine($"Gas estimate from RPC: {gasEstimate.Value}");

// Simulate with gas tracking
var simulator = new ERC20ContractSimulator(web3, 1, contractAddress);
var blockNumber = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
var nodeDataService = new RpcNodeDataService(web3.Eth, new BlockParameter(blockNumber));
var stateService = new ExecutionStateService(nodeDataService);

var balance = await simulator.SimulateGetBalanceAsync(address, stateService);

// Access trace for gas analysis
// Note: Requires tracing enabled in ExecuteAsync
```

## Use Cases

### 1. Pre-Transaction Validation

Validate transfers before broadcasting:

```csharp
var simulator = new ERC20ContractSimulator(web3, 1, tokenAddress);

// Check if transfer would succeed
var result = await simulator.SimulateTransferAndBalanceStateAsync(
    senderAddress,
    receiverAddress,
    amount
);

// Validate sender has sufficient balance
if (result.BalanceSenderAfter < 0)
{
    throw new Exception("Insufficient balance");
}

// Validate expected balance changes
var expectedSenderBalance = result.BalanceSenderBefore - amount;
var expectedReceiverBalance = result.BalanceReceiverBefore + amount;

if (result.BalanceSenderAfter != expectedSenderBalance ||
    result.BalanceReceiverAfter != expectedReceiverBalance)
{
    throw new Exception("Unexpected balance changes");
}

// Validate events
if (!result.TransferLogs.Any(log =>
    log.Topics.Length > 0 &&
    log.Topics[0] == "0xddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef"))
{
    throw new Exception("Missing Transfer event");
}
```

### 2. Storage Layout Analysis

Reverse-engineer contract storage for data extraction:

```csharp
var simulator = new ERC20ContractSimulator(web3, 1, contractAddress);

// Find balance storage slot
var balanceSlot = await simulator.CalculateMappingBalanceSlotAsync(knownAddress);

Console.WriteLine($"Balances stored at slot: {balanceSlot}");

// Now you can directly read any address's balance from storage
var anyAddress = "0xAnyAddress";
var storageKey = StorageUtil.CalculateMappingAddressStorageKeyAsBigInteger(
    anyAddress,
    (ulong)balanceSlot
);

var balanceBytes = await web3.Eth.GetStorageAt.SendRequestAsync(
    contractAddress,
    storageKey.ToHexBigInteger()
);

var balance = new IntTypeDecoder().DecodeBigInteger(balanceBytes);
Console.WriteLine($"Balance of {anyAddress}: {balance}");
```

### 3. Testing Contract Upgrades

Test new implementations before deployment:

```csharp
// Fetch current implementation bytecode
var currentCode = await web3.Eth.GetCode.SendRequestAsync(tokenAddress);
var currentSimulator = new ERC20ContractSimulator(web3, 1, tokenAddress, currentCode.HexToByteArray());

// Simulate with current implementation
var currentResult = await currentSimulator.SimulateTransferAndBalanceStateAsync(addr1, addr2, 100);

// Load new implementation bytecode
var newCode = File.ReadAllBytes("NewImplementation.bin");
var newSimulator = new ERC20ContractSimulator(web3, 1, tokenAddress, newCode);

// Simulate with new implementation
var newResult = await newSimulator.SimulateTransferAndBalanceStateAsync(addr1, addr2, 100);

// Compare results
Assert.Equal(currentResult.BalanceSenderAfter, newResult.BalanceSenderAfter);
Assert.Equal(currentResult.BalanceReceiverAfter, newResult.BalanceReceiverAfter);
```

### 4. Multi-Transfer Simulation

Simulate complex transfer sequences:

```csharp
var simulator = new ERC20ContractSimulator(web3, 1, tokenAddress);
var blockNumber = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
var nodeDataService = new RpcNodeDataService(web3.Eth, new BlockParameter(blockNumber));

// Reuse state service across simulations (preserves state changes)
var stateService = new ExecutionStateService(nodeDataService);

// Simulation 1: A -> B
await simulator.SimulateTransferAsync("0xA", "0xB", 100, stateService);

// Simulation 2: B -> C (uses updated state from simulation 1)
await simulator.SimulateTransferAsync("0xB", "0xC", 50, stateService);

// Simulation 3: C -> A (uses updated state from simulations 1 & 2)
await simulator.SimulateTransferAsync("0xC", "0xA", 25, stateService);

// Query final balances
var balanceA = await simulator.SimulateGetBalanceAsync("0xA", stateService);
var balanceB = await simulator.SimulateGetBalanceAsync("0xB", stateService);
var balanceC = await simulator.SimulateGetBalanceAsync("0xC", stateService);
```

## Dependencies

Required packages:
- **Nethereum.EVM** - EVM simulator engine
- **Nethereum.Contracts** - ERC20 contract definitions and handlers
- **Nethereum.Web3** - Web3 client for RPC access
- **Nethereum.ABI** - ABI encoding/decoding
- **Nethereum.RPC** - RPC infrastructure

## Limitations

### Current Limitations

This simulator has the following limitations:

1. **ERC20 Focus** - Currently only provides ERC20 simulator (more contract types planned)
2. **Gas Accuracy** - Gas calculations may differ slightly from actual execution
3. **Precompiled Contracts** - Limited support for precompiled contract interactions
4. **State Consistency** - No automatic state revert between simulations
5. **Performance** - Slower than native execution (designed for testing, not production)

### Storage Slot Discovery Constraints

The `CalculateMappingBalanceSlotAsync` method:
- Requires a non-zero balance at the test address
- Tests slots sequentially (can be slow for high slot numbers)
- May fail if contract uses non-standard storage layouts
- Limited to `numberOfSlotsToTry` attempts (default 10,000)

### Design Scope

Not designed for:
- High-frequency simulation requirements (use direct RPC for production)
- Consensus-critical operations
- Real-time gas estimation (use RPC `eth_estimateGas` for production)

## Source Files Reference

**Contract Simulators:**
- `ERC20/ERC20ContractSimulator.cs` - ERC20 token simulator

**Test Files:**
- `tests/Nethereum.Contracts.IntegrationTests/EVM/Erc20EVMContractSimulatorAndStorage.cs` - Integration tests

## Future Enhancements

Planned additions:
- ERC721 (NFT) contract simulator
- ERC1155 (Multi-token) contract simulator
- Generic contract simulator with custom ABI
- Batch operation simulation
- State snapshot and rollback
- Parallel simulation support

## License

Nethereum is licensed under the MIT License.

## Related Packages

- **Nethereum.EVM** - Core EVM simulator
- **Nethereum.Contracts** - Smart contract interaction
- **Nethereum.Contracts.ContractStorage** - Storage utilities
- **Nethereum.Web3** - Ethereum client library

## Support

- GitHub: https://github.com/Nethereum/Nethereum
- Documentation: https://docs.nethereum.com
- Discord: https://discord.gg/jQPrR58FxX
