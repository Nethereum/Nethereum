# Nethereum.EVM

Run Ethereum bytecode in your own process. Point it at a node and it replays a transaction against real chain state; point it at a dictionary and it runs a contract with no node at all. Either way you get the gas, the logs, the storage writes, the revert reason and a step-by-step trace — without a tracing node, and without broadcasting anything.

## What you can do with it

- **Preview a transaction before a user signs it** — will it succeed, what will it cost, and who ends up with what.
- **Explain a transaction that already happened** — replay it against archive state and read the decoded call tree.
- **Get a `debug_traceTransaction`-style trace from any node** — the trace is produced locally, so the node only has to answer ordinary state reads: `eth_getBalance`, `eth_getCode`, `eth_getStorageAt`, `eth_getTransactionCount` and `eth_getBlockByNumber`, plus `eth_getProof` and `debug_storageRangeAt` on the two paths listed under `RpcNodeDataService` below.
- **See who gained and lost what** — ETH, ERC-20, ERC-721 and ERC-1155 movements, cross-checked against the balances the execution actually observed.
- **Decode a result into calls, logs and custom errors**, including the inner call tree and the revert reason.
- **Step through Solidity source** — breakpoints by file and line, stack, memory and storage at every step.
- **Disassemble deployed bytecode** and ask which function selectors a contract implements.
- **Replay a whole block** and check the gas, receipts and post-state root.
- **Execute under any fork's rules**, from Frontier to Amsterdam, chosen per block rather than per process.

## Quick start

Simulate a transaction and read the result.

*From `EvmSimulatorDocExampleTests.QuickStart_SimulateATransactionAndReadTheResult` (use case `quick-start`) — a tagged, passing test.*

```csharp
IStateReader state = new InMemoryStateReader(new Dictionary<string, AccountState>
{
    [SenderAddress] = new AccountState { Balance = EvmUInt256.Parse("1000000000000000000") },
    [ContractAddress] = new AccountState { Code = "600760005500".HexToByteArray() }
});

var result = await new TransactionExecutor(DefaultHardforkConfigs.Cancun).ExecuteAsync(
    new TransactionExecutionContext
    {
        Sender = SenderAddress,
        To = ContractAddress,
        Data = new byte[0],
        GasLimit = 1_000_000,
        GasPrice = 1,
        ChainId = 1,
        BlockNumber = 19_426_587,
        Timestamp = MainnetChainActivations.CancunTimestamp,
        Coinbase = SenderAddress,
        ExecutionState = new ExecutionStateService(state)
    });

Assert.True(result.Success, result.Error);
Assert.True(result.GasUsed > 21_000);
Assert.Empty(result.Logs);
```

`state` is the only line that changes when you move to a real chain — swap the in-memory reader for the RPC-backed one and the same simulation runs against live mainnet state. That reader needs a node, so its constructor is shown as a declaration rather than a runnable snippet; it is the tagged `RpcNodeDataService` (`Nethereum.EVM.BlockchainState`), whose full surface is below.

```csharp
public RpcNodeDataService(IEthApiService ethApiService, BlockParameter currentBlock)
```

## Entry points

**Start with `TransactionExecutor`.** It is what you want for “what would this transaction do?” — it handles intrinsic gas, validation, EIP-7702 setup, execution and refunds. Reach past it only when you want a single frame (`EVMSimulator`) or a whole block (`BlockExecutor`).

| I want to… | Reach for |
|---|---|
| **Simulate one transaction** | **`new TransactionExecutor(config).ExecuteAsync(ctx)`** |
| Run raw bytecode, one frame | `new EVMSimulator(config).ExecuteWithCallStackAsync(program)` |
| Execute a whole block | `BlockExecutor.ExecuteAsync(block, encodingProvider, registry, …)` |
| Read state from a live node | `new RpcNodeDataService(web3.Eth, blockParameter)` |
| Get the rules for a fork | `DefaultHardforkConfigs.Cancun`, or `DefaultMainnetHardforkRegistry.Instance.Get(fork)` |
| Turn a result into calls, logs and errors | `new ProgramResultDecoder(abiStorage).Decode(…)` |
| See who gained and lost what | `new StateChangesExtractor().ExtractFromDecodedResultAsync(decoded, …)` |
| Step through Solidity source | `program.CreateDebugSession(abiStorage, chainId)` |
| Disassemble deployed bytecode | `ProgramInstructionsUtils.GetProgramInstructions(byteCode)` |

