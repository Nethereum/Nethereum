# Nethereum.ChainStateVerification

**Nethereum.ChainStateVerification** provides verified execution-state primitives using cryptographic proofs rooted in consensus-layer light client state. It enables trustless querying of account balances, storage, code, and nonces with merkle proof verification against finalized or optimistic headers.

## Overview

This package allows applications to query Ethereum state (accounts, storage, code) from untrusted RPC nodes while cryptographically verifying responses against a trusted state root provided by a light client. This enables:

- **Trustless RPC access** - Verify RPC responses without trusting the node
- **Light client integration** - Use consensus layer finality for execution layer state
- **Proof-based verification** - Merkle Patricia Trie proofs (`eth_getProof`)
- **Caching** - Thread-safe verified state caching for performance
- **Fault tolerance** - Detect tampering or stale data from RPC nodes

## Installation

```bash
dotnet add package Nethereum.ChainStateVerification
```

## Key Concepts

### Trust Model

Traditional Ethereum clients must trust RPC nodes to provide correct state. This package enables verification:

1. **Light Client** provides trusted state root from consensus layer (finalized or optimistic)
2. **RPC Node** provides state values + merkle proofs (`eth_getProof`)
3. **Verifier** checks proofs against trusted state root
4. **Result** is either verified state or `InvalidChainDataException`

### Verification Modes

**Finalized Mode** (default):
- Uses finalized consensus state (2/3 validators)
- ~12-15 minutes behind chain tip
- Maximum security

**Optimistic Mode**:
- Uses optimistic consensus state (67% sync committee)
- ~12 seconds behind chain tip
- Higher freshness, lower security

### Merkle Patricia Trie Proofs

Ethereum stores state in a Merkle Patricia Trie:
- **State Root** commits to all account states
- **Storage Root** (per account) commits to all storage slots
- **Proof** provides branch from root to specific value
- **Verification** validates proof matches root

Located in `TrieProofVerifier.cs:14-47`

## Core Components

### IVerifiedStateService

Main interface for verified state queries. Located in `IVerifiedStateService.cs:8-25`.

**Methods:**
- `GetAccountAsync(string address)` - Get full account (balance, nonce, codeHash, storageRoot)
- `GetBalanceAsync(string address)` - Get account balance
- `GetNonceAsync(string address)` - Get account nonce
- `GetCodeAsync(string address)` - Get contract bytecode with hash verification
- `GetCodeHashAsync(string address)` - Get contract code hash
- `GetStorageAtAsync(string address, BigInteger position)` - Get storage value at slot
- `GetStorageAtAsync(string address, string slotHex)` - Get storage value at hex slot
- `GetBlockHash(ulong blockNumber)` - Get block hash from light client
- `GetCurrentHeader()` - Get current trusted header (finalized or optimistic)

**Properties:**
- `Mode` - `VerificationMode.Finalized` or `VerificationMode.Optimistic`

### VerifiedStateService

Implementation of `IVerifiedStateService`. Located in `VerifiedStateService.cs:15-265`.

**Constructor:**
```csharp
public VerifiedStateService(
    ITrustedHeaderProvider headerProvider,  // Light client header provider
    IEthGetProof getProof,                  // RPC eth_getProof
    IEthGetCode getCode,                    // RPC eth_getCode
    ITrieProofVerifier proofVerifier       // Merkle proof verifier
)
```

**Properties:**
- `Mode` - Verification mode (default: `VerificationMode.Finalized`)
- `EnableCaching` - Enable caching (default: `true`)
- `VerifyCodeHash` - Verify code hash matches account (default: `true`)

**Methods:**
- `ClearCache()` - Clear verified state cache
- `Dispose()` - Dispose resources

### TrieProofVerifier

Verifies Merkle Patricia Trie proofs. Located in `TrieProofVerifier.cs:12-48`.

**Methods:**
- `VerifyAccountProof(byte[] stateRoot, AccountProof accountProof)` - Verifies account proof against state root
- `VerifyStorageProof(Account account, StorageProof storageProof)` - Verifies storage proof against account storage root

Throws `InvalidChainDataException` if proof is invalid.

