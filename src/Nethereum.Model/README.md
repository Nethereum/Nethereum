# Nethereum.Model

Core Ethereum data models representing transactions, blocks, accounts, and cryptographic signatures.

## Overview

Nethereum.Model is a foundational package that defines the core data structures used throughout the Ethereum protocol. These models represent:
- **Transaction Types**: Support for Legacy, EIP-2930, EIP-1559, and EIP-7702 transactions
- **Block Headers**: Ethereum block header structure with all consensus fields
- **Account State**: Account nonce, balance, storage root, and code hash
- **Signatures**: ECDSA signature components (R, S, V) for transaction verification
- **Event Logs**: Smart contract event data structures

This package provides the canonical implementation of Ethereum data structures with built-in RLP encoding/decoding support.

## Installation

```bash
dotnet add package Nethereum.Model
```

### Dependencies

**Nethereum Dependencies:**
- **Nethereum.RLP** - Recursive Length Prefix encoding/decoding
- **Nethereum.Util** - Keccak-256 hashing, address utilities, unit conversion

## Key Concepts

### Transaction Types

Ethereum has evolved to support multiple transaction formats:

- **Legacy Transactions** (`LegacyTransaction`): Original transaction format (pre-EIP-155)
- **Legacy with Chain ID** (`LegacyTransactionChainId`): EIP-155 transactions preventing replay attacks
- **EIP-2930** (`Transaction2930`): Transactions with access lists for gas optimization
- **EIP-1559** (`Transaction1559`): Fee market transactions with base fee and priority fee
- **EIP-7702** (`Transaction7702`): Account abstraction with authorization lists

### RLP Encoding

All models implement RLP (Recursive Length Prefix) encoding, the serialization format used by Ethereum for:
- Transmitting transactions over the network
- Computing cryptographic hashes (transaction hash, block hash)
- Storing data in the Ethereum state trie

### Signatures

Ethereum uses ECDSA signatures with three components:
- **R**: X-coordinate of the random point on the elliptic curve
- **S**: Signature proof value
- **V**: Recovery identifier (allows deriving the public key from the signature)

### Chain Identification

The `Chain` enum provides constants for major Ethereum networks (MainNet, Sepolia, Polygon, Arbitrum, Optimism, Base, etc.) and is used with EIP-155 to prevent cross-chain replay attacks.

## Quick Start

```csharp
using Nethereum.Model;
using System.Numerics;

// Create a simple EIP-1559 transaction
var transaction = new Transaction1559(
    chainId: 1,  // Ethereum Mainnet
    nonce: 0,
    maxPriorityFeePerGas: BigInteger.Parse("2000000000"),  // 2 gwei
    maxFeePerGas: BigInteger.Parse("100000000000"),        // 100 gwei
    gasLimit: 21000,
    receiverAddress: "0x742d35Cc6634C0532925a3b844Bc9e7595f0bEb",
    amount: BigInteger.Parse("1000000000000000000"),       // 1 ETH
    data: "",
    accessList: null
);
```

## Usage Examples

### Example 1: Creating a Legacy Transaction

```csharp
using Nethereum.Model;
using System.Numerics;

// Create a legacy transaction (most common pre-2021)
var legacyTx = new LegacyTransaction(
    to: "0x13978aee95f38490e9769c39b2773ed763d9cd5f",
    amount: new BigInteger(10000000000000000L),  // 0.01 ETH
    nonce: 0,
    gasPrice: new BigInteger(1000000000000L),    // 1000 gwei
    gasLimit: 10000
);

// Transaction is ready to be signed with Nethereum.Signer
```

*Source: tests/Nethereum.Signer.UnitTests/TransactionTests.cs*

### Example 2: Decoding a Signed Transaction from RLP

