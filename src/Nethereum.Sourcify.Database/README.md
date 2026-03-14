# Nethereum.Sourcify.Database

EF Core (PostgreSQL) implementation of `ISourcifyRepository` for storing Sourcify verified contract data locally. Provides a complete relational schema mirroring Sourcify's data model with 10 entity types.

## Installation

```bash
dotnet add package Nethereum.Sourcify.Database
```

## Problems This Library Solves

| Problem | Solution |
|---------|----------|
| "I want to query Sourcify data locally" | PostgreSQL storage of all Sourcify entities |
| "I need fast 4-byte selector lookups" | Indexed signature table with hash4/hash32 lookups |
| "I want to import Sourcify's Parquet exports" | `BulkInsertAsync` + idempotent add operations |
| "I need to link deployments to verified source" | Full relational model: deployment → contract → compiled → sources |

## Quick Start

### Setup

```csharp
using Microsoft.EntityFrameworkCore;
using Nethereum.Sourcify.Database;

// Register in DI
services.AddDbContext<SourcifyDbContext>(options =>
    options.UseNpgsql("Host=localhost;Database=sourcify;Username=postgres;Password=secret"));

services.AddScoped<ISourcifyRepository, EFCoreSourcifyRepository>();
```

### Store and Query Verified Contracts

```csharp
using Nethereum.DataServices.Sourcify.Database;
using Nethereum.DataServices.Sourcify.Database.Models;

var repository = serviceProvider.GetRequiredService<ISourcifyRepository>();

// Store a contract deployment
var deployment = new ContractDeployment
{
    Id = Guid.NewGuid(),
    ChainId = 1,
    Address = addressBytes,
    TransactionHash = txHashBytes,
    BlockNumber = 18000000,
    ContractId = contractGuid
};
await repository.AddDeploymentAsync(deployment);

// Query verified contract by chain + address
var verified = await repository.GetVerifiedContractAsync(chainId: 1, addressBytes);
if (verified != null)
{
    Console.WriteLine($"Creation match: {verified.CreationMatch}");
    Console.WriteLine($"Runtime match: {verified.RuntimeMatch}");
}
```

### Signature Lookups

```csharp
// Lookup by 4-byte selector (indexed for fast queries)
byte[] selector = new byte[] { 0xa9, 0x05, 0x9c, 0xbb };
var sig = await repository.GetSignatureByHash4Async(selector);
Console.WriteLine(sig?.SignatureText); // "transfer(address,uint256)"

// Full-text search (returns up to 100 results)
var results = await repository.SearchSignaturesAsync("transfer");
foreach (var s in results)
    Console.WriteLine($"{s.SignatureText}");

// Store a new signature
await repository.AddSignatureAsync(new Signature
{
    SignatureHash32 = keccak256Bytes,
    SignatureHash4 = selectorBytes,
    SignatureText = "transfer(address,uint256)",
    CreatedAt = DateTime.UtcNow
});
```

### Bulk Import (Parquet Exports)

```csharp
using Nethereum.DataServices.Sourcify;

// Download Sourcify Parquet data
var parquet = new SourcifyParquetExportService();
await parquet.SyncTableToDirectoryAsync("verified_contracts", "/data/sourcify");

// Parse Parquet files and bulk insert (using your Parquet reader)
var verifiedContracts = ParseParquetFile<VerifiedContract>("/data/sourcify/verified_contracts.parquet");
await repository.BulkInsertAsync(verifiedContracts);
```

## Database Schema

### Entity Relationship

```
Code (bytecode storage)
  ↑ CreationCodeHash / RuntimeCodeHash
Contract (abstract contract definition)
  ↑ ContractId
ContractDeployment (chain-specific deploy, UNIQUE on ChainId+Address)
  ↑ DeploymentId
VerifiedContract (verification result linking deployment ↔ compilation)
  ↑ VerifiedContractId
SourcifyMatch (match quality: perfect/partial)

CompiledContract (compiler output + settings as JSONB)
  ↑ CompilationId
CompiledContractSource (M:M → Source)
CompiledContractSignature (M:M → Signature, with SignatureType)

Source (deduplicated source files by hash)
Signature (function/event/error signatures, indexed by 4-byte selector)
```

### Tables