### VerifiedStateCache

Thread-safe cache for verified state. Located in `Caching/VerifiedStateCache.cs:9-233`.

**Properties:**
- `BlockNumber` - Current cached block
- `StateRoot` - Current cached state root

**Methods:**
- `SetBlock(ulong blockNumber, byte[] stateRoot)` - Set block (clears cache if changed)
- `TryGetAccount(string address, out VerifiedAccountState state)` - Get cached account
- `SetAccount(string address, Account account)` - Cache account
- `TryGetCode(string address, out byte[] code)` - Get cached code
- `SetCode(string address, byte[] code)` - Cache code
- `TryGetStorage(string address, string slotHex, out byte[] value)` - Get cached storage
- `SetStorage(string address, string slotHex, byte[] value)` - Cache storage
- `Clear()` - Clear all cache

Cache is automatically cleared when block changes.

### VerifiedNodeDataService

Adapter that implements `INodeDataService` (from `Nethereum.EVM.BlockchainState`) backed by verified state. This allows the EVM simulator to use proof-verified data for balance, code, storage, nonce, and block hash lookups. Located in `NodeData/VerifiedNodeDataService.cs:9-81`.

**Constructor:**
```csharp
public VerifiedNodeDataService(IVerifiedStateService verifiedState)
```

**Implements:**
- `GetBalanceAsync(string address)` - Verified balance via merkle proof
- `GetCodeAsync(string address)` - Verified contract code with hash check
- `GetStorageAtAsync(string address, BigInteger position)` - Verified storage slot
- `GetTransactionCount(string address)` - Verified nonce
- `GetBlockHashAsync(BigInteger blockNumber)` - Block hash from light client (within 256-block window)

### VerificationMode

Enum for verification mode. Located in `VerificationMode.cs:3-8`.

```csharp
public enum VerificationMode
{
    Finalized,   // Use finalized header (~12-15 min behind)
    Optimistic   // Use optimistic header (~12 sec behind)
}
```

### InvalidChainDataException

Exception thrown when proof verification fails. Located in `InvalidChainDataException.cs:5-19`.

Indicates RPC node returned tampered or invalid data.

## Usage Examples

### Example 1: Basic Verified Balance Query

```csharp
using Nethereum.ChainStateVerification;
using Nethereum.Consensus.LightClient;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;

// Setup light client (provides trusted state root)
var lightClient = await CreateLightClientAsync(); // See Nethereum.Consensus.LightClient
var trustedProvider = new TrustedHeaderProvider(lightClient);

// Setup RPC client (untrusted)
var rpcClient = new RpcClient(new Uri("https://mainnet.infura.io/v3/YOUR_KEY"));
var ethGetProof = new EthGetProof(rpcClient);
var ethGetCode = new EthGetCode(rpcClient);

// Setup verifier
var trieVerifier = new TrieProofVerifier();

// Create verified state service
var verifiedState = new VerifiedStateService(
    trustedProvider,
    ethGetProof,
    ethGetCode,
    trieVerifier
);

// Query balance with proof verification
var address = "0x742d35Cc6634C0532925a3b844Bc9e7595f0bEb";
var balance = await verifiedState.GetBalanceAsync(address);

Console.WriteLine($"Verified balance: {balance} wei");
// Balance is cryptographically verified against light client state root
```

From: `VerifiedStateService.cs:98-102`

### Example 2: Verified Storage Query

```csharp
using Nethereum.ChainStateVerification;
using System.Numerics;

// Using verifiedState from Example 1

// Query storage slot 0 of WETH contract
var wethContract = "0xC02aaA39b223FE8D0A0e5C4F27eAD9083C756Cc2";
var slot = BigInteger.Zero;

var storageValue = await verifiedState.GetStorageAtAsync(wethContract, slot);

Console.WriteLine($"Storage slot 0: {storageValue.ToHex(true)}");
// Storage value is verified via merkle proof
```

From test: `VerifiedStorageProofLiveTests.cs:25-49`

### Example 3: Verified Mapping Storage Query

For Solidity mappings, calculate storage slot using `keccak256`:

