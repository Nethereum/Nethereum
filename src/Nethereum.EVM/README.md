# Nethereum.EVM

**Nethereum.EVM** is a production-ready Ethereum Virtual Machine (EVM) execution engine that runs bytecode instruction-by-instruction with full trace support and gas calculation. Passes all Ethereum VM and State tests.

## Overview

This package provides a local EVM implementation that can:
- Execute EVM bytecode step-by-step
- Calculate gas costs for all opcodes including dynamic gas operations
- Track warm/cold storage and address access (EIP-2929)
- Maintain execution traces with stack, memory, and storage snapshots
- Interact with real blockchain state via RPC
- Parse and disassemble bytecode
- Support all EVM opcodes including recent additions (Cancun, Shanghai forks)

**Status**: Production - passes all Ethereum VM and State tests. Purpose-built for development tooling, testing, debugging, and simulation.

## Installation

```bash
dotnet add package Nethereum.EVM
```

## Core Components

### EVMSimulator

Main execution engine that processes EVM bytecode. Located in `EVMSimulator.cs:30-417`.

**Key Methods:**
- `ExecuteAsync(Program program, ...)` - Executes program until completion
- `StepAsync(Program program, ...)` - Executes single instruction

**Example: Basic Bytecode Execution**
```csharp
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;

// Execute PUSH1 0xA0 (pushes 0xA0 onto stack)
var vm = new EVMSimulator();
var program = new Program("60A0".HexToByteArray());
await vm.StepAsync(program, 0);

var result = program.StackPeek(); // Returns: 0x00000000...0000A0
```

**Example: Multi-Instruction Execution**
```csharp
// Execute: PUSH1 0x04, PUSH1 0x04, ADD (4 + 4 = 8)
var vm = new EVMSimulator();
var bytecode = "6004600401".HexToByteArray(); // PUSH1 0x04, PUSH1 0x04, ADD
var program = new Program(bytecode);

// Step through each instruction
await vm.StepAsync(program, 0); // PUSH1 0x04
await vm.StepAsync(program, 1); // PUSH1 0x04
await vm.StepAsync(program, 2); // ADD

var result = program.StackPeek(); // Returns: 0x0000...0008
```

From test: `EvmSimulatorTests.cs:232-237`

### Program

Represents EVM program state including stack, memory, storage, and instructions. Located in `Program.cs:14-293`.

**Key Properties:**
- `Instructions` - Parsed bytecode instructions
- `Memory` - EVM memory (expandable byte array)
- `Trace` - Execution trace history
- `ProgramResult` - Execution outcome
- `ProgramContext` - Execution context (addresses, block data, gas)
- `MAX_STACKSIZE = 1024` - Stack limit

**Stack Operations (Program.cs:102-170):**
```csharp
// Stack operations (32-byte values, stack grows downward)
program.StackPush(value);          // Push 32-byte value
var top = program.StackPeek();     // Peek at top
var item = program.StackPeekAt(2); // Peek at position 2
program.StackPop();                // Remove top
program.StackSwap(1);              // Swap positions
```

**Memory Operations (Program.cs:171-209):**
```csharp
// Memory expands automatically (32-byte increments)
program.WriteToMemory(index, totalSize, data, extend: true);
// Memory is accessed directly via the Memory property (List<byte>)
var memorySize = program.Memory.Count;
```

### ProgramContext

Execution environment with blockchain state. Located in `ProgramContext.cs:16-45`.

**Properties:**
- `AddressContract` - Contract being executed
- `AddressCaller` - Caller address
- `AddressOrigin` - Transaction originator
- `Gas`, `Value`, `ChainId` - Transaction parameters
- `BlockNumber`, `Timestamp`, `Coinbase`, `BaseFee`, `BlobBaseFee` - Block context
- `GasPrice`, `GasLimit`, `Difficulty` - Network parameters
- `TransientStorage` - EIP-1153 transient storage
- `ExecutionStateService` - State management

**Example: Creating Context**
```csharp
var callInput = new CallInput
{
    From = "0x742d35Cc6634C0532925a3b844Bc9e7595f0bEb",
    To = "0x1234567890123456789012345678901234567890",
    Value = new HexBigInteger(1000000000000000000), // 1 ETH
    Gas = new HexBigInteger(21000),
    ChainId = 1
};

var executionStateService = new ExecutionStateService(nodeDataService);
var context = new ProgramContext(callInput, executionStateService);
```

### Instruction Enum

Complete EVM opcode set. Located in `Instruction.cs:4-329`.

**Categories:**

**Arithmetic & Logic (0x00-0x1F):**
- `ADD, MUL, SUB, DIV, SDIV, MOD, SMOD, ADDMOD, MULMOD, EXP, SIGNEXTEND`
- `LT, GT, SLT, SGT, EQ, ISZERO, AND, OR, XOR, NOT, BYTE, SHL, SHR, SAR`

**Cryptographic (0x20):**
- `KECCAK256` - SHA3-256 hash

**Environment & Context (0x30-0x4A):**
- `ADDRESS, BALANCE, ORIGIN, CALLER, CALLVALUE, CALLDATALOAD, CALLDATASIZE, CALLDATACOPY`
- `CODESIZE, CODECOPY, GASPRICE, EXTCODESIZE, EXTCODECOPY, RETURNDATASIZE, RETURNDATACOPY, EXTCODEHASH`
- `BLOCKHASH, COINBASE, TIMESTAMP, NUMBER, DIFFICULTY, GASLIMIT, CHAINID, SELFBALANCE, BASEFEE`
- `BLOBHASH, BLOBBASEFEE` (Cancun fork)

**Stack, Memory, Storage (0x50-0x5F):**
- `POP, MLOAD, MSTORE, MSTORE8, SLOAD, SSTORE, JUMP, JUMPI, PC, MSIZE, GAS, JUMPDEST`
- `TLOAD, TSTORE` (Cancun - EIP-1153 transient storage)
- `MCOPY` (Cancun - memory copy)
- `PUSH0` (Shanghai - EIP-3855)

**Push Operations (0x60-0x7F):**
- `PUSH1` through `PUSH32` - Push 1-32 bytes onto stack

**Duplicate Operations (0x80-0x8F):**
- `DUP1` through `DUP16` - Duplicate stack items

**Swap Operations (0x90-0x9F):**
- `SWAP1` through `SWAP16` - Swap stack items

**Logging (0xA0-0xA4):**
- `LOG0, LOG1, LOG2, LOG3, LOG4` - Event logging

**Contract Operations (0xF0-0xFF):**
- `CREATE, CALL, CALLCODE, RETURN, DELEGATECALL, CREATE2, STATICCALL, REVERT, INVALID, SELFDESTRUCT`

### OpcodeGasTable

Comprehensive gas calculation for all opcodes with EIP-2929 (warm/cold access) support. Located in `Gas/OpcodeGasTable.cs:10-523`.

**Static Gas Costs (OpcodeGasTable.cs:12-109):**
```csharp
// Common static costs
ADD = 3
MUL = 5
PUSH1-PUSH32 = 3
DUP1-DUP16 = 3
SWAP1-SWAP16 = 3
SELFDESTRUCT = -1 (dynamic)
TLOAD, TSTORE = 100 (transient storage)
```

**Dynamic Gas Calculation:**

Operations marked with `-1` cost have dynamic gas calculated based on:
- Memory expansion
- Storage access (warm/cold, original/current values)
- Call operations (account creation, value transfer)

**Example: SSTORE Gas (OpcodeGasTable.cs:280-318)**
```csharp
// SSTORE has complex gas calculation:
// - Cold access: +2100 gas
// - Setting from zero: 20000 gas
// - Setting from non-zero to different non-zero: 2900 gas (if original value)
// - Setting to same value: 100 gas
// - Dirty slot (already modified): 100 gas
```

**Example: CALL Gas (OpcodeGasTable.cs:357-396)**
```csharp
// CALL gas includes:
// - Cold account access: 2600 gas (warm: 100 gas)
// - Memory expansion cost
// - Value transfer: +9000 gas
// - Account creation (if empty): +25000 gas
```