```csharp
using Nethereum.Model;
using Nethereum.Hex.HexConvertors.Extensions;

// RLP-encoded signed transaction (typically from network or storage)
var rlpHex = "f86b8085e8d4a510008227109413978aee95f38490e9769c39b2773ed763d9cd5f872386f26fc10000801ba0eab47c1a49bf2fe5d40e01d313900e19ca485867d462fe06e139e3a536c6d4f4a014a569d327dcda4b29f74f93c0e9729d2f49ad726e703f9cd90dbb0fbf6649f1";

// Decode the transaction
var tx = new LegacyTransaction(rlpHex.HexToByteArray());

// Access transaction properties
var nonce = tx.Nonce.ToBigIntegerFromRLPDecoded();
var gasPrice = tx.GasPrice.ToBigIntegerFromRLPDecoded();
var value = tx.Value.ToBigIntegerFromRLPDecoded();
var to = tx.ReceiveAddress.ToHex();

// Access signature components
var v = tx.Signature.V[0];  // 27 or 28 for legacy transactions
var r = tx.Signature.R.ToHex();
var s = tx.Signature.S.ToHex();
```

*Source: tests/Nethereum.Signer.UnitTests/TransactionTests.cs*

### Example 3: Using TransactionFactory to Decode Any Transaction Type

```csharp
using Nethereum.Model;

// TransactionFactory automatically detects transaction type
var rlpEncoded = GetTransactionFromNetwork();  // byte[] from RPC or P2P

ISignedTransaction transaction = TransactionFactory.CreateTransaction(rlpEncoded);

// Check the transaction type
switch (transaction.TransactionType)
{
    case TransactionType.LegacyTransaction:
        var legacy = (LegacyTransaction)transaction;
        // Handle legacy transaction
        break;

    case TransactionType.EIP1559:
        var eip1559 = (Transaction1559)transaction;
        var maxFeePerGas = eip1559.MaxFeePerGas;
        var maxPriorityFee = eip1559.MaxPriorityFeePerGas;
        break;

    case TransactionType.LegacyEIP2930:
        var eip2930 = (Transaction2930)transaction;
        var accessList = eip2930.AccessList;
        break;

    case TransactionType.EIP7702:
        var eip7702 = (Transaction7702)transaction;
        var authList = eip7702.AuthorisationList;
        break;
}
```

*Source: src/Nethereum.Model/TransactionFactory.cs*

### Example 4: Creating an EIP-1559 Transaction with Access List

```csharp
using Nethereum.Model;
using System.Numerics;
using System.Collections.Generic;

// Create an access list (EIP-2930) to reduce gas costs
var accessList = new List<AccessListItem>
{
    new AccessListItem(
        address: "0x742d35Cc6634C0532925a3b844Bc9e7595f0bEb",
        storageKeys: new List<byte[]>
        {
            "0x0000000000000000000000000000000000000000000000000000000000000001".HexToByteArray()
        }
    )
};

// Create an EIP-1559 transaction
var tx1559 = new Transaction1559(
    chainId: (BigInteger)Chain.MainNet,
    nonce: 42,
    maxPriorityFeePerGas: new BigInteger(2_000_000_000),   // 2 gwei tip
    maxFeePerGas: new BigInteger(100_000_000_000),         // 100 gwei max
    gasLimit: 100_000,
    receiverAddress: "0x742d35Cc6634C0532925a3b844Bc9e7595f0bEb",
    amount: BigInteger.Parse("5000000000000000000"),       // 5 ETH
    data: "0x",
    accessList: accessList
);

// Get RLP encoding for signing
var rlpForSigning = tx1559.GetRLPEncodedRaw();
```

*Source: src/Nethereum.Model/Transaction1559.cs, src/Nethereum.Model/AccessListItem.cs*

### Example 5: Working with Ethereum Account State

```csharp
using Nethereum.Model;
using System.Numerics;

// Create an account state object (as stored in the Ethereum state trie)
var account = new Account
{
    Nonce = 42,  // Number of transactions sent from this address
    Balance = BigInteger.Parse("5000000000000000000000"),  // 5000 ETH in wei
    StateRoot = stateRootHash,  // Root of the account's storage trie
    CodeHash = contractCodeHash  // Keccak-256 hash of the contract bytecode
};

// For EOA (Externally Owned Accounts), defaults are used:
var eoa = new Account
{
    Nonce = 0,
    Balance = BigInteger.Parse("1000000000000000000"),  // 1 ETH
    StateRoot = DefaultValues.EMPTY_TRIE_HASH,  // Empty storage
    CodeHash = DefaultValues.EMPTY_DATA_HASH    // No code
};
```

*Source: src/Nethereum.Model/Account.cs*

