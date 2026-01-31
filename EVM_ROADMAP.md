# Nethereum EVM Strategic Roadmap

> **Living Document** - Updated as work progresses. Track completion, capture insights, identify reusable patterns.

---

## Quick Status

| Phase | Task | Status | Notes |
|-------|------|--------|-------|
| 0 | Prague EVM Implementation | ✅ COMPLETE | BN128, BLS12-381, KZG, EIP-2935, EIP-7623 |
| 1 | Extract TransactionExecutor | ✅ COMPLETE | 4-phase executor, simplified HardforkConfig |
| 2 | Long-Running Tests | 🔄 IN PROGRESS | 4 tests enabled, multi-fork Theory added |
| 3 | Wire Native Precompiles | 🔲 TODO | Connect Herumi/Ckzg |
| 4 | NetDapps Simulation | 🔲 TODO | Integration |
| 5 | CoreChain/DevChain DB | 🔲 TODO | Test vectors against DB |
| 6 | ERC-4337 Bundler | 🔲 TODO | Validation & simulation |

### Future Considerations (Out of Current Scope)
| Feature | Notes |
|---------|-------|
| Historical Replay | Hardfork-aware EVM for replaying old transactions accurately |
| Pre-Cancun Support | Berlin/London/Shanghai gas tables if needed |

---

## Phase 0: Prague Implementation ✅ COMPLETE

### Completed Items
- [x] BN128 Pairing (0x08) - Fp2/Fp6/Fp12 tower, OptimalAtePairing
- [x] Precompile Abstraction - IPrecompileProvider, IPrecompiledContractsExecution
- [x] BLS12-381 Precompiles - BlsPrecompileProvider + Herumi bindings
- [x] KZG Point Evaluation - KzgPrecompileProvider + Ckzg + trusted_setup.txt embedded
- [x] EIP-2935 BLOCKHASH - 8192 block window, system contract
- [x] EIP-7623 Calldata Floor - Token-based floor gas calculation
- [x] CREATE failure LastCallReturnData fix - All failure paths clear return data

### Test Results
- 56 passed, 4 skipped (long-running), 1 expected fail (Prague vectors missing)

### Key Files Modified
- `src/Nethereum.Signer/Crypto/BN128/` - Pairing implementation (Fp2, Fp6, Fp12, TwistPoint, OptimalAtePairing)
- `src/Nethereum.EVM/Execution/EvmPreCompiledContractsExecution.cs` - Precompile stubs
- `src/Nethereum.EVM/Execution/EvmBlockchainCurrentContractContextExecution.cs` - EIP-2935 BLOCKHASH
- `src/Nethereum.EVM/EVMSimulator.cs` - CREATE failure paths, LastCallReturnData clearing
- `src/Nethereum.EVM.Precompiles.Kzg/trusted_setup.txt` - Embedded KZG trusted setup

### Existing Libraries (Ready to Use)
| Library | Location | Purpose |
|---------|----------|---------|
| BouncyCastle | Dependency | BN128 G1 point arithmetic |
| Herumi MCL | `Nethereum.Signer.Bls.Herumi` | BLS12-381 operations |
| Ckzg.Bindings | `Nethereum.EVM.Precompiles.Kzg` | KZG point evaluation |

---

## Phase 1: Extract TransactionExecutor ✅ COMPLETE

### Goal
Create reusable transaction execution logic from GeneralStateTestRunner

### Important Note
`src/Nethereum.CoreChain/TransactionProcessor.cs` exists but is **less comprehensive**.
Missing: EIP-4844, EIP-7623, EIP-6780. The extracted TransactionExecutor should be the canonical implementation.

### Architecture - 4 Execution Phases