### ExecutionStateService

Manages account states during execution. Located in `BlockchainState/ExecutionStateService.cs:11-137`.

**Key Methods:**
- `GetFromStorageAsync(address, key)` - Fetch storage with caching
- `GetCodeAsync(address)` - Fetch contract code
- `GetNonceAsync(address)` - Fetch account nonce
- `GetTotalBalanceAsync(address)` - Fetch account balance
- `SaveToStorage(address, key, value)` - Update storage
- `LoadBalanceNonceAndCodeFromStorageAsync(address)` - Load full account state
- `MarkAddressAsWarm(address)` - Track warm addresses for gas calculation

**Example: Using with RPC Node**
```csharp
using Nethereum.EVM.BlockchainState;
using Nethereum.Web3;

var web3 = new Web3("https://mainnet.infura.io/v3/YOUR_KEY");
var nodeDataService = new RpcNodeDataService(
    web3.Eth,
    BlockParameter.CreateLatest()
);

var stateService = new ExecutionStateService(nodeDataService);

// Fetch and cache account data
var code = await stateService.GetCodeAsync("0x1234...");
var storage = await stateService.GetFromStorageAsync("0x1234...", BigInteger.Zero);
var balance = await stateService.GetTotalBalanceAsync("0x1234...");
```

From: `RpcNodeDataService.cs:14-109`

### ProgramResult

Execution outcome with results, logs, and tracking. Located in `ProgramResult.cs:11-42`.

**Properties:**
- `Result` - Return data (byte[])
- `Logs` - Event logs (FilterLog list)
- `IsRevert` - Revert flag
- `IsSelfDestruct` - Self-destruct flag
- `DeletedContractAccounts` - Destroyed contracts
- `CreatedContractAccounts` - Created contracts
- `InnerCalls` - Sub-calls made
- `InnerContractCodeCalls` - Called contract codes
- `Exception` - Execution exception

**Example: Handling Results**
```csharp
await vm.ExecuteAsync(program);

var result = program.ProgramResult;

if (result.IsRevert)
{
    // Decode revert message (ABI-encoded Error(string))
    var message = result.GetRevertMessage();
    Console.WriteLine($"Reverted: {message}");
}
else
{
    var returnData = result.Result;
    foreach (var log in result.Logs)
    {
        Console.WriteLine($"Log: {log.Topics[0]}");
    }
}
```

### ProgramTrace

Execution trace for debugging. Located in `ProgramTrace.cs:9-101`.

**Properties:**
- `ProgramAddress`, `CodeAddress` - Execution addresses
- `VMTraceStep`, `ProgramTraceStep` - Step counters
- `Depth` - Call depth
- `Instruction` - Executed instruction
- `Stack` - Stack state snapshot
- `Memory` - Memory state snapshot
- `Storage` - Storage state snapshot
- `GasCost` - Gas consumed by instruction

**Example: Analyzing Traces**
```csharp
var vm = new EVMSimulator();
var program = new Program(bytecode, context);

await vm.ExecuteAsync(program, traceEnabled: true);

foreach (var trace in program.Trace)
{
    Console.WriteLine(trace.ToString());
    // Output includes:
    // - Address, VMTraceStep, Depth, Gas
    // - Instruction with arguments
    // - Stack contents
    // - Memory contents
    // - Storage changes
}
```

From: `ProgramTrace.cs:79-99`

### Bytecode Utilities

Parse and disassemble EVM bytecode. Located in `ProgramInstructionsUtils.cs:8-146`.

**Disassembly Methods:**
```csharp
using Nethereum.EVM;

var bytecode = "0x60806040526004361060...";

// Parse into instructions
var instructions = ProgramInstructionsUtils.GetProgramInstructions(bytecode);

// Full disassembly
var disassembly = ProgramInstructionsUtils.DisassembleToString(bytecode);
// Output format: "0000   60   PUSH1  0x80"

// Simplified disassembly
var simplified = ProgramInstructionsUtils.DisassembleSimplifiedToString(bytecode);
// Output format: "PUSH1 0x80 PUSH1 0x40 MSTORE"
```

**Function Signature Detection:**
```csharp
var instructions = ProgramInstructionsUtils.GetProgramInstructions(contractCode);

// Check for specific function signature
bool hasTransfer = ProgramInstructionsUtils.ContainsFunctionSignature(
    instructions,
    "0xa9059cbb" // transfer(address,uint256)
);

// Check for multiple signatures
var signatures = new[] { "0xa9059cbb", "0x70a08231" }; // transfer, balanceOf
bool hasAll = ProgramInstructionsUtils.ContainsFunctionSignatures(instructions, signatures);
```

From: `ProgramInstructionsUtils.cs:10-35, 38-146`

## Complete Examples

### Example 1: Testing Arithmetic Operations

```csharp
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using System.Numerics;

// Test: 2 + 2 with ADDMOD 3 (should equal 1)
var bytecode = "60036002600208"; // PUSH1 0x03, PUSH1 0x02, PUSH1 0x02, ADDMOD

var vm = new EVMSimulator();
var program = new Program(bytecode.HexToByteArray());

// Execute all instructions
await vm.ExecuteAsync(program, traceEnabled: false);

var result = program.StackPeek();
// result = 0x0000...0001 (4 % 3 = 1)
```

From test pattern: `EvmSimulatorTests.cs:253-258`

### Example 2: Testing Memory Operations with KECCAK256

```csharp
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;

// Store 0x01 at memory[0], then hash 1 byte starting at memory[0]
// PUSH1 0x01, PUSH1 0x00, MSTORE8, PUSH1 0x01, PUSH1 0x00, KECCAK256
var bytecode = "6001600053600160002016".HexToByteArray();

var vm = new EVMSimulator();
var program = new Program(bytecode);

await vm.ExecuteAsync(program);

var hash = program.StackPeek().ToHex();
// hash = keccak256(0x01) = "5fe7f977e71dba2ea1a68e21057beebb9be2ac30c6410aa38d4f3fbe41dcffd2"
```

From test: `EvmSimulatorTests.cs:294-297`

### Example 3: Testing Conditional Jumps

```csharp
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;

// PUSH1 0x01 (condition=true), PUSH1 0x05 (jump target), JUMPI, JUMPDEST, PUSH1 0xCC
// If condition is true, jump to JUMPDEST at position 5, then push 0xCC
var bytecode = "60016005575B60CC".HexToByteArray();

var vm = new EVMSimulator();
var program = new Program(bytecode);

await vm.ExecuteAsync(program);

var result = program.StackPeek();
// result = 0xCC (jump was taken)
```

From test: `EvmSimulatorTests.cs:306-309`

### Example 4: Executing Contract Bytecode with RPC State

```csharp
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Hex.HexTypes;

// Connect to Ethereum node
var web3 = new Web3("https://mainnet.infura.io/v3/YOUR_KEY");

// Create node data service for fetching blockchain state
var nodeDataService = new RpcNodeDataService(
    web3.Eth,
    BlockParameter.CreateLatest()
);

var stateService = new ExecutionStateService(nodeDataService);

// Create call input
var callInput = new CallInput
{
    From = "0x0000000000000000000000000000000000000001",
    To = "0xContractAddress",
    Gas = new HexBigInteger(1000000),
    ChainId = 1,
    Data = "0x" // Function call data
};

// Create program context
var context = new ProgramContext(callInput, stateService);

// Get contract bytecode from blockchain
var contractCode = await stateService.GetCodeAsync(callInput.To);

// Execute contract code
var vm = new EVMSimulator();
var program = new Program(contractCode, context);

await vm.ExecuteAsync(program, traceEnabled: true);

// Check results
if (program.ProgramResult.IsRevert)
{
    Console.WriteLine($"Reverted: {program.ProgramResult.GetRevertMessage()}");
}
else
{
    var returnData = program.ProgramResult.Result;
    Console.WriteLine($"Success: {returnData.ToHex()}");
}

// Analyze gas usage
var totalGas = program.Trace.Sum(t => t.GasCost);
Console.WriteLine($"Gas used: {totalGas}");
```

### Example 5: Disassembling Contract Bytecode