`HardforkConfig` is the bundle of rules for one fork — opcode handlers, gas rules, precompiles. Every executor takes one. `DefaultHardforkConfigs` exposes presets from `Frontier` through `Osaka`, and `DefaultHardforkConfigs.Default` is `Osaka` — not the newest fork the engine knows. Amsterdam, the last entry in `HardforkName` and in `HardforkSpecRegistry.All`, has no `DefaultHardforkConfigs` preset: reach it through `DefaultMainnetHardforkRegistry.Instance.Get(HardforkName.Amsterdam)`.

## Installation

```bash
dotnet add package Nethereum.EVM
```

BLS12-381 precompiles (0x0b–0x11) and KZG point evaluation (0x0a) are not in the box — add `Nethereum.EVM.Precompiles.Bls` or `Nethereum.EVM.Precompiles.Kzg` and call `WithBlsBackend` / `WithKzgBackend` if you need them. `Nethereum.CoreChain` adds the Patricia state- and block-root calculators.

## Running bytecode

Every runnable snippet below is extracted from a `[NethereumDocExample(DocSection.EvmSimulator, …)]`-tagged **passing** test in `tests/Nethereum.EVM.UnitTests`.

### Execute raw bytecode

*From `EvmSimulatorDocExampleTests.RunRawBytecodeAndReadTheStack` (use case `run-bytecode`).*

```csharp
var simulator = new EVMSimulator(DefaultHardforkConfigs.Cancun);
var program = new Program("6002600301".HexToByteArray());

await simulator.ExecuteWithCallStackAsync(program, traceEnabled: false);

Assert.Equal(
    "0000000000000000000000000000000000000000000000000000000000000005",
    program.StackPeek().ToHex());
```

`Program` holds the bytecode, the stack, the memory, the gas counters and the accumulated trace. `EVMSimulator` drives it. The bytecode above is `PUSH1 2; PUSH1 3; ADD`.

`DefaultHardforkConfigs` (from the source-linked `Nethereum.EVM.Precompiles`) exposes a ready `HardforkConfig` per fork — `Frontier` … `Osaka`, plus `Default` (currently Osaka) — each already carrying its precompile registry.

### Trace every step

Tracing needs a `ProgramContext`: the trace records the executing contract and code address, so a bare `new Program(bytecode)` cannot be traced.

*From `EvmSimulatorDocExampleTests.ReadThePerStepTrace` (use case `run-bytecode`).*

```csharp
var context = new ProgramContext(
    new EvmCallContext { From = SenderAddress, To = ContractAddress, Data = new byte[0], Gas = 1_000_000 },
    new ExecutionStateService(new InMemoryStateReader(new Dictionary<string, AccountState>())));

var simulator = new EVMSimulator(DefaultHardforkConfigs.Cancun);
var program = new Program("6002600301".HexToByteArray(), context);

await simulator.ExecuteWithCallStackAsync(program, traceEnabled: true);

var steps = program.Trace;
Assert.Equal(Instruction.PUSH1, steps[0].Instruction.Instruction);
Assert.Equal(3, steps[0].GasCost);
Assert.Equal(Instruction.ADD, steps[2].Instruction.Instruction);
Assert.Equal(9, program.TotalGasUsed);
```

Each `ProgramTrace` carries `ProgramAddress`, `CodeAddress`, `VMTraceStep`, `ProgramTraceStep`, `Depth`, `Instruction`, `Stack`, `Memory` / `MemoryAsArray`, `Storage`, `GasCost`, `GasRemaining` and `OutOfGas`.

### Disassemble

*From `EvmSimulatorDocExampleTests.DisassembleDeployedBytecode` (use case `disassemble-bytecode`).*

```csharp
var instructions = ProgramInstructionsUtils.GetProgramInstructions("60806040523415");

Assert.Equal("0000   60   PUSH1  0x80", instructions[0].ToDisassemblyLine());
Assert.Equal(Instruction.MSTORE, instructions[2].Instruction);
Assert.Equal(Instruction.CALLVALUE, instructions[3].Instruction);

var listing = ProgramInstructionsUtils.DisassembleToString(instructions);
Assert.Contains("MSTORE", listing);
```

`ProgramInstructionsUtils` also offers `GetProgramInstructions(byte[] byteCodeArray)`, the `bool eip8024Enabled` overloads, `DisassembleToString(string byteCode)`, `DisassembleSimplifiedToString(string byteCode)`, and the dispatcher helpers `GetFunctionDispatcherMap(...)`, `ContainsFunctionSignature(List<ProgramInstruction> instructions, string signature)` and `ContainsFunctionSignatures(List<ProgramInstruction> instructions, string[] signatures)` — enough to answer "does this deployed contract implement `transfer(address,uint256)`?" from bytecode alone.

## Fork rules and precompiles

### Resolve the fork, then look up its rules