```
┌─────────────────────────────────────────────────────────────────┐
│                    TransactionExecutor                           │
├─────────────────────────────────────────────────────────────────┤
│  1. VALIDATION        │  2. STATE SETUP    │  3. EXECUTION      │
│  (lines 244-342)      │  (lines 335-481)   │  (lines 495-663)   │
│  - EIP-1559 parse     │  - Warm addresses  │  - EVMSimulator    │
│  - EIP-2681 nonce     │  - Access lists    │  - Value transfer  │
│  - EIP-3607 EOA       │  - Snapshot        │  - Code execution  │
│  - EIP-4844 blobs     │  - Collision check │  - EIP-3541 check  │
│  - EIP-7623 floor     │  (EIP-684+EIP-7610)│  - EIP-161 nonce   │
│  - EIP-3860 initcode  │                    │                    │
├─────────────────────┴────────────────────┴──────────────────────┤
│  4. POST-EXECUTION (lines 721-756)                               │
│  - Gas refunds (EIP-3529: max = gasUsed/5, only on success)     │
│  - EIP-7623 floor application                                    │
│  - EIP-6780 cleanup (delete created+self-destructed contracts)  │
│  - Coinbase reward: gasUsed * (effectiveGasPrice - baseFee)     │
│  - Sender refund: (gasLimit - gasUsed) * effectiveGasPrice      │
└─────────────────────────────────────────────────────────────────┘
```

### Validation Phase Details (lines 244-342)
- EIP-1559 vs Legacy transaction parsing
- `maxFeePerGas >= baseFee` validation
- `maxPriorityFeePerGas <= maxFeePerGas` validation
- Transaction gas limit vs block gas limit check
- Intrinsic gas with EIP-3860 initcode limits (49152 bytes max)
- EIP-7623 (Prague): Floor gas limit enforcement
- EIP-2681: Nonce overflow protection (nonce < 2^64-1)
- EIP-3607: Sender must be EOA (reject if sender has code)
- EIP-4844: Blob transaction validation (Type-3)
  - Blob count limits (6 Cancun, 9 Prague)
  - Versioned hash validation (0x01 prefix)

### State Setup Phase Details (lines 335-481)
- ExecutionStateService initialization with pre-state
- Warm address marking: sender, coinbase (EIP-3651), precompiles (0x01-0x09)
- Access list processing (EIP-2930)
- Account creation for all warm addresses
- **Critical**: Snapshot taken AFTER sender nonce increment + gas payment
- Contract collision detection (EIP-684 + EIP-7610)

### Execution Phase Details (lines 495-663)
- Program context creation with block parameters
- EVMSimulator execution with `EnforceGasSentry = true`
- Value transfer (sender → receiver/contract)
- EIP-3541: Reject deployed code starting with 0xEF
- EIP-161: Contract nonce = 1 before initcode runs

### Post-Execution Phase Details (lines 721-756)
- Gas refund calculation (EIP-3529): `maxRefund = gasUsed / 5`
- `effectiveGasUsed = gasUsed - min(RefundCounter, maxRefund)`
- EIP-7623 floor application: `finalGas = max(effectiveGasUsed, floorGas)`
- EIP-6780 (Cancun): Delete contracts that were BOTH created AND self-destructed in same TX

### New Files to Create
```
src/Nethereum.EVM/
├── TransactionExecutor.cs              # Core execution (4 phases)
├── TransactionExecutionResult.cs       # Result: gasUsed, success, logs, state
├── TransactionExecutionContext.cs      # Context passed between phases
├── HardforkConfig.cs                   # Feature flags per hardfork
└── Gas/
    └── IntrinsicGasCalculator.cs       # All gas calculations
```

### Key Interfaces
```csharp
public class TransactionExecutor
{
    public TransactionExecutor(EVMSimulator simulator, HardforkConfig config);

    public async Task<TransactionExecutionResult> ExecuteAsync(
        SignedTransaction transaction,
        ExecutionStateService state,
        BlockContext block);
}

public class TransactionExecutionResult
{
    public BigInteger GasUsed { get; set; }
    public bool Success { get; set; }
    public byte[] ReturnData { get; set; }
    public string RevertReason { get; set; }
    public List<LogEntry> Logs { get; set; }
    public byte[] StateRoot { get; set; }
    public BigInteger RefundAmount { get; set; }
}

public class HardforkConfig
{
    public bool EnableEIP1559 { get; set; }      // London
    public bool EnableEIP3860 { get; set; }      // Shanghai (initcode limits)
    public bool EnableEIP4844 { get; set; }      // Cancun (blobs)
    public bool EnableEIP7623 { get; set; }      // Prague (floor gas)
    public int MaxBlobsPerBlock { get; set; }    // 6 Cancun, 9 Prague
    public int MaxInitcodeSize { get; set; }     // 49152 (EIP-3860)

    public static HardforkConfig Cancun => new()
    {
        EnableEIP1559 = true,
        EnableEIP3860 = true,
        EnableEIP4844 = true,
        EnableEIP7623 = false,
        MaxBlobsPerBlock = 6,
        MaxInitcodeSize = 49152,
    };

    public static HardforkConfig Prague => new()
    {
        EnableEIP1559 = true,
        EnableEIP3860 = true,
        EnableEIP4844 = true,
        EnableEIP7623 = true,
        MaxBlobsPerBlock = 9,
        MaxInitcodeSize = 49152,
    };
}
```

