# Nethereum.Model.SSZ

SSZ execution-layer type encoders for Ethereum transactions, receipts, and block headers per EIP-6404, EIP-6466, and EIP-7807.

## Overview

Nethereum.Model.SSZ bridges Ethereum's execution-layer types (`Transaction1559`, `Transaction7702`, `BlockHeader`, `Log`) with SSZ serialization. These EIPs propose replacing RLP encoding with SSZ for execution-layer data structures, enabling:

- **Unified serialization** across consensus and execution layers
- **Efficient Merkle proofs** via hash tree roots (no more RLP-encoded tries)
- **Forward-compatible encoding** using ProgressiveContainer and CompatibleUnion (EIP-7495)

Each encoder provides encode, decode, and hash-tree-root operations:

- **SszTransactionEncoder** — EIP-6404 transaction encoding (EIP-1559, EIP-7702)
- **SszReceiptEncoder** — EIP-6466 receipt encoding (basic, create, set-code)
- **SszBlockHeaderEncoder** — EIP-7807 block header encoding
- **SszLogEncoder** — Log encoding for receipts
- **SszAccessListEncoder** — Access list encoding for transactions
- **SszHashTreeRootHelper** — Primitive leaf hash functions

## Installation

```bash
dotnet add package Nethereum.Model.SSZ
```

### Dependencies

- **Nethereum.Ssz** — SszWriter, SszReader, SszMerkleizer
- **Nethereum.Model** — Transaction1559, Transaction7702, BlockHeader, Log, AccessListItem

## Quick Start

```csharp
using Nethereum.Model.SSZ;

// Encode a transaction to SSZ
var encoded = SszTransactionEncoder.Current.EncodeTransaction1559Payload(tx);

// Decode back
var decoded = SszTransactionEncoder.Current.DecodeTransaction1559Payload(encoded, isCreate: false);

// Compute hash tree root (SSZ equivalent of transaction hash)
var root = SszTransactionEncoder.Current.HashTreeRootTransaction1559(tx);
```

## Usage Examples

### Example 1: Transaction Encode/Decode Round-Trip

```csharp
using Nethereum.Model;
using Nethereum.Model.SSZ;
using System.Numerics;

var tx = new Transaction1559
{
    ChainId = 1,
    Nonce = 42,
    MaxPriorityFeePerGas = 1_500_000_000,
    MaxFeePerGas = 30_000_000_000,
    GasLimit = 21000,
    ReceiveAddress = "0x742d35Cc6634C0532925a3b844Bc9e7595f0bEb".HexToByteArray(),
    Value = BigInteger.Parse("1000000000000000000"),
    Data = Array.Empty<byte>()
};

// Encode payload
var encoded = SszTransactionEncoder.Current.EncodeTransaction1559Payload(tx);

// Decode and verify round-trip
var decoded = SszTransactionEncoder.Current.DecodeTransaction1559Payload(encoded, isCreate: false);
```

### Example 2: Hash Tree Root Stability

The hash tree root is deterministic — encode/decode round-trips produce the same root:

```csharp
var rootBefore = SszTransactionEncoder.Current.HashTreeRootTransaction1559(tx);
var encoded = SszTransactionEncoder.Current.EncodeTransaction1559Payload(tx);
var decoded = SszTransactionEncoder.Current.DecodeTransaction1559Payload(encoded, isCreate: false);
var rootAfter = SszTransactionEncoder.Current.HashTreeRootTransaction1559(decoded);

// rootBefore == rootAfter — hash tree root is stable across encode/decode
```

### Example 3: Full Transaction Container (Payload + Signature)

Wrap the payload in a full transaction container with signature:

```csharp
var payloadEncoded = SszTransactionEncoder.Current.EncodeTransaction1559Payload(tx);
var sigBytes = SszTransactionEncoder.PackSignatureBytes(rBytes, sBytes, vBytes);

var fullTx = SszTransactionEncoder.Current.EncodeTransaction(
    SszTransactionEncoder.SelectorRlpBasic,  // EIP-1559 basic transfer
    payloadEncoded,
    sigBytes);
```

### Example 4: EIP-7702 Transaction