*From `EvmSimulatorDocExampleTests.ResolveTheForkForABlockAndLookUpItsRules` (use case `hardfork-config`).*

```csharp
var fork = MainnetChainActivations.Instance.ResolveAt(
    blockNumber: 19_426_587, timestamp: MainnetChainActivations.CancunTimestamp);

Assert.Equal(HardforkName.Cancun, fork);

var config = DefaultMainnetHardforkRegistry.Instance.Get(fork);

Assert.Equal(GasConstants.MAX_CODE_SIZE, config.MaxCodeSize);
Assert.True(config.BaseFeeApplies);
```

`DefaultMainnetHardforkRegistry.Instance` is `MainnetHardforkRegistry.Build(DefaultPrecompileBackends.Instance)` — every mainnet fork, wired with the default crypto. `HardforkRegistry.Get` throws for `HardforkName.Unspecified` and for an unregistered fork; it never falls back to a default.

For a non-mainnet chain, register its activation table first — an unknown chain id is refused rather than silently replayed under mainnet rules:

*From `EvmSimulatorDocExampleTests.AnUnregisteredChainIdIsRefused` (use case `hardfork-config`).*

```csharp
var registry = new ChainActivationsRegistry();

Assert.Equal(
    HardforkName.Cancun,
    registry.ResolveAt(chainId: 1, blockNumber: 19_426_587, timestamp: MainnetChainActivations.CancunTimestamp));

Assert.Throws<System.InvalidOperationException>(
    () => registry.ResolveAt(chainId: 424242, blockNumber: 1, timestamp: 1));
```

### The precompile registry

A `HardforkConfig` **static preset** carries no precompiles — `HardforkConfig.Cancun.Precompiles` is `null`. `WithPrecompiles` clones the config and attaches a registry, so the shared preset is never mutated.

*From `EvmSimulatorDocExampleTests.AttachAPrecompileRegistryToAHardforkConfig` (use case `precompiles`).*

```csharp
Assert.Null(HardforkConfig.Cancun.Precompiles);

var config = HardforkConfig.Cancun.WithPrecompiles(DefaultPrecompileRegistries.CancunBase());

Assert.True(config.Precompiles.CanHandle(0x01));
Assert.True(config.Precompiles.CanHandle(0x09));
Assert.False(config.Precompiles.CanHandle(0x0b));
```

`DefaultHardforkConfigs.Cancun` is the same thing done for you.

Gas is a separate concern from execution, so you can price a precompile call without running it:

*From `EvmSimulatorDocExampleTests.QueryPrecompileGasAndExecuteAHandler` (use case `precompiles`).*

```csharp
var registry = DefaultPrecompileRegistries.OsakaBase();

Assert.Equal(6900L, registry.GetGasCost(0x100, new byte[160]));

var identity = registry.Get(0x04);
Assert.Equal(0x04, identity.AddressNumeric);
Assert.Equal(new byte[] { 0x01, 0x02, 0x03 }, registry.Execute(0x04, new byte[] { 0x01, 0x02, 0x03 }));
```

| Registry factory (`DefaultPrecompileRegistries`) | |
|---|---|
| `FrontierBase()`, `ByzantiumBase()`, `IstanbulBase()`, `BerlinBase()`, `CancunBase()`, `PragueBase()`, `OsakaBase()` | fork-shaped handler sets and gas schedules |
| `.WithBlsBackend(IBls12381Operations)` (`Nethereum.EVM.Precompiles.Bls`) | installs 0x0b–0x11 |
| `.WithKzgBackend()` (`Nethereum.EVM.Precompiles.Kzg`) | installs 0x0a |

The same two extensions exist on `HardforkConfig`, so `HardforkConfig.Prague.WithPrecompiles(DefaultPrecompileRegistries.PragueBase()).WithBlsBackend(ops)` composes in one expression.

## Simulating a transaction

`TransactionExecutor` runs one transaction end to end — intrinsic gas, validation rules, EIP-7702 setup, execution, refunds, receipt construction — against any `IStateReader`.

*From `EvmSimulatorDocExampleTests.SimulateATransactionAgainstInMemoryState` (use case `simulate-transaction`).*

