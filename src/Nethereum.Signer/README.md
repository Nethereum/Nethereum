# Nethereum.Signer

Cryptographic signing library for Ethereum transactions, messages, and typed data using secp256k1 elliptic curve cryptography.

## Overview

Nethereum.Signer is the **core cryptographic engine** for all signing operations in Nethereum. It provides secure key generation, transaction signing (Legacy, EIP-155, EIP-1559, EIP-7702), message signing, and signature recovery using the secp256k1 elliptic curve (same as Bitcoin and Ethereum).

**Key Features:**
- Secure random EC key pair generation using BouncyCastle
- All Ethereum transaction types: Legacy, EIP-155 (replay protection), EIP-1559 (fee market), EIP-7702 (account abstraction)
- Ethereum message signing with "\x19Ethereum Signed Message:\n" prefix
- Signature recovery (ecRecover) to derive addresses from signatures
- Deterministic ECDSA (RFC 6979) for reproducible signatures
- Support for external signers (hardware wallets, key vaults)
- Uses BouncyCastle for cryptography (NBitcoin.Secp256k1 on .NET 6+)

## Installation

```bash
dotnet add package Nethereum.Signer
```

Or via Package Manager Console:

```powershell
Install-Package Nethereum.Signer
```

## Dependencies

**External:**
- **BouncyCastle.Cryptography** (net472, net6.0+) or **Portable.BouncyCastle** (other frameworks)
- **NBitcoin.Secp256k1** (net6.0+ for optimized signing)

**Nethereum:**
- **Nethereum.Hex** - Hex encoding/decoding
- **Nethereum.Util** - Keccak-256 hashing, address utilities
- **Nethereum.RLP** - RLP encoding for transactions
- **Nethereum.ABI** - ABI encoding for typed data (EIP-712)
- **Nethereum.Model** - Transaction and signature models

## Key Concepts

### secp256k1 Elliptic Curve

Ethereum uses the secp256k1 elliptic curve (same as Bitcoin) for public-key cryptography:
- **Private Key**: 256-bit random number (64 hex characters)
- **Public Key**: EC point derived from private key (128 hex characters uncompressed)
- **Address**: Last 20 bytes of Keccak-256 hash of public key

### ECDSA Signatures

Ethereum signatures consist of three components:
- **r**: 32 bytes - x-coordinate of random EC point
- **s**: 32 bytes - proof computed from private key and message
- **v**: 1 byte - recovery ID (allows deriving public key from signature)

Combined signature format: `0x` + r (64 hex) + s (64 hex) + v (2 hex) = 132 hex characters

### Ethereum Message Signing

Ethereum messages are prefixed before signing to prevent signing malicious transactions:

```
"\x19Ethereum Signed Message:\n" + message.length + message
```

This prefix ensures that a signed message cannot be a valid transaction.

### Transaction Types

1. **Legacy**: Original transaction format (no replay protection)
2. **EIP-155**: Adds chain ID for replay protection
3. **EIP-1559**: Fee market with base fee + priority fee
4. **EIP-7702**: Account abstraction with authorization lists

## Quick Start

```csharp
using Nethereum.Signer;
using Nethereum.Hex.HexConvertors.Extensions;

// Generate new key pair
var key = EthECKey.GenerateKey();
string privateKey = key.GetPrivateKey();
string address = key.GetPublicAddress();

Console.WriteLine($"Address: {address}");
Console.WriteLine($"Private Key: {privateKey}");

// Sign a message
var signer = new EthereumMessageSigner();
string message = "Hello Ethereum!";
string signature = signer.EncodeUTF8AndSign(message, key);

// Recover address from signature
string recoveredAddress = signer.EncodeUTF8AndEcRecover(message, signature);
Console.WriteLine($"Recovered: {recoveredAddress}");
```

## Usage Examples

### Example 1: Generate and Use EC Keys

```csharp
using Nethereum.Signer;
using Nethereum.Hex.HexConvertors.Extensions;

// Generate new random key pair
var key = EthECKey.GenerateKey();

// Get private key (KEEP SECRET!)
string privateKeyHex = key.GetPrivateKey();
byte[] privateKeyBytes = key.GetPrivateKeyAsBytes();

// Get public key
byte[] publicKey = key.GetPubKey(); // Uncompressed (65 bytes with 0x04 prefix)
byte[] publicKeyCompressed = key.GetPubKeyCompressed(); // Compressed (33 bytes)

// Get Ethereum address
string address = key.GetPublicAddress();
Console.WriteLine($"Address: {address}"); // 0x...

// Recreate key from existing private key
var existingKey = new EthECKey("0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7");
Console.WriteLine($"Restored address: {existingKey.GetPublicAddress()}");
```