### Example 6: Creating Event Logs

```csharp
using Nethereum.Model;
using Nethereum.Hex.HexConvertors.Extensions;

// Create an event log (emitted by smart contracts)
var eventData = "0x0000000000000000000000000000000000000000000000000de0b6b3a7640000".HexToByteArray();
var topic1 = "0xddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef".HexToByteArray();  // Transfer event signature
var topic2 = "0x000000000000000000000000742d35cc6634c0532925a3b844bc9e7595f0beb".HexToByteArray();  // From address
var topic3 = "0x00000000000000000000000088e6a0c2ddd26feeb64f039a2c41296fcb3f5640".HexToByteArray();  // To address

var log = Log.Create(
    data: eventData,
    address: "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48",  // Contract address
    topics: new[] { topic1, topic2, topic3 }
);

// Or create log without data
var logWithoutData = Log.Create(
    address: "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48",
    topics: topic1, topic2, topic3
);
```

*Source: src/Nethereum.Model/Log.cs*

### Example 7: Working with Block Headers

```csharp
using Nethereum.Model;
using System.Numerics;

// Construct a block header
var blockHeader = new BlockHeader
{
    ParentHash = parentBlockHash,
    UnclesHash = unclesHash,
    Coinbase = "0x1f9090aaE28b8a3dCeaDf281B0F12828e676c326",  // Miner address
    StateRoot = stateRoot,
    TransactionsHash = transactionsTrieRoot,
    ReceiptHash = receiptsTrieRoot,
    BlockNumber = 18_000_000,
    LogsBloom = logsBloomFilter,
    Difficulty = 0,  // Post-merge (PoS) difficulty is always 0
    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
    GasLimit = 30_000_000,
    GasUsed = 12_543_210,
    MixHash = mixHash,
    ExtraData = extraData,
    Nonce = nonce,
    BaseFee = new BigInteger(15_000_000_000)  // EIP-1559 base fee
};

// Encode the block header for hashing
var encoded = BlockHeaderEncoder.Current.EncodeHeader(blockHeader);
```

*Source: src/Nethereum.Model/BlockHeader.cs*

### Example 8: Creating Transactions for Different Networks

```csharp
using Nethereum.Model;
using Nethereum.Signer;
using System.Numerics;

// Ethereum Mainnet transaction
var mainnetTx = new Transaction1559(
    chainId: (BigInteger)Chain.MainNet,  // 1
    nonce: 0,
    maxPriorityFeePerGas: new BigInteger(2_000_000_000),
    maxFeePerGas: new BigInteger(50_000_000_000),
    gasLimit: 21000,
    receiverAddress: "0x742d35Cc6634C0532925a3b844Bc9e7595f0bEb",
    amount: BigInteger.Parse("1000000000000000000"),
    data: "",
    accessList: null
);

// Polygon transaction (lower gas fees)
var polygonTx = new Transaction1559(
    chainId: (BigInteger)Chain.Polygon,  // 137
    nonce: 0,
    maxPriorityFeePerGas: new BigInteger(30_000_000_000),  // 30 gwei
    maxFeePerGas: new BigInteger(200_000_000_000),         // 200 gwei
    gasLimit: 21000,
    receiverAddress: "0x742d35Cc6634C0532925a3b844Bc9e7595f0bEb",
    amount: BigInteger.Parse("1000000000000000000"),
    data: "",
    accessList: null
);

// Base L2 transaction
var baseTx = new Transaction1559(
    chainId: (BigInteger)Chain.Base,  // 8453
    nonce: 0,
    maxPriorityFeePerGas: new BigInteger(100_000),      // Very low on L2
    maxFeePerGas: new BigInteger(1_000_000),
    gasLimit: 21000,
    receiverAddress: "0x742d35Cc6634C0532925a3b844Bc9e7595f0bEb",
    amount: BigInteger.Parse("1000000000000000000"),
    data: "",
    accessList: null
);
```

*Source: `Chain.cs` enum values*

### Example 9: Account Storage Encoding