```csharp
var accounts = new Dictionary<string, AccountState>
{
    [SenderAddress] = new AccountState { Balance = EvmUInt256.Parse("1000000000000000000") },
    [ContractAddress] = new AccountState { Code = "600760005500".HexToByteArray() }
};
var stateReader = new InMemoryStateReader(accounts);

var ctx = new TransactionExecutionContext
{
    Sender = SenderAddress,
    To = ContractAddress,
    Data = new byte[0],
    GasLimit = 1_000_000,
    GasPrice = 1,
    Nonce = 0,
    BlockNumber = 19_426_587,
    Timestamp = MainnetChainActivations.CancunTimestamp,
    Coinbase = SenderAddress,
    ChainId = 1,
    ExecutionState = new ExecutionStateService(stateReader)
};

var executor = new TransactionExecutor(DefaultMainnetHardforkRegistry.Instance.Get(HardforkName.Cancun));
var result = await executor.ExecuteAsync(ctx);

Assert.True(result.Success, result.Error);
Assert.Equal(TransactionError.None, result.ErrorCode);
Assert.True(result.GasUsed > 21_000);
```

### Failure is reported in two different places

`TransactionExecutionResult.ErrorCode` is a `TransactionError`, and it reports **pre-execution validation** failures only:

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
    InsufficientMaxFeePerBlobGas,
    GasLimitExceedsMaximum,
    Type4TxContractCreation,
    Type4EmptyAuthorizationList,
    TransactionTypeNotSupported,
    InvalidChainId,
    NonceMismatch,
}
```

An in-EVM `REVERT` is *not* one of them — and `Reverted` is declared but never assigned, so check `ProgramResult.IsRevert`, not the code:

*From `EvmSimulatorDocExampleTests.AnInEvmRevertFailsTheTransactionWithoutSettingAnErrorCode` (use case `simulate-transaction`).*

```csharp
Assert.False(result.Success);
Assert.True(result.ProgramResult.IsRevert);
Assert.Equal(TransactionError.None, result.ErrorCode);
Assert.False(result.IsValidationError);
```

So: check `Success` first; then `IsValidationError` / `ErrorCode` for a rejected transaction, or `ProgramResult.IsRevert` and `RevertReason` for a reverted one.

`TransactionExecutionResult` also carries `Error` — the failure message the snippets above pass to `Assert.True(result.Success, result.Error)` — along with `GasUsed`, `GasRefund`, `EffectiveGasUsed`, `ExecutionGasUsed`, `StateGasUsed`, `ReturnData`, `Logs`, `StateRoot`, `ContractAddress`, `Traces`, `Program`, `CreatedAccounts`, `DeletedAccounts`, `InnerCalls` and `InnerContractCodeCalls`.

### Simulating against a live chain: `RpcNodeDataService`

`RpcNodeDataService : IStateReader, IAccountStorageReader` is the host-only state reader that answers every read from a node over JSON-RPC.

```csharp
public class RpcNodeDataService : IStateReader, IAccountStorageReader
{
    public RpcNodeDataService(IEthApiService ethApiService, BlockParameter currentBlock);

    public RpcNodeDataService(
        IEthApiService ethApiService,
        BlockParameter currentBlock,
        IDebugApiService debugApiService,
        string blockHash,
        int transactionIndex,
        bool useDebugStorageAt = true);

    public IEthApiService EthApiService { get; }
    public BlockParameter CurrentBlock { get; }
    public IDebugApiService DebugApiService { get; }
    public string BlockHash { get; }
    public int TransactionIndex { get; }
    public bool UseDebugStorageAt { get; }