```csharp
var tx7702 = new Transaction7702
{
    ChainId = 1,
    Nonce = 1,
    MaxPriorityFeePerGas = 1_000_000_000,
    MaxFeePerGas = 20_000_000_000,
    GasLimit = 100_000,
    ReceiveAddress = targetAddress,
    Value = BigInteger.Zero,
    Data = callData,
    AuthorisationList = new List<Authorisation7702Signed> { auth }
};

var encoded = SszTransactionEncoder.Current.EncodeTransaction7702Payload(tx7702);
var decoded = SszTransactionEncoder.Current.DecodeTransaction7702Payload(encoded);
var root = SszTransactionEncoder.Current.HashTreeRootTransaction7702(tx7702);
```

### Example 5: Receipt Encoding

```csharp
using Nethereum.Model.SSZ;
using Nethereum.RPC.Eth.DTOs;

// Basic receipt (transfer)
var encoded = SszReceiptEncoder.Current.EncodeBasicReceipt(
    from: "0x742d35Cc6634C0532925a3b844Bc9e7595f0bEb",
    gasUsed: 21000,
    logs: new List<Log>(),
    status: true);

// Create receipt (contract deployment)
var createEncoded = SszReceiptEncoder.Current.EncodeCreateReceipt(
    from: senderAddress,
    gasUsed: 500000,
    contractAddress: "0xNewContractAddress",
    logs: deployLogs,
    status: true);

// Wrap in CompatibleUnion
var unionEncoded = SszReceiptEncoder.Current.EncodeReceipt(
    SszReceiptEncoder.SelectorBasicReceipt, encoded);
```

### Example 6: Block Header with Transactions and Receipts Root

```csharp
using Nethereum.Model;
using Nethereum.Model.SSZ;

// Compute transaction roots
var txRoots = transactions.Select(tx =>
    SszTransactionEncoder.Current.HashTreeRootTransaction1559(tx)).ToList();
var transactionsRoot = SszTransactionEncoder.Current.HashTreeRootTransactionsRoot(txRoots);

// Compute receipt roots
var receiptRoots = receipts.Select(r =>
    SszReceiptEncoder.Current.HashTreeRootReceipt(selector, dataRoot)).ToList();
var receiptsRoot = SszReceiptEncoder.Current.HashTreeRootReceiptsRoot(receiptRoots);

// Build header
var header = new BlockHeader
{
    TransactionsHash = transactionsRoot,
    ReceiptHash = receiptsRoot,
    // ... other fields
};

// Block hash IS the SSZ hash tree root
var blockHash = SszBlockHeaderEncoder.Current.BlockHash(header);
```

## API Reference

### SszTransactionEncoder

Singleton: `SszTransactionEncoder.Current`

**Encode/Decode:**

| Method | Description |
|--------|-------------|
| `EncodeTransaction1559Payload(Transaction1559)` | Encode EIP-1559 transaction payload |
| `DecodeTransaction1559Payload(data, isCreate, signature?)` | Decode EIP-1559 payload |
| `EncodeTransaction7702Payload(Transaction7702)` | Encode EIP-7702 transaction payload |
| `DecodeTransaction7702Payload(data, signature?)` | Decode EIP-7702 payload |
| `EncodeTransaction(selector, payloadData, signatureBytes)` | Wrap in CompatibleUnion container |
| `EncodeAuthorisationList(auths)` | Encode EIP-7702 authorization list |
| `DecodeAuthorisationList(data)` | Decode authorization list |
| `PackSignatureBytes(r, s, v)` | Pack signature components into SSZ format |

**Hash Tree Roots:**

| Method | Description |
|--------|-------------|
| `HashTreeRootTransaction1559(tx)` | Root for EIP-1559 payload |
| `HashTreeRootTransaction7702(tx)` | Root for EIP-7702 payload |
| `HashTreeRootAuthorisation7702(auth)` | Root for single authorization |
| `HashTreeRootTransactionContainer(payloadRoot, selector, sig)` | Root for full transaction |
| `HashTreeRootTransactionsRoot(txRoots)` | Aggregate transactions root |
| `HashTreeRootBasicFees(regularFee)` | Root for fee structure |
| `HashTreeRootBlobFees(regularFee, blobFee)` | Root for blob fee structure |

**Selectors:**

| Constant | Value | Transaction Type |
|----------|-------|-----------------|
| `SelectorRlpBasic` | `0x07` | EIP-1559 basic transfer |
| `SelectorRlpCreate` | `0x08` | EIP-1559 contract create |
| `SelectorRlpSetCode` | `0x0a` | EIP-7702 set-code |
| `SelectorRlpBlob` | `0x09` | EIP-4844 blob |