```csharp
using Nethereum.ChainStateVerification;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using System.Linq;

var verifiedState = CreateVerifiedStateService();

// For mapping(address => uint256) balances at slot 3:
// storage_slot = keccak256(abi.encodePacked(address, uint256(3)))

var addressBytes = "0xVitalikAddress".HexToByteArray();
var paddedAddress = new byte[32];
Buffer.BlockCopy(addressBytes, 0, paddedAddress, 12, 20); // Left pad to 32 bytes

var slotIndex = new byte[32];
slotIndex[31] = 3; // Slot 3

var combined = paddedAddress.Concat(slotIndex).ToArray();
var mappingSlot = new Sha3Keccack().CalculateHash(combined);

var wethContract = "0xC02aaA39b223FE8D0A0e5C4F27eAD9083C756Cc2";
var balanceValue = await verifiedState.GetStorageAtAsync(
    wethContract,
    mappingSlot.ToHex(true)
);

var balance = new BigInteger(balanceValue.Reverse().Concat(new byte[] { 0 }).ToArray());
Console.WriteLine($"WETH balance: {balance}");
```

From test: `VerifiedStorageProofLiveTests.cs:52-85`

### Example 4: Verified Contract Code

```csharp
using Nethereum.ChainStateVerification;

var verifiedState = CreateVerifiedStateService();

// Get contract code with hash verification
var usdcContract = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48";
var code = await verifiedState.GetCodeAsync(usdcContract);

Console.WriteLine($"Contract code size: {code.Length} bytes");
// Code hash is verified against account codeHash from proof
```

From: `VerifiedStateService.cs:116-166`

**Code Verification Process:**
1. Get account via `GetAccountAsync` (includes codeHash in proof)
2. If codeHash is empty (0xc5d246...), return empty code
3. Fetch code via `eth_getCode`
4. Compute `keccak256(code)`
5. Verify computed hash matches account codeHash
6. Throw `InvalidChainDataException` if mismatch

Located in `VerifiedStateService.cs:150-158`

### Example 5: Optimistic vs Finalized Mode

```csharp
using Nethereum.ChainStateVerification;

var verifiedState = CreateVerifiedStateService();

// Use finalized mode (maximum security, ~12-15 min old)
verifiedState.Mode = VerificationMode.Finalized;
var finalizedBalance = await verifiedState.GetBalanceAsync(address);
var finalizedHeader = verifiedState.GetCurrentHeader();

Console.WriteLine($"Finalized block: {finalizedHeader.BlockNumber}");
Console.WriteLine($"Finalized balance: {finalizedBalance}");

// Switch to optimistic mode (fresher data, ~12 sec old)
verifiedState.Mode = VerificationMode.Optimistic;
var optimisticBalance = await verifiedState.GetBalanceAsync(address);
var optimisticHeader = verifiedState.GetCurrentHeader();

Console.WriteLine($"Optimistic block: {optimisticHeader.BlockNumber}");
Console.WriteLine($"Optimistic balance: {optimisticBalance}");

// Optimistic is typically 60-80 blocks ahead of finalized
```

From test: `VerifiedStorageProofLiveTests.cs:146-180`

### Example 6: Multiple Storage Slots

```csharp
using Nethereum.ChainStateVerification;
using System.Numerics;

var verifiedState = CreateVerifiedStateService();
var wethContract = "0xC02aaA39b223FE8D0A0e5C4F27eAD9083C756Cc2";

// Query multiple slots sequentially
var slots = new[] { BigInteger.Zero, BigInteger.One, new BigInteger(2) };

foreach (var slot in slots)
{
    var storageValue = await verifiedState.GetStorageAtAsync(wethContract, slot);
    Console.WriteLine($"Slot {slot}: {storageValue.ToHex(true)}");
}

// Each query is individually verified via merkle proof
```

From test: `VerifiedStorageProofLiveTests.cs:116-144`

### Example 7: Full Account Information