```csharp
using Nethereum.Model;
using Nethereum.Util.HashProviders;
using Nethereum.Hex.HexConvertors.Extensions;

var sha3Provider = new Sha3KeccackHashProvider();

// Encode a storage key for the state trie
var storageKey = "0x0000000000000000000000000000000000000000000000000000000000000001".HexToByteArray();
var encodedKey = AccountStorage.EncodeKeyForStorage(storageKey, sha3Provider);

// Encode a storage value
var storageValue = "0x0000000000000000000000000000000000000000000000000de0b6b3a7640000".HexToByteArray();
var encodedValue = AccountStorage.EncodeValueForStorage(storageValue);

// These encoded values are used in the Patricia Merkle Trie
```

*Source: src/Nethereum.Model/Account.cs*

## API Reference

### Transaction Types

#### `TransactionType` Enum
- `LegacyTransaction` (-1): Pre-EIP-155 transactions
- `LegacyChainTransaction` (-2): EIP-155 transactions with chain ID
- `LegacyEIP2930` (0x01): Transactions with access lists
- `EIP1559` (0x02): Fee market transactions
- `EIP7702` (0x04): Account abstraction with authorization

#### `LegacyTransaction`
Pre-EIP-155 transaction format with basic fields.

**Key Properties:**
- `byte[] Nonce`: Transaction sequence number
- `byte[] GasPrice`: Gas price in wei
- `byte[] GasLimit`: Maximum gas units
- `byte[] ReceiveAddress`: Recipient address
- `byte[] Value`: Amount in wei
- `byte[] Data`: Contract call data or deployment code
- `Signature Signature`: Transaction signature (R, S, V)

**Key Methods:**
- `byte[] GetRLPEncoded()`: Get signed transaction RLP encoding
- `EthECKey GetKey()`: Recover signer's public key from signature

#### `LegacyTransactionChainId`
EIP-155 transaction with chain ID to prevent replay attacks.

Inherits from `LegacyTransaction` with added chain ID in V signature component.

#### `Transaction1559`
EIP-1559 fee market transaction.

**Key Properties:**
- `BigInteger ChainId`: Network chain identifier
- `BigInteger? Nonce`: Transaction sequence number
- `BigInteger? MaxPriorityFeePerGas`: Miner tip
- `BigInteger? MaxFeePerGas`: Maximum total fee per gas
- `BigInteger? GasLimit`: Gas limit
- `string ReceiverAddress`: Recipient
- `BigInteger? Amount`: Value to transfer
- `string Data`: Call data
- `List<AccessListItem> AccessList`: Optional access list

**Key Methods:**
- `byte[] GetRLPEncoded()`: Get complete RLP-encoded transaction
- `byte[] GetRLPEncodedRaw()`: Get RLP encoding for signing (without signature)

#### `Transaction2930`
EIP-2930 transaction with access list.

Similar structure to `Transaction1559` but uses `GasPrice` instead of base/priority fees.

#### `Transaction7702`
EIP-7702 account abstraction transaction.

Extends `Transaction1559` with:
- `List<Authorisation7702Signed> AuthorisationList`: Signed authorizations

#### `TransactionFactory`
Factory class for creating and decoding transactions.

**Key Methods:**
- `static ISignedTransaction CreateTransaction(byte[] rlp)`: Automatically detects and decodes any transaction type
- `static ISignedTransaction CreateTransaction(string rlpHex)`: Hex string overload
- `static ISignedTransaction Create1559Transaction(...)`: Create EIP-1559 transaction
- `static ISignedTransaction Create2930Transaction(...)`: Create EIP-2930 transaction
- `static ISignedTransaction Create7702Transaction(...)`: Create EIP-7702 transaction
- `static ISignedTransaction CreateLegacyTransaction(...)`: Create legacy transaction

### Block Models

#### `BlockHeader`
Ethereum block header structure.

**Properties:**
- `byte[] ParentHash`: Hash of parent block
- `byte[] UnclesHash`: Hash of uncle blocks list
- `string Coinbase`: Miner/validator address
- `byte[] StateRoot`: State trie root hash
- `byte[] TransactionsHash`: Transactions trie root hash
- `byte[] ReceiptHash`: Receipts trie root hash
- `BigInteger BlockNumber`: Block height
- `byte[] LogsBloom`: Bloom filter for logs
- `BigInteger Difficulty`: Mining difficulty (0 post-merge)
- `long Timestamp`: Block timestamp (Unix seconds)
- `long GasLimit`: Maximum gas for block
- `long GasUsed`: Total gas used in block
- `byte[] MixHash`: PoW mix hash (random post-merge)
- `byte[] ExtraData`: Arbitrary extra data
- `byte[] Nonce`: PoW nonce (0 post-merge)
- `BigInteger? BaseFee`: EIP-1559 base fee per gas