### Gas Calculation Methods to Extract
```csharp
public static class IntrinsicGasCalculator
{
    // Base: 21000 + 32000 (contract creation)
    // Initcode gas: ceil(data.length / 32) * 2 per EIP-3860
    // Data gas: 4 per zero byte, 16 per non-zero byte
    // Access list: 2400 per address + 1900 per storage key
    public static BigInteger CalculateIntrinsicGas(
        byte[] data, bool isContractCreation,
        List<AccessListItem> accessList, HardforkConfig config);

    // Floor = 21000 + (10 * tokens) + (32000 if creation)
    // Tokens = zero_bytes + (non_zero_bytes * 4)
    public static BigInteger CalculateFloorGasLimit(
        byte[] data, bool isContractCreation);

    // EIP-4844 blob base fee using FakeExponential
    // MIN_BASE_FEE_PER_BLOB_GAS = 1
    // BLOB_BASE_FEE_UPDATE_FRACTION = 3338477
    public static BigInteger CalculateBlobBaseFee(BigInteger excessBlobGas);

    // Tokens = zero_bytes + (non_zero_bytes * 4)
    public static BigInteger CalculateTokensInCalldata(byte[] data);
}
```

### Files Modified
- [x] `tests/.../GeneralStateTestRunner.cs` - Added TransactionExecutor-based methods

### Tasks
- [x] Create `TransactionExecutor.cs` ✅ 2026-01-31
- [x] Create `TransactionExecutionResult.cs` ✅ 2026-01-31
- [x] Create `TransactionExecutionContext.cs` ✅ 2026-01-31
- [x] Create `HardforkConfig.cs` ✅ 2026-01-31
- [x] Create `Gas/IntrinsicGasCalculator.cs` ✅ 2026-01-31
- [x] Add `RunTestWithExecutorAsync` to GeneralStateTestRunner ✅ 2026-01-31
- [x] Add comparison tests (stChainId, stExample) ✅ 2026-01-31
- [x] Run broader comparison tests against more categories ✅ 2026-01-31
- [x] Verify all 56 tests still pass with both implementations ✅ 2026-01-31

### Verification
```bash
cd tests/Nethereum.EVM.UnitTests
dotnet test --filter "RunSpecificCategory" --no-build -v normal
# All 56 tests must pass after refactor
```

---

## Phase 2: Long-Running Tests & Cleanup 🔄 IN PROGRESS

### Goal
Enable and run comprehensive tests, fix Cancun-only tests

### Previously Skipped Tests - NOW ENABLED
All previously skipped long-running tests now pass:

| Test | Duration | Status |
|------|----------|--------|
| stQuadraticComplexityTest | 4m 24s | ✅ PASSED |
| stTimeConsuming | 13m 52s | ✅ PASSED |
| stMemoryStressTest | 235ms | ✅ PASSED |
| stAttackTest | 13s | ✅ PASSED (was marked "HANGING") |

### Multi-Fork Testing Results (using TransactionExecutor)
| Category | Cancun | Prague |
|----------|--------|--------|
| stChainId | ✅ | ✅ |
| stExample | ✅ | ✅ |
| stSLoadTest | ✅ | ✅ |
| stSStoreTest | ✅ | ✅ |
| stCallCodes | ✅ | ✅ |
| stCreate2 | ✅ | ✅ |
| stCreateTest | ✅ | ✅ |
| stMemoryTest | ✅ | ✅ |
| stTransactionTest | ✅ | ✅ |
| stPreCompiledContracts | ❌ 126 failures | ✅ |