### Example 2: Sign Ethereum Messages (Real Test Example)

```csharp
using Nethereum.Signer;
using Nethereum.Hex.HexConvertors.Extensions;

// Example from EthereumMessageSignerTests.cs
var privateKey = "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";
var address = "0x12890D2cce102216644c59daE5baed380d84830c";
var message = "Hello from Nethereum";

var signer = new EthereumMessageSigner();
var key = new EthECKey(privateKey);

// Sign message (automatically adds Ethereum prefix and hashes)
string signature = signer.EncodeUTF8AndSign(message, key);

// Expected signature from test
var expectedSignature = "0xe20e42c13fbf52a5d65229f4dd1dcd3255691166ce2852456631baf4836afd4630480609a76794ee3018c5514ee3a0592031cf2490e7356dffe4ed202606f5181c";

Console.WriteLine($"Signature: {signature}");
Console.WriteLine($"Match: {signature == expectedSignature}");

// Recover signer's address from signature
string recoveredAddress = signer.EncodeUTF8AndEcRecover(message, signature);
Console.WriteLine($"Recovered address: {recoveredAddress}");
Console.WriteLine($"Match original: {address.Equals(recoveredAddress, StringComparison.OrdinalIgnoreCase)}");
```

### Example 3: Verify MetaMask / MEW Signatures (Real Test Example)

```csharp
using Nethereum.Signer;
using Nethereum.Util;

// Verify signature from MyEtherWallet (from EthereumMessageSignerTests.cs)
var address = "0xe651c5051ce42241765bbb24655a791ff0ec8d13";
var message = "wee test message 18/09/2017 02:55PM";
var mewSignature = "0xf5ac62a395216a84bd595069f1bb79f1ee08a15f07bb9d9349b3b185e69b20c60061dbe5cdbe7b4ed8d8fea707972f03c21dda80d99efde3d96b42c91b2703211b";

var signer = new EthereumMessageSigner();
string recoveredAddress = signer.EncodeUTF8AndEcRecover(message, mewSignature);

bool isValid = address.IsTheSameAddress(recoveredAddress);
Console.WriteLine($"MEW signature valid: {isValid}");
Console.WriteLine($"Expected: {address}");
Console.WriteLine($"Recovered: {recoveredAddress}");

// This works with signatures from:
// - MetaMask
// - MyEtherWallet (MEW)
// - Ledger
// - Trezor
// - Any wallet following EIP-191 standard
```

### Example 4: Sign EIP-155 Transaction with Chain ID (Real Test Example)

```csharp
using Nethereum.Signer;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Hex.HexConvertors.Extensions;
using System.Numerics;

// Example from Eip155SignerTests.cs
var privateKey = "4646464646464646464646464646464646464646464646464646464646464646";
var key = new EthECKey(privateKey);

// Create transaction with chain ID (EIP-155 for replay protection)
var nonce = 9.ToBytesForRLPEncoding();
var gasPrice = BigInteger.Parse("20000000000").ToBytesForRLPEncoding();
var gasLimit = 21000.ToBytesForRLPEncoding();
var to = "0x3535353535353535353535353535353535353535".HexToByteArray();
var value = BigInteger.Parse("1000000000000000000").ToBytesForRLPEncoding();
var data = "".HexToByteArray();
var chainId = 1.ToBytesForRLPEncoding(); // Mainnet

var tx = new LegacyTransactionChainId(nonce, gasPrice, gasLimit, to, value, data, chainId);

// Sign transaction
var signer = new LegacyTransactionSigner();
signer.SignTransaction(privateKey.HexToByteArray(), tx);

// V value includes chain ID: v = {0,1} + CHAIN_ID * 2 + 35
Console.WriteLine($"V value: {tx.Signature.V.ToIntFromRLPDecoded()}"); // 37 for mainnet

// Get signed transaction bytes (ready to broadcast)
byte[] signedTxBytes = tx.GetRLPEncoded();
string signedTxHex = signedTxBytes.ToHex(true);
Console.WriteLine($"Signed tx: {signedTxHex}");

// Recover signer from signed transaction
var recoveredTx = new LegacyTransactionChainId(signedTxBytes);
string recoveredAddress = recoveredTx.GetKey().GetPublicAddress();
Console.WriteLine($"Signer: {recoveredAddress}");
Console.WriteLine($"Match: {key.GetPublicAddress() == recoveredAddress}");
```

### Example 5: Sign EIP-1559 Transaction (Fee Market)