#### `BlockHeaderEncoder`
RLP encoder/decoder for block headers.

**Methods:**
- `byte[] EncodeHeader(BlockHeader header)`: Encode block header to RLP
- `BlockHeader DecodeHeader(byte[] rlp)`: Decode RLP to block header

### Account Models

#### `Account`
Ethereum account state.

**Properties:**
- `BigInteger Nonce`: Transaction count or contract creation count
- `BigInteger Balance`: Account balance in wei
- `byte[] StateRoot`: Storage trie root (default: empty trie hash)
- `byte[] CodeHash`: Contract code hash (default: empty data hash)

#### `AccountStorage`
Utilities for encoding account storage.

**Methods:**
- `static byte[] EncodeKeyForStorage(byte[] key, Sha3KeccackHashProvider)`: Encode storage key
- `static byte[] EncodeValueForStorage(byte[] value)`: Encode storage value

### Signature Models

#### `Signature : ISignature`
ECDSA signature components.

**Properties:**
- `byte[] R`: Signature R component
- `byte[] S`: Signature S component
- `byte[] V`: Recovery identifier

**Constructors:**
- `Signature()`: Default constructor
- `Signature(byte[] r, byte[] s, byte[] v)`: Create with components

#### `SignatureExtensions`
Extension methods for signature operations.

**Key Methods:**
- `bool IsVSignedForChain(this Signature)`: Check if V indicates chain-specific signature
- `BigInteger GetChainFromVChain(BigInteger v)`: Extract chain ID from V

### Event Log Models

#### `Log`
Smart contract event log.

**Properties:**
- `string Address`: Contract address that emitted the log
- `byte[] Data`: Non-indexed event data
- `List<byte[]> Topics`: Indexed event topics (event signature + indexed parameters)

**Static Methods:**
- `static Log Create(byte[] data, string address, params byte[][] topics)`: Create log with data
- `static Log Create(string address, params byte[][] topics)`: Create log without data

#### `LogBloomFilter`
Bloom filter for efficient log filtering.

### Access List Models

#### `AccessListItem`
EIP-2930 access list entry.

**Properties:**
- `string Address`: Contract address
- `List<byte[]> StorageKeys`: Storage slots to pre-warm

**Constructors:**
- `AccessListItem()`: Default constructor
- `AccessListItem(string address, List<byte[]> storageKeys)`: Create with values

#### `AccessListRLPEncoderDecoder`
RLP encoder/decoder for access lists.

### Authorization Models (EIP-7702)

#### `Authorisation7702Signed`
Signed authorization for account abstraction.

**Properties:**
- Authorization details for delegated transaction execution

#### `AuthorisationListRLPEncoderDecoder`
RLP encoder/decoder for authorization lists.

### Enumerations

#### `Chain`
Well-known Ethereum network chain IDs.

**Common Values:**
- `MainNet = 1`: Ethereum Mainnet
- `Sepolia = 11155111`: Sepolia testnet
- `Polygon = 137`: Polygon PoS
- `Arbitrum = 42161`: Arbitrum One
- `Optimism = 10`: Optimism
- `Base = 8453`: Base L2
- `Avalanche = 43114`: Avalanche C-Chain
- `Binance = 56`: BNB Chain

*See `Chain.cs` for the complete list of 100+ networks*

### Utility Classes

#### `DefaultValues`
Default constant values used throughout the package.

**Constants:**
- `EMPTY_BYTE_ARRAY`: Empty byte array
- `EMPTY_TRIE_HASH`: Keccak-256 of empty trie
- `EMPTY_DATA_HASH`: Keccak-256 of empty data

#### `VRecoveryAndChainCalculations`
Utilities for signature V value and chain ID calculations.

**Methods:**
- `static BigInteger GetChainFromVChain(BigInteger vChain)`: Extract chain ID from V
- V value encoding/decoding for EIP-155