```csharp
using Nethereum.ChainStateVerification;

var verifiedState = CreateVerifiedStateService();

var address = "0x742d35Cc6634C0532925a3b844Bc9e7595f0bEb";

// Get complete account state
var account = await verifiedState.GetAccountAsync(address);

Console.WriteLine($"Balance: {account.Balance} wei");
Console.WriteLine($"Nonce: {account.Nonce}");
Console.WriteLine($"Code Hash: {account.CodeHash.ToHex(true)}");
Console.WriteLine($"Storage Root: {account.StateRoot.ToHex(true)}");

// All fields are cryptographically verified via account proof
```

From: `VerifiedStateService.cs:61-96`

### Example 8: Caching and Performance

```csharp
using Nethereum.ChainStateVerification;

var verifiedState = CreateVerifiedStateService();

// First query: fetches proof from RPC and verifies
var balance1 = await verifiedState.GetBalanceAsync(address);

// Second query: uses cached verified account (no RPC call)
var balance2 = await verifiedState.GetBalanceAsync(address);

// Both balances are identical and verified
Assert.Equal(balance1, balance2);

// Query storage (uses cached account if available)
var storage = await verifiedState.GetStorageAtAsync(address, BigInteger.Zero);

// Cache persists across queries for the same block
var nonce = await verifiedState.GetNonceAsync(address); // Uses cache

// Clear cache if needed
verifiedState.ClearCache();

// Disable caching if desired
verifiedState.EnableCaching = false;
```

From: `VerifiedStateService.cs:70-78, 90-93, 192-200, 242-245`

### Example 9: Error Handling

```csharp
using Nethereum.ChainStateVerification;
using System;

var verifiedState = CreateVerifiedStateService();

try
{
    var balance = await verifiedState.GetBalanceAsync(address);
    Console.WriteLine($"Verified balance: {balance}");
}
catch (InvalidChainDataException ex)
{
    // RPC node returned invalid proof or tampered data
    Console.WriteLine($"Proof verification failed: {ex.Message}");

    // Should try different RPC node or report malicious behavior
}
catch (InvalidOperationException ex) when (ex.Message.Contains("proof"))
{
    // RPC node didn't return expected proof data
    Console.WriteLine($"Incomplete proof: {ex.Message}");
}
```

From: `VerifiedStateService.cs:84-86, TrieProofVerifier.cs:22-25, 40-43`

### Example 10: Integration with Light Client

Complete setup with light client:

```csharp
using Nethereum.ChainStateVerification;
using Nethereum.Consensus.LightClient;
using Nethereum.Consensus.LightClient.BeaconApiClient;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;

// Step 1: Setup light client
var beaconClient = new BeaconApiHttpClient("https://lodestar-mainnet.chainsafe.io");

var config = new LightClientConfig
{
    GenesisValidatorsRoot = "0x4b363db94e286120d76eb905340fdd4e54bfe9f06bf33ff6cf5ad27f511bfe95".HexToByteArray(),
    CurrentForkVersion = "0x06000000".HexToByteArray(), // Electra
    SlotsPerEpoch = 32,
    SecondsPerSlot = 12,
    WeakSubjectivityRoot = await beaconClient.GetFinalizedBlockRootAsync()
};

var store = new InMemoryLightClientStore();
var bls = new HerumiNativeBindings();
bls.InitializeLibrary();

var lightClient = new LightClientService(config, beaconClient, store, bls);
await lightClient.InitializeAsync();

// Sync light client (run periodically)
await lightClient.SyncAsync();

// Step 2: Create verified state service
var trustedProvider = new TrustedHeaderProvider(lightClient);

var executionRpcUrl = "https://mainnet.infura.io/v3/YOUR_KEY";
var rpcClient = new RpcClient(new Uri(executionRpcUrl));
var ethGetProof = new EthGetProof(rpcClient);
var ethGetCode = new EthGetCode(rpcClient);
var trieVerifier = new TrieProofVerifier();

var verifiedState = new VerifiedStateService(
    trustedProvider,
    ethGetProof,
    ethGetCode,
    trieVerifier
);

// Step 3: Use verified state
verifiedState.Mode = VerificationMode.Finalized;

var balance = await verifiedState.GetBalanceAsync(address);
var nonce = await verifiedState.GetNonceAsync(address);

Console.WriteLine($"Verified balance: {balance}");
Console.WriteLine($"Verified nonce: {nonce}");
```