**Total: 19 passed, 1 failed** (precompile detection needs hardfork awareness)

### FIXED: Precompile Warming by Hardfork
Added `MaxPrecompileAddress` to `HardforkConfig`:
- Cancun: 10 (0x01-0x0A) - includes KZG point evaluation
- Prague: 17 (0x01-0x11) - includes BLS12-381

This fixed SELFDESTRUCT cold address cost issues in stCreate2 and stCreateTest.

### Remaining Issue: Precompile Detection
`stPreCompiledContracts_MultiFork(Cancun)` fails 126 tests because `IsPrecompiledAddress`
hardcodes 0x0B-0x11 as precompiles regardless of hardfork. Requires making precompile
detection hardfork-aware (deeper change, deferred).

### Tasks
- [x] Enable stAttackTest ✅ 2026-01-31
- [x] Enable stQuadraticComplexityTest ✅ 2026-01-31
- [x] Enable stTimeConsuming ✅ 2026-01-31
- [x] Enable stMemoryStressTest ✅ 2026-01-31
- [x] Add multi-fork Theory tests (Cancun + Prague) ✅ 2026-01-31
- [x] Fix precompile warming per hardfork (MaxPrecompileAddress) ✅ 2026-01-31
- [x] Switch tests to use TransactionExecutor ✅ 2026-01-31
- [ ] Make `IsPrecompiledAddress` hardfork-aware (stPreCompiledContracts Cancun)
- [ ] Add Prague test vectors when available

### Verification
```bash
# Run ALL tests including long-running
cd tests/Nethereum.EVM.UnitTests
dotnet test --filter "RunAllGeneralStateTests" -v normal
```

---

## Phase 3: Wire Native Precompiles 🔲 TODO

### Goal
Connect BLS12-381 (Herumi) and KZG (Ckzg) to test runner for full precompile testing

### Existing Components (Ready to Wire)
| Component | Location | Status |
|-----------|----------|--------|
| `BlsPrecompileProvider` | `Nethereum.EVM.Precompiles.Bls` | ✅ Implemented |
| `Bls12381Operations` | `Nethereum.Signer.Bls.Herumi` | ✅ Implemented |
| `IBls12381Operations` | `Nethereum.Signer.Bls` | ✅ Interface |
| `KzgPrecompileProvider` | `Nethereum.EVM.Precompiles.Kzg` | ✅ Implemented |
| `CkzgOperations` | `Nethereum.EVM.Precompiles.Kzg` | ✅ Implemented |
| `IKzgOperations` | `Nethereum.EVM.Precompiles.Kzg` | ✅ Interface |
| `trusted_setup.txt` | Embedded resource | ✅ Added |

### BLS12-381 Precompile Addresses (EIP-2537)
| Address | Operation | Gas Cost |
|---------|-----------|----------|
| 0x0B | G1ADD | 375 |
| 0x0C | G1MSM | Variable (discount table) |
| 0x0D | G2ADD | 600 |
| 0x0E | G2MSM | Variable (discount table) |
| 0x0F | PAIRING | 37700 + 32600*k |
| 0x10 | MAP_FP_TO_G1 | 5500 |
| 0x11 | MAP_FP2_TO_G2 | 23800 |

### Integration Code
```csharp
// Initialize native operations
var blsOps = new Bls12381Operations();
CkzgOperations.InitializeFromEmbeddedSetup();
var kzgOps = new CkzgOperations();

// Create providers
var blsProvider = new BlsPrecompileProvider(blsOps);
var kzgProvider = new KzgPrecompileProvider(kzgOps);

// May need: CompositePrecompileProvider to chain multiple providers
var compositeProvider = new CompositePrecompileProvider(blsProvider, kzgProvider);

// Inject into EVM
var precompiles = new EvmPreCompiledContractsExecution(compositeProvider);
var evmSimulator = new EVMSimulator(new EvmProgramExecution(precompiles));
```

### Tasks
- [ ] Create `CompositePrecompileProvider` (chains multiple IPrecompileProvider)
- [ ] Wire providers in `GeneralStateTestRunner`
- [ ] Add tests for BLS12-381 operations
- [ ] Add tests for KZG point evaluation
- [ ] Verify precompile gas costs match EIP specs

