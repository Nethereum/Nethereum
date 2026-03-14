---
name: evm-simulation
description: Help users simulate Ethereum transactions, preview state changes, decode call trees, extract token transfers, decode revert reasons, debug EVM execution, or disassemble bytecode using Nethereum.EVM (.NET). Use this skill whenever the user mentions transaction simulation, transaction preview, state changes preview, call tracing, EVM debugging, bytecode execution, bytecode disassembly, opcode analysis, revert decoding, custom error decoding, ERC-20 simulation, balance change extraction, or anything involving local EVM execution with C# or .NET.
user-invocable: true
---

# EVM Simulation — Nethereum.EVM

The Nethereum EVM simulator is a full in-process Ethereum Virtual Machine that executes bytecode locally against real blockchain state. It powers the wallet transaction preview, DevChain, CoreChain, and the Solidity debugger.

## When to Use This

- **Transaction preview**: Simulate a transaction to see all balance changes (ETH, ERC-20, NFT) before signing
- **Call tree decoding**: Understand which contracts and functions are called during a complex transaction
- **Event extraction**: Get all events emitted and consolidate token transfers into net balance changes
- **Revert debugging**: Decode why a transaction failed — custom errors, require messages, panic codes
- **ERC-20 testing**: Simulate token transfers, approvals, and detect fee-on-transfer tokens
- **Bytecode analysis**: Execute, debug, or disassemble EVM bytecode
- **Source-level debugging**: Step through execution with Solidity source maps

## Required Packages

```bash
dotnet add package Nethereum.EVM          # Core simulator, executor, decoder, extractor
dotnet add package Nethereum.EVM.Contracts # ERC-20 contract simulator
dotnet add package Nethereum.Web3          # RPC connection for blockchain state
```

## The Transaction Simulation Pipeline

This is the primary pattern — simulate a transaction and extract all state changes:

```csharp
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Decoding;
using Nethereum.EVM.Execution;
using Nethereum.EVM.StateChanges;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using System.Numerics;

// 1. Connect to blockchain state
var web3 = new Web3("https://mainnet.infura.io/v3/YOUR_KEY");
var blockNumber = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
var block = await web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber
    .SendRequestAsync(blockNumber);

var nodeDataService = new RpcNodeDataService(web3.Eth, new BlockParameter(blockNumber));
var executionState = new ExecutionStateService(nodeDataService);

// 2. Build execution context
var ctx = new TransactionExecutionContext
{
    Mode = ExecutionMode.Call,           // Simulate without gas payment
    Sender = "0xYourAddress",
    To = "0xContractAddress",
    Data = calldata.HexToByteArray(),    // ABI-encoded function call
    GasLimit = 10_000_000,
    Value = BigInteger.Zero,
    GasPrice = block.BaseFeePerGas.Value + 1_000_000_000,
    MaxFeePerGas = block.BaseFeePerGas.Value + 1_000_000_000,
    MaxPriorityFeePerGas = 1_000_000_000,
    Nonce = BigInteger.Zero,
    IsEip1559 = true,
    IsContractCreation = false,
    BlockNumber = (long)blockNumber.Value,
    Timestamp = (long)block.Timestamp.Value,
    BaseFee = block.BaseFeePerGas.Value,
    Coinbase = "0x0000000000000000000000000000000000000000",
    BlockGasLimit = 30_000_000,
    ExecutionState = executionState,
    TraceEnabled = true
};

// 3. Execute
var executor = new TransactionExecutor(HardforkConfig.Default);
var execResult = await executor.ExecuteAsync(ctx);

// 4. Decode call tree and events
var decoder = new ProgramResultDecoder(abiStorage); // IABIInfoStorage
var decoded = decoder.Decode(execResult, callInput, chainId);

// 5. Extract balance changes
var extractor = new StateChangesExtractor();
var stateChanges = extractor.ExtractFromDecodedResult(
    decoded, executionState, "0xCurrentUserAddress");

// Check results
if (execResult.Success)
{
    Console.WriteLine(stateChanges.ToSummaryString());
    foreach (var change in stateChanges.BalanceChanges)
    {
        Console.WriteLine($"{change.Address}: {change.Change} ({change.Type})");
    }
}
else
{
    Console.WriteLine($"Would revert: {decoded.RevertReason?.GetDisplayMessage()}");
}
```

## Key Classes