```csharp
using Nethereum.EVM;
using Nethereum.Web3;

var web3 = new Web3("https://mainnet.infura.io/v3/YOUR_KEY");

// Fetch USDC contract bytecode
var usdcAddress = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48";
var bytecode = await web3.Eth.GetCode.SendRequestAsync(usdcAddress);

// Parse into instructions
var instructions = ProgramInstructionsUtils.GetProgramInstructions(bytecode);

Console.WriteLine($"Total instructions: {instructions.Count}");

// Check for specific functions
bool hasTransfer = ProgramInstructionsUtils.ContainsFunctionSignature(
    instructions,
    "0xa9059cbb" // transfer(address,uint256) signature
);

Console.WriteLine($"Has transfer function: {hasTransfer}");

// Full disassembly
var disassembly = ProgramInstructionsUtils.DisassembleToString(bytecode);
Console.WriteLine(disassembly);

// Output format:
// 0000   60   PUSH1  0x80
// 0002   60   PUSH1  0x40
// 0004   52   MSTORE
// ...
```

### Example 6: Step-by-Step Execution with Traces

```csharp
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;

// Bytecode: PUSH1 0xF0, PUSH1 0x0F, OR (0xF0 | 0x0F = 0xFF)
var bytecode = "60F0600F17".HexToByteArray();

var vm = new EVMSimulator();
var program = new Program(bytecode);

// Step through each instruction
int step = 0;

// Step 1: PUSH1 0xF0
await vm.StepAsync(program, step++);
Console.WriteLine($"After PUSH1 0xF0: Stack top = {program.StackPeek().ToHex()}");
// Stack: [0xF0]

// Step 2: PUSH1 0x0F
await vm.StepAsync(program, step++);
Console.WriteLine($"After PUSH1 0x0F: Stack has {program.GetCurrentStackAsHex().Count} items");
// Stack: [0x0F, 0xF0]

// Step 3: OR
await vm.StepAsync(program, step++);
Console.WriteLine($"After OR: Stack top = {program.StackPeek().ToHex()}");
// Stack: [0xFF]

// Check traces
foreach (var trace in program.Trace)
{
    Console.WriteLine($"Step {trace.VMTraceStep}: {trace.Instruction?.Instruction} - Gas: {trace.GasCost}");
}
```

From test pattern: `EvmSimulatorTests.cs:117-121, 334-344`

## Gas Calculation Details

### Warm/Cold Access (EIP-2929)

**First access (cold) costs more:**
- Address access: 2600 gas (BALANCE, EXTCODESIZE, EXTCODECOPY, EXTCODEHASH, CALL, etc.)
- Storage slot access: 2100 gas (SLOAD)

**Subsequent access (warm) costs less:**
- Address access: 100 gas
- Storage slot access: 100 gas

Located in `OpcodeGasTable.cs:224-278, 478-486`

### Storage Operations (SSTORE)

Complex gas calculation based on EIP-2200 and EIP-2929:

```csharp
// Cold storage access: +2100 gas (first time)
// Warm storage access: 0 gas (already accessed)

// Value changes:
// 1. Setting from zero to non-zero: 20000 gas (new slot)
// 2. Setting from non-zero to different non-zero: 2900 gas (if original value)
// 3. Setting to same value: 100 gas (no-op)
// 4. Dirty slot (already modified in transaction): 100 gas
```

Located in `OpcodeGasTable.cs:280-318`

### Memory Expansion

Memory expands in 32-byte increments with quadratic cost:

```csharp
// Gas cost = memory_size_word * 3 + floor(memory_size_word^2 / 512)
// where memory_size_word = ceil(memory_size_byte / 32)
```

Located in `Program.cs:256-266`

### Call Operations

CALL, CALLCODE, DELEGATECALL, STATICCALL have multiple cost components:

```csharp
// Base costs:
// - Account access (warm: 100, cold: 2600)
// - Memory expansion (input + output)
// - Value transfer: +9000 gas (if value > 0)
// - Account creation: +25000 gas (if target is empty account and value > 0)
```

Located in `OpcodeGasTable.cs:357-462`

## Supported EVM Features

### Fork Support

**Cancun (Latest):**
- `BLOBHASH` (0x49) - Get blob versioned hashes
- `BLOBBASEFEE` (0x4A) - Blob base fee
- `TLOAD` (0x5C) - Transient storage load (EIP-1153)
- `TSTORE` (0x5D) - Transient storage store (EIP-1153)
- `MCOPY` (0x5E) - Memory copy

**Shanghai:**
- `PUSH0` (0x5F) - Push zero onto stack (EIP-3855)

**London:**
- `BASEFEE` (0x48) - Current block's base fee (EIP-3198)

**Istanbul:**
- `CHAINID` (0x46) - Network chain ID (EIP-1344)
- `SELFBALANCE` (0x47) - Contract's own balance (EIP-1884)

**Constantinople:**
- `CREATE2` (0xF5) - Deterministic contract creation (EIP-1014)
- `EXTCODEHASH` (0x3F) - Contract code hash (EIP-1052)
- `SHL, SHR, SAR` (0x1B-0x1D) - Bit shifting

**Byzantium:**
- `RETURNDATASIZE` (0x3D), `RETURNDATACOPY` (0x3E) - Return data (EIP-211)
- `STATICCALL` (0xFA) - Static call (EIP-214)
- `REVERT` (0xFD) - Revert with data (EIP-140)

From: `Instruction.cs:4-329`

## Scope and Use Cases

### Production Use Cases

This EVM implementation is designed for:
- **DevChain**: Local development node (similar to Hardhat Network, Anvil)
- **Simulation**: Transaction simulation and what-if analysis
- **Testing**: Smart contract testing and debugging
- **Analysis**: Bytecode disassembly and gas optimization research

**Not designed for:**
- Full Ethereum client (not a geth/reth replacement)
- Mainnet validator node
- Consensus-critical public chain execution

### Known Limitations

1. **Precompiled Contracts**: Not all precompiled contracts are fully implemented
2. **State Trie**: No full state trie implementation
3. **Gas Refunds**: Gas refunds (e.g., SSTORE clearing) not fully tracked
4. **Block Hash Limits**: `BLOCKHASH` limited by RPC node capabilities
5. **Performance**: Not optimized for high-throughput execution

## Advanced Usage

### Custom State Providers

Implement `INodeDataService` for custom state sources:

```csharp
public class CustomStateProvider : INodeDataService
{
    public async Task<BigInteger> GetBalanceAsync(string address)
    {
        // Custom balance logic
        return BigInteger.Zero;
    }

    public async Task<byte[]> GetCodeAsync(string address)
    {
        // Custom code retrieval
        return new byte[0];
    }

    public async Task<byte[]> GetStorageAtAsync(string address, BigInteger position)
    {
        // Custom storage logic
        return new byte[32];
    }

    // Implement other INodeDataService methods...
}
```

From interface: `INodeDataService.cs:6-18`

### Debug Storage Access

Use `debug_storageRangeAt` for precise storage state at transaction index:

```csharp
var nodeDataService = new RpcNodeDataService(
    web3.Eth,
    BlockParameter.CreateLatest(),
    web3.DebugApiService,
    blockHash: "0xabc123...",
    transactionIndex: 5,
    useDebugStorageAt: true
);

// Storage reads will use debug_storageRangeAt for transaction-level precision
```

From: `RpcNodeDataService.cs:23-90`

## Transaction Execution Pipeline

The `TransactionExecutor` provides a complete EVM transaction execution pipeline that handles gas calculation, EIP-1559 fee logic, EIP-4844 blob transactions, EIP-7702 authorization lists, and state management.

### TransactionExecutor

```csharp
public class TransactionExecutor
{
    public TransactionExecutor(
        HardforkConfig config = null,
        EVMSimulator evmSimulator = null,
        IPrecompileProvider customPrecompileProvider = null);

    public async Task<TransactionExecutionResult> ExecuteAsync(TransactionExecutionContext ctx);
}
```