    public Task<bool> AccountHasStorageAsync(string address);
    public static bool StorageHashIndicatesStorage(string storageHash);
}
```

Each read maps to a JSON-RPC call, and the set is wider than the four core account reads:

| Member | JSON-RPC issued |
|---|---|
| `GetBalanceAsync` | `eth_getBalance` |
| `GetCodeAsync` | `eth_getCode` |
| `GetStorageAtAsync` | `eth_getStorageAt`, preceded by `debug_storageRangeAt` when `UseDebugStorageAt` is set |
| `GetTransactionCountAsync` | `eth_getTransactionCount` |
| `GetBlockHashAsync` | `eth_getBlockByNumber` |
| `AccountExistsAsync` | `eth_getBalance`, then `eth_getTransactionCount`, then `eth_getCode` |
| `AccountHasStorageAsync` | `eth_getProof` |

`eth_getProof` is the one read a node may not serve — Erigon in particular does not. `AccountHasStorageAsync` catches the failure and returns `false`, so the simulation degrades rather than throwing — but on such a node the answer is "no storage", not the truth.

Point it at `web3.Eth`, hand it to an `ExecutionStateService`, and the simulator replays a transaction against real mainnet state without a local node database. The second constructor is the interesting one: it reads state **mid-block**, at a given transaction index, using `debug_storageRangeAt`. That is what makes “what would this transaction have done at position 7 of block N?” answerable.

## Decoding a result

Raw EVM output is bytes. `ProgramResultDecoder` turns a finished `ProgramResult` into a call tree, decoded logs, decoded return values and a decoded revert, using ABIs from an `IABIInfoStorage` (`Nethereum.ABI.ABIRepository`).

| Member | |
|---|---|
| `ProgramResultDecoder(IABIInfoStorage abiStorage)` | constructor |
| `DecodedProgramResult Decode(...)` | three overloads over a program result / call |
| `DecodedCall DecodeCall(CallInput call, BigInteger chainId, int depth)` | one call frame |
| `DecodedLog DecodeLog(FilterLog log, BigInteger chainId)` | one log |
| `DecodedError DecodeRevert(byte[] revertData, BigInteger chainId, string contractAddress)` | revert data → error |
| `List<ParameterOutput> DecodeReturnValue(FunctionABI functionABI, string output)` | return values |

| Result type | Members |
|---|---|
| `DecodedProgramResult` | `RootCall`, `DecodedLogs`, `ReturnValue`, `RevertReason`, `IsRevert`, `IsSuccess`, `OriginalResult`, `OriginalCall`, `ChainId`, `ToHumanReadableString()` |
| `DecodedCall` | `From`, `To`, `ContractName`, `Function`, `InputParameters`, `OutputParameters`, `InnerCalls`, `Logs`, `CallType`, `Depth`, `IsDecoded`, `RawInput`, `RawOutput`, `Value`, `GasUsed`, `IsRevert`, `Error`, `OriginalCall`, `GetFunctionSignature()`, `GetFunctionName()`, `GetDisplayName()` |
| `DecodedLog` | `ContractAddress`, `ContractName`, `Event`, `Parameters`, `IsDecoded`, `OriginalLog`, `LogIndex`, `CallDepth`, `GetEventSignature()`, `GetEventName()`, `GetDisplayName()` |
| `DecodedError` | `Error`, `Parameters`, `Message`, `IsStandardError`, `IsDecoded`, `RawData`, `GetErrorSignature()`, `GetErrorName()`, `GetDisplayMessage()`, `FromStandardError(string message, string rawData = null)`, `FromUnknownError(string rawData)` |

Each frame records how it was entered:

```csharp
public enum CallType
{
    Call,
    DelegateCall,
    StaticCall,
    CallCode,
    Create,
    Create2
}
```

`IsDecoded` is `false` when no ABI was found — the frame still shows with its raw calldata, so an unknown contract does not break the tree.

## Extracting state changes

`StateChangesExtractor : IStateChangesExtractor` reads a `DecodedProgramResult` and produces "what actually moved" — the answer a wallet needs before asking a user to sign.

```csharp
StateChangesResult ExtractFromDecodedResult(
    DecodedProgramResult decodedResult,
    ExecutionStateService stateService = null,
    string currentUserAddress = null);

StateChangesResult ExtractFromDecodedResult(
    DecodedProgramResult decodedResult,
    ExecutionStateService stateService,
    string currentUserAddress,
    Func<string, TokenInfo> tokenResolver);

Task<StateChangesResult> ExtractFromDecodedResultAsync(
    DecodedProgramResult decodedResult,
    ExecutionStateService stateService = null,
    string currentUserAddress = null,
    Func<string, Task<TokenInfo>> tokenResolverAsync = null,
    CancellationToken cancellationToken = default);
```

It recognises transfers by event signature — the public constants `TRANSFER_EVENT_SIGNATURE`, `TRANSFER_SINGLE_EVENT_SIGNATURE` and `TRANSFER_BATCH_EVENT_SIGNATURE` cover ERC-20/721 `Transfer`, ERC-1155 `TransferSingle` and `TransferBatch` — and the `tokenResolver` you supply turns a token address into a `TokenInfo { Symbol, Decimals }` for display.

| Type | Members |
|---|---|
| `StateChangesResult` | `BalanceChanges`, `RootCall`, `DecodedLogs`, `DecodedResult`, `Error`, `Traces`, `GasUsed`, `HasError`, `HasBalanceChanges`, `HasDecodedLogs`, `HasTraces`, `ToSummaryString()` |
| `BalanceChange` | `Address`, `AddressLabel`, `IsCurrentUser`, `Type`, `TokenAddress`, `TokenSymbol`, `TokenDecimals`, `TokenId`, `Change`, `BalanceBefore`, `BalanceAfter`, `ActualChange`, `ActualOwner`, `ValidationStatus`, `HasDiscrepancy`, `GetTokenIdentifier()`, `GetDisplaySymbol()`, `GetAddressDisplay()` |

```csharp
public enum BalanceChangeType
{
    Native,
    ERC20,
    ERC721,
    ERC1155
}