```csharp
using Nethereum.Signer;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Hex.HexConvertors.Extensions;
using System.Numerics;

var privateKey = "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";

// EIP-1559 transaction with maxFeePerGas and maxPriorityFeePerGas
var chainId = new BigInteger(1); // Mainnet
var nonce = new BigInteger(5);
var maxPriorityFeePerGas = new BigInteger(2000000000); // 2 gwei
var maxFeePerGas = new BigInteger(100000000000); // 100 gwei
var gasLimit = new BigInteger(21000);
var to = "0x3535353535353535353535353535353535353535";
var value = new BigInteger(1000000000000000000); // 1 ETH
var data = "".HexToByteArray();

var tx = new Transaction1559(
    chainId,
    nonce,
    maxPriorityFeePerGas,
    maxFeePerGas,
    gasLimit,
    to,
    value,
    data,
    null // access list
);

// Sign
var signer = new Transaction1559Signer();
signer.SignTransaction(privateKey.HexToByteArray(), tx);

// Transaction type 0x02 for EIP-1559
byte[] signedTx = tx.GetRLPEncoded();
Console.WriteLine($"Type: 0x{signedTx[0]:X2}"); // 0x02

// Recover signer
var recovered = new Transaction1559(signedTx);
Console.WriteLine($"Signer: {recovered.Key.GetPublicAddress()}");
```

### Example 6: Hash and Sign Raw Messages

```csharp
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Hex.HexConvertors.Extensions;

var privateKey = "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";
var key = new EthECKey(privateKey);

// Method 1: Sign with Ethereum prefix (most common)
var signer = new EthereumMessageSigner();
string message = "test";
string signature1 = signer.EncodeUTF8AndSign(message, key);
Console.WriteLine($"Ethereum signature: {signature1}");

// Method 2: Hash message yourself, then sign with prefix
var hasher = new Sha3Keccack();
byte[] messageHash = hasher.CalculateHash(message);
string signature2 = signer.Sign(messageHash, key);
Console.WriteLine($"Pre-hashed signature: {signature2}");

// Method 3: Sign raw hash WITHOUT Ethereum prefix (not recommended)
var rawSigner = new MessageSigner();
string signature3 = rawSigner.Sign(messageHash, key);
Console.WriteLine($"Raw signature: {signature3}");

// Verify: signature1 == signature2 (both use Ethereum prefix)
Console.WriteLine($"Ethereum signatures match: {signature1 == signature2}");
```

### Example 7: Deterministic Key Generation from Seed

```csharp
using Nethereum.Signer;
using Nethereum.Hex.HexConvertors.Extensions;
using System.Text;

// Generate deterministic key from seed (useful for testing)
byte[] seed = Encoding.UTF8.GetBytes("my-secret-seed-phrase");
var key1 = EthECKey.GenerateKey(seed);
var key2 = EthECKey.GenerateKey(seed);

// Same seed = same key
Console.WriteLine($"Key 1: {key1.GetPrivateKey()}");
Console.WriteLine($"Key 2: {key2.GetPrivateKey()}");
Console.WriteLine($"Match: {key1.GetPrivateKey() == key2.GetPrivateKey()}");

// Different seed = different key
byte[] differentSeed = Encoding.UTF8.GetBytes("different-seed");
var key3 = EthECKey.GenerateKey(differentSeed);
Console.WriteLine($"Key 3: {key3.GetPrivateKey()}");
Console.WriteLine($"Different: {key1.GetPrivateKey() != key3.GetPrivateKey()}");

// WARNING: For production, use truly random keys:
var randomKey = EthECKey.GenerateKey(); // Cryptographically secure random
```

### Example 8: Verify Signature with AllowOnlyLowS (Prevent Malleability)

```csharp
using Nethereum.Signer;
using Nethereum.Signer.Crypto;
using Nethereum.Hex.HexConvertors.Extensions;

var privateKey = "0x4646464646464646464646464646464646464646464646464646464646464646";
var key = new EthECKey(privateKey);
var message = "test message".HexToByteArray();

// Sign message
var signer = new MessageSigner();
string signatureHex = signer.Sign(message, key);
byte[] signatureBytes = signatureHex.HexToByteArray();

// Parse signature
var signature = new EthECDSASignature(signatureBytes);

// Verify with low-S enforcement (prevents signature malleability)
bool isValid = key.VerifyAllowingOnlyLowS(message, signature);
Console.WriteLine($"Signature valid (low-S only): {isValid}");

// Without low-S enforcement (accepts both high and low S values)
bool isValidAny = key.Verify(message, signature);
Console.WriteLine($"Signature valid (any S): {isValidAny}");

// Why enforce low-S?
// ECDSA signatures have two valid S values (s and n-s)
// Bitcoin/Ethereum enforce low-S to prevent transaction malleability
// Always use VerifyAllowingOnlyLowS for security
```