The constructor accepts an optional `HardforkConfig` (defaults to `HardforkConfig.Default`), an optional pre-configured `EVMSimulator`, and an optional custom `IPrecompileProvider` that gets composed with the config's built-in provider via `CompositePrecompileProvider`.

From: `TransactionExecutor.cs:17-81`

### ExecutionMode

```csharp
public enum ExecutionMode
{
    Transaction,  // Full transaction execution with gas payment
    Call           // eth_call mode - skips gas price validation
}
```

From: `TransactionExecutionContext.cs:9-13`

### TransactionExecutionContext

All inputs needed to execute a transaction:

**Transaction fields:**

| Property | Type | Description |
|---|---|---|
| `Mode` | `ExecutionMode` | Transaction or Call mode |
| `IsCallMode` | `bool` | Shorthand for `Mode == ExecutionMode.Call` |
| `Sender` | `string` | Transaction sender address |
| `To` | `string` | Destination address (null for contract creation) |
| `Data` | `byte[]` | Transaction calldata |
| `GasLimit` | `BigInteger` | Gas limit |
| `Value` | `BigInteger` | ETH value in wei |
| `GasPrice` | `BigInteger` | Legacy gas price |
| `MaxFeePerGas` | `BigInteger` | EIP-1559 max fee |
| `MaxPriorityFeePerGas` | `BigInteger` | EIP-1559 priority fee |
| `EffectiveGasPrice` | `BigInteger` | Calculated effective gas price |
| `Nonce` | `BigInteger` | Sender nonce |
| `IsEip1559` | `bool` | Whether this is a type-2 transaction |
| `IsContractCreation` | `bool` | Whether this creates a contract |
| `IsType3Transaction` | `bool` | EIP-4844 blob transaction |
| `IsType4Transaction` | `bool` | EIP-7702 authorization transaction |
| `BlobVersionedHashes` | `List<string>` | Blob versioned hashes (type-3) |
| `MaxFeePerBlobGas` | `BigInteger` | Max blob gas fee (type-3) |
| `AccessList` | `List<AccessListEntry>` | EIP-2930 access list |
| `AuthorisationList` | `List<Authorisation7702Signed>` | EIP-7702 authorizations |

**Block context:**

| Property | Type | Description |
|---|---|---|
| `BlockNumber` | `long` | Current block number |
| `Timestamp` | `long` | Block timestamp |
| `Coinbase` | `string` | Block coinbase address |
| `BaseFee` | `BigInteger` | Block base fee |
| `Difficulty` | `BigInteger` | Block difficulty |
| `BlockGasLimit` | `BigInteger` | Block gas limit |
| `ExcessBlobGas` | `BigInteger` | Excess blob gas |
| `BlobBaseFee` | `BigInteger` | Blob base fee |
| `ChainId` | `BigInteger` | Chain ID |

**Computed/state fields:**

| Property | Type | Description |
|---|---|---|
| `IntrinsicGas` | `BigInteger` | Calculated intrinsic gas cost |
| `FloorGas` | `BigInteger` | EIP-7623 floor gas limit |
| `MinGasRequired` | `BigInteger` | Max of intrinsic and floor gas |
| `BlobGasCost` | `BigInteger` | Total blob gas cost |
| `ContractAddress` | `string` | Created contract address |
| `ExecutionState` | `ExecutionStateService` | State service for account/storage access |
| `SenderAccount` | `AccountExecutionState` | Sender's account state |
| `Code` | `byte[]` | Bytecode to execute |
| `DelegateAddress` | `string` | EIP-7702 delegate address |
| `TraceEnabled` | `bool` | Whether to capture execution traces |

From: `TransactionExecutionContext.cs:15-65`

### TransactionExecutionResult

```csharp
public class TransactionExecutionResult
{
    public bool Success { get; set; }
    public BigInteger GasUsed { get; set; }
    public BigInteger GasRefund { get; set; }
    public BigInteger EffectiveGasUsed { get; set; }
    public byte[] ReturnData { get; set; }
    public string RevertReason { get; set; }
    public List<FilterLog> Logs { get; set; }
    public byte[] StateRoot { get; set; }
    public string ContractAddress { get; set; }
    public string Error { get; set; }
    public bool IsValidationError { get; set; }
    public List<ProgramTrace> Traces { get; set; }
    public List<string> CreatedAccounts { get; set; }
    public List<string> DeletedAccounts { get; set; }
    public List<CallInput> InnerCalls { get; set; }
    public Dictionary<string, List<ProgramInstruction>> InnerContractCodeCalls { get; set; }
    public ProgramResult ProgramResult { get; set; }
}
```

From: `TransactionExecutionResult.cs:7-26`

### TransactionError Enum

```csharp
public enum TransactionError
{
    None,
    InsufficientMaxFeePerGas,
    PriorityGreaterThanMaxFee,
    InsufficientBalance,
    GasAllowanceExceeded,
    IntrinsicGasTooLow,
    NonceIsMax,
    SenderNotEOA,
    InitcodeSizeExceeded,
    Type3TxContractCreation,
    Type3TxZeroBlobs,
    Type3TxBlobCountExceeded,
    Type3TxInvalidBlobVersionedHash,
    AddressCollision,
    InvalidEFPrefix,
    MaxCodeSizeExceeded,
    OutOfGas,
    Reverted,
}
```

From: `TransactionExecutionResult.cs:28-48`

### HardforkConfig

```csharp
public class HardforkConfig
{
    public bool EnableEIP4844 { get; set; }     // Blob transactions (Cancun)
    public bool EnableEIP7623 { get; set; }     // Calldata gas floor (Prague)
    public bool EnableEIP7702 { get; set; }     // Authorization lists (Prague)
    public int MaxBlobsPerBlock { get; set; }
    public IPrecompileProvider PrecompileProvider { get; set; }

    public static HardforkConfig Cancun { get; }   // EIP-4844 only, 6 blobs, Cancun precompiles (1-10)
    public static HardforkConfig Prague { get; }    // All EIPs, 9 blobs, Prague precompiles (1-17)
    public static HardforkConfig Default { get; }   // Same as Prague

    public static HardforkConfig FromName(string hardfork); // "cancun" or "prague"
}
```

From: `HardforkConfig.cs:1-48`

### Transaction Simulation Example

This example shows the wallet preview pattern used by `StateChangesPreviewService` to simulate a transaction and extract state changes:

```csharp
var nodeDataService = new RpcNodeDataService(web3.Eth, new BlockParameter(blockNumber));
var executionStateService = new ExecutionStateService(nodeDataService);

var ctx = new TransactionExecutionContext
{
    Mode = ExecutionMode.Call,
    Sender = callInput.From,
    To = callInput.To,
    Data = callInput.Data?.HexToByteArray(),
    GasLimit = 10_000_000,
    Value = callInput.Value?.Value ?? BigInteger.Zero,
    GasPrice = baseFee + 1_000_000_000,
    MaxFeePerGas = callInput.MaxFeePerGas?.Value ?? baseFee + 1_000_000_000,
    MaxPriorityFeePerGas = callInput.MaxPriorityFeePerGas?.Value ?? 1_000_000_000,
    Nonce = BigInteger.Zero,
    IsEip1559 = callInput.MaxFeePerGas != null,
    IsContractCreation = string.IsNullOrEmpty(callInput.To),
    BlockNumber = (long)blockNumber.Value,
    Timestamp = timestamp,
    BaseFee = baseFee,
    Coinbase = "0x0000000000000000000000000000000000000000",
    BlockGasLimit = 30_000_000,
    ExecutionState = executionStateService,
    TraceEnabled = true
};

var config = HardforkConfig.Default;
var executor = new TransactionExecutor(config);
var execResult = await executor.ExecuteAsync(ctx);

if (execResult.Success)
{
    // Decode and extract state changes
    var decoder = new ProgramResultDecoder(abiStorage);
    var decodedResult = decoder.Decode(execResult, callInput, chainId);

    var extractor = new StateChangesExtractor();
    var stateChanges = extractor.ExtractFromDecodedResult(
        decodedResult, executionStateService, currentUserAddress);
}
```

Reference: `StateChangesPreviewService.cs` in `Nethereum.Wallet`

