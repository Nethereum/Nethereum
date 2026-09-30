# Nethereum.AppChain.Policy

> **PREVIEW** — This package is in preview. APIs may change between releases.

Access control and governance for [Nethereum AppChain](../Nethereum.AppChain/README.md) — your business, your rules.

## Overview

AppChain operation is centralised by design. This package gives the operator control over who can write, what they can write, and how much they can write — while keeping those rules transparent and verifiable. Policies can start as simple local configuration and optionally migrate to an L1 smart contract for decentralised governance.

The package supports writer/admin authorisation using merkle tree proofs (gas-efficient on-chain verification of large allowlists), calldata size limits, log output limits, and per-block gas limits. A `BootstrapPolicyService` handles local policy management, an `EvmPolicyService` reads policies from L1, and a `PolicySyncWorker` periodically syncs policy state from L1.

### Key Features

- **Merkle Tree Authorization**: Writers and admins validated via merkle proofs for gas-efficient on-chain verification
- **Bootstrap → L1 Migration**: Start with local policies, migrate to L1 contract when ready
- **Epoch-Based Updates**: Atomic policy updates tied to epoch numbers
- **Policy Sync Worker**: Background service syncs policy changes from L1
- **Calldata & Gas Limits**: Configurable per-transaction calldata size and per-block gas limits
- **Blacklist Support**: Separate merkle tree for blacklisted addresses

## Installation

```bash
dotnet add package Nethereum.AppChain.Policy
```

### Dependencies

- **Nethereum.CoreChain** - Chain state access
- **Nethereum.Web3** - L1 contract interaction for policy queries
- **Nethereum.Contracts** - Contract function encoding/decoding
- **Microsoft.Extensions.Hosting.Abstractions** - `IHostedService` for policy sync
- **Microsoft.Extensions.Logging.Abstractions** - `ILogger` for diagnostics

## Key Concepts

### Merkle Tree Authorization

Rather than storing all authorized addresses on-chain (expensive), the policy contract stores only the merkle root. Writers prove their authorization by submitting a merkle proof that their address is included in the tree. This enables gas-efficient verification of large allowlists.

```csharp
using Nethereum.AppChain.Policy.Bootstrap;

var migrationService = new PolicyMigrationService();
byte[] root = migrationService.ComputeMerkleRoot(allowedAddresses);
byte[][] proof = migrationService.ComputeMerkleProof(myAddress, allowedAddresses);
bool valid = migrationService.VerifyMerkleProof(myAddress, root, proof);
```

### Bootstrap → L1 Flow

1. **Bootstrap**: Start with local `BootstrapPolicyService` containing allowed writers/admins
2. **Operate**: Chain runs with local policy enforcement
3. **Migrate**: When L1 contract deployed, compute merkle roots and submit to contract
4. **Switch**: `PolicySyncWorker` detects L1 policy, transitions to `EvmPolicyService`

### Epochs

Policy changes are versioned by epoch number. Each epoch represents an atomic update to the entire policy state. The `PolicySyncWorker` detects epoch changes and refreshes the cached policy.

## Quick Start

```csharp
using Nethereum.AppChain.Policy;
using Nethereum.AppChain.Policy.Bootstrap;

// Bootstrap with local policy
var bootstrapConfig = new BootstrapPolicyConfig
{
    AllowedWriters = new List<string> { writer1, writer2 },
    AllowedAdmins = new List<string> { admin1 },
    MaxCalldataBytes = 128_000,
    MaxLogBytes = 64_000
};

var policyService = new BootstrapPolicyService(bootstrapConfig);
bool canWrite = await policyService.IsValidWriterAsync(writer1, proof, null);
```

## Usage Examples

### Example 1: Query L1 Policy

```csharp
using Nethereum.AppChain.Policy;
using Nethereum.Hex.HexConvertors.Extensions;

var config = new PolicyConfig
{
    TargetRpcUrl = "http://127.0.0.1:8545",
    PolicyContractAddress = contractAddress
};
var policyService = new EvmPolicyService(config);
var policy = await policyService.GetCurrentPolicyAsync();

Console.WriteLine($"Epoch: {policy.Epoch}");
Console.WriteLine($"Max Calldata: {policy.MaxCalldataBytes}");
Console.WriteLine($"Writers Root: {policy.WritersRoot.ToHex()}");
```

### Example 2: Manage Local Policy

```csharp
using Nethereum.AppChain.Policy.Bootstrap;

var bootstrap = new BootstrapPolicyService(bootstrapConfig);

// Dynamic policy updates without restart
bootstrap.AddWriter(newWriterAddress);
bootstrap.RemoveWriter(revokedAddress);
bootstrap.AddAdmin(newAdminAddress);

Console.WriteLine($"Is admin: {bootstrap.IsValidAdmin(adminAddress)}");
```

### Example 3: Prepare Migration Data

```csharp
using Nethereum.AppChain.Policy.Bootstrap;

var migration = new PolicyMigrationService();
var data = migration.PrepareMigrationData(bootstrapConfig);

// data.WritersRoot - merkle root for writers
// data.AdminsRoot - merkle root for admins
// data.WriterProofs - per-address merkle proofs
// data.AdminProofs - per-address merkle proofs
```

## API Reference

### EvmPolicyService

L1 policy contract interaction.

```csharp
public class EvmPolicyService : IPolicyService
{
    public Task<PolicyInfo> GetCurrentPolicyAsync();
    public Task<byte[]?> GetWritersRootAsync();
    public Task<byte[]?> GetAdminsRootAsync();
    public Task<byte[]?> GetBlacklistRootAsync();
    public Task<BigInteger> GetEpochAsync();
    public Task<bool> IsValidWriterAsync(string address, byte[][] proof, byte[]? blacklistProof);
}
```

### BootstrapPolicyService

Local policy management with optional L1 migration.

```csharp
public class BootstrapPolicyService : IPolicyService
{
    public bool IsMigratedToL1 { get; }
    public void AddWriter(string address);
    public void RemoveWriter(string address);
    public void AddAdmin(string address);
    public void RemoveAdmin(string address);
    public bool IsValidAdmin(string address);
}
```

### PolicySyncWorker

Background service for L1 policy synchronization.

```csharp
public class PolicySyncWorker : IHostedService, IDisposable
{
    public PolicyInfo? CachedPolicy { get; }
    public bool IsRunning { get; }
    public Task StartAsync(CancellationToken ct);
    public Task StopAsync(CancellationToken ct);
    public Task ForceSyncAsync();
    public void Dispose();
    public event Action<PolicyInfo>? OnPolicyUpdated;
}
```

### PolicyMigrationService

Merkle tree computation and proof generation.

- `ComputeMerkleRoot(addresses) : byte[]`
- `ComputeMerkleProof(address, allAddresses) : byte[][]`
- `VerifyMerkleProof(address, root, proof) : bool`
- `PrepareMigrationData(config) : MigrationData`

## Related Packages

### Used By (Consumers)
- **[Nethereum.AppChain.Server](../Nethereum.AppChain.Server/README.md)** - Integrates policy enforcement in the server pipeline

### Dependencies
- **[Nethereum.CoreChain](../Nethereum.CoreChain/README.md)** - Chain state access
- **[Nethereum.Web3](../Nethereum.Web3/README.md)** - L1 contract queries

## Additional Resources

- [Nethereum Documentation](https://docs.nethereum.com)
