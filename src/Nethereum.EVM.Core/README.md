# Nethereum.EVM.Core

Lean, AOT-/trim-friendly EVM execution engine. Source-shared into
[`Nethereum.EVM`](../Nethereum.EVM/README.md) and the Zisk zkVM guest.

## Overview

`Nethereum.EVM.Core` is the foundational EVM implementation in Nethereum.
It is designed to be:

- **`BigInteger`-free on the hot path** — all 256-bit arithmetic goes
  through `EvmUInt256` / `EvmInt256` from
  [`Nethereum.Util`](../Nethereum.Util/README.md).
- **AOT- / trim-safe** — no `System.Linq`, no dynamic reflection, no
  static state that couples to non-trimmable dependencies.
- **Host- or guest-compilable** — every async API in the core ships a
  synchronous counterpart behind `#if EVM_SYNC`. The Zisk zkVM guest
  (see [`Nethereum.EVM.Zisk`](../Nethereum.EVM.Zisk/README.md)) picks
  the sync build; standard hosts pick the async build.
- **Fork-parameterised by registry lookup, not static globals.**
  Consumers build a `HardforkRegistry` once at startup and the executor
  is a dumb `registry.Get(block.Features.Fork)` lookup.

Most users consume the higher-level [`Nethereum.EVM`](../Nethereum.EVM/README.md)
package (which links in `Nethereum.EVM.Core` sources and layers in RPC-
backed state readers, tracing helpers, etc.). Direct references to
`Nethereum.EVM.Core` are for zkVM guests, compliance tooling, or when
you want the minimum possible dependency footprint.

## Installation

```bash
dotnet add package Nethereum.EVM.Core
```

### Dependencies

- [`Nethereum.Util`](../Nethereum.Util/README.md) — `EvmUInt256`,
  `EvmInt256`, address utils, Keccak.
- [`Nethereum.Model`](../Nethereum.Model/README.md) — transaction and
  block header types, `IBlockEncodingProvider`.
- [`Nethereum.RLP`](../Nethereum.RLP/README.md) — RLP encode/decode.
- [`Nethereum.Signer`](../Nethereum.Signer/README.md) — signature
  recovery on the host build (the `#if EVM_SYNC` guest path expects
  authorities to be pre-recovered into the witness).

## Key Concepts

### Hardforks by registry, not static globals

`HardforkName` is a chronological enum covering every Ethereum mainnet
hardfork from Frontier to Osaka (including consensus-only forks
`FrontierThawing`, `DaoFork`, `MuirGlacier`, `ArrowGlacier`,
`GrayGlacier` for naming completeness — they alias the parent fork's
EVM behaviour).

`HardforkConfig` is the executable bundle for a single fork — opcode
handlers, gas rule sets, intrinsic gas rules, call-frame init rules,
transaction validation + setup rules, and a precompile registry. It's
non-nullable at construction; no silent `HardforkConfig.Default`.

`HardforkRegistry` maps `HardforkName` to `HardforkConfig`. Consumers
build one per chain via
`MainnetHardforkRegistry.Build(PrecompileBackends backends)` in
`Nethereum.EVM.Core` (the precompile backends bundle is supplied by
`Nethereum.EVM.Precompiles`, `Nethereum.EVM.Zisk`, or your own
implementation).

### Chain-ID dispatch

`IChainActivations.ResolveAt(long blockNumber, ulong timestamp)`
returns the active `HardforkName` for a given block/time on a specific
chain. `MainnetChainActivations` ships the authoritative Ethereum
mainnet block-number + timestamp activation table (Frontier Thawing
200_000 through Prague at `timestamp >= 1_746_612_311`).

`ChainActivationsRegistry` maps chain id to activations. Mainnet is
pre-registered; L2s, testnets, and AppChains register their own:

```csharp
ChainActivationsRegistry.Instance.Register(10, OptimismActivations.Instance);
var fork = ChainActivationsRegistry.Instance.ResolveAt(
    chainId: 1, blockNumber: 19_000_000, timestamp: 1_710_000_000);
// → HardforkName.Cancun
```

Unknown chain ids throw — silent fallback would replay transactions
against the wrong fork rules.

### State reading: `IStateReader`

```csharp
public interface IStateReader
{
    Task<EvmUInt256> GetBalanceAsync(string address);
    Task<byte[]>     GetCodeAsync(string address);
    Task<byte[]>     GetStorageAtAsync(string address, EvmUInt256 position);
    Task<EvmUInt256> GetTransactionCountAsync(string address);
    // byte[] address overloads + sync counterparts behind #if EVM_SYNC
}
```

The `#if EVM_SYNC` build replaces every `…Async` with its synchronous
equivalent, enabling the Zisk zkVM guest to execute without the .NET
Task state machine.