## Decoding Pipeline

The decoding pipeline transforms raw EVM execution results into human-readable decoded calls, logs, errors, and return values using ABI information from `IABIInfoStorage`.

### ProgramResultDecoder

```csharp
public class ProgramResultDecoder
{
    public ProgramResultDecoder(IABIInfoStorage abiStorage);

    public DecodedProgramResult Decode(
        Program program, CallInput initialCall, BigInteger chainId);

    public DecodedProgramResult Decode(
        TransactionExecutionResult executionResult, CallInput initialCall, BigInteger chainId);

    public DecodedProgramResult Decode(
        ProgramResult programResult, List<ProgramTrace> trace,
        CallInput initialCall, BigInteger chainId);

    public DecodedCall DecodeCall(CallInput call, BigInteger chainId, int depth);

    public DecodedLog DecodeLog(FilterLog log, BigInteger chainId);

    public DecodedError DecodeRevert(byte[] revertData, BigInteger chainId, string contractAddress);

    public List<ParameterOutput> DecodeReturnValue(FunctionABI functionABI, string output);
}
```

The decoder resolves function signatures, event topics, and error selectors against `IABIInfoStorage`. It supports three input types: a `Program` (from `EVMSimulator`), a `TransactionExecutionResult` (from `TransactionExecutor`), or a raw `ProgramResult` with trace.

From: `Decoding/ProgramResultDecoder.cs:13-282`

### DecodedProgramResult

```csharp
public class DecodedProgramResult
{
    public DecodedCall RootCall { get; set; }
    public List<DecodedLog> DecodedLogs { get; set; }
    public List<ParameterOutput> ReturnValue { get; set; }
    public DecodedError RevertReason { get; set; }
    public bool IsRevert { get; set; }
    public bool IsSuccess { get; }              // !IsRevert
    public ProgramResult OriginalResult { get; set; }
    public CallInput OriginalCall { get; set; }
    public BigInteger ChainId { get; set; }

    public string ToHumanReadableString();       // Formatted call tree, events, and result
}
```

From: `Decoding/DecodedProgramResult.cs:9-19`

### DecodedCall

```csharp
public enum CallType
{
    Call, DelegateCall, StaticCall, CallCode, Create, Create2
}

public class DecodedCall
{
    public string From { get; set; }
    public string To { get; set; }
    public string ContractName { get; set; }
    public FunctionABI Function { get; set; }
    public List<ParameterOutput> InputParameters { get; set; }
    public List<ParameterOutput> OutputParameters { get; set; }
    public List<DecodedCall> InnerCalls { get; set; }
    public List<DecodedLog> Logs { get; set; }
    public CallType CallType { get; set; }
    public int Depth { get; set; }
    public bool IsDecoded { get; set; }
    public string RawInput { get; set; }
    public string RawOutput { get; set; }
    public BigInteger Value { get; set; }
    public BigInteger GasUsed { get; set; }
    public bool IsRevert { get; set; }
    public DecodedError Error { get; set; }
    public CallInput OriginalCall { get; set; }

    public string GetFunctionSignature();  // Returns Function.Sha3Signature
    public string GetFunctionName();       // Returns Function.Name
    public string GetDisplayName();        // "ContractName.FunctionName" or fallback
}
```

From: `Decoding/DecodedCall.cs:9-68`

### DecodedLog

```csharp
public class DecodedLog
{
    public string ContractAddress { get; set; }
    public string ContractName { get; set; }
    public EventABI Event { get; set; }
    public List<ParameterOutput> Parameters { get; set; }
    public bool IsDecoded { get; set; }
    public FilterLog OriginalLog { get; set; }
    public int LogIndex { get; set; }
    public int CallDepth { get; set; }

    public string GetEventSignature();  // Returns Event.Sha3Signature
    public string GetEventName();       // Returns Event.Name
    public string GetDisplayName();     // "ContractName.EventName" or fallback
}
```

From: `Decoding/DecodedLog.cs:8-43`

### DecodedError

```csharp
public class DecodedError
{
    public ErrorABI Error { get; set; }
    public List<ParameterOutput> Parameters { get; set; }
    public string Message { get; set; }
    public bool IsStandardError { get; set; }
    public bool IsDecoded { get; set; }
    public string RawData { get; set; }

    public string GetErrorSignature();     // Returns Error.Sha3Signature
    public string GetErrorName();          // Returns Error.Name
    public string GetDisplayMessage();     // Message, Error.Name, or RawData fallback

    public static DecodedError FromStandardError(string message, string rawData = null);
    public static DecodedError FromUnknownError(string rawData);
}
```

From: `Decoding/DecodedError.cs:7-61`

### Decoding Example

```csharp
// After executing a transaction
var executor = new TransactionExecutor(HardforkConfig.Default);
var execResult = await executor.ExecuteAsync(ctx);

// Decode the result using ABI storage
var decoder = new ProgramResultDecoder(abiStorage);
var decoded = decoder.Decode(execResult, callInput, chainId);

// Inspect the decoded result
Console.WriteLine(decoded.ToHumanReadableString());

// Access individual parts
if (decoded.IsSuccess)
{
    foreach (var param in decoded.ReturnValue)
    {
        Console.WriteLine($"{param.Parameter.Name}: {param.Result}");
    }
}
else
{
    Console.WriteLine($"Reverted: {decoded.RevertReason?.GetDisplayMessage()}");
}

// Walk the call tree
Console.WriteLine($"Root: {decoded.RootCall.GetDisplayName()}");
foreach (var inner in decoded.RootCall.InnerCalls)
{
    Console.WriteLine($"  -> {inner.GetDisplayName()}");
}

// Inspect events
foreach (var log in decoded.DecodedLogs)
{
    Console.WriteLine($"Event: {log.GetDisplayName()}");
}
```

## State Changes Extraction

The state changes extraction pipeline analyzes decoded execution results to identify all balance changes (native ETH, ERC20, ERC721, ERC1155) that occurred during a transaction.

### StateChangesExtractor

```csharp
public class StateChangesExtractor : IStateChangesExtractor
{
    public StateChangesResult ExtractFromDecodedResult(
        DecodedProgramResult decodedResult,
        ExecutionStateService stateService = null,
        string currentUserAddress = null);

    public StateChangesResult ExtractFromDecodedResult(
        DecodedProgramResult decodedResult,
        ExecutionStateService stateService,
        string currentUserAddress,
        Func<string, TokenInfo> tokenResolver);

    public async Task<StateChangesResult> ExtractFromDecodedResultAsync(
        DecodedProgramResult decodedResult,
        ExecutionStateService stateService = null,
        string currentUserAddress = null,
        Func<string, Task<TokenInfo>> tokenResolverAsync = null,
        CancellationToken cancellationToken = default);

    public void ValidateTokenBalances(
        StateChangesResult result,
        Program program,
        ExecutionStateService stateService,
        Func<string, string, BigInteger> getErc20Balance = null,
        Func<string, BigInteger, string> getErc721Owner = null,
        Func<string, string, BigInteger, BigInteger> getErc1155Balance = null);
}
```

The extractor:
- Parses `Transfer` events from logs to identify ERC20/ERC721 transfers
- Parses `TransferSingle` and `TransferBatch` events for ERC1155 transfers
- Walks the call tree to find native ETH value transfers
- Consolidates duplicate changes per address/token pair (net zero changes are removed)
- Orders results: current user first, then by type, then by address
- Optionally enriches with before/after balances from `ExecutionStateService`
- `ValidateTokenBalances` cross-checks extracted changes against actual state, detecting fee-on-transfer tokens and rebasing tokens

From: `StateChanges/StateChangesExtractor.cs:21-574`

### StateChangesResult

```csharp
public class StateChangesResult
{
    public List<BalanceChange> BalanceChanges { get; set; }
    public DecodedCall RootCall { get; set; }
    public List<DecodedLog> DecodedLogs { get; set; }
    public DecodedProgramResult DecodedResult { get; set; }
    public string Error { get; set; }
    public List<ProgramTrace> Traces { get; set; }
    public BigInteger GasUsed { get; set; }

    public bool HasError { get; }             // !string.IsNullOrEmpty(Error)
    public bool HasBalanceChanges { get; }
    public bool HasDecodedLogs { get; }
    public bool HasTraces { get; }

    public string ToSummaryString();           // Formatted balance changes, call tree, and events
}
```