## Security Considerations

### Trust Assumptions

1. **Consensus Layer Light Client** must be trustworthy:
   - 2/3 validators finality (Finalized mode)
   - 67% sync committee (Optimistic mode)
   - See Nethereum.Consensus.LightClient for details

2. **RPC Node** is untrusted:
   - Can lie about state
   - Can provide stale data
   - Can censor queries
   - **Cannot** forge valid proofs

3. **Proof Verification** is cryptographic:
   - Based on Merkle Patricia Trie
   - Uses Keccak-256 hashing
   - Verifies against consensus state root

### Attack Scenarios

**Scenario 1: Malicious RPC Node**
- **Attack**: RPC returns incorrect balance with fake proof
- **Defense**: `TrieProofVerifier` detects invalid proof
- **Result**: `InvalidChainDataException` thrown

**Scenario 2: Stale Data**
- **Attack**: RPC returns old but valid state
- **Defense**: Proofs must match current light client state root
- **Result**: Proof verification fails (state root mismatch)

**Scenario 3: Code Tampering**
- **Attack**: RPC returns modified bytecode
- **Defense**: Code hash verification (if `VerifyCodeHash = true`)
- **Result**: `InvalidChainDataException` when hash mismatches

**Scenario 4: Eclipse Attack**
- **Attack**: Attacker controls all RPC nodes queried
- **Defense**: Light client provides trusted state root from consensus
- **Result**: Invalid proofs still cannot be verified

### Best Practices

1. **Use Finalized Mode** for maximum security
2. **Enable Code Verification** (`VerifyCodeHash = true`)
3. **Handle Exceptions** - switch RPC nodes on `InvalidChainDataException`
4. **Keep Light Client Synced** - regularly call `lightClient.SyncAsync()`
5. **Monitor Staleness** - check header age: `DateTime.UtcNow - header.Timestamp`

## Performance Considerations

### RPC Calls

Each verified query requires:
- **Without Cache**: 1-2 RPC calls (`eth_getProof` + optional `eth_getCode`)
- **With Cache**: 0 RPC calls (if account already cached)

### Proof Verification Cost

- **Account Proof**: ~1-2ms (merkle tree traversal)
- **Storage Proof**: ~1-2ms per slot
- **Code Verification**: ~0.1ms (keccak256 hash)

### Caching Strategy

**Default behavior** (EnableCaching = true):
- Account proofs cached per block
- Storage slots cached per address per block
- Code cached per address (persists across blocks)
- Cache cleared when block changes

**Memory usage**:
- ~500 bytes per cached account
- ~32 bytes per cached storage slot
- Variable per cached code (contract size)

### Optimization Tips

1. **Batch queries** at same block to maximize cache hits
2. **Reuse VerifiedStateService** instance across queries
3. **Disable verification** for non-critical paths (`VerifyCodeHash = false`)
4. **Use Optimistic mode** when freshness > security

## Web3 Integration (RPC Interceptor)

The interceptor subsystem lets you transparently verify RPC responses by plugging into the standard Nethereum `Web3` pipeline. Instead of calling `IVerifiedStateService` directly, the `VerifiedStateInterceptor` hooks into the RPC client and replaces supported method responses with proof-verified data.

### Quick Start: UseVerifiedState Extension

The simplest way to enable verified state is the `UseVerifiedState()` extension method, available on both `IWeb3` and `IClient`:

```csharp
using Nethereum.ChainStateVerification.Interceptor;
using Nethereum.Web3;

var verifiedState = CreateVerifiedStateService(); // See earlier examples

// Option 1: On Web3 (returns IWeb3 for fluent chaining)
var web3 = new Web3("https://mainnet.infura.io/v3/YOUR_KEY");
web3.UseVerifiedState(verifiedState, config =>
{
    config.Mode = VerificationMode.Finalized;
    config.FallbackOnError = true;
});

var blockNumber = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
var balance = await web3.Eth.GetBalance.SendRequestAsync("0xd8dA6BF26964aF9D7eEd9e03E53415D37aA96045");

// Option 2: On IClient directly
var rpcClient = new RpcClient(new Uri("https://mainnet.infura.io/v3/YOUR_KEY"));
rpcClient.UseVerifiedState(verifiedState, config =>
{
    config.Mode = VerificationMode.Finalized;
});
var web3FromClient = new Web3(rpcClient);
var nonce = await web3FromClient.Eth.Transactions.GetTransactionCount.SendRequestAsync("0xd8dA6BF26964aF9D7eEd9e03E53415D37aA96045");
```

