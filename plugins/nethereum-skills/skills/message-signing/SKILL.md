---
name: message-signing
description: Sign and verify Ethereum messages with Nethereum. Use when the user wants to sign a message, verify a signature, recover a signer address, use personal_sign, or implement login-with-Ethereum style authentication.
user-invocable: true
---

# Message Signing with Nethereum

NuGet: `Nethereum.Signer`

Source: `PersonalSignDocExampleTests`, `SignerDocExampleTests`

## Required Usings

```csharp
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Hex.HexConvertors.Extensions;
```

## Sign a UTF-8 Message and Recover Address

The most common pattern. Signs using the Ethereum personal_sign prefix (`\x19Ethereum Signed Message:\n` + length).

```csharp
var signer = new EthereumMessageSigner();
var message = "Hello from Nethereum";

var signature = signer.EncodeUTF8AndSign(message, new EthECKey(privateKey));

var recoveredAddress = signer.EncodeUTF8AndEcRecover(message, signature);
// recoveredAddress matches the signer's public address
```

## Sign Raw Bytes

Use when the payload is binary rather than a UTF-8 string.

```csharp
var signer = new EthereumMessageSigner();
var data = new byte[] { 0x48, 0x65, 0x6c, 0x6c, 0x6f }; // "Hello"

var signature = signer.Sign(data, privateKey);
var recoveredAddress = signer.EcRecover(data, signature);

Assert.True(expectedAddress.IsTheSameAddress(recoveredAddress));
```

## Verify External Wallet Signature (MetaMask, MEW)

Works with any wallet that implements `personal_sign`. Pass the original message and the hex signature.

```csharp
var expectedAddress = "0xe651c5051ce42241765bbb24655a791ff0ec8d13";
var message = "wee test message 18/09/2017 02:55PM";
var signature = "0xf5ac62a395216a84bd595069f1bb79f1ee08a15f07bb9d9349b3b185e69b20c60061dbe5cdbe7b4ed8d8fea707972f03c21dda80d99efde3d96b42c91b2703211b";

var signer = new EthereumMessageSigner();
var recoveredAddress = signer.EncodeUTF8AndEcRecover(message, signature);

Assert.True(expectedAddress.IsTheSameAddress(recoveredAddress));
```

## HashAndSign / HashAndEcRecover Shortcuts

Shorthand that hashes the message before signing. Useful when you want a deterministic hash-based signature without the personal_sign prefix.

```csharp
var signer = new EthereumMessageSigner();
var message = "test";

var signature = signer.HashAndSign(message, privateKey);
var recovered = signer.HashAndEcRecover(message, signature);

Assert.True(expectedAddress.IsTheSameAddress(recovered));
```

## Low-S Signature Verification (VerifyAllowingOnlyLowS)

EIP-2 requires the S value of ECDSA signatures to be in the lower half of the curve order. Use `VerifyAllowingOnlyLowS` for strict validation.

```csharp
var ecKey = new EthECKey(privateKey);
var message = "test message";

var signer = new EthereumMessageSigner();
var signature = signer.EncodeUTF8AndSign(message, ecKey);

var prefixedHash = signer.HashPrefixedMessage(message);
var ethSignature = MessageSigner.ExtractEcdsaSignature(signature);

Assert.True(ecKey.VerifyAllowingOnlyLowS(prefixedHash, ethSignature));
```

## EC Key Management

Generate a new key or reconstruct from a known private key.

```csharp
// Generate a new random key
var ecKey = EthECKey.GenerateKey();
var privateKeyHex = ecKey.GetPrivateKey();
var publicKeyBytes = ecKey.GetPubKey();
var address = ecKey.GetPublicAddress();

// Reconstruct from existing private key
var reconstructed = new EthECKey(privateKeyHex);
Assert.Equal(address, reconstructed.GetPublicAddress());
```

## Method Reference

| Method | Purpose |
|--------|---------|
| `EncodeUTF8AndSign` | Sign UTF-8 string with personal_sign prefix |
| `EncodeUTF8AndEcRecover` | Recover address from UTF-8 signed message |
| `Sign(byte[], key)` | Sign raw bytes with personal_sign prefix |
| `EcRecover(byte[], sig)` | Recover address from raw byte signature |
| `HashAndSign` | Hash then sign (no personal prefix) |
| `HashAndEcRecover` | Hash then recover (no personal prefix) |
| `HashPrefixedMessage` | Get the prefixed hash without signing |
| `ExtractEcdsaSignature` | Parse hex signature into ECDSA components |
| `VerifyAllowingOnlyLowS` | Strict EIP-2 low-S verification |