### Example 9: Shared Secret Calculation (ECDH)

```csharp
using Nethereum.Signer;
using Nethereum.Hex.HexConvertors.Extensions;

// Alice generates key pair
var aliceKey = EthECKey.GenerateKey();
Console.WriteLine($"Alice address: {aliceKey.GetPublicAddress()}");

// Bob generates key pair
var bobKey = EthECKey.GenerateKey();
Console.WriteLine($"Bob address: {bobKey.GetPublicAddress()}");

// Alice calculates shared secret using Bob's public key
var bobPublicKey = new EthECKey(bobKey.GetPubKey(), false);
byte[] aliceSharedSecret = aliceKey.CalculateCommonSecret(bobPublicKey);

// Bob calculates shared secret using Alice's public key
var alicePublicKey = new EthECKey(aliceKey.GetPubKey(), false);
byte[] bobSharedSecret = bobKey.CalculateCommonSecret(alicePublicKey);

// Both shared secrets are identical
Console.WriteLine($"Alice secret: {aliceSharedSecret.ToHex(true)}");
Console.WriteLine($"Bob secret: {bobSharedSecret.ToHex(true)}");
Console.WriteLine($"Secrets match: {aliceSharedSecret.SequenceEqual(bobSharedSecret)}");

// Use shared secret for symmetric encryption (AES, etc.)
// This is the basis of ECIES (Elliptic Curve Integrated Encryption Scheme)
```

## API Reference

### EthECKey

Ethereum elliptic curve key pair.

```csharp
// Constructors
public EthECKey(string privateKeyHex);
public EthECKey(byte[] keyData, bool isPrivate);

// Static methods
public static EthECKey GenerateKey();
public static EthECKey GenerateKey(byte[] seed);

// Properties & Methods
public string GetPrivateKey(); // Hex string with 0x prefix
public byte[] GetPrivateKeyAsBytes();
public byte[] GetPubKey(); // Uncompressed (65 bytes)
public byte[] GetPubKeyCompressed(); // Compressed (33 bytes)
public string GetPublicAddress(); // Ethereum address (0x...)

// Signing & Verification
public EthECDSASignature Sign(byte[] hash);
public EthECDSASignature SignAndCalculateV(byte[] hash);
public bool Verify(byte[] hash, EthECDSASignature signature);
public bool VerifyAllowingOnlyLowS(byte[] hash, EthECDSASignature signature);

// ECDH
public byte[] CalculateCommonSecret(EthECKey publicKey);
```

### EthereumMessageSigner

Sign and verify Ethereum messages with standard prefix.

```csharp
public class EthereumMessageSigner : MessageSigner
{
    // Sign message (adds Ethereum prefix)
    public string EncodeUTF8AndSign(string message, EthECKey key);
    public override string Sign(byte[] message, EthECKey key);
    public override string HashAndSign(byte[] message, EthECKey key);

    // Recover signer address
    public string EncodeUTF8AndEcRecover(string message, string signature);
    public override string EcRecover(byte[] message, string signature);
    public override string HashAndEcRecover(string message, string signature);

    // Hash with Ethereum prefix
    public byte[] HashPrefixedMessage(string message);
    public byte[] HashPrefixedMessage(byte[] message);
}
```

### MessageSigner

Raw message signing (without Ethereum prefix).

```csharp
public class MessageSigner
{
    public virtual string Sign(byte[] message, EthECKey key);
    public virtual string Sign(byte[] message, string privateKey);
    public virtual string HashAndSign(string message, EthECKey key);

    public virtual string EcRecover(byte[] message, string signature);
    public virtual string HashAndEcRecover(string message, string signature);

    public byte[] Hash(string message);
    public byte[] Hash(byte[] message);
}
```

### Transaction Signers

```csharp
// Legacy transactions
public class LegacyTransactionSigner
{
    public void SignTransaction(byte[] privateKey, LegacyTransaction transaction);
    public void SignTransaction(byte[] privateKey, LegacyTransactionChainId transaction);
}

// EIP-1559 transactions
public class Transaction1559Signer
{
    public void SignTransaction(byte[] privateKey, Transaction1559 transaction);
}

// EIP-7702 transactions
public class Transaction7702Signer
{
    public void SignTransaction(byte[] privateKey, Transaction7702 transaction);
}

// Authorization lists (EIP-7702)
public class Authorisation7702Signer
{
    public void Sign(byte[] privateKey, Authorisation7702 authorisation);
}
```