| Table | Primary Key | Notable Columns |
|-------|-------------|-----------------|
| `code` | `code_hash` (byte[]) | `code_hash_keccak`, `code` (bytecode) |
| `contracts` | `id` (Guid) | `creation_code_hash`, `runtime_code_hash` |
| `contract_deployments` | `id` (Guid) | `chain_id` + `address` (unique index), `transaction_hash`, `deployer` |
| `compiled_contracts` | `id` (Guid) | `compiler`, `version`, `language`, `name`, `compiler_settings` (JSONB) |
| `sources` | `source_hash` (byte[]) | `source_hash_keccak`, `content` (text) |
| `compiled_contracts_sources` | `id` (Guid) | `compilation_id`, `source_hash`, `path` |
| `signatures` | `signature_hash` (byte[32]) | `selector` (byte[4], indexed), `signature_text` |
| `compiled_contracts_signatures` | `id` (Guid) | `compilation_id`, `signature_hash`, `type` (Function/Event/Error) |
| `verified_contracts` | `id` (long) | `deployment_id`, `compilation_id`, creation/runtime match flags, JSONB values |
| `sourcify_matches` | `id` (long) | `verified_contract_id`, `creation_match`, `runtime_match`, `metadata` (JSONB) |

### SignatureType Enum

```csharp
public enum SignatureType
{
    Function = 0,  // Function selector (4 bytes)
    Event = 1,     // Event topic (32 bytes)
    Error = 2      // Custom error selector (4 bytes)
}
```

## Key Patterns

### Idempotent Adds

All `Add*Async` methods check for existing records before inserting — safe to call multiple times with the same data:

```csharp
// Safe to call repeatedly — won't duplicate
await repository.AddCodeAsync(code);
await repository.AddCodeAsync(code); // No-op if already exists
```

### Hash-Based Lookups

Code and Source entities use content hashes as primary keys for deduplication:

```csharp
// Same bytecode deployed to multiple chains shares one Code record
var code = await repository.GetCodeAsync(codeHash);
```

### Two-Step Verified Contract Lookup

`GetVerifiedContractAsync` first finds the deployment by chain + address, then finds the verification record:

```csharp
// Internally: deployment = FindByChainAndAddress → verified = FindByDeploymentId
var verified = await repository.GetVerifiedContractAsync(chainId: 1, addressBytes);
```

## Repository Methods

| Method | Description |
|--------|-------------|
| `GetCodeAsync(codeHash)` | Get bytecode by SHA256 hash |
| `AddCodeAsync(code)` | Store bytecode (idempotent) |
| `GetContractAsync(id)` | Get contract definition |
| `AddContractAsync(contract)` | Store contract (idempotent) |
| `GetDeploymentAsync(chainId, address)` | Get deployment by chain + address |
| `AddDeploymentAsync(deployment)` | Store deployment (idempotent) |
| `GetCompiledContractAsync(id)` | Get compilation output |
| `AddCompiledContractAsync(compiled)` | Store compilation (idempotent) |
| `GetVerifiedContractAsync(chainId, address)` | Get verification via deployment lookup |
| `AddVerifiedContractAsync(verified)` | Store verification (idempotent) |
| `GetSignatureByHash4Async(hash4)` | Lookup signature by 4-byte selector |
| `GetSignatureByHash32Async(hash32)` | Lookup signature by full keccak256 hash |
| `SearchSignaturesAsync(query)` | Text search on signature text (max 100) |
| `AddSignatureAsync(signature)` | Store signature (idempotent) |
| `GetSourceAsync(sourceHash)` | Get source file by hash |
| `AddSourceAsync(source)` | Store source (idempotent) |
| `GetSourcesForCompilationAsync(compilationId)` | Get all source files for a compilation |
| `AddCompiledContractSourceAsync(source)` | Link source to compilation |
| `GetSourcifyMatchAsync(verifiedContractId)` | Get match quality record |
| `AddSourcifyMatchAsync(match)` | Store match record (idempotent) |
| `BulkInsertAsync<T>(entities)` | Bulk insert without duplicate checking |

## Dependencies

- `Nethereum.DataServices` (models and `ISourcifyRepository` interface)
- `Microsoft.EntityFrameworkCore` 8.0.0
- `Npgsql.EntityFrameworkCore.PostgreSQL` 8.0.0

## License

MIT License — see the main Nethereum repository for details.