`InMemoryStateReader` is the witness-backed implementation: given an
`AccountState` dictionary, every read resolves locally. Set
`Strict = true` to turn missing-data misses into
`MissingWitnessDataException` instead of silent zero-returns —
essential for zkVM guest execution where a missed read indicates a
recorder bug, not a legitimate zero slot.

`ExecutionStateService` wraps a reader and tracks every read/write
during transaction execution. It's the single source of truth for
warm/cold access, balance credits/debits (via
`AccountExecutionBalance`), storage modifications, and code deployment
within a transaction. `IsAccountEmpty` and `AccountExistsAsync` both
fall through to the backing reader when a field has not yet been
lazy-loaded into the cache — this fixes the spurious
`CALL_NEW_ACCOUNT` charge that would otherwise hit pre-existing
contracts.

### BLOCKHASH via EIP-2935 (Prague+)

`BlockHashExecutor` reads `history_contract.Storage[blockNumber %
8191]` of the EIP-2935 history contract at
`0x0000F90827F1C53a10cb7A02335B175320002935`. No separate block-hash
witness field — the ancestor-hash ring lives in normal storage, so a
witness recorder captures it automatically as an entry on the history
contract's storage set. BLOCKHASH's gas cost stays at 20 (unchanged
from pre-Prague); the history contract is not warmed under EIP-2929.

`HistoryContractHelpers.PopulateFromBlockHashes(accounts,
blockHashes)` seeds a witness's history-contract storage from a
`(blockNumber → hash)` map for test vectors that don't already carry
the storage slots.

### Witness format (v1)

`BlockWitnessData` is the in-memory witness: block context, the
signed-RLP transactions, the pre-state account set (with optional
Merkle proofs), and execution flags.

`BinaryBlockWitness` is the wire format (v1):

```
[u8  version = 1]
[u8  flags]        bit0 VerifyWitnessProofs
                   bit1 ComputePostStateRoot
                   bit2 ProduceBlockCommitments
[u8  fork]         HardforkName — Unspecified rejected on both sides
[BlockContext]     number, ts, baseFee, gasLimit, chainId, coinbase,
                   difficulty, parentHash, extraData, mixHash, nonce
[u16 txCount][Transaction*]
[u16 accountCount][Account*]
[pad to 8-byte alignment]
```

`WitnessRecordingStateReader` wraps any `IStateReader` and captures
every read; `GetWitnessAccounts()` returns the minimal pre-state
needed to re-execute. Feed its output straight into
`BinaryBlockWitness.Serialize` for Zisk or stateless verification.

### Source-sharing with `Nethereum.EVM`

`Nethereum.EVM` links `Nethereum.EVM.Core` source files directly
(`<Compile Include="..\Nethereum.EVM.Core\..." />`). Both projects build
the same `.cs` files — `Nethereum.EVM` adds reflection-based decoders,
RPC-backed state readers, and tooling helpers on top of the lean core.

## Quick Start

```csharp
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Precompiles;
using Nethereum.EVM.Witness;

// 1. Build a mainnet registry wired with default crypto backends.
var registry = DefaultMainnetHardforkRegistry.Instance;

// 2. Feed a witness (block context + signed-RLP txs + pre-state).
var witness = new BlockWitnessData
{
    BlockNumber = 1,
    Timestamp   = 1_700_000_000,
    BaseFee     = 7,
    BlockGasLimit = 30_000_000,
    ChainId     = 1,
    Features    = BlockFeatureConfig.Prague,
    Accounts    = /* IReadOnlyList<WitnessAccount> pre-state */,
    Transactions= /* IReadOnlyList<BlockWitnessTransaction> */,
};

// 3. Execute the block.
var result = await BlockExecutor.ExecuteAsync(
    witness,
    RlpBlockEncodingProvider.Instance,
    registry,
    new PatriciaStateRootCalculator(RlpBlockEncodingProvider.Instance));

// result.CumulativeGasUsed, result.StateRoot, result.TxResults[i].Success
```

For a synchronous, zkVM-guest flow, the same pattern uses
`BlockExecutor.Execute` (the sync overload emitted by `#if EVM_SYNC`).

## Examples

Every code snippet below is backed by a passing test in
`tests/Nethereum.EVM.Core.Tests` or `tests/Nethereum.EVM.UnitTests`.

### Example 1: Resolve the active fork for a given block

```csharp
using Nethereum.EVM;

var fork = MainnetChainActivations.Instance.ResolveAt(
    blockNumber: 19_426_587, timestamp: 1_710_338_135);
// → HardforkName.Cancun  (EIP-4844 activation block)
```

### Example 2: Build a witness, serialise, round-trip

```csharp
using Nethereum.EVM.Witness;

var witness = new BlockWitnessData { /* … */
    Features = BlockFeatureConfig.Prague };

var bytes  = BinaryBlockWitness.Serialize(witness);
var back   = BinaryBlockWitness.Deserialize(bytes);
Assert.Equal(witness.Features.Fork, back.Features.Fork);
```