### New Files
- [ ] `src/Nethereum.EVM/Execution/CompositePrecompileProvider.cs`

### Verification
```bash
# BLS12-381 and KZG precompile tests should pass
cd tests/Nethereum.EVM.UnitTests
dotnet test --filter "stPreCompiledContracts" --no-build -v normal
```

---

## Phase 4: NetDapps Simulation Integration 🔲 TODO

### Goal
Ensure NetDapps simulation uses the new TransactionExecutor

### Key Files to Investigate
- `src/NetDapps/Services/` - Simulation services
- Look for existing EVM simulation usage

### Tasks
- [ ] Identify current simulation implementation in NetDapps
- [ ] Replace with TransactionExecutor
- [ ] Add integration tests
- [ ] Verify consistency with GeneralStateTests

### Verification
Integration tests verifying simulation against known transactions

---

## Phase 5: CoreChain/DevChain Database Integration 🔲 TODO

### Goal
Run test vectors against database for full integration testing (full circle)

### Architecture
```
Test Vectors (JSON)
    ↓
TransactionExecutor (from Phase 1)
    ↓
ExecutionStateService
    ↓
Database (RocksDB / InMemory)
    ↓
State Root Comparison
```

### Key Projects
- `src/Nethereum.CoreChain/`
- `src/Nethereum.DevChain/`
- `src/Nethereum.AppChain/`
- `src/Nethereum.CoreChain.RocksDB/`

### Tasks
- [ ] Create test infrastructure to load state into database
- [ ] Execute transactions via CoreChain/DevChain
- [ ] Compare final state roots with expected values
- [ ] Full circle: set data → execute → verify state

### Verification
```bash
cd tests/Nethereum.CoreChain.IntegrationTests
dotnet test -v normal
# State root comparisons should match test vectors
```

---

## Phase 6: ERC-4337 Bundler with EVM Validation 🔲 TODO

### Goal
Complete 4337 bundler using EVM for validation and simulation

### Components
1. **UserOperation Validation** - EVM validates UserOps
2. **Gas Estimation** - Simulate execution for gas estimates
3. **Bundler Integration** - Connect to AccountAbstraction services

### Key Projects
- `src/Nethereum.AccountAbstraction/`
- `src/Nethereum.AccountAbstraction.Bundler/`
- `tests/Nethereum.AccountAbstraction.IntegrationTests/`

### Tasks
- [ ] Wire TransactionExecutor into bundler for simulation
- [ ] Implement EIP-7562 validation rules
- [ ] Gas estimation using EVM simulation
- [ ] Integration tests for bundler flow

### Verification
```bash
cd tests/Nethereum.AccountAbstraction.IntegrationTests
dotnet test -v normal
```

---

## Implementation Order & Dependencies

| Phase | Task | Dependencies | Blocks |
|-------|------|--------------|--------|
| 1 | Extract TransactionExecutor | None | 2, 3, 4, 5, 6 |
| 2 | Long-running tests & cleanup | Phase 1 | None |
| 3 | Wire native precompiles | Phase 1 | None |
| 4 | NetDapps simulation integration | Phase 1 | 5 |
| 5 | CoreChain/DevChain DB integration | Phase 1, 4 | 6 |
| 6 | ERC-4337 Bundler | Phase 1, 5 | None |

---

## Files Summary

### Phase 1 - New Files
- `src/Nethereum.EVM/TransactionExecutor.cs`
- `src/Nethereum.EVM/TransactionExecutionResult.cs`
- `src/Nethereum.EVM/TransactionExecutionContext.cs`
- `src/Nethereum.EVM/HardforkConfig.cs`
- `src/Nethereum.EVM/Gas/IntrinsicGasCalculator.cs`

### Phase 1 - Modified Files
- `tests/Nethereum.EVM.UnitTests/GeneralStateTests/GeneralStateTestRunner.cs`

### Phase 3 - New Files
- `src/Nethereum.EVM/Execution/CompositePrecompileProvider.cs`

### Phase 3 - Modified Files
- `tests/Nethereum.EVM.UnitTests/GeneralStateTests/GeneralStateTestRunner.cs`