public enum BalanceValidationStatus
{
    NotValidated,
    Verified,
    FeeOnTransfer,
    Rebasing,
    OwnerMismatch,
    Mismatch
}

public class TokenInfo
{
    public string Symbol { get; set; }
    public int Decimals { get; set; }

    public TokenInfo();
    public TokenInfo(string symbol, int decimals);
}
```

`ValidateTokenBalances(...)` cross-checks the transfer events against the balances actually observed in the execution state, but it only validates a token type when you pass the matching resolver delegate (`getErc20Balance`, `getErc721Owner`, `getErc1155Balance`); otherwise the change stays `NotValidated`. That is what surfaces `FeeOnTransfer` and `Rebasing`: the token emitted a `Transfer` for X but the recipient's balance moved by something else. `HasDiscrepancy` is the one flag a UI should never hide.

## Source-level debugging

`EVMDebuggerSession` replays a trace with Solidity source mapping — the same experience as a step debugger, over a simulated transaction.

| Area | Members |
|---|---|
| Construction / loading | `EVMDebuggerSession(IABIInfoStorage abiStorage)`; `LoadFromProgram(Program executedProgram, BigInteger chainId)`, `LoadFromProgramAsync`, `LoadFromTrace(List<ProgramTrace> trace, BigInteger chainId)`, `LoadFromTraceAsync` |
| Navigation | `Trace`, `CurrentStep`, `TotalSteps`, `CanStepForward`, `CanStepBack`, `StepForward()`, `StepBack()`, `GoToStep(int step)`, `GoToStart()`, `GoToEnd()` |
| Current state | `CurrentTrace`, `CurrentInstruction`, `CurrentStack`, `CurrentMemory`, `CurrentStorage`, `CurrentDepth`, `CurrentGasCost`, `CurrentCodeAddress`, `CurrentProgramAddress` |
| Source mapping | `GetCurrentSourceLocation()`, `GetSourceLocationForStep(int stepIndex)`, `GetNearestSourceLocation(int stepIndex, int maxLookahead = 20)`, `GetFunctionDeclarationLocation(string functionName, string codeAddress)`, `FindStepsForSourceLine(string filePath, int lineNumber)` |
| Call decoding | `GetFunctionNameForStep(int stepIndex)`, `GetCurrentContractName()`, `GetCallInfoForStep(int stepIndex)` → `CallStepInfo` |
| Debug info | `SetContractDebugInfo(string address, ABIInfo abiInfo)`, `GetABIInfoForAddress(string address)`, `GetContractNameForAddress(string address)`, `GetSourceFiles()`, `GetAllSourceFileContents()` |
| Rendering | `ToDebugString()`, `ToSummaryString()` |

`FindStepsForSourceLine` is the breakpoint primitive: give it a file and a line and it returns every trace step that maps there. `GetCallInfoForStep` answers “which call am I standing in?”:

```csharp
public class CallStepInfo
{
    public string TargetAddress { get; set; }
    public string ContractName { get; set; }
    public string CallType { get; set; }
    public string Selector { get; set; }
    public string FunctionName { get; set; }
    public string FunctionSignature { get; set; }
    public List<ParameterOutput> DecodedInputs { get; set; }
    public string RawCalldata { get; set; }
}
```

`EVMDebuggerExtensions` adds the convenience layer: `program.CreateDebugSession(abiStorage, chainId)` and `trace.CreateDebugSession(abiStorage, chainId)` (both with `…Async` twins), `GenerateFullTraceString()`, `GenerateSourceAnnotatedTrace()`, `GetUniqueSourceLocations()`, `HasDebugInfo()`, `EnumerateWithSource()` → `DebugStepInfo { Step, Trace, Source }`, and `StepToNextSourceLine()` / `StepToPreviousSourceLine()` for stepping by Solidity line instead of by opcode.

A `SourceLocation` carries `FilePath`, `Position`, `Length`, `SourceCode`, `FullFileContent`, `LineNumber`, `ColumnNumber`, `SourceFileIndex`, `JumpType`, `ModifierDepth`, and `GetContextLines(int linesBefore = 2, int linesAfter = 2)` for rendering a snippet around the current line.

Worked debugger scenarios live in `tests/Nethereum.EVM.UnitTests/EVMDebuggerTests.cs`.

## Bridging to the RPC types

`EvmTypeConversions` (`Nethereum.EVM.Compatibility`) converts between the engine's own types and the RPC DTOs — this is the seam that only exists in the host arm:

| Extension | |
|---|---|
| `CallInput.ToEvmCallContext()` / `TransactionInput.ToEvmCallContext()` | RPC → engine |
| `EvmCallContext.ToCallInput()` / `List<EvmCallContext>.ToCallInputs()` | engine → RPC |
| `EvmLog.ToFilterLog()` / `List<EvmLog>.ToFilterLogs()` | engine → RPC |

## Blocks, requests and state gas

Block execution, witnesses, the EIP-7685 execution requests, the EIP-8037 state-gas dimension and the system-call policy live in the shared engine source, so they are in this assembly too. They are explained once, in **[the `Nethereum.EVM.Core` README](../Nethereum.EVM.Core/README.md)** — [hardforks and the registry](../Nethereum.EVM.Core/README.md#hardforks-a-registry-lookup-not-a-static-global), [state reading](../Nethereum.EVM.Core/README.md#state-reading), [execution requests](../Nethereum.EVM.Core/README.md#eip-7685-execution-requests), [system calls](../Nethereum.EVM.Core/README.md#system-calls), [state gas](../Nethereum.EVM.Core/README.md#eip-8037-the-state-gas-dimension) and [witnesses](../Nethereum.EVM.Core/README.md#witnesses-and-block-execution). Use `BlockExecutor.ExecuteAsync(...)` here where that page shows `BlockExecutor.Execute(...)`; the parameters are identical.

The declarations you are most likely to reach for from this package:

```csharp
public enum HardforkName
{
    Unspecified = 0,
    Frontier,
    FrontierThawing,
    Homestead,
    DaoFork,
    TangerineWhistle,
    SpuriousDragon,
    Byzantium,
    Constantinople,
    Petersburg,
    Istanbul,
    MuirGlacier,
    Berlin,
    London,
    ArrowGlacier,
    GrayGlacier,
    Paris,
    Shanghai,
    Cancun,
    Prague,
    Osaka,
    OsakaBpo1,
    OsakaBpo2,
    Amsterdam
}