| Class | Purpose |
|-------|---------|
| `TransactionExecutor` | Executes transactions with full gas/fee logic |
| `TransactionExecutionContext` | All inputs: sender, target, calldata, block context, state |
| `TransactionExecutionResult` | Results: Success, GasUsed, Logs, Traces, ReturnData |
| `HardforkConfig` | Cancun/Prague/Default presets controlling EVM features |
| `ProgramResultDecoder` | Decodes raw results into call trees, events, errors |
| `DecodedProgramResult` | Root call tree, decoded logs, return value, revert reason |
| `DecodedCall` | From, To, Function, InputParameters, InnerCalls, CallType |
| `DecodedLog` | ContractAddress, Event, Parameters, IsDecoded |
| `DecodedError` | Error ABI, Parameters, Message |
| `StateChangesExtractor` | Extracts balance changes from decoded results |
| `StateChangesResult` | BalanceChanges, RootCall, DecodedLogs, GasUsed, Error |
| `BalanceChange` | Type (Native/ERC20/ERC721/ERC1155), Change, TokenAddress |
| `EVMSimulator` | Core bytecode execution engine |
| `Program` | Bytecode, stack, memory, storage, trace |
| `EVMDebuggerSession` | Interactive debugging with source maps |
| `ProgramInstructionsUtils` | Bytecode disassembly and function signature detection |
| `ERC20ContractSimulator` | Specialized ERC-20 token simulation |

## Decoding Call Trees

```csharp
var decoded = decoder.Decode(execResult, callInput, chainId);

// Walk the call tree
void PrintCallTree(DecodedCall call, int indent = 0)
{
    var prefix = new string(' ', indent * 2);
    Console.WriteLine($"{prefix}{call.CallType}: {call.GetDisplayName()} (gas: {call.GasUsed})");
    if (call.InputParameters != null)
        foreach (var p in call.InputParameters)
            Console.WriteLine($"{prefix}  {p.Parameter.Name}: {p.Result}");
    if (call.InnerCalls != null)
        foreach (var inner in call.InnerCalls)
            PrintCallTree(inner, indent + 1);
}
PrintCallTree(decoded.RootCall);
```

## Decoding Reverts

```csharp
if (!execResult.Success)
{
    var decoded = decoder.Decode(execResult, callInput, chainId);
    if (decoded.RevertReason != null)
    {
        Console.WriteLine($"Error: {decoded.RevertReason.GetErrorName()}");
        Console.WriteLine($"Message: {decoded.RevertReason.GetDisplayMessage()}");
        if (decoded.RevertReason.Parameters != null)
            foreach (var p in decoded.RevertReason.Parameters)
                Console.WriteLine($"  {p.Parameter.Name}: {p.Result}");
    }
}
```

## Bytecode Execution

```csharp
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;

var bytecode = "6004600401".HexToByteArray(); // PUSH1 4, PUSH1 4, ADD
var vm = new EVMSimulator();
var program = new Program(bytecode);
await vm.ExecuteAsync(program, traceEnabled: true);

var result = program.StackPeek(); // 0x08
foreach (var trace in program.Trace)
    Console.WriteLine($"{trace.Instruction?.Instruction} Gas:{trace.GasCost}");
```

## Bytecode Disassembly

```csharp
var disassembly = ProgramInstructionsUtils.DisassembleToString(bytecodeHex);
// "0000   60   PUSH1  0x80\n0002   60   PUSH1  0x40\n..."

var instructions = ProgramInstructionsUtils.GetProgramInstructions(bytecodeHex);
bool hasTransfer = ProgramInstructionsUtils.ContainsFunctionSignature(
    instructions, "0xa9059cbb"); // transfer(address,uint256)
```

## ExecutionMode

- `ExecutionMode.Call` — simulates without gas payment (like `eth_call`)
- `ExecutionMode.Transaction` — full execution with gas payment validation

## HardforkConfig

- `HardforkConfig.Cancun` — EIP-4844 only, 6 blobs, precompiles 1-10
- `HardforkConfig.Prague` — All EIPs (4844, 7623, 7702), 9 blobs, precompiles 1-17
- `HardforkConfig.Default` — Same as Prague

## Balance Change Types

| Type | Source | Key Properties |
|------|--------|---------------|
| `Native` | Call tree value transfers | `Change` (wei) |
| `ERC20` | `Transfer` events | `Change`, `TokenAddress`, `TokenSymbol` |
| `ERC721` | `Transfer` events (indexed tokenId) | `TokenId`, `TokenAddress` |
| `ERC1155` | `TransferSingle`/`TransferBatch` | `TokenId`, `Change`, `TokenAddress` |

## Validation Statuses

`BalanceValidationStatus`: `NotValidated`, `Verified`, `FeeOnTransfer`, `Rebasing`, `OwnerMismatch`, `Mismatch`

For full documentation, see: https://docs.nethereum.com/docs/evm-simulator/overview