---

## Reusable Patterns & Skills

### Pattern: EVM Debug Workflow (`/evm-debug` skill)
1. Run test, read FIRST MISMATCH output
2. Identify opcode and gas values
3. Search ALL code paths for the opcode
4. Trace execution flow (calculate → capture → deduct → execute)
5. Compare with Geth source
6. Fix and verify

### Pattern: Precompile Provider Injection
```csharp
// Create provider implementing IPrecompileProvider
public class MyProvider : IPrecompileProvider
{
    public bool CanHandle(string address) => ...;
    public BigInteger GetGasCost(string address, byte[] data) => ...;
    public byte[] Execute(string address, byte[] data) => ...;
}

// Inject into EVM
var precompiles = new EvmPreCompiledContractsExecution(myProvider);
var simulator = new EVMSimulator(new EvmProgramExecution(precompiles));
```

### Pattern: Hardfork Feature Flags
```csharp
public class HardforkConfig
{
    public static HardforkConfig Cancun => new()
    {
        EnableEIP4844 = true,
        MaxBlobsPerBlock = 6,
    };

    public static HardforkConfig Prague => new()
    {
        EnableEIP4844 = true,
        EnableEIP7623 = true,
        MaxBlobsPerBlock = 9,
    };
}
```

### Pattern: CREATE Failure Handling
Always clear `LastCallReturnData = null` on ANY CREATE failure path.
This ensures RETURNDATASIZE returns 0 after failed CREATE.
Failure paths: memory overflow, max initcode, max depth, EIP-3541, insufficient balance, nonce limit, collision.

### Pattern: Snapshot Timing
**Critical**: Snapshot must be taken AFTER nonce increment and gas payment.
This ensures nonce persists even if transaction fails.
```csharp
// 1. Increment sender nonce
senderAccount.Nonce = senderNonceBeforeIncrement + 1;
// 2. Deduct gas payment
senderAccount.Balance.UpdateExecutionBalance(-(gasLimit * effectiveGasPrice));
// 3. THEN take snapshot
var snapshotId = executionState.TakeSnapshot();
// 4. Execute...
// 5. On failure: RevertToSnapshot (but nonce/gas payment persists!)
```

---

## Important Insights

### Insight 1: Gas Calculation Complexity (EIP-7623)
EIP-7623 introduces a "floor" mechanism:
- Standard intrinsic gas: 4 per zero byte, 16 per non-zero byte
- Token count: 1 per zero byte, 4 per non-zero byte
- Floor = 21000 + 10*tokens + (32000 if contract creation)
- Final gas = max(execution_gas, floor)

### Insight 2: EIP-6780 Self-Destruct Cleanup
Post-Cancun: Only delete contracts that were BOTH created AND self-destructed
in the SAME transaction.
```csharp
var createdSet = new HashSet<string>(program.ProgramResult.CreatedContractAccounts);
var deletedSet = program.ProgramResult.DeletedContractAccounts;
var toDelete = deletedSet.Where(d => createdSet.Contains(d));
```

### Insight 3: EIP-4844 Blob Limits
- Cancun: Max 6 blobs per block
- Prague: Max 9 blobs per block
- Blob gas: 131072 per blob
- Versioned hash: Must start with 0x01

### Insight 4: EIP-3860 Initcode Limits
- Max initcode size: 49152 bytes (2 * MAX_CODE_SIZE)
- Initcode word gas: 2 per 32-byte word
- Applied to CREATE/CREATE2 and contract creation transactions

### Insight 5: EIP-3651 Coinbase Warm
Shanghai+: Coinbase address is warm at transaction start.
```csharp
if (!string.IsNullOrEmpty(env.CurrentCoinbase))
{
    executionState.MarkAddressAsWarm(env.CurrentCoinbase);
}
```

---

## Commands Reference

### Run All Category Tests
```bash
cd tests/Nethereum.EVM.UnitTests
dotnet test --filter "RunSpecificCategory" --no-build -v normal
```

### Run Specific Category
```bash
dotnet test --filter "RunSpecificCategory_stZeroKnowledge" --no-build -v normal
```