Backed by `tests/Nethereum.EVM.Core.Tests/GeneralStateTests/BlockSyncTests.cs`
(`BlockWitness_RoundtripSerializeDeserialize`).

### Example 3: Record a witness while executing

```csharp
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Witness;

// Wrap any IStateReader (in-memory, RPC-backed, historical) with the
// recorder. Every touched account/slot is captured.
var inner    = new InMemoryStateReader(preStateAccounts);
var recorder = new WitnessRecordingStateReader(inner);

var execState = new ExecutionStateService(recorder);
var ctx       = TransactionContextFactory.FromBlockWitnessTransaction(
    witness.Transactions[0], witness, execState);

var executor  = new TransactionExecutor(
    DefaultMainnetHardforkRegistry.Instance.Get(HardforkName.Prague));
await executor.ExecuteAsync(ctx);

// Minimal witness — only the accounts actually touched.
var recorded = recorder.GetWitnessAccounts();
```

Backed by `tests/Nethereum.EVM.UnitTests/GeneralStateTests/RecordingAsyncStateTestRunner.cs`.

### Example 4: Strict reader for zkVM execution

```csharp
var reader = new InMemoryStateReader(witnessAccounts) { Strict = true };

// If the executor asks for an account or slot not in the witness,
// reader throws MissingWitnessDataException instead of silently
// returning zero — surfacing recorder gaps immediately.
var execState = new ExecutionStateService(reader);
```

Backed by
`tests/Nethereum.EVM.UnitTests/WitnessCompletenessTests.cs`.

## Project Layout

| Area | Key types |
|------|-----------|
| Forks & registries | `HardforkName`, `HardforkConfig`, `HardforkRegistry`, `MainnetHardforkRegistry`, `IChainActivations`, `MainnetChainActivations`, `ChainActivationsRegistry`, `PrecompileBackends` |
| State layer | `IStateReader`, `InMemoryStateReader`, `ExecutionStateService`, `AccountState`, `AccountExecutionState`, `AccountExecutionBalance`, `IStateSnapshot`, `MissingWitnessDataException` |
| Executor | `EVMSimulator`, `TransactionExecutor`, `BlockExecutor`, `CallFrame`, `ProgramContext`, `ProgramResult`, `Program`, `ProgramTrace` |
| Opcodes | `Execution/Opcodes/Executors/*Executor.cs` — one handler per opcode family (arithmetic, stack, memory, control flow, storage, CALL/CREATE, SELFDESTRUCT, BLOCKHASH) |
| Gas | `Gas/IntrinsicGasRuleSets`, `Gas/Opcodes/Costs/*`, `Gas/Opcodes/Rules/*` (EIP-2929 access rules, EIP-3860 initcode, EIP-7623 floor) |
| Call-frame rules | `Execution/CallFrame/Rules/Eip150GasRetentionRule`, `Eip7702DelegationRule` |
| Self-destruct rules | `Execution/SelfDestruct/Rules/PreCancunSelfDestructRule`, `Eip6780SelfDestructRule` |
| Transaction setup / validation | `Execution/TransactionSetup/Rules/Eip7702TransactionSetupRule`, `Execution/TransactionValidation/Rules/*` |
| Witness | `Witness/BlockWitnessData`, `BinaryBlockWitness`, `WitnessRecordingStateReader`, `HistoryContractHelpers`, `WitnessStateBuilder` |
| Tries | `IStateRootCalculator`, `IBlockRootCalculator`, `IMerkleTreeBuilder` (implementations live in `Nethereum.CoreChain`) |

## Reuse

- `EvmUInt256` / `EvmInt256` from `Nethereum.Util` — all 256-bit
  arithmetic.
- `Nethereum.Model.BlockHeader`, `Receipt`, transaction types — block
  context + receipt encoding via `IBlockEncodingProvider` /
  `RlpBlockEncodingProvider`.
- `Nethereum.Merkle.Patricia.PatriciaTrie` — the trie type used by
  `PatriciaStateRootCalculator` / `PatriciaBlockRootCalculator` in
  `Nethereum.CoreChain`.
- `Nethereum.Signer.EthECDSASignatureFactory.FromSignature` —
  signature decoding for EIP-7702 authorization validation
  (`Eip7702TransactionSetupRule`).

## See Also

- [`Nethereum.EVM`](../Nethereum.EVM/README.md) — the full-featured
  wrapper (RPC-backed state reader, tracing, disassembler).
- [`Nethereum.EVM.Precompiles`](../Nethereum.EVM.Precompiles/README.md)
  — default precompile backends.
- [`Nethereum.EVM.Zisk`](../Nethereum.EVM.Zisk/README.md) — zkVM
  guest-side wiring.
