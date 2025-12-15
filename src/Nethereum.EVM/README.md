# Nethereum.EVM

**Nethereum.EVM** is an experimental Ethereum Virtual Machine (EVM) simulator that executes EVM bytecode instruction-by-instruction with full trace support and gas calculation.

## Overview

This package provides a local EVM implementation that can:
- Execute EVM bytecode step-by-step
- Calculate gas costs for all opcodes including dynamic gas operations
- Track warm/cold storage and address access (EIP-2929)
- Maintain execution traces with stack, memory, and storage snapshots
- Interact with real blockchain state via RPC
- Parse and disassemble bytecode
- Support all EVM opcodes including recent additions (Cancun, Shanghai forks)

**Status**: Experimental - suitable for testing, debugging, and educational purposes.

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
var bytecode = "600460040116".HexToByteArray(); // PUSH1 0x04, PUSH1 0x04, ADD
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
var memData = program.ReadFromMemory(index, size);
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
SELFDESTRUCT = 5000
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
var bytecode = "03020208"; // PUSH1 0x03, PUSH1 0x02, PUSH1 0x02, ADDMOD

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
var bytecode = "600160055B60CC".HexToByteArray();

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

## Limitations

### Experimental Status

This is an **experimental** EVM implementation suitable for:
- Testing and debugging smart contracts locally
- Educational purposes and EVM learning
- Bytecode analysis and disassembly
- Gas estimation and optimization research

**Not recommended for:**
- Production systems
- Mainnet transaction simulation requiring 100% accuracy
- Consensus-critical applications

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

**Gas Calculation:**
- `Gas/OpcodeGasTable.cs` - Comprehensive gas costs

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

## Support

- GitHub: https://github.com/Nethereum/Nethereum
- Documentation: https://docs.nethereum.com
- Discord: https://discord.gg/jQPrR58FxX
