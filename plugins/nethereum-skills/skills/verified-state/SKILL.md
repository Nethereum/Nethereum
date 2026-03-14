---
name: verified-state
description: Help users verify Ethereum state (balances, nonces, storage, code) without trusting their RPC provider using beacon chain light client proofs. Use this skill whenever the user mentions trustless queries, verified balances, light client verification, proof verification, Merkle proofs for state, eth_getProof, UseVerifiedState, VerifiedStateService, or wants to verify data from an RPC provider they don't trust.
user-invocable: true
---

# Verified State Queries — Nethereum.ChainStateVerification

Verified state queries let your application cryptographically confirm that balances, nonces, contract code, and storage values returned by an RPC provider are genuine. Instead of trusting the RPC node, the data is verified against a beacon chain state root using Merkle proofs.

## When to Use This

- **Trustless balance checks**: Verify ETH balances before signing transactions or displaying in a wallet
- **Nonce verification**: Confirm transaction counts to prevent replay or stuck transactions
- **Contract code verification**: Ensure contract bytecode has not been tampered with by a malicious RPC
- **Storage proof verification**: Read and verify individual storage slots in smart contracts
- **Financial dashboards**: Display balances with cryptographic proof of correctness
- **Self-custodial wallets**: Eliminate trust in third-party RPC providers

## Required Packages

```bash
dotnet add package Nethereum.ChainStateVerification    # VerifiedStateService, interceptor
dotnet add package Nethereum.Consensus.LightClient     # Light client protocol
dotnet add package Nethereum.Beaconchain               # Beacon chain API client
dotnet add package Nethereum.Signer.Bls.Herumi         # BLS signature verification (native)
dotnet add package Nethereum.Web3                       # Standard Web3 client
```

You need two endpoints:
- **Beacon chain API URL** (e.g., `https://ethereum-beacon-api.publicnode.com`)
- **Execution RPC URL** that supports `eth_getProof`

## Full Initialization

```csharp
using Nethereum.Signer.Bls;
using Nethereum.Signer.Bls.Herumi;
using Nethereum.Beaconchain;
using Nethereum.Beaconchain.LightClient;
using Nethereum.Consensus.Ssz;
using Nethereum.Consensus.LightClient;
using Nethereum.ChainStateVerification;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.Hex.HexConvertors.Extensions;

// 1. Initialize BLS native library (required for sync committee signature verification)
var nativeBls = new NativeBls(new HerumiNativeBindings());
await nativeBls.InitializeAsync();

// 2. Connect to beacon chain and fetch finality checkpoint
var beaconClient = new BeaconApiClient("https://ethereum-beacon-api.publicnode.com");
var response = await beaconClient.LightClient.GetFinalityUpdateAsync();
var finalityUpdate = LightClientResponseMapper.ToDomain(response);
var weakSubjectivityRoot = finalityUpdate.FinalizedHeader.Beacon.HashTreeRoot();

// 3. Configure for Ethereum mainnet
var config = new LightClientConfig
{
    GenesisValidatorsRoot = "0x4b363db94e286120d76eb905340fdd4e54bfe9f06bf33ff6cf5ad27f511bfe95"
        .HexToByteArray(),
    CurrentForkVersion = new byte[] { 0x06, 0x00, 0x00, 0x00 },
    SlotsPerEpoch = 32,
    SecondsPerSlot = 12,
    WeakSubjectivityRoot = weakSubjectivityRoot
};

// 4. Create and initialize light client
var store = new InMemoryLightClientStore();
var lightClient = new LightClientService(
    beaconClient.LightClient, nativeBls, config, store);
await lightClient.InitializeAsync();

// 5. Build VerifiedStateService
var trustedProvider = new TrustedHeaderProvider(lightClient);
var rpcClient = new RpcClient(new Uri("https://mainnet.rpc.url"));
var ethGetProof = new EthGetProof(rpcClient);
var ethGetCode = new EthGetCode(rpcClient);
var trieVerifier = new TrieProofVerifier();

var verifiedStateService = new VerifiedStateService(
    trustedProvider, ethGetProof, ethGetCode, trieVerifier);
```

## The Simple Path: UseVerifiedState()

The recommended approach. Installs an interceptor on Web3 that transparently verifies `eth_getBalance`, `eth_getTransactionCount`, `eth_getCode`, and `eth_blockNumber` via Merkle proofs. All other RPC methods pass through unmodified.