public enum ExecutionMode
{
    Transaction,
    Call,
    SystemCall
}
```

Supply state by implementing one interface — ten methods and the engine runs:

```csharp
public interface IStateReader
{
    Task<EvmUInt256> GetBalanceAsync(byte[] address);
    Task<EvmUInt256> GetBalanceAsync(string address);
    Task<byte[]> GetCodeAsync(byte[] address);
    Task<byte[]> GetCodeAsync(string address);
    Task<byte[]> GetStorageAtAsync(byte[] address, EvmUInt256 position);
    Task<byte[]> GetStorageAtAsync(string address, EvmUInt256 position);
    Task<EvmUInt256> GetTransactionCountAsync(byte[] address);
    Task<EvmUInt256> GetTransactionCountAsync(string address);
    Task<bool> AccountExistsAsync(string address);
    Task<byte[]> GetBlockHashAsync(long blockNumber);
}
```

From Prague a block also commits to its execution requests, and from Amsterdam gas has a second, state-growth dimension:

```csharp
public static class ExecutionRequests
{
    public const byte DepositRequestType = 0x00;
    public const byte WithdrawalRequestType = 0x01;
    public const byte ConsolidationRequestType = 0x02;
    public const byte BuilderDepositRequestType = 0x03;
    public const byte BuilderExitRequestType = 0x04;

    public static byte RequestTypeFor(string predeployAddress);
    public static byte[] Compose(byte requestType, byte[] requestData);
    public static bool CarriesData(byte[] request);
    public static bool IsActive(HardforkName fork);
    public static byte[] CommitmentFor(HardforkName fork, IEnumerable<byte[]> blockRequests);
    public static byte[] ComputeRequestsHash(IEnumerable<byte[]> blockRequests);
}

public static class SystemCallContracts
{
    public const string BeaconRoots = "0x000f3df6d732807ef1319fb7b8bb8522d0beac02";
    public const string HistoryStorage = "0x0000F90827F1C53a10cb7A02335B175320002935";
    public const string WithdrawalRequests = "0x00000961Ef480Eb55e80D19ad83579A64c007002";
    public const string ConsolidationRequests = "0x0000BBdDc7CE488642fb579F8B00f3a590007251";
    public const string BuilderDeposit = "0x0000BFF46984E3725691FA540A8C7589300D8282";
    public const string BuilderExit = "0x000064D678505AD48F8CCB093BC65613800E8282";

    public const string SystemCaller = Nethereum.Util.AddressUtil.SYSTEM_ADDRESS;

    public static IReadOnlyList<string> RequestContractsFor(HardforkName fork);

    public static readonly IReadOnlyList<string> AllRequestContracts;
}