From: `StateChanges/StateChangesResult.cs:8-79`

### BalanceChange

```csharp
public enum BalanceChangeType
{
    Native,   // ETH transfers
    ERC20,    // ERC20 token transfers
    ERC721,   // ERC721 NFT transfers
    ERC1155   // ERC1155 multi-token transfers
}

public enum BalanceValidationStatus
{
    NotValidated,    // Not yet validated against actual state
    Verified,        // Change matches actual state
    FeeOnTransfer,   // Actual change < expected (fee-on-transfer token)
    Rebasing,        // Actual change > expected (rebasing token)
    OwnerMismatch,   // ERC721 owner doesn't match expected
    Mismatch         // General mismatch
}

public class BalanceChange
{
    // Identity
    public string Address { get; set; }
    public string AddressLabel { get; set; }
    public bool IsCurrentUser { get; set; }

    // Token info
    public BalanceChangeType Type { get; set; }
    public string TokenAddress { get; set; }
    public string TokenSymbol { get; set; }
    public int TokenDecimals { get; set; }
    public BigInteger? TokenId { get; set; }        // For ERC721/ERC1155

    // Change amount
    public BigInteger Change { get; set; }           // Positive = received, negative = sent
    public BigInteger? BalanceBefore { get; set; }
    public BigInteger? BalanceAfter { get; set; }

    // Validation
    public BigInteger? ActualChange { get; set; }
    public string ActualOwner { get; set; }          // For ERC721 validation
    public BalanceValidationStatus ValidationStatus { get; set; }
    public bool HasDiscrepancy { get; }

    // Display helpers
    public string GetTokenIdentifier();    // "ETH", token address, or "address:tokenId"
    public string GetDisplaySymbol();      // "ETH", "USDC", "NFT #42", etc.
    public string GetAddressDisplay();     // Label or truncated address "0x1234...abcd"
}
```

From: `StateChanges/BalanceChange.cs:1-85`

### TokenInfo

```csharp
public class TokenInfo
{
    public string Symbol { get; set; }
    public int Decimals { get; set; }

    public TokenInfo();
    public TokenInfo(string symbol, int decimals);
}
```

Used as the return type for token resolver functions passed to the extractor.

From: `StateChanges/TokenInfo.cs:1-18`

### State Changes Extraction Example

```csharp
// After decoding a transaction execution result
var decoder = new ProgramResultDecoder(abiStorage);
var decoded = decoder.Decode(execResult, callInput, chainId);

// Extract state changes
var extractor = new StateChangesExtractor();
var stateChanges = extractor.ExtractFromDecodedResult(
    decoded,
    executionStateService,
    currentUserAddress: "0xYourAddress");

// Print summary
Console.WriteLine(stateChanges.ToSummaryString());

// Inspect individual balance changes
foreach (var change in stateChanges.BalanceChanges)
{
    var sign = change.Change >= 0 ? "+" : "";
    Console.WriteLine(
        $"{change.GetAddressDisplay()}: {sign}{change.Change} {change.GetDisplaySymbol()}" +
        (change.IsCurrentUser ? " (you)" : ""));
}

// Async version with token metadata resolution
var stateChangesWithMetadata = await extractor.ExtractFromDecodedResultAsync(
    decoded,
    executionStateService,
    currentUserAddress: "0xYourAddress",
    tokenResolverAsync: async (address) =>
    {
        var info = await tokenService.GetTokenAsync(chainId, address);
        return info != null ? new TokenInfo(info.Symbol, info.Decimals) : null;
    });
```

## EVM Debugging

The debugging subsystem provides a step-through debugger for EVM execution traces with Solidity source mapping support.

### EVMDebuggerSession

```csharp
public class EVMDebuggerSession
{
    public EVMDebuggerSession(IABIInfoStorage abiStorage);

    // Loading
    public void LoadFromProgram(Program executedProgram, BigInteger chainId);
    public async Task LoadFromProgramAsync(Program executedProgram, BigInteger chainId);
    public void LoadFromTrace(List<ProgramTrace> trace, BigInteger chainId);
    public async Task LoadFromTraceAsync(List<ProgramTrace> trace, BigInteger chainId);
    public void SetContractDebugInfo(string address, ABIInfo abiInfo);

    // Navigation
    public void StepForward();
    public void StepBack();
    public void GoToStep(int step);
    public void GoToStart();
    public void GoToEnd();

    // State properties
    public List<ProgramTrace> Trace { get; }
    public int CurrentStep { get; }
    public bool CanStepForward { get; }
    public bool CanStepBack { get; }
    public int TotalSteps { get; }

    // Current step inspection
    public ProgramTrace CurrentTrace { get; }
    public ProgramInstruction CurrentInstruction { get; }
    public List<string> CurrentStack { get; }
    public string CurrentMemory { get; }
    public Dictionary<string, string> CurrentStorage { get; }
    public int CurrentDepth { get; }
    public BigInteger CurrentGasCost { get; }
    public string CurrentCodeAddress { get; }
    public string CurrentProgramAddress { get; }

    // Source mapping
    public SourceLocation GetCurrentSourceLocation();
    public SourceLocation GetSourceLocationForStep(int stepIndex);
    public SourceLocation GetNearestSourceLocation(int stepIndex, int maxLookahead = 20);
    public SourceLocation GetFunctionDeclarationLocation(string functionName, string codeAddress);
    public List<int> FindStepsForSourceLine(string filePath, int lineNumber);

    // Function/contract resolution
    public string GetFunctionNameForStep(int stepIndex);
    public string GetCurrentContractName();
    public string GetContractNameForAddress(string address);
    public ABIInfo GetABIInfoForAddress(string address);

    // Call decoding
    public CallStepInfo GetCallInfoForStep(int stepIndex);

    // Source file access
    public IEnumerable<string> GetSourceFiles();
    public Dictionary<string, string> GetAllSourceFileContents();

    // Display
    public string ToDebugString();       // Full debug view (step, address, source, stack, storage)
    public string ToSummaryString();     // One-line summary: "[step/total] OPCODE | file:line"
}
```

From: `Debugging/EVMDebuggerSession.cs:14-802`

### CallStepInfo

Returned by `GetCallInfoForStep` when the current instruction is a CALL/STATICCALL/DELEGATECALL/CALLCODE opcode:

```csharp
public class CallStepInfo
{
    public string TargetAddress { get; set; }
    public string ContractName { get; set; }
    public string CallType { get; set; }          // "CALL", "STATICCALL", etc.
    public string Selector { get; set; }           // "0x70a08231" etc.
    public string FunctionName { get; set; }
    public string FunctionSignature { get; set; }
    public List<ParameterOutput> DecodedInputs { get; set; }
    public string RawCalldata { get; set; }
}
```

From: `Debugging/EVMDebuggerSession.cs:804-814`

### EVMDebuggerExtensions

Extension methods for creating and navigating debug sessions:

```csharp
public static class EVMDebuggerExtensions
{
    // Create from Program
    public static EVMDebuggerSession CreateDebugSession(
        this Program program, IABIInfoStorage abiStorage, long chainId);
    public static async Task<EVMDebuggerSession> CreateDebugSessionAsync(
        this Program program, IABIInfoStorage abiStorage, long chainId);

    // Create from trace list
    public static EVMDebuggerSession CreateDebugSession(
        this List<ProgramTrace> trace, IABIInfoStorage abiStorage, long chainId);
    public static async Task<EVMDebuggerSession> CreateDebugSessionAsync(
        this List<ProgramTrace> trace, IABIInfoStorage abiStorage, long chainId);

    // Trace generation
    public static string GenerateFullTraceString(this EVMDebuggerSession session);
    public static string GenerateSourceAnnotatedTrace(this EVMDebuggerSession session);

    // Source navigation
    public static List<SourceLocation> GetUniqueSourceLocations(this EVMDebuggerSession session);
    public static bool HasDebugInfo(this EVMDebuggerSession session);
    public static IEnumerable<DebugStepInfo> EnumerateWithSource(this EVMDebuggerSession session);
    public static void StepToNextSourceLine(this EVMDebuggerSession session);
    public static void StepToPreviousSourceLine(this EVMDebuggerSession session);
}
```

