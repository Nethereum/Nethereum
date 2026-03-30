# Nethereum.PrivacyPools

.NET SDK for the [0xbow Privacy Pools](https://github.com/0xbow-io/privacy-pools-core) protocol. Deposit ETH or ERC20 tokens with Poseidon commitments, withdraw with ZK proofs (direct or via relayer), recover accounts from a mnemonic, manage ASP trees, and run a relay service — all in C#, cross-compatible with the 0xbow TypeScript SDK.

## Use Cases

### Create Account from Mnemonic

Derive deterministic master keys from a BIP-39 mnemonic. The same mnemonic always produces the same keys, enabling cross-device recovery.

```csharp
var account = new PrivacyPoolAccount(mnemonic);
// account.MasterNullifier — derived from m/44'/60'/0'/0/0
// account.MasterSecret    — derived from m/44'/60'/1'/0/0

var legacyAccount = PrivacyPoolAccount.CreateLegacy(mnemonic);
// Use only when recovering deposits created by older SDKs that derived
// master keys via JavaScript's lossy bytesToNumber() path.

var pp = PrivacyPool.FromDeployment(web3, deployment, account, legacyAccount);
// Pass both accounts when you want mnemonic-equivalent recovery without
// constructing PrivacyPool directly from the mnemonic.
```

<!-- Verified: AccountTests.Legacy_CreateAccount_FromMnemonic_DerivesMasterKeys -->
<!-- Verified: AccountTests.Safe_CreateAccount_FromMnemonic_DerivesMasterKeys -->

### Deposit with Deterministic Secrets

Generate deposit secrets offline, compute the precommitment, then deposit on-chain via the `PrivacyPool` facade.

```csharp
var pp = PrivacyPool.FromDeployment(web3, deployment, mnemonic);
await pp.InitializeAsync();

var depositValue = Web3.Convert.ToWei(1);
var result = await pp.DepositAsync(depositValue, depositIndex: 0);
// result.Commitment.CommitmentHash — on-chain commitment
// result.Receipt — transaction receipt
```

<!-- Verified: PrivacyPoolIntegrationTests.FullJourney_Deposit_Process_Recover_Ragequit -->

### Process Events

Use `PrivacyPoolLogProcessingService` with Nethereum's `BlockchainProcessor` to index deposit, withdrawal, ragequit, and leaf-insert events. Optionally pass a `PoseidonMerkleTree` to keep the state tree in sync automatically.

```csharp
var processingService = new PrivacyPoolLogProcessingService(web3, poolAddress);
var repository = new InMemoryPrivacyPoolRepository();
var stateTree = new PoseidonMerkleTree();

var processor = processingService.CreateProcessor(repository, stateTree);
await processor.ExecuteAsync(currentBlock, startAtBlockNumberIfNotProcessed: 0);

var deposits = await repository.GetDepositsAsync();
var leaves = await repository.GetLeavesAsync();
```

<!-- Verified: PrivacyPoolIntegrationTests.MultipleDeposits_ProcessAndRecover -->

### Recover Accounts

Given on-chain events, scan for deposits that match your mnemonic by brute-forcing the deposit index derivation path.

```csharp
var pp = PrivacyPool.FromDeployment(web3, deployment, mnemonic);
await pp.InitializeAsync();

var recovered = pp.RecoverAccounts(deposits, withdrawals, ragequits, leaves);
var spendable = pp.GetSpendableAccounts();
// Each PoolAccount tracks: Deposit, Withdrawals, SpendableValue,
// IsRagequitted, and IsMigrated

var safeOnly = PrivacyPool.FromDeployment(web3, deployment, new PrivacyPoolAccount(mnemonic));
await safeOnly.InitializeAsync();
var safeRecovered = safeOnly.RecoverSafeAccounts(deposits, withdrawals, ragequits, leaves);
// RecoverSafeAccounts intentionally scans only v1.2.0 safe-key deposits.
// RecoverAccounts requires a mnemonic or an explicit legacy companion account
// so migrated funds are not skipped.
```

<!-- Verified: PrivacyPoolIntegrationTests.FullJourney_Deposit_Process_Recover_Ragequit -->

### Ragequit with ZK Proof

Exit the pool by proving knowledge of the commitment preimage (nullifier + secret) via a Groth16 proof. Requires circuit artifacts from `Nethereum.PrivacyPools.Circuits` and a proof provider — choose one:

| Provider | Package | Runtime |
|----------|---------|---------|
| `SnarkjsProofProvider` | `Nethereum.ZkProofs.Snarkjs` | Node.js (CLI / server) |
| `SnarkjsBlazorProvider` | `Nethereum.ZkProofs.Snarkjs.Blazor` | Browser (Blazor WASM) |

```csharp
var circuitSource = new PrivacyPoolCircuitSource();

// CLI / server — requires Node.js installed
var proofProvider = pp.CreateProofProvider(new SnarkjsProofProvider(), circuitSource);

// Blazor WASM — runs in-browser via JS interop (snarkjs.min.mjs must be in wwwroot)
// var blazorProvider = new SnarkjsBlazorProvider(jsRuntime, "./js/snarkjs.min.mjs");
// await blazorProvider.InitializeAsync();
// var proofProvider = pp.CreateProofProvider(blazorProvider, circuitSource);

var spendable = pp.GetSpendableAccounts();
var ragequitResult = await pp.RagequitAsync(spendable[0], proofProvider);
// ragequitResult.Receipt — transaction receipt
```

<!-- Verified: PrivacyPoolIntegrationTests.FullJourney_Deposit_Process_Recover_Ragequit -->

### Tree Export/Import

Serialize the Merkle tree to JSON for caching, then resume processing from where you left off.

```csharp
// Export current tree state
var exported = tree.Export();

// Later: import and continue
var tree2 = PoseidonMerkleTree.Import(exported);
var processor = processingService.CreateProcessor(repository, tree2);
await processor.ExecuteAsync(currentBlock, startAtBlockNumberIfNotProcessed: lastBlock + 1);
```

<!-- Verified: PrivacyPoolIntegrationTests.TreeExportImport_IncrementalUpdate -->

### Deploy Full Stack

Deploy all contracts (Entrypoint, PrivacyPool, Verifiers, PoseidonT3/T4) in a single call.

```csharp
var deployment = await PrivacyPoolDeployer.DeployFullStackAsync(web3,
    new PrivacyPoolDeploymentConfig { OwnerAddress = ownerAddress });

// deployment.Entrypoint   — EntrypointService
// deployment.Pool         — PrivacyPoolSimpleService
// deployment.ProxyAddress — ERC1967 proxy address
```

<!-- Verified: all integration tests use PrivacyPoolDeployer.DeployFullStackAsync -->

### Relayer

Run a relay service that validates withdrawal proofs and submits transactions on behalf of users (preserving sender privacy).

```csharp
var verifier = new PrivacyPoolProofVerifier(withdrawalVkJson);
var relayer = new PrivacyPoolRelayer(web3, new RelayerConfig
{
    EntrypointAddress = entrypointAddress,
    PoolAddress = poolAddress,
    FeeReceiverAddress = feeReceiver
}, verifier);
await relayer.InitializeAsync();

var details = relayer.GetDetails();
// details.PoolAddress, details.FeeBps, details.MinWithdrawAmount

var result = await relayer.HandleRelayRequestAsync(request);
// result.IsSuccess, result.TransactionHash
```

<!-- Verified: PrivacyPoolIntegrationTests.Relayer_InitializesAndReportsDetails -->

### Local Proof Verification

Verify Groth16 proofs locally without on-chain calls using BN128 pairing checks.

```csharp
var verifier = new PrivacyPoolProofVerifier(withdrawalVkJson, ragequitVkJson);
var result = verifier.VerifyWithdrawalProof(proofJson, publicInputsJson);
// result.IsValid
```

<!-- Verified: ProofVerificationTests.PrivacyPoolProofVerifier_LoadsRealWithdrawalVk -->

### Embedded Circuit Artifacts

The companion `Nethereum.PrivacyPools.Circuits` package embeds WASM and zkey files as resources, so no file-system setup is needed.

```csharp
var source = new PrivacyPoolCircuitSource();
if (source.HasCircuit("commitment"))
{
    var wasm = await source.GetWasmAsync("commitment");
    var zkey = await source.GetZkeyAsync("commitment");
    var vkJson = source.GetVerificationKeyJson("commitment");
}
```

<!-- Verified: integration tests use PrivacyPoolCircuitSource -->

### Download Circuit Artifacts from URL

Alternatively, fetch circuit artifacts from a remote URL with automatic local caching via `UrlCircuitArtifactSource`. The built-in v1.2.0 artifact hash manifest is applied by default, so every downloaded artifact is integrity-checked before use.

```csharp
var source = new UrlCircuitArtifactSource(
    "https://example.com/circuits/v1",
    cacheDir: "./circuit-cache");
await source.InitializeAsync("commitment", "withdraw");

var proofProvider = new PrivacyPoolProofProvider(new SnarkjsProofProvider(), source);
```

### ERC20 Deposits

Deposit ERC20 tokens into a `PrivacyPoolComplex` pool. Approve the Entrypoint first, then deposit.

```csharp
var pp = PrivacyPool.FromDeployment(web3, deployment, mnemonic);
await pp.InitializeAsync();

await pp.ApproveERC20Async(tokenAddress, Web3.Convert.ToWei(1000));
var result = await pp.DepositERC20Async(tokenAddress, Web3.Convert.ToWei(100), depositIndex: 0);
// result.Commitment.CommitmentHash — on-chain commitment
```

<!-- Verified: CrossSdkTests.TsSdkERC20Deposit_NethereumRagequit, NethereumERC20Deposit_TsSdkRagequit -->

### Deploy ERC20 Pool

Deploy a full ERC20 privacy pool stack (Entrypoint + PrivacyPoolComplex + Verifiers + Poseidon libs).

```csharp
var deployment = await PrivacyPoolDeployer.DeployERC20FullStackAsync(web3,
    new PrivacyPoolERC20DeploymentConfig
    {
        OwnerAddress = ownerAddress,
        TokenAddress = erc20TokenAddress
    });
// deployment.Pool — PrivacyPoolComplexService (same interface as PrivacyPoolSimpleService)
```

### Direct Withdrawal

Withdraw directly from the pool contract without a relayer. The caller's address is `processooor` (visible on-chain).

```csharp
var withdrawResult = await pp.Pool.WithdrawDirectAsync(
    commitment, leafIndex, withdrawnValue, recipientAddress,
    proofProvider, stateTree, aspTree);
// withdrawResult.Receipt — transaction receipt
// withdrawResult.NewCommitment — change commitment
```

<!-- Verified: E2ETests.Direct_Withdrawal_Cycle -->

### ASP Tree Management

Build and manage the Association Set Provider (ASP) Merkle tree, publish roots on-chain, and generate inclusion proofs for withdrawals.

```csharp
var asp = pp.CreateASPTreeService();
asp.BuildFromDeposits(deposits);

await asp.PublishRootAsync("bafybei...");
var isPublished = await asp.IsRootPublishedAsync();

var (siblings, index) = asp.GenerateProof(label);
// siblings — padded to 32 elements for the withdrawal circuit
```

<!-- Verified: E2ETests.ASPTreeService_BuildPublishAndWithdraw -->

### One-Call Account Sync

Fetch all on-chain events, recover accounts, and build state + ASP trees in a single call.

```csharp
var pp = PrivacyPool.FromDeployment(web3, deployment, mnemonic);
await pp.InitializeAsync();

var sync = await pp.SyncFromChainAsync();
var safeOnly = PrivacyPool.FromDeployment(web3, deployment, new PrivacyPoolAccount(mnemonic));
await safeOnly.InitializeAsync();
var safeSync = await safeOnly.SyncSafeFromChainAsync();
// Safe-only sync skips legacy/migration recovery by design.
// sync.PoolAccounts — recovered accounts with spendable balances
// sync.StateTree — Merkle tree of all commitments
// sync.ASPTree — ASP tree built from deposit labels
// sync.Deposits — all deposit events
// sync.LastBlockProcessed — for incremental re-sync
```

## How Privacy Pools Work

### What is a Privacy Pool?

A Privacy Pool lets users deposit ETH into a smart contract and later withdraw it to a different address without revealing which deposit funded the withdrawal. Unlike a simple mixer, Privacy Pools add an **Association Set Provider (ASP)** layer — a public allow-list that lets users prove their deposit is "clean" without revealing which specific deposit is theirs.

The lifecycle:

1. **Deposit** — User sends ETH (or ERC20 tokens) with a Poseidon commitment (hiding their nullifier and secret)
2. **Accumulation** — The contract inserts each commitment into a LeanIncrementalMerkleTree
3. **Withdrawal** — User generates a ZK proof showing: "I know a commitment in this tree, it has value X, and it's in the ASP's approved set" — without revealing which leaf
4. **Ragequit** — Alternative exit that reveals the commitment (no privacy, but no ASP approval needed)

### Commitment Scheme

Privacy Pools use a three-layer Poseidon hash chain to create commitments:

```
Step 1 (T1): NullifierHash  = Poseidon(nullifier)              — public, used to prevent double-spend
Step 2 (T2): Precommitment  = Poseidon(nullifier, secret)      — sent to contract during deposit
Step 3 (T3): CommitmentHash = Poseidon(value, label, precommitment) — stored in Merkle tree
```

The `label` ties a commitment to a specific pool scope: `label = keccak256(scope, nonce) % SNARK_SCALAR_FIELD`.

The nullifier and secret are private — only the depositor knows them. The precommitment is public (sent with the deposit transaction), but it reveals nothing about the nullifier or secret individually because Poseidon is a one-way function.

### Merkle Tree

All commitments (deposits AND withdrawal change-commitments) are leaves in a `LeanIncrementalMerkleTree` using Poseidon T2 as the pair hash. This is the same tree structure used by the 0xbow TypeScript SDK.

To withdraw, the user must prove their commitment is in the tree by providing a Merkle inclusion proof (sibling hashes along the path from leaf to root). The tree supports up to 2^32 leaves.

### Circuits

Two circuits power the protocol:

- **Commitment circuit** (`commitment.circom`) — Proves knowledge of (value, label, nullifier, secret) that hash to a given CommitmentHash. Used for ragequit.
- **Withdrawal circuit** (`withdrawal.circom`) — Proves: (1) the user knows a commitment in the state tree, (2) the commitment is in the ASP's approved set, (3) the nullifier hasn't been used, and (4) the withdrawal amount is valid. Produces 8 public signals: new commitment hash, nullifier hash, withdrawn value, state root, state tree depth, ASP root, ASP tree depth, and context.

### SNARK Lifecycle

```
Circom circuit (.circom)
    ↓ compile
WASM witness generator + R1CS constraint system
    ↓ trusted setup (Powers of Tau + phase 2)
Proving key (.zkey) + Verification key (.vk.json)
    ↓ at runtime
Witness generation (WASM) → Proof generation (Groth16) → On-chain verification (BN128 pairing)
```

The `PrivacyPoolCircuitSource` class embeds the compiled WASM and zkey files. `SnarkjsProofProvider` (from `Nethereum.ZkProofs.Snarkjs`) generates proofs using Node.js + snarkjs. The on-chain verifier contract performs the Groth16 BN128 pairing check.

### ASP (Association Set Provider)

The ASP maintains a separate Merkle tree of "approved" commitments. When withdrawing, the user must prove their commitment exists in **both** the state tree and the ASP tree. This lets the pool operator (or a DAO) exclude commitments linked to illicit activity without breaking privacy for legitimate users.

The withdrawal proof includes both `stateRoot` + `ASPRoot`, along with separate sibling paths for each tree.

### Relayer

If a user withdraws directly, their `msg.sender` links the withdrawal to their address — defeating privacy. A **relayer** solves this by:

1. Receiving the user's proof + withdrawal data off-chain
2. Validating the proof locally (`PrivacyPoolProofVerifier`)
3. Submitting the transaction on-chain from the relayer's address
4. Taking a fee (configurable via `RelayerConfig.BaseFeeBps`)

The user's address never appears on-chain in the withdrawal transaction.

### Account Recovery

Privacy Pool accounts are deterministically derived from a BIP-39 mnemonic using HD wallet key paths:

```
MasterNullifier = Poseidon(BytesToBigInt(key at m/44'/60'/0'/0/0))
MasterSecret    = Poseidon(BytesToBigInt(key at m/44'/60'/1'/0/0))
```

Each deposit gets unique secrets via: `Poseidon(masterKey, scope, depositIndex)`. Given the mnemonic and on-chain events, the SDK tries each deposit index until it finds matching precommitments, recovering all accounts.

## Component Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                         User Layer                              │
│                                                                 │
│  PrivacyPool (facade)                                           │
│    ├── DepositAsync / DepositERC20Async — ETH and ERC20 deposits│
│    ├── WithdrawDirectAsync             — direct pool withdrawal │
│    ├── RagequitAsync                   — emergency exit         │
│    ├── SyncFromChainAsync              — one-call account sync  │
│    ├── CreateASPTreeService            — ASP tree factory       │
│    ├── PrivacyPoolAccount              — mnemonic key derivation│
│    └── PrivacyPoolAccountRecovery      — scan events for yours  │
│                                                                 │
├─────────────────────────────────────────────────────────────────┤
│                    Processing & ASP Layer                        │
│                                                                 │
│  PrivacyPoolLogProcessingService                                │
│    ├── BlockchainProcessor        — block-by-block event scan   │
│    ├── IPrivacyPoolRepository     — store decoded events        │
│    └── PoseidonMerkleTree         — auto-sync state tree        │
│                                                                 │
│  ASPTreeService                                                 │
│    ├── BuildFromDeposits          — build tree from labels      │
│    ├── PublishRootAsync           — push root to entrypoint     │
│    ├── GenerateProof              — inclusion proof for withdraw│
│    └── Export / Import            — persist tree state          │
│                                                                 │
├─────────────────────────────────────────────────────────────────┤
│                        Relayer Layer                             │
│                                                                 │
│  PrivacyPoolRelayer                                             │
│    ├── RelayerConfig              — fees, gas limits, addresses │
│    ├── PrivacyPoolProofVerifier   — validate before broadcast   │
│    └── IRelayRequestStore         — track request lifecycle     │
│                                                                 │
├─────────────────────────────────────────────────────────────────┤
│                      Cryptography Layer                          │
│                                                                 │
│  PrivacyPoolCommitment            — T1/T2/T3 Poseidon hash chain│
│  PoseidonMerkleTree               — LeanIMT with Poseidon T2   │
│  PrivacyPoolProofProvider         — witness gen + Groth16 prove │
│  PrivacyPoolProofConverter        — JSON proof → Solidity struct│
│  WithdrawalContextHelper          — compute withdrawal context  │
│  ICommitmentStore                 — track spent/unspent commits │
│  UrlCircuitArtifactSource         — fetch circuits from URL     │
│                                                                 │
├─────────────────────────────────────────────────────────────────┤
│                       Contract Layer                             │
│                                                                 │
│  PrivacyPoolDeployer              — deploy ETH or ERC20 stack  │
│  EntrypointService                — typed Entrypoint (UUPS proxy)│
│  PrivacyPoolSimpleService         — native ETH pool             │
│  PrivacyPoolComplexService        — ERC20 token pool            │
│  PrivacyPoolBase (shared types)   — DTOs, events, errors        │
│  WithdrawalVerifierService        — Groth16 on-chain verifier   │
└─────────────────────────────────────────────────────────────────┘
```

## Package Relationship

| Package | Purpose | Node.js Required |
|---------|---------|:---:|
| **Nethereum.PrivacyPools** | Core SDK: commitments, tree, accounts, deployer, relayer, event processing | No |
| **Nethereum.PrivacyPools.Circuits** | Embedded WASM/zkey/vk circuit artifacts via `PrivacyPoolCircuitSource` | No |
| **Nethereum.ZkProofs.Snarkjs** | Proof generation via Node.js snarkjs | Yes |
| **Nethereum.ZkProofs.Snarkjs.Blazor** | Browser-based proof generation via JS interop | No |
| **Nethereum.ZkProofsVerifier** | Local Groth16 BN128 proof verification | No |

## Commitment Scheme

```
                    ┌─────────────────┐
                    │    nullifier    │
                    └────────┬────────┘
                             │
              ┌──────────────┼──────────────┐
              │              │              │
              ▼              │              │
    ┌─────────────────┐      │              │
    │ Poseidon_T1     │      │              │
    │ (1 input)       │      │              │
    └────────┬────────┘      │              │
             │               │              │
             ▼               ▼              │
    ┌─────────────┐  ┌─────────────┐        │
    │NullifierHash│  │   secret    │        │
    └─────────────┘  └──────┬──────┘        │
                            │               │
              ┌─────────────┼───────────────┘
              │             │
              ▼             ▼
    ┌─────────────────────────────┐
    │ Poseidon_T2 (2 inputs)     │
    │ (nullifier, secret)        │
    └────────────┬───────────────┘
                 │
                 ▼
    ┌──────────────────┐
    │  Precommitment   │  ← sent to contract on deposit
    └────────┬─────────┘
             │
    ┌────────┼──────────────────────┐
    │        │                      │
    ▼        ▼                      ▼
┌───────┐┌──────────────┐  ┌─────────────┐
│ value ││precommitment │  │    label     │
└───┬───┘└──────┬───────┘  └──────┬──────┘
    │           │                 │
    ▼           ▼                 ▼
    ┌─────────────────────────────────────┐
    │ Poseidon_T3 (3 inputs)             │
    │ (value, label, precommitment)      │
    └──────────────────┬─────────────────┘
                       │
                       ▼
              ┌────────────────┐
              │ CommitmentHash │  ← stored as Merkle tree leaf
              └────────────────┘
```

## Cross-Compatibility with 0xbow TypeScript SDK

The C# and TypeScript implementations produce identical outputs for the same inputs. This is validated at three levels:

- **Unit tests** (`CrossCompatibilityTests`) — hardcoded value matching for master keys, deposit/withdrawal secrets, commitment hashes, and context hashes against the JavaScript SDK
- **E2E cross-SDK tests** (`CrossSdkTests`) — deposits made by the 0xbow TypeScript SDK are withdrawn/ragequitted by Nethereum, and vice versa, on a shared Geth dev chain with real Groth16 proof generation and on-chain verification. Covers both native ETH and ERC20 token flows

As of SDK v1.2.0, the TypeScript SDK derives master keys with `bytesToBigInt`, which matches `new PrivacyPoolAccount(mnemonic)` in C#. Older deposits created before that change used JavaScript's lossy `bytesToNumber()` path; Nethereum preserves compatibility for those historical accounts via `PrivacyPoolAccount.CreateLegacy(...)` and migration-aware recovery.

## Dependencies

- `Nethereum.Web3` — Ethereum RPC and transaction management
- `Nethereum.Merkle` — LeanIncrementalMerkleTree base
- `Nethereum.Util` — PoseidonHasher, PoseidonParameterPreset
- `Nethereum.ZkProofs` — `ICircuitArtifactSource`, `IZkProofProvider` interfaces
- `Nethereum.ZkProofsVerifier` — Groth16 BN128 pairing verification
- `Nethereum.BlockchainProcessing` — BlockchainProcessor for event indexing
