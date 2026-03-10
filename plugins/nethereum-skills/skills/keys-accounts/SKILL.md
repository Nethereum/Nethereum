---
name: keys-accounts
description: Manage Ethereum keys, accounts, and keystores with Nethereum. Use when the user needs to generate keys, create accounts, encrypt/decrypt keystores, load wallets, or work with ViewOnlyAccount. Also triggers for private key, public address, or key management questions.
user-invocable: true
---

# Keys and Accounts with Nethereum

NuGet: `Nethereum.Web3`

## Generate EC key

```csharp
var ecKey = EthECKey.GenerateKey();

var privateKeyHex = ecKey.GetPrivateKey();
var publicKeyBytes = ecKey.GetPubKey();
var address = ecKey.GetPublicAddress();

// Reconstruct from private key
var reconstructed = new EthECKey(privateKeyHex);
// reconstructed.GetPublicAddress() == address
```

Source: `SignerDocExampleTests.ShouldGenerateKeyAndDerivePublicKeyAndAddress`

## Deterministic key from known private key

```csharp
var ecKey = new EthECKey("0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7");
var address = ecKey.GetPublicAddress();
// "0x12890D2cce102216644c59daE5baed380d84830c"
```

Source: `SignerDocExampleTests.ShouldDeriveKnownAddressFromPrivateKey`

## Create Account with chain ID

```csharp
var privateKey = "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";
var account = new Account(privateKey, Chain.MainNet);
// account.Address == "0x12890D2cce102216644c59daE5baed380d84830c"
// account.ChainId == 1
```

Source: `AccountTypesDocExampleTests.ShouldCreateAccountWithChainId`

## ViewOnlyAccount for read-only access

```csharp
var address = "0xde0B295669a9FD93d5F28D9Ec85E40f4cb697BAe";
var viewOnly = new ViewOnlyAccount(address);
// viewOnly.Address, viewOnly.TransactionManager available
// Cannot sign transactions
```

Source: `AccountTypesDocExampleTests.ShouldCreateViewOnlyAccount`

## Encrypt to keystore (default Scrypt)

```csharp
var ecKey = EthECKey.GenerateKey();
var privateKeyBytes = ecKey.GetPrivateKeyAsBytes();
var address = ecKey.GetPublicAddress();

var keyStoreService = new KeyStoreService();
var json = keyStoreService.EncryptAndGenerateDefaultKeyStoreAsJson(
    "myPassword", privateKeyBytes, address);
```

Source: `KeyStoreDocExampleTests.ShouldUseDefaultKeyStoreServiceWithScrypt`

## Decrypt from keystore

```csharp
var keyStoreService = new KeyStoreService();
var decryptedBytes = keyStoreService.DecryptKeyStoreFromJson("myPassword", json);
```

Source: `KeyStoreDocExampleTests.ShouldUseDefaultKeyStoreServiceWithScrypt`

## Load Account from keystore

```csharp
var account = Account.LoadFromKeyStore(json, "myPassword");
// account.Address available, ready to sign transactions
```

Source: `AccountTypesDocExampleTests.ShouldLoadAccountFromKeystore`

## Custom Scrypt parameters

```csharp
var customParams = new ScryptParams { Dklen = 32, N = 4096, R = 8, P = 1 };
var scryptService = new KeyStoreScryptService();
var json = scryptService.EncryptAndGenerateKeyStoreAsJson(
    "myPassword", privateKeyBytes, address, customParams);

var decryptedBytes = scryptService.DecryptKeyStoreFromJson("myPassword", json);
```

Source: `KeyStoreDocExampleTests.ShouldCreateKeystoreWithCustomScryptParams`

## PBKDF2 keystore (legacy)

```csharp
var pbkdf2Params = new Pbkdf2Params { Dklen = 32, Count = 1024, Prf = "hmac-sha256" };
var pbkdf2Service = new KeyStorePbkdf2Service();
var json = pbkdf2Service.EncryptAndGenerateKeyStoreAsJson(
    "myPassword", privateKeyBytes, address, pbkdf2Params);

var decryptedBytes = pbkdf2Service.DecryptKeyStoreFromJson("myPassword", json);
```

Source: `KeyStoreDocExampleTests.ShouldCreatePbkdf2Keystore`

## Generate UTC filename

```csharp
var keyStoreService = new KeyStoreService();
var filename = keyStoreService.GenerateUTCFileName(address);
// e.g. "UTC--2026-03-10T...--12890d2cce102216644c59dae5baed380d84830c"
```

Source: `AccountTypesDocExampleTests.ShouldGenerateKeystoreFilename`

## Full roundtrip: generate, encrypt, decrypt, verify

```csharp
var ecKey = EthECKey.GenerateKey();
var originalPrivateKey = ecKey.GetPrivateKeyAsBytes();
var originalAddress = ecKey.GetPublicAddress();

var scryptService = new KeyStoreScryptService();
var scryptParams = new ScryptParams { Dklen = 32, N = 8192, R = 8, P = 1 };
var json = scryptService.EncryptAndGenerateKeyStoreAsJson(
    "myPassword", originalPrivateKey, originalAddress, scryptParams);

var decryptedKey = scryptService.DecryptKeyStoreFromJson("myPassword", json);
var recoveredEcKey = new EthECKey(decryptedKey, true);
var recoveredAddress = recoveredEcKey.GetPublicAddress();

// originalAddress.IsTheSameAddress(recoveredAddress) == true
```

Source: `KeyStoreDocExampleTests.ShouldRoundtripKeyThroughKeystore`

## Required usings

```csharp
using Nethereum.Web3.Accounts;
using Nethereum.Accounts.ViewOnly;
using Nethereum.Signer;
using Nethereum.KeyStore;
using Nethereum.KeyStore.Model;
using Nethereum.Hex.HexConvertors.Extensions;
```
