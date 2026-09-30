# Nethereum.AccountAbstraction.Bundler

Run an ERC-4337 bundler inside your own .NET process. It takes UserOperations, validates them, keeps them in a mempool, packs them into a bundle, and lands that bundle on-chain through the EntryPoint's `handleOps` — so an AppChain, a test suite or a product can have a bundler without depending on someone else's.

## What you can do with it

| Task | Reach for |
|---|---|
| Accept a UserOperation and get back its hash | `BundlerService.SendUserOperationAsync(userOp, entryPoint)` |
| Ask what happened to it — pending, submitted, included, failed, dropped | `GetUserOperationStatusAsync(hash)` / `GetUserOperationReceiptAsync(hash)` |
| Bundle right now instead of waiting for the timer | `FlushAsync()` / `ExecuteBundleAsync()` |
| Quote gas for an operation before it is signed | `EstimateUserOperationGasAsync(userOp, entryPoint)` |
| Enforce the ERC-7562 validation-scope rules (forbidden opcodes, storage access, entity rules) | `EnableERC7562Validation` → `ERC7562SimulationService` |
| Keep a mempool with TTL, per-sender limits, fee-bump replacement and priority ordering | `InMemoryUserOpMempool` (`IUserOpMempool`) |
| Ban or throttle a misbehaving paymaster/factory/account | inject an `IReputationService` (e.g. `InMemoryReputationService`) — **without one, nothing is enforced**; see [Reputation](#reputation-only-runs-if-you-inject-it) |
| Aggregate BLS signatures into `handleAggregatedOps` | `EnableBlsAggregation` + `BlsAggregatorAddresses` |
| Start from a sane preset instead of tuning 30 fields | `BundlerConfig.CreateAppChainConfig` / `CreateStandardConfig` / `CreateProductionConfig` |

Three sibling packages extend it:

| Package | What it adds |
|---|---|
| **Nethereum.AccountAbstraction.Bundler.RpcServer** | exposes the bundler as a JSON-RPC endpoint |
| **Nethereum.AccountAbstraction.Bundler.RocksDB** | `RocksDbUserOpMempool` and `RocksDbReputationStore` — a mempool and a reputation store that survive a restart |
| **Nethereum.AccountAbstraction.Bundler.InProcess** | `InProcessBundlerHost` — brings up a devchain plus a bundler in one process, for tests |

## Install

```bash
dotnet add package Nethereum.AccountAbstraction.Bundler
```

## Quick Start — submit a UserOperation and read its status

Extracted from `BundlerServiceTests.Flush_ExecutesPendingOperations` (`tests/Nethereum.AccountAbstraction.IntegrationTests/Bundler/BundlerServiceTests.cs`) and the `BundlerTestFixture` it builds its config in, which run it against a deployed EntryPoint.

```csharp
// The config the test actually runs: a hand-built BundlerConfig, not one of the presets.
var config = new BundlerConfig
{
    SupportedEntryPoints = new[] { entryPointAddress },
    BeneficiaryAddress = beneficiaryAddress,
    MaxBundleSize = 10,
    MaxMempoolSize = 100,
    MinPriorityFeePerGas = 0,
    MaxBundleGas = 15_000_000,
    AutoBundleIntervalMs = 0,      // manual: FlushAsync is the only trigger
    StrictValidation = false,
    SimulateValidation = false,
    UnsafeMode = true,             // test-only: skips the reputation check, NOT validation
    ChainId = chainId
};

using var bundler = new BundlerService(web3, config);

var userOpHash = await bundler.SendUserOperationAsync(userOp, entryPointAddress);

var before = await bundler.GetUserOperationStatusAsync(userOpHash);
// before.State == UserOpState.Pending

var bundleTxHash = await bundler.FlushAsync();   // builds and submits the bundle now

var after = await bundler.GetUserOperationStatusAsync(userOpHash);
// after.State is UserOpState.Included, or UserOpState.Failed if the op reverted on-chain
```

**Do not copy that config into production** — it is a test harness. For a real deployment start from a preset instead (`BundlerConfig.CreateAppChainConfig(entryPointAddress, beneficiaryAddress)` for a private chain, `CreateStandardConfig` / `CreateProductionConfig` for a public one); every preset leaves `UnsafeMode = false` and `SimulateValidation = true`, and bundles on a timer rather than only on `FlushAsync`. The call sequence above is identical either way.

`SendUserOperationAsync` returns a 66-character `0x` hash and puts the operation in the mempool — it does **not** wait for a block. `FlushAsync` returns the bundle's transaction hash (`0x…`), or `null` when there was nothing to bundle. Leave `AutoBundleIntervalMs` above zero (every preset does) and a timer bundles on its own at that interval, applying a base-fee floor to the candidates that `FlushAsync` does not; `SetBundlingMode(BundlingMode.Manual)` stops that timer at runtime and `BundlingMode.Auto` restarts it. At `0` no timer is created at all.

The `userOp` here is a `PackedUserOperation` — build and sign one with `EntryPointService.SignAndInitialiseUserOperationAsync(...)` from **Nethereum.AccountAbstraction**, or let `IAAClient` do it for you.

## Entry points

**Reach for `BundlerService` first.** It is the bundler: constructed from a `Web3` and a `BundlerConfig`, it owns the mempool, the validator, the executor and the auto-bundle timer, and it implements the whole RPC surface (`IBundlerServiceExtended`, which extends `IBundlerService`). Everything else in this package is something `BundlerService` composes and you can replace.

| I want to... | Use |
|---|---|
| stand a bundler up | `new BundlerService(web3, config)` — `config` from a `BundlerConfig.Create*Config` preset |
| submit an operation | `SendUserOperationAsync(userOp, entryPoint, eip7702Auth)` |
| poll an operation | `GetUserOperationStatusAsync(hash)`, `GetUserOperationByHashAsync(hash)`, `GetUserOperationReceiptAsync(hash)` |
| force / control bundling | `FlushAsync()`, `ExecuteBundleAsync()`, `SetBundlingMode(BundlingMode.Auto \| Manual)` |
| estimate gas before signing | `EstimateUserOperationGasAsync(userOp, entryPoint)` |
| swap the mempool (e.g. for a persistent one) | `IUserOpMempool` — pass it to the `BundlerService` constructor |
| tighten or relax validation | `BundlerConfig` — `StrictValidation`, `SimulateValidation`, `EnableERC7562Validation`, `MinStake` (**not** `UnsafeMode`, which only skips the reputation check) |
| enable ban/throttle enforcement | pass an `IReputationService` to the six-argument `BundlerService` constructor — there is no default |
| watch what the bundler is doing | `GetStatsAsync()` → `BundlerStats`, `GetPendingUserOperationsAsync()` |
| handle a rejection | catch `BundlerRpcException` and switch on its `Code` against `BundlerErrorCodes` |

## Key Concepts

### Bundler Pipeline

1. **Receive**: UserOperation arrives via `SendUserOperationAsync`
2. **Validate**: Check nonce, signature, sender, EntryPoint support, blacklist/whitelist
3. **Simulate** (optional): Run ERC-7562 validation via EVM simulation
4. **Mempool**: Add to priority-ordered mempool
5. **Bundle**: Timer triggers bundle building from pending operations
6. **Execute**: Submit bundle to EntryPoint via `handleOps()`
7. **Track**: Update reputation based on inclusion/failure — **only when an `IReputationService` was injected**; see [Reputation](#reputation-only-runs-if-you-inject-it)

### Bundler Modes

```csharp
// Simplified: no strict rules, for private chains
var appChain = BundlerConfig.CreateAppChainConfig(entryPoint, beneficiary);

// Standard: full ERC-7562, for public testnets
var standard = BundlerConfig.CreateStandardConfig(entryPoint, beneficiary);

// Production: strict + reputation + min stake
var production = BundlerConfig.CreateProductionConfig(entryPoint, beneficiary);
```

The three differ only in the fields they set: `CreateAppChainConfig` turns `StrictValidation` off and bundles every second; `CreateStandardConfig` and `CreateProductionConfig` both turn on `StrictValidation` and `EnableERC7562Validation`, require a 1 gwei priority fee and bundle every 10 seconds, and production additionally pins `MinStake` (1 ETH) and `MinUnstakeDelaySec` (86400). None of them enables `UnsafeMode`.

## Usage Examples

### Example 1: Configure and Run Bundler

```csharp
using Nethereum.AccountAbstraction.Bundler;

var config = new BundlerConfig
{
    SupportedEntryPoints = new[] { entryPointAddress },
    BeneficiaryAddress = bundlerAddress,
    MaxBundleSize = 10,
    MaxMempoolSize = 1000,
    AutoBundleIntervalMs = 10000,
    EnableERC7562Validation = true,
    StrictValidation = true
};

var bundler = new BundlerService(web3, config);
// Auto-bundling starts automatically
```

### Example 2: Custom Mempool

```csharp
var mempool = new InMemoryUserOpMempool(
    maxSize: 5000, entryTtl: TimeSpan.FromMinutes(30));

// validator and executor default to a UserOpValidator/BundleExecutor built from
// web3 + config when passed null; pass real instances to customize them instead.
var bundler = new BundlerService(web3, config, mempool: mempool, validator: null, executor: null);
```

### Example 3: Manual Bundle Execution

```csharp
// Disable auto-bundling
config.AutoBundleIntervalMs = 0;
var bundler = new BundlerService(web3, config);

// Submit operations
await bundler.SendUserOperationAsync(userOp1, entryPoint);
await bundler.SendUserOperationAsync(userOp2, entryPoint);

// Manually trigger bundle
BundleExecutionResult? result = await bundler.ExecuteBundleAsync();
```

`ExecuteBundleAsync` returns `null` when there was nothing to bundle. Otherwise it returns the full outcome — `FlushAsync` is the thin wrapper that hands back only `TransactionHash`:

```csharp
public class BundleExecutionResult
{
    public bool Success { get; set; }
    public string? TransactionHash { get; set; }
    public TransactionReceipt? Receipt { get; set; }
    public string? Error { get; set; }
    public UserOpExecutionResult[] UserOpResults { get; set; } = Array.Empty<UserOpExecutionResult>();
    public BigInteger GasUsed { get; set; }
    public int? FailedOpIndex { get; set; }
    public string? FailedOpReason { get; set; }
    public bool ReceiptTimedOut { get; set; }

    public static BundleExecutionResult Failed(string error);
    public static BundleExecutionResult Succeeded(string txHash, TransactionReceipt receipt);
}
```

`FailedOpIndex` / `FailedOpReason` name the single operation the EntryPoint rejected, and `ReceiptTimedOut` distinguishes "the bundle transaction never got a receipt within `BundleReceiptTimeoutSeconds`" from "it landed and reverted".

### Errors: `BundlerRpcException` and `BundlerErrorCodes`

Every rejection on the way in is thrown, not returned. `SendUserOperationAsync` throws `BundlerRpcException` for a blacklisted sender and for any failed validation, and the RPC server surfaces `Code` as the JSON-RPC error code.

```csharp
public class BundlerRpcException : Exception
{
    public int Code { get; }
    public object? ErrorData { get; }

    public BundlerRpcException(int code, string message, object? errorData = null);

    public static BundlerRpcException FromValidationResult(UserOpValidationResult result);
    public static int ToErrorCode(UserOpValidationError error);
}
```

`BundlerErrorCodes` holds the ERC-7769 code set (each is an alias of the matching `Erc7769ErrorCodes` constant):

| Constant | Value | Meaning |
|---|---|---|
| `ParseError` | `-32700` | malformed request |
| `InvalidRequest` | `-32600` | invalid request object |
| `MethodNotFound` | `-32601` | unknown RPC method |
| `InvalidFields` | `-32602` | bad fields — also the code for a duplicate op, an underpriced replacement, a gas overflow, or an invalid EIP-7702 authorisation |
| `InternalError` | `-32603` | internal failure |
| `SimulateValidation` | `-32500` | account validation failed — the default for any unmapped validation error |
| `SimulatePaymasterValidation` | `-32501` | paymaster not deployed, or its validation/postOp failed |
| `OpcodeValidation` | `-32502` | forbidden opcode or storage access during validation |
| `NotInTimeRange` | `-32503` | expired or not yet valid |
| `Reputation` | `-32504` | banned or throttled entity, or a blacklisted sender |
| `InsufficientStake` | `-32505` | entity stake below `MinStake` |
| `UnsupportedSignatureAggregator` | `-32506` | unknown aggregator, or aggregator validation failed |
| `InvalidSignature` | `-32507` | signature invalid or failed validation |
| `PaymasterDepositTooLow` | `-32508` | paymaster cannot cover the operation |
| `UserOperationReverted` | `-32521` | the operation reverted |

```csharp
try
{
    var hash = await bundler.SendUserOperationAsync(userOp, entryPointAddress);
}
catch (BundlerRpcException ex) when (ex.Code == BundlerErrorCodes.InvalidFields)
{
    // e.g. this exact userOp is already in the mempool
    Console.WriteLine($"rejected: {ex.Message}");
}
```

`ToErrorCode` is the mapping from a `UserOpValidationError` to its code, exposed so a custom RPC layer can reuse it.

## API Reference

### `IBundlerService` / `IBundlerServiceExtended`

`BundlerService` implements `IBundlerServiceExtended` (which extends `IBundlerService`) and `IDisposable`.

```csharp
public interface IBundlerService
{
    Task<string> SendUserOperationAsync(
        PackedUserOperation userOp, string entryPoint, Authorisation eip7702Auth = null);
    Task<UserOperationGasEstimate> EstimateUserOperationGasAsync(
        UserOperation userOp, string entryPoint);
    Task<UserOperationReceipt?> GetUserOperationReceiptAsync(string userOpHash);
    Task<IncludedUserOperation?> GetUserOperationByHashAsync(string userOpHash);
    Task<string[]> SupportedEntryPointsAsync();
    Task<BigInteger> ChainIdAsync();
}

public interface IBundlerServiceExtended : IBundlerService
{
    Task<UserOperationStatus> GetUserOperationStatusAsync(string userOpHash);
    Task<PendingUserOperation[]> GetPendingUserOperationsAsync();
    Task<bool> DropUserOperationAsync(string userOpHash);
    Task<string?> FlushAsync();
    Task<BundlerStats> GetStatsAsync();
    Task SetReputationAsync(string address, ReputationEntry reputation);
    Task<ReputationEntry> GetReputationAsync(string address);
    Task<ReputationEntry[]> GetAllReputationAsync();
    Task<StakeStatus> GetStakeStatusAsync(string address, string entryPoint);
    void SetBundlingMode(BundlingMode mode);
    Task ClearStateAsync();
    Task ClearMempoolAsync();
    Task ClearReputationAsync();
}
```

`SendUserOperationAsync` takes the packed operation form plus an optional EIP-7702
authorisation tuple (v0.9 EntryPoint), carried as a side-channel alongside the op.
`EstimateUserOperationGasAsync` takes the unpacked `UserOperation` (before packing)
and does not accept an authorisation tuple. `BundlerService` additionally exposes
`ExecuteBundleAsync()` and `ExecuteBundleAsync(BigInteger? minBaseFee)` for manual bundling,
and `SetBundlingMode(BundlingMode.Auto | BundlingMode.Manual)` switches the timer off and on.

Supporting types:

```csharp
public enum BundlingMode { Auto, Manual }
public enum UserOpState { Pending, Submitted, Included, Failed, Dropped }

public class BundlerStats
{
    public int PendingCount { get; set; }
    public int SubmittedCount { get; set; }
    public int IncludedCount { get; set; }
    public int FailedCount { get; set; }
    public int BundlesSubmitted { get; set; }
    public BigInteger TotalGasUsed { get; set; }
    public DateTimeOffset StartedAt { get; set; }
}

public class ReputationEntry
{
    public string Address { get; set; }
    public int OpsSeen { get; set; }
    public int OpsIncluded { get; set; }
    public int OpsFailed { get; set; }
    public int OpsDropped { get; set; }
    public ReputationStatus Status { get; set; }
    public DateTimeOffset LastUpdated { get; set; }
    public DateTimeOffset? BannedUntil { get; set; }
    public DateTimeOffset? ThrottledUntil { get; set; }
}

public enum ReputationStatus { Ok, Throttled, Banned }
```

`ReputationStatusCalculator` derives `Status` from the counters and `ReputationDecayCalculator` ages them; `FailedOpBlameResolver` decides which entity a `FailedOp` is charged to, and `StakingInfoService` reads stake from the EntryPoint (surfaced as `StakeStatus`).

### Reputation only runs if you inject it

**`new BundlerService(web3, config)` has no reputation service, and none is created for you.** Unlike the mempool, validator and executor — each of which the constructor defaults to a concrete implementation — `IReputationService` is stored as-is and left `null`. With it null:

- no sender, factory or paymaster is ever checked for ban or throttle on `SendUserOperationAsync`;
- no `OpsSeen` / `OpsIncluded` / `OpsFailed` counters are recorded;
- the reputation decay timer is never started, whatever `ReputationDecayIntervalMs` says;
- `GetReputationAsync` / `SetReputationAsync` / `GetAllReputationAsync` still work, but against a plain in-process dictionary that no enforcement path reads. `GetReputationAsync` for an unknown address returns a fresh entry with `Status = ReputationStatus.Ok`.

So the advertised ban/throttle capability does nothing out of the box. Turn it on with the six-argument constructor:

```csharp
using Nethereum.AccountAbstraction.Bundler.Reputation;

using var bundler = new BundlerService(
    web3,
    config,
    mempool: null,               // defaults to InMemoryUserOpMempool(config.MaxMempoolSize)
    validator: null,             // defaults to UserOpValidator(web3, config, ...)
    executor: null,              // defaults to BundleExecutor(web3, config)
    reputationService: new InMemoryReputationService());   // no default - pass one or get nothing
```

`InMemoryReputationService(ReputationConfig? config = null)` ships in this package; `ReputationConfig` carries the thresholds (`MinInclusionDenominator` 10, `ThrottlingSlack` 10, `BanSlack` 50, decay 23/24, default throttle 1 h, default ban 24 h, staked-accountability penalty 10000). For a store that survives a restart, use `RocksDbReputationStore` from **Nethereum.AccountAbstraction.Bundler.RocksDB**.

Note that `UnsafeMode = true` disables the ban/throttle check even when a reputation service *is* injected — that is the only thing `UnsafeMode` does.

### Mempool

```csharp
public interface IUserOpMempool
{
    Task<MempoolAddOutcome> AddAsync(MempoolEntry entry);
    Task<MempoolEntry?> GetAsync(string userOpHash);
    Task<MempoolEntry[]> GetPendingAsync(int maxCount, BigInteger? maxGas = null);
    Task<MempoolEntry[]> GetAllPendingAsync();
    Task<MempoolEntry[]> GetBySenderAsync(string sender);
    Task<bool> RemoveAsync(string userOpHash);
    Task MarkSubmittedAsync(string[] userOpHashes, string transactionHash);
    Task MarkIncludedAsync(
        string[] userOpHashes, string transactionHash, BigInteger blockNumber, string? blockHash = null);
    Task MarkFailedAsync(string[] userOpHashes, string error);
    Task RevertSubmittedAsync(string transactionHash);
    Task ClearAsync();
    Task<int> CountAsync();
    Task<MempoolStats> GetStatsAsync();
    Task<int> PruneAsync();
}

public enum MempoolAddOutcome
{
    Added,
    Replaced,
    RejectedDuplicate,
    RejectedUnderpriced,
    RejectedFull
}

public enum MempoolEntryState { Pending, Submitted, Included, Failed, Dropped }
```

`InMemoryUserOpMempool` is the thread-safe in-memory implementation, holding `maxSize` entries (default 1000) with a per-entry TTL that defaults to 30 minutes. `AddAsync` returns a `MempoolAddOutcome` rather than a plain success flag, so a caller can tell a fee-bump *replacement* from each distinct rejection; `MempoolReplacementRules` decides when a new op replaces an existing one for the same sender+nonce, and `PruneAsync` returns the number of expired/stale entries removed. `RevertSubmittedAsync` puts a bundle's operations back to pending when its transaction did not land. `MempoolChainKey` and `MempoolPendingChains` track per-sender nonce chains, and `MultipleRolesRule` enforces ERC-7562's "an address cannot be both an account and a factory/paymaster in the same mempool" constraint.

## ERC-7562 validation

`ERC7562SimulationService` re-executes the UserOperation's validation phase in the in-process EVM (`Nethereum.EVM`) with `ERC7562TracingInterceptor` attached, and hands each observed opcode, storage access and call to `ERC7562RuleEnforcer`. Every rejection is an `ERC7562Violation` whose `Rule` string is **usually** a spec rule id — the `OP-*` / `STO-*` ids from ERC-7562 and the `AA*` / `EREP-*` codes from ERC-4337 — but one, `SIMULATION_ERROR`, is this library's own marker for "the simulation itself threw", not a spec id. Match on the string, and treat an unrecognised value as a rejection you cannot attribute to a rule.

```csharp
public class ERC7562SimulationService
{
    // nodeDataService: where state is read from. Web3NodeDataServiceAdapter (below) is the
    // implementation that reads it over JSON-RPC; both arguments throw ArgumentNullException if null.
    public ERC7562SimulationService(IStateReader nodeDataService, HardforkConfig hardforkConfig);

    public Task<ERC7562ValidationResult> ValidateUserOperationAsync(
        PackedUserOperationDTO userOp,
        string entryPointAddress,
        Erc4337Entity sender,
        Erc4337Entity factory = null,
        Erc4337Entity paymaster = null,
        Erc4337Entity aggregator = null,
        long blockNumber = -1,
        long timestamp = -1,
        string coinbase = null,
        BigInteger chainId = default,
        Authorisation eip7702Auth = null);
}

public class ERC7562ValidationResult
{
    public bool IsValid { get; set; }
    public List<ERC7562Violation> Violations { get; set; } = new();
    public List<StorageSlotAccess> StorageAccesses { get; set; } = new();
    public List<OpcodeExecution> OpcodeExecutions { get; set; } = new();
    public List<TracedCall> Calls { get; set; } = new();
    public HashSet<string> AccessedAddresses { get; set; } = new();
}

public class ERC7562Violation
{
    public string Rule { get; set; } = "";
    public string Message { get; set; } = "";
    public string Address { get; set; } = "";
    public Instruction? Opcode { get; set; }
    public BigInteger? Slot { get; set; }
    public EntityType? Entity { get; set; }

    public static ERC7562Violation FromOpcode(string rule, string message, Instruction opcode, ERC7562ValidationContext context);
    public static ERC7562Violation FromStorage(string rule, string message, string address, BigInteger slot, ERC7562ValidationContext context);
    public static ERC7562Violation FromCall(string rule, string message, string address, ERC7562ValidationContext context);
}
```

The service needs an `IStateReader` (where state comes from) and a **non-null** `HardforkConfig` (which EVM rules apply). Resolve the config with `DefaultMainnetHardforkRegistry.Instance.Get(HardforkNames.Parse(name))`.

`Web3NodeDataServiceAdapter` is the `IStateReader` that reads state over JSON-RPC, and is what makes the constructor above callable at all — `UserOpValidator` defaults to `Web3NodeDataServiceAdapter.CreateForLatest(web3)` when you do not supply one:

```csharp
public class Web3NodeDataServiceAdapter : IStateReader
{
    public Web3NodeDataServiceAdapter(IWeb3 web3, BlockParameter? blockParameter = null);

    public static Web3NodeDataServiceAdapter CreateForLatest(IWeb3 web3);
    public static Web3NodeDataServiceAdapter CreateForPending(IWeb3 web3);
    public static Web3NodeDataServiceAdapter CreateForBlock(IWeb3 web3, BigInteger blockNumber);
}
```

```csharp
var hardforkConfig = DefaultMainnetHardforkRegistry.Instance.Get(HardforkNames.Parse("osaka"));
var simulation = new ERC7562SimulationService(
    Web3NodeDataServiceAdapter.CreateForLatest(web3), hardforkConfig);
```

### Entities

Rules are scoped per entity, and staking widens what an entity may do:

```csharp
public enum EntityType
{
    None,
    Sender,
    Factory,
    Paymaster,
    Aggregator
}

public class Erc4337Entity
{
    public string Address { get; set; } = "";
    public EntityType Type { get; set; }
    public bool IsStaked { get; set; }
    public BigInteger StakeAmount { get; set; }
    public ulong UnstakeDelaySec { get; set; }

    public static Erc4337Entity Create(
        EntityType type, string address, bool isStaked = false,
        BigInteger? stake = null, ulong unstakeDelay = 0);
    public static Erc4337Entity CreateSender(...);
    public static Erc4337Entity CreateFactory(...);
    public static Erc4337Entity CreatePaymaster(...);
    public static Erc4337Entity CreateAggregator(...);
}
```

`EntityTypeExtensions.ToErc7562EntityName()` maps these to the lower-case names the spec and eth-infinitism's reference bundler use in error text - note that `Sender` is reported as **`"account"`**, so a violation reads `account uses banned opcode: ORIGIN`.

### Opcode sets

```csharp
public static class ForbiddenOpcodes
{
    public static readonly HashSet<Instruction> AlwaysForbidden;
    public static readonly HashSet<Instruction> StakedOnlyOpcodes;
    public static readonly HashSet<Instruction> ConditionalOpcodes;
    public static readonly HashSet<Instruction> CallOpcodes;
    public static readonly HashSet<Instruction> StorageOpcodes;
    public static readonly HashSet<Instruction> TransientStorageOpcodes;
    public static readonly HashSet<Instruction> ExtCodeOpcodes;
    public static readonly HashSet<Instruction> ValidOpcodes;
    public static readonly HashSet<int> AllowedPrecompiles;
    public const int Secp256r1Precompile = 0x100;

    public static bool IsAlwaysForbidden(Instruction opcode);
    public static bool RequiresStaking(Instruction opcode);
    public static bool IsCallOpcode(Instruction opcode);
    public static bool IsAllowedPrecompile(int address, bool includeRip7212 = false);
    public static bool IsCreateOpcode(Instruction opcode);
    public static bool IsStorageOpcode(Instruction opcode);
    public static bool IsTransientStorageOpcode(Instruction opcode);
    public static bool IsExtCodeOpcode(Instruction opcode);
    public static bool IsValidOpcode(Instruction opcode);
    public static bool IsWriteStorageOpcode(Instruction opcode);
}
```

| Set | Members |
|---|---|
| `AlwaysForbidden` | `ORIGIN`, `GASPRICE`, `BLOCKHASH`, `COINBASE`, `TIMESTAMP`, `NUMBER`, `DIFFICULTY`, `GASLIMIT`, `BASEFEE`, `BLOBHASH`, `BLOBBASEFEE`, `INVALID`, `SELFDESTRUCT` |
| `StakedOnlyOpcodes` | `BALANCE`, `SELFBALANCE` |
| `ConditionalOpcodes` | `GAS`, `CREATE`, `CREATE2` |
| `CallOpcodes` | `CALL`, `CALLCODE`, `DELEGATECALL`, `STATICCALL` |
| `StorageOpcodes` | `SLOAD`, `SSTORE` |
| `TransientStorageOpcodes` | `TLOAD`, `TSTORE` |
| `ExtCodeOpcodes` | `EXTCODESIZE`, `EXTCODECOPY`, `EXTCODEHASH` |
| `ValidOpcodes` | every assigned opcode (anything outside this set is unassigned) |
| `AllowedPrecompiles` | `0x01`–`0x11` |

`IsAllowedPrecompile(address, includeRip7212)` additionally accepts `Secp256r1Precompile` (`0x100`, RIP-7212 / EIP-7951 `P256VERIFY`) when the context's `AllowRip7212Precompile` is set - that is how a passkey-owned account's validation can call the P-256 verifier.

> **Precompiles are decided by two separate rules.** ERC-7562 [OP-062] names a FIXED list — *"The core precompiles `0x1`-`0x11`. The `P256VERIFY` secp256r1 precompile defined in EIP-7951."* — so `ForbiddenOpcodes.AllowedPrecompiles` holds `0x01`–`0x11` and is written out rather than derived from a fork. Separately, [OP-041] forbids calling an address without deployed code, and a precompile has none; that exemption asks whether the address is a precompile **at the fork being simulated**, which `ERC7562TracingInterceptor` takes from `HardforkConfig.Precompiles`. The two questions have two different sources on purpose: sharing one would either exempt `0x0b` on a pre-Prague chain, where it is an ordinary empty account, or accept a future precompile the ERC has not accepted. A UserOperation calling an EIP-2537 BLS precompile (`0x0b`–`0x11`) during validation passes both checks on a fork that has them.

### Rules enforced

Violations come from **two** places. `ERC7562RuleEnforcer` raises the opcode and storage rules below; `ERC7562SimulationService` raises five more of its own while driving the simulation. Both land in the same `ERC7562ValidationResult.Violations` list.

#### From `ERC7562RuleEnforcer`

| Rule | Raised when |
|---|---|
| `OP-011` | an entity executes an `AlwaysForbidden` opcode |
| `OP-012` | `GAS` is used other than immediately before a `*CALL` opcode |
| `OP-013` | an unassigned opcode is executed (not in `ValidOpcodes`) |
| `OP-020` | validation runs out of gas |
| `OP-031` | `CREATE2` outside the permitted deployment case |
| `OP-032` | `CREATE` outside the permitted case |
| `OP-041` | a call or `EXTCODE*` touches an address with no deployed code (the sender during its own deployment phase is exempt) |
| `OP-052` | `EntryPoint.depositTo` called by anyone other than the sender or the factory |
| `OP-053` | the EntryPoint fallback called by anyone other than the sender |
| `OP-054` | `EntryPoint.incrementNonce` called by anyone other than the sender, or any `EXTCODE*` access to the EntryPoint — **except** the `EXTCODESIZE`-then-`ISZERO` pair (see below) |
| `OP-055` | any other EntryPoint method called during validation |
| `OP-061` | `CALL` with value to anything other than the EntryPoint |
| `OP-062` | a call to a precompile outside the allowed set |
| `OP-080` | an unstaked entity uses a `StakedOnlyOpcodes` opcode (`BALANCE` / `SELFBALANCE`) |
| `STO-010` | direct EntryPoint storage access from outside a permitted EntryPoint call |
| `STO-031` | unauthorized storage/transient **read** |
| `STO-032` | unauthorized storage/transient **write** |

**The `EXTCODESIZE` / `ISZERO` exemption.** An `EXTCODESIZE` against the EntryPoint does not raise `OP-054` immediately. `ERC7562TracingInterceptor` holds the violation back, then looks at the very next opcode: if it is `ISZERO`, the violation is **dropped**; anything else and it is added to the list. That is the "is there code at this address" idiom — the caller only wants the zero/non-zero answer, never the size — which the ERC allows and which Solidity's own `address.code.length == 0` compiles to. `EXTCODECOPY` and `EXTCODEHASH` against the EntryPoint are never deferred and always violate.

#### From `ERC7562SimulationService`

These five are raised by the simulation service itself rather than the enforcer, and are **not** in the `ERC7562RuleEnforcer` table above:

| Rule | Raised when |
|---|---|
| `AA13` | the factory in `initCode` reverted, so the sender was never deployed |
| `AA20` | the sender has no code **and** the operation carries no `initCode` |
| `AA30` | the paymaster address has no code deployed |
| `EREP-050` | an **unstaked** paymaster's `validatePaymasterUserOp` returned a non-empty `context` |
| `SIMULATION_ERROR` | the simulation itself threw — **not a spec rule id**, this library's own marker, message `Simulation failed: {exception message}` |

`SIMULATION_ERROR` is the one to special-case: it means the bundler could not reach a verdict, not that the operation broke a rule.

Storage access is allowed when: the sender reads or writes **its own** storage; a staked entity touches **its own** contract; the slot is *associated* with the entity, with a staked factory or no deploying factory in play; or a staked entity reads a non-entity address. Anything else is `STO-031`/`STO-032`.

"Associated" is decided by `AssociatedStorageCalculator`, which watches the `KECCAK256` calls the simulation makes (`TrackKeccak` / `TrackKeccakFromHash`) and remembers each result's preimage. A slot counts as associated with an address when its preimage is a mapping keyed on that address (`SlotAssociationType.Mapping`), when it is a nested mapping whose base slot resolves the same way (`NestedMapping`), or when it falls within 128 slots after such a mapping slot — the window that covers a multi-word struct stored at that mapping entry. `RegisterSenderSlot(sender, baseSlot)` pre-registers `keccak(pad32(sender) ‖ pad32(baseSlot))` for a known mapping, and `IsAssociatedSlot(contract, slot, sender)` is the question the enforcer asks.

`ERC7562ValidationContext` carries the rest - the four entities, the current entity and call depth, the deployment-phase flag, the accumulated `AssociatedSlots` and `EntityOwnAssociatedSlots`, `AllowRip7212Precompile`, and the traced opcode/storage/call lists.

### Gas estimation

```csharp
public class TransactionExecutorGasEstimator : IEvmGasEstimator
{
    public TransactionExecutorGasEstimator(
        IStateReader nodeDataService,
        BigInteger chainId,
        HardforkConfig hardforkConfig,
        long blockGasLimit = 0);
}
```

Same `IStateReader` + non-null `HardforkConfig` pair as the simulation service. `SimulationGasEstimator` is the EntryPoint-simulation-based alternative, and `Eip7623PreVerificationGasCalculator` computes the EIP-7623 calldata floor component of `preVerificationGas`.

### `BundlerConfig`

```csharp
public class BundlerConfig
{
    public string[] SupportedEntryPoints { get; set; } = Array.Empty<string>();
    public string BeneficiaryAddress { get; set; }
    public int MaxBundleSize { get; set; } = 10;
    public int MaxMempoolSize { get; set; } = 1000;
    public BigInteger MinPriorityFeePerGas { get; set; } = 0;
    public BigInteger MaxBundleGas { get; set; } = 15_000_000;
    public bool SkipUnderpricedOpsInAutoBundle { get; set; } = true;
    public int AutoBundleIntervalMs { get; set; } = 10_000;
    public int ReputationDecayIntervalMs { get; set; } = 3_600_000;
    public int BundleReceiptTimeoutSeconds { get; set; } = 90;
    public BigInteger ReceiptLogLookbackBlocks { get; set; } = 10_000;
    public bool StrictValidation { get; set; } = true;
    public bool SimulateValidation { get; set; } = true;
    public bool UnsafeMode { get; set; } = false;
    public bool EnableERC7562Validation { get; set; } = false;
    public BigInteger MinStake { get; set; } = 1_000_000_000_000_000_000;
    public uint MinUnstakeDelaySec { get; set; } = 86400;
    public int MaxUnstakedSenderMempoolCount { get; set; } = 4;
    public HashSet<string> WhitelistedAddresses { get; set; } = new();
    public HashSet<string> BlacklistedAddresses { get; set; } = new();
    public int MaxVerificationGas { get; set; } = 1_500_000;
    public BigInteger? ChainId { get; set; }
    public string Hardfork { get; set; }
    public ChainForkSchedule ForkSchedule { get; set; }
    public ChainForkSchedule ResolveForkSchedule(long chainId);
    public bool EnableBlsAggregation { get; set; } = false;
    public string[] BlsAggregatorAddresses { get; set; } = Array.Empty<string>();

    public static BundlerConfig CreateAppChainConfig(string entryPoint, string beneficiary);
    public static BundlerConfig CreateStandardConfig(string entryPoint, string beneficiary);
    public static BundlerConfig CreateProductionConfig(string entryPoint, string beneficiary);
}
```

Two knobs deserve attention:

- **Which fork ERC-7562 validation simulates under** is answered by the chain, not by the bundler. Leave `Hardfork` and `ForkSchedule` unset and `BundlerChainRules` resolves it: chain id from `ChainId` or `eth_chainId`, the fork from that chain's schedule at its head. `ForkSchedule` states a chain's schedule directly — this is what an AppChain or dev chain passes, and it is the only way a chain the registry has never heard of gets the right fork. `Hardfork` is the single-fork shorthand for the same thing, and `ResolveForkSchedule(chainId)` is where the two become one answer: `ForkSchedule` if set, otherwise `ChainForkSchedule.Running(chainId, Hardfork)`, otherwise nothing and the registry decides. A chain nobody has described falls back to Amsterdam, stated at the call site.

  The resolution happens on first validation rather than in the constructor, because reading a chain's head is an RPC call. It is read only when `EnableERC7562Validation` is `true`; `SimulationGasEstimator` takes `(IWeb3, BundlerConfig, ILogger?)` and never touches it, so **none of these affect gas estimation** and none has any effect while `EnableERC7562Validation` is `false`.
- **`UnsafeMode`** does **not** skip validation. Its only behavioural effect is to skip the **reputation** check on `SendUserOperationAsync`: the guard is `if (_reputationService != null && !_config.UnsafeMode) await CheckReputationAsync(userOp);`, and `_validator.ValidateAsync(...)` on the next line runs unconditionally either way. So `UnsafeMode = true` lets a banned or throttled sender/factory/paymaster through — and only that. To relax *validation* you want `SimulateValidation`, `StrictValidation` and `EnableERC7562Validation`, which are separate flags. Testing only.

### Aggregation

`AggregatorRegistry` (`IAggregatorRegistry`) resolves an aggregator address to a signature aggregator; `BlsAggregator` and `BlsAggregatorFactory` provide the BLS implementation used for `handleAggregatedOps`. `EnableBlsAggregation` plus `BlsAggregatorAddresses` turn it on.

## Related Packages

### Used By (Consumers)
- **[Nethereum.AccountAbstraction.Bundler.RpcServer](../Nethereum.AccountAbstraction.Bundler.RpcServer/README.md)** - Exposes bundler as JSON-RPC endpoint
- **Nethereum.AccountAbstraction.Bundler.InProcess** - `InProcessBundlerHost`, a devchain + bundler in one process
- **Nethereum.AccountAbstraction.Bundler.RocksDB** - `RocksDbUserOpMempool` and `RocksDbReputationStore`, both surviving a restart

### Dependencies
- **[Nethereum.AccountAbstraction](../Nethereum.AccountAbstraction/README.md)** - Core ERC-4337 types

## Additional Resources

- [ERC-4337: Account Abstraction](https://eips.ethereum.org/EIPS/eip-4337)
- [ERC-7562: Account Abstraction Validation Scope Rules](https://eips.ethereum.org/EIPS/eip-7562)
- [Nethereum Documentation](https://docs.nethereum.com)