`DebugStepInfo` combines step index, `ProgramTrace`, and resolved `SourceLocation` for enumeration.

From: `Debugging/EVMDebuggerExtensions.cs:10-240`

### SourceLocation

```csharp
public class SourceLocation
{
    public string FilePath { get; set; }
    public int Position { get; set; }           // Byte offset in source
    public int Length { get; set; }             // Length of source range
    public string SourceCode { get; set; }      // The source snippet
    public string FullFileContent { get; set; }
    public int LineNumber { get; set; }
    public int ColumnNumber { get; set; }
    public int SourceFileIndex { get; set; }
    public string JumpType { get; set; }
    public int ModifierDepth { get; set; }

    public static SourceLocation FromSourceMap(SourceMap sourceMap, string filePath, string fileContent);

    public string GetContextLines(int linesBefore = 2, int linesAfter = 2);
    // Returns surrounding source lines with ">>> " marker on the current line

    public override string ToString();  // "FilePath:LineNumber:ColumnNumber"
}
```

From: `SourceInfo/SourceLocation.cs:1-99`

### Debugging Example

```csharp
// Create a debug session from a Program after EVM execution
var session = program.CreateDebugSession(abiStorage, chainId);

// Or from a TransactionExecutionResult's traces
// var session = execResult.Traces.CreateDebugSession(abiStorage, chainId);

// Step through the execution
while (session.CanStepForward)
{
    var source = session.GetCurrentSourceLocation();
    if (source != null)
    {
        Console.WriteLine($"Step {session.CurrentStep}: {source}");
        Console.WriteLine(source.GetContextLines());
    }

    // Inspect call opcodes
    var callInfo = session.GetCallInfoForStep(session.CurrentStep);
    if (callInfo != null)
    {
        Console.WriteLine($"  CALL -> {callInfo.ContractName ?? callInfo.TargetAddress}");
        Console.WriteLine($"  Function: {callInfo.FunctionName ?? callInfo.Selector}");
    }

    session.StepForward();
}

// Or step by source line (skips opcodes that map to the same line)
session.GoToStart();
while (session.CanStepForward)
{
    session.StepToNextSourceLine();
    Console.WriteLine(session.ToSummaryString());
}

// Generate a full annotated trace
Console.WriteLine(session.GenerateSourceAnnotatedTrace());

// Full debug view at current step
Console.WriteLine(session.ToDebugString());
```

## Gas Calculation Utilities

Low-level gas calculation functions used by `TransactionExecutor` and available for standalone use.

### IntrinsicGasCalculator

```csharp
public static class IntrinsicGasCalculator
{
    // Constants
    public const int G_TRANSACTION = 21000;
    public const int G_TXDATAZERO = 4;
    public const int G_TXDATANONZERO = 16;
    public const int G_TXCREATE = 32000;
    public const int G_CODEDEPOSIT = 200;
    public const int G_ACCESS_LIST_ADDRESS = 2400;
    public const int G_ACCESS_LIST_STORAGE = 1900;
    public const int G_INITCODE_WORD = 2;
    public const int G_FLOOR_PER_TOKEN = 10;
    public const int G_TOKENS_PER_NONZERO = 4;
    public const int GAS_PER_BLOB = 131072;
    public const int MIN_BASE_FEE_PER_BLOB_GAS = 1;
    public const int BLOB_BASE_FEE_UPDATE_FRACTION = 3338477;

    // Intrinsic gas: base 21000 + calldata costs + access list + create + initcode words
    public static BigInteger CalculateIntrinsicGas(
        byte[] data, bool isContractCreation, IList<AccessListEntry> accessList);

    // EIP-7623: Floor gas limit based on calldata token count
    public static BigInteger CalculateFloorGasLimit(byte[] data, bool isContractCreation);

    // Calldata token count: zero_bytes + (nonzero_bytes * 4)
    public static BigInteger CalculateTokensInCalldata(byte[] data);

    // EIP-4844 blob gas
    public static BigInteger CalculateBlobBaseFee(BigInteger excessBlobGas);
    public static BigInteger CalculateBlobGasCost(int blobCount, BigInteger blobBaseFee);

    // Contract creation helpers
    public static BigInteger CalculateCodeDepositGas(int codeLength);  // codeLength * 200
    public static BigInteger CalculateMaxRefund(BigInteger gasUsed);   // gasUsed / 5
}
```

From: `Gas/IntrinsicGasCalculator.cs:1-129`

### GasConstants

Comprehensive gas cost constants organized by EIP:

| Group | Constants |
|---|---|
| **EIP-2929 (Berlin)** | `COLD_SLOAD_COST` (2100), `COLD_ACCOUNT_ACCESS_COST` (2600), `WARM_STORAGE_READ_COST` (100) |
| **EIP-2200/2929 SSTORE** | `SSTORE_SET` (20000), `SSTORE_RESET` (2900), `SSTORE_NOOP` (100), `SSTORE_SENTRY` (2300) |
| **EIP-3529 Refunds** | `SSTORE_CLEARS_SCHEDULE` (4800), `REFUND_QUOTIENT` (5), `SSTORE_SET_REFUND` (19900), `SSTORE_RESET_REFUND` (2800) |
| **Base Opcode Costs** | `G_ZERO` (0), `G_JUMPDEST` (1), `G_BASE` (2), `G_VERYLOW` (3), `G_LOW` (5), `G_MID` (8), `G_HIGH` (10), `G_BLOCKHASH` (20) |
| **EXP** | `EXP_BASE` (10), `EXP_BYTE` (50) |
| **Memory** | `COPY_BASE` (3), `COPY_PER_WORD` (3), `MEMORY_BASE` (3), `QUAD_COEFF_DIV` (512) |
| **KECCAK256** | `KECCAK256_BASE` (30), `KECCAK256_PER_WORD` (6) |
| **LOG** | `LOG_BASE` (375), `LOG_PER_TOPIC` (375), `LOG_PER_BYTE` (8) |
| **CREATE** | `CREATE_BASE` (32000), `CREATE2_HASH_PER_WORD` (6), `CREATE_DATA_GAS` (200), `INIT_CODE_WORD_GAS` (2) |
| **CALL** | `G_CALL` (700), `CALL_VALUE_TRANSFER` (9000), `CALL_NEW_ACCOUNT` (25000), `CALL_STIPEND` (2300), `GAS_DIVISOR` (64) |
| **Limits** | `MAX_CALL_DEPTH` (1024), `MAX_CODE_SIZE` (24576), `MAX_INITCODE_SIZE` (49152) |
| **Transient (EIP-1153)** | `TLOAD_COST` (100), `TSTORE_COST` (100) |
| **EIP-7623 Floor** | `TX_FLOOR_PER_TOKEN` (10), `TX_TOKENS_PER_NON_ZERO_BYTE` (4) |
| **Precompiles** | `ECRECOVER_GAS` (3000), `SHA256_BASE_GAS` (60), `KZG_POINT_EVALUATION_GAS` (50000), BLS12-381 costs |

From: `Gas/GasConstants.cs:1-121`

### Gas Calculation Example

```csharp
// Calculate intrinsic gas for a transaction
var calldata = "0xa9059cbb000000000000000000000000...".HexToByteArray();
var intrinsicGas = IntrinsicGasCalculator.CalculateIntrinsicGas(
    calldata,
    isContractCreation: false,
    accessList: null);
// Result: 21000 + (4 * zeroBytes) + (16 * nonZeroBytes)

// EIP-7623 floor gas check
var floorGas = IntrinsicGasCalculator.CalculateFloorGasLimit(calldata, isContractCreation: false);
var minGasRequired = BigInteger.Max(intrinsicGas, floorGas);

// Blob gas calculation (EIP-4844)
var blobBaseFee = IntrinsicGasCalculator.CalculateBlobBaseFee(excessBlobGas);
var blobGasCost = IntrinsicGasCalculator.CalculateBlobGasCost(blobCount: 3, blobBaseFee);
```