## Related Packages

### Used By (Consumers)

- **Nethereum.Signer** - Signs transactions using the transaction models defined here
- **Nethereum.RPC** - RPC methods return block headers, transactions, and logs
- **Nethereum.Contracts** - Contract deployment and interaction uses transaction models
- **Nethereum.Accounts** - Account management uses transaction types for sending
- **Nethereum.BlockchainProcessing** - Processes blocks, transactions, and logs

### Dependencies

- **Nethereum.RLP** - All models use RLP encoding for serialization
- **Nethereum.Util** - Uses Keccak-256 hashing, address validation, and byte conversions

## Important Notes

### Transaction Type Detection

When decoding transactions from RLP:
- The first byte determines if it's a typed transaction (0x00-0x7F) or legacy (≥0xC0)
- `TransactionFactory.CreateTransaction()` automatically detects the type
- Legacy transactions are identified by their RLP list structure

### V Signature Component

The V value in signatures serves dual purposes:
- **Recovery ID**: Allows deriving the public key (and address) from the signature
- **Chain ID** (EIP-155): For legacy transactions, V encodes chain ID as `V = CHAIN_ID * 2 + 35 + {0,1}`
- For typed transactions (EIP-2930, EIP-1559, EIP-7702), V is simply 0 or 1

### Gas Pricing Models

Different transaction types use different gas pricing:
- **Legacy & EIP-2930**: Single `gasPrice` (user pays gasPrice × gasUsed)
- **EIP-1559+**: `maxFeePerGas` and `maxPriorityFeePerGas`
  - Actual fee = min(maxFeePerGas, baseFee + maxPriorityFeePerGas) × gasUsed
  - BaseFee is burned, priority fee goes to validator

### Chain ID Usage

Always specify the correct chain ID to prevent:
- **Replay attacks**: Same transaction valid on multiple chains (e.g., mainnet and testnet)
- **Lost funds**: Sending to wrong network addresses

### RLP Encoding for Signing vs. Transmission

Transactions have two RLP encodings:
- **Raw encoding** (`GetRLPEncodedRaw()`): Unsigned, used for signing
- **Full encoding** (`GetRLPEncoded()`): Includes signature, used for transmission

### Block Header Post-Merge Changes

After Ethereum's transition to Proof-of-Stake (The Merge):
- `Difficulty` is always 0
- `Nonce` is 0 (no longer used for mining)
- `MixHash` contains randomness from the beacon chain
- `Coinbase` is the fee recipient (validator or pool)

### Account State Storage

Account state is stored in a Patricia Merkle Trie:
- **StateRoot**: Root hash of the account's storage trie
- **CodeHash**: Keccak-256 of contract bytecode (empty hash for EOAs)
- These hashes allow verification without storing full state

## Additional Resources

### Ethereum Improvement Proposals (EIPs)
- [EIP-155](https://eips.ethereum.org/EIPS/eip-155): Simple replay attack protection (chain ID)
- [EIP-2718](https://eips.ethereum.org/EIPS/eip-2718): Typed transaction envelope
- [EIP-2930](https://eips.ethereum.org/EIPS/eip-2930): Optional access lists
- [EIP-1559](https://eips.ethereum.org/EIPS/eip-1559): Fee market change (base fee + priority fee)
- [EIP-7702](https://eips.ethereum.org/EIPS/eip-7702): Set EOA account code for one transaction

### Ethereum Protocol
- [Ethereum Yellow Paper](https://ethereum.github.io/yellowpaper/paper.pdf): Section 4.1 (Account State), Section 4.3 (Transactions)
- [RLP Encoding Specification](https://ethereum.org/en/developers/docs/data-structures-and-encoding/rlp/)
- [Ethereum.org - Transactions](https://ethereum.org/en/developers/docs/transactions/)
- [Ethereum.org - Blocks](https://ethereum.org/en/developers/docs/blocks/)

### Nethereum Documentation
- [Nethereum Documentation](https://docs.nethereum.com)
- [Transaction Signing Guide](https://docs.nethereum.com/en/latest/nethereum-signing-transactions/)
- [Nethereum GitHub](https://github.com/Nethereum/Nethereum)