### Fluent Chaining

`UseVerifiedState()` on `IWeb3` returns the `IWeb3` instance, enabling one-liner queries:

```csharp
using Nethereum.ChainStateVerification.Interceptor;
using Nethereum.Web3;

var verifiedState = CreateVerifiedStateService();

var balance = await new Web3("https://mainnet.infura.io/v3/YOUR_KEY")
    .UseVerifiedState(verifiedState, config =>
    {
        config.Mode = VerificationMode.Finalized;
        config.FallbackOnError = true;
    })
    .Eth.GetBalance.SendRequestAsync("0xd8dA6BF26964aF9D7eEd9e03E53415D37aA96045");

Console.WriteLine($"Verified balance: {UnitConversion.Convert.FromWei(balance.Value)} ETH");
```

### VerifiedStateInterceptorConfiguration

The configuration object controls interceptor behavior:

| Property | Type | Default | Description |
|---|---|---|---|
| `Mode` | `VerificationMode` | `Finalized` | Which consensus header to verify against (`Finalized` or `Optimistic`) |
| `FallbackOnError` | `bool` | `true` | When `true`, falls back to normal RPC if verification fails; when `false`, exceptions propagate |
| `EnabledMethods` | `HashSet<string>` | See below | Set of RPC methods the interceptor handles |

**Default enabled methods:**
- `eth_getBalance`
- `eth_getTransactionCount`
- `eth_getCode`
- `eth_getStorageAt`
- `eth_blockNumber`

Any RPC method not in `EnabledMethods` passes through to the node unmodified. For example, `eth_gasPrice` and `eth_call` always go directly to the RPC node.

### CreateVerifiedStateInterceptor Factory

For advanced scenarios where you need direct access to the interceptor (e.g., to subscribe to events), use the `CreateVerifiedStateInterceptor()` factory method:

```csharp
using Nethereum.ChainStateVerification.Interceptor;
using Nethereum.JsonRpc.Client;
using Nethereum.Web3;

var verifiedState = CreateVerifiedStateService();

var interceptor = verifiedState.CreateVerifiedStateInterceptor(config =>
{
    config.Mode = VerificationMode.Finalized;
    config.FallbackOnError = true;
});

var rpcClient = new RpcClient(new Uri("https://mainnet.infura.io/v3/YOUR_KEY"));
rpcClient.OverridingRequestInterceptor = interceptor;

var web3 = new Web3(rpcClient);
var balance = await web3.Eth.GetBalance.SendRequestAsync("0xd8dA6BF26964aF9D7eEd9e03E53415D37aA96045");
```

### FallbackTriggered Event

When `FallbackOnError = true` and verification fails (e.g., the RPC node cannot provide a valid proof for the finalized state root, often due to state pruning), the interceptor falls back to the unverified RPC response and raises the `FallbackTriggered` event:

```csharp
using Nethereum.ChainStateVerification.Interceptor;

var verifiedState = CreateVerifiedStateService();

var interceptor = verifiedState.CreateVerifiedStateInterceptor(config =>
{
    config.Mode = VerificationMode.Finalized;
    config.FallbackOnError = true;
});

interceptor.FallbackTriggered += (sender, args) =>
{
    Console.WriteLine($"Fallback triggered for method: {args.Method}");
    Console.WriteLine($"Reason: {args.Exception?.Message}");
};

rpcClient.OverridingRequestInterceptor = interceptor;
var web3 = new Web3(rpcClient);

// If proof verification fails, FallbackTriggered fires and the
// balance is returned from the unverified RPC response instead
var balance = await web3.Eth.GetBalance.SendRequestAsync(address);
```