### Run Single Test with Debug Output
```bash
dotnet test --filter "TestName" --no-build -v normal 2>&1 | grep -A 50 "FIRST MISMATCH"
```

### Build Without Restore
```bash
dotnet build --no-restore
```

### Run CoreChain Integration Tests
```bash
cd tests/Nethereum.CoreChain.IntegrationTests
dotnet test -v normal
```

### Run AccountAbstraction Tests
```bash
cd tests/Nethereum.AccountAbstraction.IntegrationTests
dotnet test -v normal
```

---

## Changelog

### 2026-01-31 (Phase 2 Progress)
- **Long-Running Tests Enabled**: All 4 previously skipped tests now pass
  - stQuadraticComplexityTest (4m 24s)
  - stTimeConsuming (13m 52s)
  - stMemoryStressTest (235ms)
  - stAttackTest (13s - was marked "HANGING" but works fine)
- **Multi-Fork Theory Tests Added**: Test both Cancun and Prague in single test run
  - Added `RunCategoryAsync(category, hardfork)` overload
  - Created 10 Theory tests with `[InlineData("Cancun")]` and `[InlineData("Prague")]`
- **Switched to TransactionExecutor**: Tests now use the new executor infrastructure
- **Fixed Precompile Warming**: Added `MaxPrecompileAddress` to `HardforkConfig`
  - Cancun: 10 (0x01-0x0A)
  - Prague: 17 (0x01-0x11)
  - Fixed SELFDESTRUCT cold address cost issues
- **Multi-Fork Results**: 19/20 tests pass (only stPreCompiledContracts Cancun fails)
- **Remaining Issue**: `IsPrecompiledAddress` needs hardfork awareness for full Cancun support

### 2026-01-31 (Phase 1 Complete)
- **Phase 1 COMPLETE**: TransactionExecutor extraction finished
  - All 56 GeneralStateTests pass (1 expected fail for missing Prague vectors, 4 skipped long-running)
  - Simplified HardforkConfig to only supported flags: `EnableEIP4844`, `EnableEIP7623`, `MaxBlobsPerBlock`
  - Original implementation preserved in GeneralStateTestRunner for reference
  - TransactionExecutor is now the canonical implementation for 4-phase transaction execution

### 2026-01-31 (Session 2)
- **Phase 1 Started**: Created TransactionExecutor infrastructure
  - `src/Nethereum.EVM/TransactionExecutor.cs` - 4-phase transaction execution
  - `src/Nethereum.EVM/TransactionExecutionResult.cs` - Result structure with logs, state root
  - `src/Nethereum.EVM/TransactionExecutionContext.cs` - Context passed between phases
  - `src/Nethereum.EVM/HardforkConfig.cs` - Berlin/London/Shanghai/Cancun/Prague presets
  - `src/Nethereum.EVM/Gas/IntrinsicGasCalculator.cs` - All gas calculations
- **Integrated into GeneralStateTestRunner** (original implementation preserved):
  - Added `RunTestWithExecutorAsync()` public method
  - Added `RunSingleTestWithExecutorAsync()` private method
  - Added `BuildExecutionContext()` helper to build context from test data
- **Added comparison tests** to verify executor matches original:
  - `TransactionExecutor_stChainId_MatchesOriginal` - PASSED
  - `TransactionExecutor_stExample_MatchesOriginal` - PASSED
- Build verified: 0 errors
- All original tests still pass (stChainId, stExample, stSLoadTest verified)
- **Simplified HardforkConfig** - Reduced from 13 flags to 3:
  - Removed fake flags (EVM is hardcoded to post-Berlin behavior)
  - Kept only: `EnableEIP4844`, `EnableEIP7623`, `MaxBlobsPerBlock`
  - Only Cancun/Prague presets (what we actually support)
  - Singletons with `init` properties for immutability
- **Design Decision**: Historical replay (hardfork-aware EVM) marked as future scope

### 2026-01-31 (Session 1)
- Created initial roadmap
- Phase 0 (Prague) marked complete
- Added trusted_setup.txt to KZG project (embedded resource)
- All 56 GeneralStateTests passing
- Merged detailed interface definitions from plan file
- Added comprehensive EIP documentation per phase
- Added gas calculation method signatures
- Added reusable patterns section