### SszReceiptEncoder

Singleton: `SszReceiptEncoder.Current`

| Method | Description |
|--------|-------------|
| `EncodeBasicReceipt(from, gasUsed, logs, status)` | Encode basic transfer receipt |
| `EncodeCreateReceipt(from, gasUsed, contractAddress, logs, status)` | Encode contract creation receipt |
| `EncodeReceipt(selector, receiptData)` | Wrap in CompatibleUnion |
| `DecodeBasicReceipt(data, out from, out gasUsed, out logs, out status)` | Decode basic receipt |
| `DecodeCreateReceipt(data, out from, out gasUsed, out contractAddress, out logs, out status)` | Decode create receipt |
| `HashTreeRootBasicReceipt(...)` | Root for basic receipt |
| `HashTreeRootCreateReceipt(...)` | Root for create receipt |
| `HashTreeRootSetCodeReceipt(...)` | Root for set-code receipt |
| `HashTreeRootReceipt(selector, dataRoot)` | Union root |
| `HashTreeRootReceiptsRoot(receiptRoots)` | Aggregate receipts root |

### SszBlockHeaderEncoder

Singleton: `SszBlockHeaderEncoder.Current`

| Method | Description |
|--------|-------------|
| `Encode(BlockHeader)` | Encode header to SSZ |
| `Decode(data)` | Decode SSZ to BlockHeader |
| `HashTreeRoot(BlockHeader)` | Compute hash tree root |
| `BlockHash(BlockHeader)` | Compute SSZ block hash (same as HashTreeRoot) |

### SszLogEncoder

Singleton: `SszLogEncoder.Current`

| Method | Description |
|--------|-------------|
| `Encode(Log)` | Encode log to SSZ |
| `Decode(data)` | Decode SSZ to Log |
| `HashTreeRoot(Log)` | Compute hash tree root |

### SszAccessListEncoder

Singleton: `SszAccessListEncoder.Current`

| Method | Description |
|--------|-------------|
| `EncodeAccessTuple(AccessListItem)` | Encode single access list entry |
| `DecodeAccessTuple(data)` | Decode single entry |
| `EncodeAccessList(List<AccessListItem>)` | Encode full access list |
| `DecodeAccessList(data)` | Decode full list |
| `HashTreeRootAccessTuple(item)` | Root for single entry |
| `HashTreeRootAccessList(list)` | Root for full list |

### SszHashTreeRootHelper

Static helper for primitive type hash tree roots:

| Method | Description |
|--------|-------------|
| `HashTreeRootUint64(value)` | 32-byte leaf for uint64 |
| `HashTreeRootUint8(value)` | 32-byte leaf for uint8 |
| `HashTreeRootBoolean(value)` | 32-byte leaf for boolean |
| `HashTreeRootUint256(value)` | 32-byte leaf for uint256 |
| `HashTreeRootAddress(hexAddress)` | 32-byte leaf for address |
| `HashTreeRootBytes32(value)` | 32-byte leaf for Bytes32 |
| `HashTreeRootProgressiveByteList(data)` | Root for variable-length bytes |
| `HashTreeRootByteList(data, maxBytes)` | Root for capped byte list |

## EIP Reference

| EIP | Title | What This Library Encodes |
|-----|-------|--------------------------|
| [EIP-6404](https://eips.ethereum.org/EIPS/eip-6404) | SSZ Transactions Root | Transaction payloads and containers |
| [EIP-6466](https://eips.ethereum.org/EIPS/eip-6466) | SSZ Receipts Root | Receipt types (basic, create, set-code) |
| [EIP-7807](https://eips.ethereum.org/EIPS/eip-7807) | SSZ Block Header | Full post-Cancun/Pectra block header |
| [EIP-7495](https://eips.ethereum.org/EIPS/eip-7495) | SSZ StableContainer | ProgressiveContainer and CompatibleUnion patterns used throughout |

## Related Packages

- **Nethereum.Ssz** — SSZ primitives (SszWriter, SszReader, SszMerkleizer)
- **Nethereum.Model** — Execution-layer types (Transaction1559, BlockHeader, Log)
- **Nethereum.Consensus.Ssz** — Consensus-layer SSZ containers (BeaconBlock, etc.)