### EthECDSASignature

ECDSA signature representation.

```csharp
public class EthECDSASignature
{
    public byte[] R { get; }
    public byte[] S { get; }
    public byte[] V { get; }

    public EthECDSASignature(byte[] signatureBytes);
    public EthECDSASignature(ECDSASignature signature, int recId);

    public bool IsLowS { get; }
    public byte[] ToByteArray();
}
```

## Related Packages

### Used By (Consumers)
- **Nethereum.Accounts** - Account management with key-based signing
- **Nethereum.KeyStore** - Encrypted keystore (UTC/JSON) wallet files
- **Nethereum.HDWallet** - BIP32/BIP39 hierarchical deterministic wallets
- **Nethereum.Signer.EIP712** - EIP-712 typed structured data signing
- **Nethereum.Signer.Ledger** - Ledger hardware wallet integration
- **Nethereum.Signer.Trezor** - Trezor hardware wallet integration
- **Nethereum.Signer.AzureKeyVault** - Azure Key Vault signing
- **Nethereum.Signer.AWSKeyManagement** - AWS KMS signing

### Dependencies
- **Nethereum.Hex** - Hex encoding/decoding
- **Nethereum.Util** - Keccak hashing, address utilities
- **Nethereum.RLP** - RLP encoding
- **Nethereum.ABI** - ABI encoding
- **Nethereum.Model** - Transaction models

## Important Notes

### Private Key Security

**NEVER expose private keys** in production code:

```csharp
// ❌ WRONG - Hard-coded private key
var key = new EthECKey("0x1234567890abcdef...");

// ✅ CORRECT - Load from secure storage
string privateKey = Environment.GetEnvironmentVariable("PRIVATE_KEY");
var key = new EthECKey(privateKey);

// ✅ BETTER - Use hardware wallet or key vault
// See Nethereum.Signer.Ledger, Nethereum.Signer.AzureKeyVault
```

### Signature Malleability

Always use `VerifyAllowingOnlyLowS` to prevent signature malleability:

```csharp
// ❌ WRONG - Allows high-S signatures (malleable)
bool valid = key.Verify(hash, signature);

// ✅ CORRECT - Enforces low-S (prevents malleability)
bool valid = key.VerifyAllowingOnlyLowS(hash, signature);
```

### Chain ID for Replay Protection

Always specify chain ID for EIP-155+ transactions:

```csharp
// ❌ WRONG - No replay protection
var tx = new LegacyTransaction(nonce, gasPrice, gasLimit, to, value, data);

// ✅ CORRECT - EIP-155 with chain ID
var chainId = 1; // Mainnet
var tx = new LegacyTransactionChainId(nonce, gasPrice, gasLimit, to, value, data, chainId.ToBytesForRLPEncoding());
```

### Ethereum Message Prefix

Use `EthereumMessageSigner` (not `MessageSigner`) for user-facing messages:

```csharp
// ❌ WRONG - No Ethereum prefix (could sign malicious transaction)
var signer = new MessageSigner();
string sig = signer.Sign(message, key);

// ✅ CORRECT - Adds Ethereum prefix (safe for user messages)
var signer = new EthereumMessageSigner();
string sig = signer.EncodeUTF8AndSign(message, key);
```

### BouncyCastle vs NBitcoin.Secp256k1

.NET 6+ uses NBitcoin.Secp256k1 for better performance:

```csharp
#if NET6_0_OR_GREATER
// Enable recoverable signatures (uses NBitcoin.Secp256k1)
EthECKey.SignRecoverable = true;
#endif
```

This is faster and avoids post-signature recovery ID calculation.

### Thread Safety

`EthECKey` instances are **not thread-safe**. Don't share key instances across threads without synchronization.

## Additional Resources

- [secp256k1 Curve Specification](https://www.secg.org/sec2-v2.pdf)
- [EIP-155: Simple Replay Attack Protection](https://eips.ethereum.org/EIPS/eip-155)
- [EIP-1559: Fee Market](https://eips.ethereum.org/EIPS/eip-1559)
- [EIP-7702: Set EOA account code](https://eips.ethereum.org/EIPS/eip-7702)
- [EIP-191: Signed Data Standard](https://eips.ethereum.org/EIPS/eip-191)
- [RFC 6979: Deterministic ECDSA](https://tools.ietf.org/html/rfc6979)
- [BouncyCastle Documentation](https://www.bouncycastle.org/csharp/)
- [Nethereum Documentation](http://docs.nethereum.com/)

## License

This package is part of the Nethereum project and follows the same MIT license.