public sealed class StateGasAccount
{
    public long ReservoirRemaining { get; set; }
    public long FromReservoir { get; set; }
    public long SpilledIntoExecution { get; set; }
}
```

`ExecutionRequests.IsActive(fork)` is `false` before Prague — and a header with no `requests_hash` is a different thing from one carrying the hash of an empty list.

## What lives only in this package

| Namespace | Types |
|---|---|
| `Nethereum.EVM.BlockchainState` | `RpcNodeDataService` |
| `Nethereum.EVM.Decoding` | `ProgramResultDecoder`, `DecodedProgramResult`, `DecodedCall`, `DecodedLog`, `DecodedError`, `CallType` |
| `Nethereum.EVM.StateChanges` | `IStateChangesExtractor`, `StateChangesExtractor`, `StateChangesResult`, `BalanceChange`, `BalanceChangeType`, `BalanceValidationStatus`, `TokenInfo` |
| `Nethereum.EVM.Debugging` | `EVMDebuggerSession`, `EVMDebuggerExtensions`, `DebugStepInfo`, `CallStepInfo` |
| `Nethereum.EVM.Compatibility` | `EvmTypeConversions` |

Everything else you can reach from this assembly is shared source from `Nethereum.EVM.Core` and `Nethereum.EVM.Precompiles`.

## Relationship to `Nethereum.EVM.Core`

`Nethereum.EVM` and [`Nethereum.EVM.Core`](../Nethereum.EVM.Core/README.md) are **one source tree compiled twice**. This package is the asynchronous host build; `Nethereum.EVM.Core` is the same files compiled with `EVM_SYNC` defined, giving a synchronous, `Task`-free, AOT- and trim-safe engine for the Zisk zkVM guest. Practically, that means `EVMSimulator`, `Program`, `TransactionExecutor`, `BlockExecutor`, `HardforkConfig` and the precompile registry are all in **this** assembly — you never need a second package reference to reach them.

What differs is only the shape of the API. This package publishes the async half of each twinned entry point:

```csharp
public async Task<Program> ExecuteWithCallStackAsync(Program program, int vmExecutionCounter = 0, int depth = 0, bool traceEnabled = true)

public async Task<TransactionExecutionResult> ExecuteAsync(TransactionExecutionContext ctx)

public static async Task<BlockExecutionResult> ExecuteAsync(
    BlockWitnessData block,
    IBlockEncodingProvider encodingProvider,
    HardforkRegistry hardforkRegistry,
    IStateRootCalculator stateRootCalculator = null,
    IBlockRootCalculator blockRootCalculator = null)
```

`Nethereum.EVM.Core` publishes `ExecuteWithCallStack`, `Execute` and `Execute` instead, and `ProgramResult.Logs` is `List<FilterLog>` here against `List<EvmLog>` there.

The two builds must stay behaviourally identical, or a zk proof would attest to different rules than the node executed. `tests/Nethereum.EVM.UnitTests/Execution/BlockExecutorArmsAgreeTests.cs` enforces that: it reads `BlockExecutor.cs`, splits it at its preprocessor directives and diffs the two arms statement by statement, permitting exactly one asymmetry — with five further tests proving the diff would catch a stray statement, a duplicated member, or a statement merely *moved* within one arm. **If you edit one arm, edit the other in the same change.**

## Scope

**Good for**: transaction simulation and preview, gas analysis, `debug_traceTransaction`-style tracing without a tracing node, contract-bytecode analysis and disassembly, replaying historical transactions against archive state, source-level debugging, block replay and consensus testing, and — via `Nethereum.EVM.Core` — stateless/zkVM execution.

**Not**: a P2P client. It executes; it does not gossip, mine, or maintain a chain. For a full node see `Nethereum.CoreChain` and `Nethereum.DevP2P`.

Precompile coverage depends on what you wire: `DefaultPrecompileRegistries.FrontierBase` carries 0x01–0x04, every base registry from `ByzantiumBase` on carries real handlers for 0x01–0x09, and `OsakaBase` adds 0x100 (`P256VERIFY`). The KZG and BLS addresses are registered but unwired — 0x0a from `CancunBase`, 0x0b–0x11 from `PragueBase` — as a `PlaceholderPrecompile` that throws `UnwiredPrecompileException` rather than returning a wrong result; supply them with `WithKzgBackend` / `WithBlsBackend` from the companion packages.

## See also

- [`Nethereum.EVM.Core`](../Nethereum.EVM.Core/README.md) — the shared engine and the guest build.
- [`Nethereum.EVM.Precompiles`](../Nethereum.EVM.Precompiles/README.md) — the default crypto backends, source-linked into this package.
- `Nethereum.EVM.Precompiles.Bls` / `Nethereum.EVM.Precompiles.Kzg` — BLS12-381 and KZG.
- `Nethereum.CoreChain` — block production, chain storage, and the Patricia root calculators.