```csharp
using Nethereum.ChainStateVerification.Interceptor;

var web3 = new Web3("https://mainnet.rpc.url");
web3.UseVerifiedState(verifiedStateService);

// Every balance/nonce/code query is now cryptographically verified
var balance = await web3.Eth.GetBalance.SendRequestAsync(address);
```

With configuration options:

```csharp
web3.UseVerifiedState(verifiedStateService, config =>
{
    config.Mode = VerificationMode.Finalized;   // Strongest guarantee
    config.FallbackOnError = true;              // Fall back to unverified if proof fails
});
```

Fluent chaining:

```csharp
var balance = await new Web3("https://mainnet.rpc.url")
    .UseVerifiedState(verifiedStateService, config =>
    {
        config.Mode = VerificationMode.Finalized;
        config.FallbackOnError = true;
    })
    .Eth.GetBalance.SendRequestAsync(address);
```

## Direct Queries

Call `VerifiedStateService` methods directly for more control:

```csharp
// Verified ETH balance
var balance = await verifiedStateService.GetBalanceAsync(address);

// Verified nonce
var nonce = await verifiedStateService.GetNonceAsync(address);

// Verified contract code
var code = await verifiedStateService.GetCodeAsync(contractAddress);

// Verified storage slot
var storageValue = await verifiedStateService.GetStorageAtAsync(contractAddress, BigInteger.Zero);
```

Each method fetches an `eth_getProof` response and verifies the Merkle proof against the trusted state root before returning.

## Finalized vs Optimistic Mode

| Mode | Security | Latency | Best For |
|------|----------|---------|----------|
| **Finalized** | Strongest -- economically final, cannot be reverted | ~12-15 min behind head | Financial operations, balance checks before transfers |
| **Optimistic** | Weaker -- based on attestations, theoretically revertible | Seconds behind head | UI display, dashboards, non-critical reads |

Switch to optimistic mode:

```csharp
await lightClient.UpdateFinalityAsync();
await lightClient.UpdateOptimisticAsync();

verifiedStateService.Mode = VerificationMode.Optimistic;
var balance = await verifiedStateService.GetBalanceAsync(address);
```

Use **finalized** for anything involving fund transfers. Use **optimistic** for display-only scenarios where freshness matters more than absolute finality.

## Caching

```csharp
verifiedStateService.EnableCaching = true;   // Cache verified results per block
verifiedStateService.ClearCache();           // Manual invalidation
```

Cache is automatically invalidated when the light client advances to a new block.

## Error Handling

```csharp
try
{
    var balance = await verifiedStateService.GetBalanceAsync(address);
}
catch (Nethereum.JsonRpc.Client.RpcResponseException ex)
{
    if (ex.Message.Contains("missing trie node") || ex.Message.Contains("proof window"))
    {
        // RPC node has pruned state for this block
        // Use optimistic mode or switch to an archive node
    }
}
```

## Common Gotchas

- **Archive node recommended for finalized mode** -- Standard nodes prune state beyond ~128 blocks. Finalized blocks are ~96 blocks behind head, near the pruning boundary.
- **BLS native library must be present** -- Ensure `bls_eth.dll` (Windows), `libbls_eth.so` (Linux), or `libbls_eth.dylib` (macOS) is in your output directory.
- **Fork version changes with network upgrades** -- `CurrentForkVersion` must match the current fork. Fetch dynamically from the beacon API for production use.
- **Optimistic is not final** -- Data verified in optimistic mode could theoretically change during a reorganization.
- **One interceptor per client** -- `UseVerifiedState` replaces any existing request interceptor on the Web3 client.

## Verified EVM Calls

For complex queries (e.g., ERC-20 `balanceOf`), combine verified state with the EVM simulator to run contract bytecode locally against verified storage:

```csharp
using Nethereum.ChainStateVerification.NodeData;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;

var nodeDataService = new VerifiedNodeDataService(verifiedStateService);
var executionStateService = new ExecutionStateService(nodeDataService);

var code = await verifiedStateService.GetCodeAsync(contractAddress);
var callInput = balanceOfFunction.CreateCallInput(contractAddress);
var programContext = new ProgramContext(callInput, executionStateService);
var program = new Program(code, programContext);
var evmSimulator = new EVMSimulator();
await evmSimulator.ExecuteAsync(program);
```

Every storage read during EVM execution is individually verified via Merkle proofs.

For full documentation, see: https://docs.nethereum.com/docs/consensus-light-client/guide-verified-state