## Precompile System

The precompile system provides pluggable support for EVM precompiled contracts, allowing custom precompiles to be added alongside built-in ones.

### IPrecompileProvider

```csharp
public interface IPrecompileProvider
{
    IEnumerable<string> GetHandledAddresses();
    bool CanHandle(string address);
    BigInteger GetGasCost(string address, byte[] data);
    byte[] Execute(string address, byte[] data);
}
```

From: `Execution/IPrecompileProvider.cs:6-12`

### BuiltInPrecompileProvider

Wraps the built-in precompile execution engine for a range of precompile addresses:

```csharp
public class BuiltInPrecompileProvider : IPrecompileProvider
{
    public static BuiltInPrecompileProvider Cancun();   // Addresses 0x01-0x0a (1-10)
    public static BuiltInPrecompileProvider Prague();    // Addresses 0x01-0x11 (1-17, includes BLS)

    public BuiltInPrecompileProvider(int start, int end);
}
```

Cancun includes: ecRecover, SHA-256, RIPEMD-160, identity, modexp, ecAdd, ecMul, ecPairing, blake2f, KZG point evaluation.

Prague adds: BLS12-381 operations (G1ADD, G1MSM, G2ADD, G2MSM, pairing, MAP_FP_TO_G1, MAP_FP2_TO_G2).

From: `Execution/BuiltInPrecompileProvider.cs:7-38`

### CompositePrecompileProvider

Composes multiple providers, delegating to the first provider that can handle a given address:

```csharp
public class CompositePrecompileProvider : IPrecompileProvider
{
    public CompositePrecompileProvider(params IPrecompileProvider[] providers);
}
```

From: `Execution/CompositePrecompileProvider.cs:7-37`

### HardforkConfigExtensions

```csharp
public static class HardforkConfigExtensions
{
    public static HardforkConfig WithPrecompileProviders(
        this HardforkConfig config,
        params IPrecompileProvider[] additionalProviders);
}
```

Creates a new `HardforkConfig` with a `CompositePrecompileProvider` that combines the additional providers with the config's existing provider. Additional providers take priority.

From: `HardforkConfigExtensions.cs:1-28`

### Custom Precompile Example

```csharp
// Implement a custom precompile
public class MyCustomPrecompile : IPrecompileProvider
{
    private const string ADDRESS = "0x0000000000000000000000000000000000000100";

    public IEnumerable<string> GetHandledAddresses() => new[] { ADDRESS };
    public bool CanHandle(string address) => address.Equals(ADDRESS, StringComparison.OrdinalIgnoreCase);
    public BigInteger GetGasCost(string address, byte[] data) => 1000;
    public byte[] Execute(string address, byte[] data)
    {
        // Custom precompile logic
        return data;
    }
}

// Use with TransactionExecutor
var config = HardforkConfig.Prague.WithPrecompileProviders(new MyCustomPrecompile());
var executor = new TransactionExecutor(config);

// Or pass directly to the constructor
var executor2 = new TransactionExecutor(
    config: HardforkConfig.Prague,
    customPrecompileProvider: new MyCustomPrecompile());
```

## Dependencies

Core dependencies:
- **Nethereum.ABI** - ABI encoding/decoding (error messages)
- **Nethereum.Hex** - Hex conversions
- **Nethereum.RPC** - Ethereum RPC (optional, for RpcNodeDataService)
- **Nethereum.Util** - Utility functions

## Source Files Reference

**Core Execution:**
- `EVMSimulator.cs` - Main execution engine
- `Program.cs` - Program state (stack, memory, instructions)
- `ProgramContext.cs` - Execution context
- `ProgramResult.cs` - Execution results
- `ProgramTrace.cs` - Execution traces
- `Instruction.cs` - Opcode definitions

**Transaction Pipeline:**
- `TransactionExecutor.cs` - Full transaction execution pipeline
- `TransactionExecutionContext.cs` - Transaction input parameters and block context
- `TransactionExecutionResult.cs` - Execution results with traces and inner calls
- `HardforkConfig.cs` - Hardfork presets (Cancun, Prague)
- `HardforkConfigExtensions.cs` - Extension methods for adding precompile providers

**Decoding:**
- `Decoding/ProgramResultDecoder.cs` - ABI-aware result decoder
- `Decoding/DecodedProgramResult.cs` - Decoded execution result container
- `Decoding/DecodedCall.cs` - Decoded function call with parameters
- `Decoding/DecodedLog.cs` - Decoded event log with parameters
- `Decoding/DecodedError.cs` - Decoded revert error with parameters

**State Changes:**
- `StateChanges/StateChangesExtractor.cs` - Balance change extraction from decoded results
- `StateChanges/StateChangesResult.cs` - State changes result container
- `StateChanges/BalanceChange.cs` - Individual balance change with validation
- `StateChanges/TokenInfo.cs` - Token metadata for resolver functions
- `StateChanges/IStateChangesExtractor.cs` - Extractor interface

**Debugging:**
- `Debugging/EVMDebuggerSession.cs` - Step-through debugger with source mapping
- `Debugging/EVMDebuggerExtensions.cs` - Extension methods for creating debug sessions

**Gas Calculation:**
- `Gas/OpcodeGasTable.cs` - Comprehensive gas costs
- `Gas/IntrinsicGasCalculator.cs` - Transaction intrinsic gas and EIP-7623 floor
- `Gas/GasConstants.cs` - Gas cost constants organized by EIP

**Precompiles:**
- `Execution/IPrecompileProvider.cs` - Precompile provider interface
- `Execution/BuiltInPrecompileProvider.cs` - Built-in precompile provider (Cancun/Prague)
- `Execution/CompositePrecompileProvider.cs` - Multi-provider composition

**Blockchain State:**
- `BlockchainState/ExecutionStateService.cs` - State management
- `BlockchainState/INodeDataService.cs` - State provider interface
- `BlockchainState/RpcNodeDataService.cs` - RPC-based state provider
- `BlockchainState/AccountExecutionState.cs` - Account state tracking
- `BlockchainState/AccountExecutionBalance.cs` - Balance tracking

**Bytecode Utilities:**
- `ProgramInstruction.cs` - Instruction representation
- `ProgramInstructionsUtils.cs` - Parsing and disassembly

**Source Mapping:**
- `SourceInfo/SourceMap.cs` - Solidity source mapping
- `SourceInfo/SourceMapUtil.cs` - Source map utilities
- `SourceInfo/SourceLocation.cs` - Resolved source location with context

## Testing

Example test pattern from `EvmSimulatorTests.cs`:

```csharp
[Fact]
public async Task TestEVMOperation()
{
    var bytecode = "..."; // Hex bytecode
    var expected = "..."; // Expected stack top result

    var vm = new EVMSimulator();
    var program = new Program(bytecode.HexToByteArray());

    // Execute N steps
    for (var i = 0; i < numberOfSteps; i++)
    {
        await vm.StepAsync(program, i);
    }

    Assert.Equal(expected.ToUpper(), program.StackPeek().ToHex().ToUpper());
}
```

## License

Nethereum is licensed under the MIT License.

## Related Packages

- **Nethereum.EVM.Contracts** - Contract-level EVM simulation utilities
- **Nethereum.ABI** - ABI encoding/decoding
- **Nethereum.Contracts** - High-level contract interaction
- **Nethereum.Web3** - Ethereum client library
- **Nethereum.Wallet** - Uses `StateChangesPreviewService` for transaction preview
- **Nethereum.Blazor.Solidity** - Blazor debugger UI components
- **Nethereum.EVM.Precompiles.Bls** - BLS12-381 precompile (EIP-2537)
- **Nethereum.EVM.Precompiles.Kzg** - KZG Point Evaluation precompile (EIP-4844)

## Support

- GitHub: https://github.com/Nethereum/Nethereum
- Documentation: https://docs.nethereum.com
- Discord: https://discord.gg/jQPrR58FxX
