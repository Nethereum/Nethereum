---
name: keystore
description: Encrypt and decrypt Ethereum private keys using Web3 Secret Storage (keystore JSON files) with Nethereum. Use this skill whenever the user asks about encrypting private keys, keystore files, Web3 secret storage, Scrypt encryption, PBKDF2, password-protected keys, key encryption, or loading encrypted wallets in C#/.NET.
user-invocable: true
---

# KeyStore Files with Nethereum

NuGet: `Nethereum.KeyStore`

```bash
dotnet add package Nethereum.KeyStore
```

## Encrypt (Scrypt — recommended)

```csharp
using Nethereum.KeyStore;
using Nethereum.Signer;

var ecKey = EthECKey.GenerateKey();
var address = ecKey.GetPublicAddress();
var privateKeyBytes = ecKey.GetPrivateKeyAsBytes();

var keyStoreService = new KeyStoreScryptService();
var json = keyStoreService.EncryptAndGenerateKeyStoreAsJson(
    "your-strong-password", privateKeyBytes, address);

File.WriteAllText($"keystore-{address}.json", json);
```

### Custom Scrypt Parameters
```csharp
// Light: N=32 (fast — for WASM, mobile, tests)
var scryptParams = new ScryptParams { N = 32, R = 8, P = 6 };
var json = keyStoreService.EncryptAndGenerateKeyStoreAsJson(
    "password", privateKeyBytes, address, scryptParams);
```

## Encrypt (PBKDF2 — legacy)

```csharp
var keyStoreService = new KeyStorePbkdf2Service();
var json = keyStoreService.EncryptAndGenerateKeyStoreAsJson(
    "password", privateKeyBytes, address);
```

## Decrypt

```csharp
var json = File.ReadAllText("keystore-file.json");

// Auto-detect KDF type
var keyStoreService = new KeyStoreService();
var privateKeyBytes = keyStoreService.DecryptKeyStoreFromJson("your-password", json);
var account = new Nethereum.Web3.Accounts.Account(privateKeyBytes, chainId: 1);
```

## Detect KDF Type

```csharp
var kdfType = KeyStoreKdfChecker.GetKdfType(json);
// Returns "scrypt" or "pbkdf2"
```

## Default Service

```csharp
var service = new KeyStoreService();

// Encrypt (Scrypt default)
var json = service.EncryptAndGenerateDefaultKeyStoreAsJson(
    "password", privateKeyBytes, address);

// Decrypt (auto-detect)
var key = service.DecryptKeyStoreFromJson("password", json);
```

For full documentation, see: https://docs.nethereum.com/docs/signing-and-key-management/guide-keystore