The `VerificationFallbackEventArgs` provides:
- `Method` - The RPC method that failed verification (e.g., `"eth_getBalance"`)
- `Exception` - The exception that caused the fallback

### Interceptor Behavior Summary

1. **Intercepted method + verification succeeds** -> Returns proof-verified result
2. **Intercepted method + verification fails + FallbackOnError=true** -> Falls back to RPC, fires `FallbackTriggered`
3. **Intercepted method + verification fails + FallbackOnError=false** -> Exception propagates to caller
4. **Non-intercepted method** (e.g., `eth_gasPrice`, `eth_call`) -> Passes through to RPC node directly
5. **Historical block parameter** (e.g., block number or `"earliest"`) -> Passes through to RPC (interceptor only handles `latest`, `pending`, `finalized`, `safe`)

## Limitations

1. **Requires Archive Node** (or recent state):
   - RPC must support `eth_getProof` at historical blocks
   - Many nodes prune state older than 128 blocks
   - Use full or archive nodes for older state

2. **Light Client Dependency**:
   - Requires running light client (see Nethereum.Consensus.LightClient)
   - Light client must stay synced
   - Initial sync takes time (~5-10 minutes)

3. **Finalized Lag**:
   - Finalized mode is ~12-15 minutes behind
   - Optimistic mode is ~12 seconds behind
   - Cannot query unfinalized state safely

4. **No Write Operations**:
   - Read-only verification
   - Cannot verify transaction outcomes
   - Use for queries, not transaction building

## Dependencies

Core dependencies:
- **Nethereum.Consensus.LightClient** - Trusted header provider
- **Nethereum.Merkle.Patricia** - Merkle Patricia Trie verification
- **Nethereum.Model** - Account and storage models
- **Nethereum.RPC** - RPC infrastructure and `eth_getProof`
- **Nethereum.EVM** - Blockchain state interfaces

## Source Files Reference

**Core Services:**
- `IVerifiedStateService.cs` - Main service interface
- `VerifiedStateService.cs` - Verified state implementation
- `ITrieProofVerifier.cs` - Proof verifier interface
- `TrieProofVerifier.cs` - Merkle proof verification

**Caching:**
- `Caching/VerifiedStateCache.cs` - Thread-safe state cache
- `Caching/VerifiedAccountState.cs` - Cached account state

**Supporting Types:**
- `VerificationMode.cs` - Finalized vs Optimistic enum
- `InvalidChainDataException.cs` - Proof verification exception

**Interceptor:**
- `Interceptor/VerifiedStateInterceptor.cs` - RPC request interceptor for transparent verification
- `Interceptor/VerifiedStateInterceptorConfiguration.cs` - Interceptor configuration options
- `Interceptor/Web3VerifiedStateExtensions.cs` - Extension methods for IWeb3 and IClient

**Node Data:**
- `NodeData/VerifiedNodeDataService.cs` - INodeDataService adapter for EVM integration

**Test Files:**
- `tests/Nethereum.Consensus.LightClient.Tests/Live/VerifiedStorageProofLiveTests.cs` - Storage proof tests
- `tests/Nethereum.Consensus.LightClient.Tests/Live/VerifiedEvmCallLiveTests.cs` - EVM call tests
- `tests/Nethereum.ChainStateVerification.Tests/Caching/VerifiedStateCacheTests.cs` - Cache tests
- `tests/Nethereum.Consensus.LightClient.Tests/Live/VerifiedStateInterceptorLiveTests.cs` - Interceptor live tests

## Related Packages

- **Nethereum.Consensus.LightClient** - Consensus layer light client (provides trusted state roots)
- **Nethereum.Merkle.Patricia** - Merkle Patricia Trie implementation
- **Nethereum.RPC** - RPC client (eth_getProof, eth_getCode)
- **Nethereum.Model** - Ethereum data models

## License

Nethereum is licensed under the MIT License.

## Support

- GitHub: https://github.com/Nethereum/Nethereum
- Documentation: https://docs.nethereum.com
- Discord: https://discord.gg/jQPrR58FxX
