# Nethereum.KeyStore

Encrypted JSON wallet file generation and management using the Web3 Secret Storage Definition (UTC/JSON keystore format).

## Overview

Nethereum.KeyStore implements the [Web3 Secret Storage Definition](https://github.com/ethereum/wiki/wiki/Web3-Secret-Storage-Definition) for securely storing private keys encrypted with a password. This is the **same format** used by Geth, MyEtherWallet, MetaMask, and most Ethereum wallets.

**Key Features:**
- Password-protected private key encryption
- Two KDF (Key Derivation Function) options: **Scrypt** (default, more secure) and **PBKDF2** (faster)
- AES-128-CTR encryption
- Compatible with all major Ethereum wallets
- Generate wallet files programmatically or decrypt existing ones

**Use Cases:**
- Create encrypted wallet files for users
- Import/export wallets between applications
- Secure local storage of private keys
- Multi-wallet management

## Installation

```bash
dotnet add package Nethereum.KeyStore
```

## Dependencies

**External:**
- **BouncyCastle.Cryptography** or **Portable.BouncyCastle** (cryptographic operations)

**Nethereum:**
- **Nethereum.Hex** - Hex encoding/decoding

## Quick Start

```csharp
using Nethereum.KeyStore;
using Nethereum.Hex.HexConvertors.Extensions;

// Create encrypted keystore
var service = new KeyStoreScryptService();
string password = "secure-password";
byte[] privateKey = "7a28b5ba57c53603b0b07b56bba752f7784bf506fa95edc395f5cf6c7514fe9d".HexToByteArray();

// Generate encrypted JSON
string json = service.EncryptAndGenerateKeyStoreAsJson(password, privateKey, "0x...");

// Save to file
File.WriteAllText("UTC--2024-01-01T00-00-00.000Z--address", json);

// Decrypt later
byte[] decryptedKey = service.DecryptKeyStoreFromJson(password, json);
```

## Web3 Secret Storage Format

Keystore files contain:
- **cipher**: Encryption algorithm (aes-128-ctr)
- **ciphertext**: Encrypted private key
- **kdf**: Key derivation function (scrypt or pbkdf2)
- **mac**: Message authentication code (prevents tampering)
- **id**: Unique identifier (UUID)
- **version**: Always 3 (current standard)

## Usage Examples

### Example 1: Generate Scrypt Keystore (Recommended)

```csharp
using Nethereum.KeyStore;
using Nethereum.Hex.HexConvertors.Extensions;

var service = new KeyStoreScryptService();
string password = "MySecurePassword123!";
string privateKey = "7a28b5ba57c53603b0b07b56bba752f7784bf506fa95edc395f5cf6c7514fe9d";
string address = "0x12890d2cce102216644c59dae5baed380d84830c";

// Generate encrypted JSON keystore
string keystoreJson = service.EncryptAndGenerateKeyStoreAsJson(
    password,
    privateKey.HexToByteArray(),
    address
);

// Keystore is compatible with MetaMask, MEW, Geth, etc.
File.WriteAllText($"keystore-{address}.json", keystoreJson);
```

### Example 2: Decrypt Scrypt Keystore (Real Test Example)

```csharp
using Nethereum.KeyStore;
using Nethereum.Hex.HexConvertors.Extensions;

var scryptKeyStoreJson = @"{
    'crypto': {
        'cipher': 'aes-128-ctr',
        'cipherparams': { 'iv': '83dbcc02d8ccb40e466191a123791e0e' },
        'ciphertext': 'd172bf743a674da9cdad04534d56926ef8358534d458fffccd4e6ad2fbde479c',
        'kdf': 'scrypt',
        'kdfparams': {
            'dklen': 32,
            'n': 262144,
            'r': 1,
            'p': 8,
            'salt': 'ab0c7876052600dd703518d6fc3fe8984592145b591fc8fb5c6d43190334ba19'
        },
        'mac': '2103ac29920d71da29f15d75b4a16dbe95cfd7ff8faea1056c33131d846e3097'
    },
    'id': '3198bc9c-6672-5ab3-d995-4942343ae5b6',
    'version': 3
}";

string password = "testpassword";
var service = new KeyStoreScryptService();

// Decrypt
byte[] privateKey = service.DecryptKeyStoreFromJson(password, scryptKeyStoreJson);

Console.WriteLine($"Private Key: {privateKey.ToHex()}");
// Output: 7a28b5ba57c53603b0b07b56bba752f7784bf506fa95edc395f5cf6c7514fe9d
```

### Example 3: Generate PBKDF2 Keystore (Faster)

```csharp
using Nethereum.KeyStore;
using Nethereum.Hex.HexConvertors.Extensions;

// PBKDF2 is faster but less secure than Scrypt
var service = new KeyStorePbkdf2Service();
string password = "MyPassword";
byte[] privateKey = "...".HexToByteArray();
string address = "0x...";

string json = service.EncryptAndGenerateKeyStoreAsJson(password, privateKey, address);
```

### Example 4: Decrypt PBKDF2 Keystore (Real Test Example)

```csharp
using Nethereum.KeyStore;
using Nethereum.Hex.HexConvertors.Extensions;

var pbkdf2KeyStoreJson = @"{
    'crypto': {
        'cipher': 'aes-128-ctr',
        'cipherparams': { 'iv': '6087dab2f9fdbbfaddc31a909735c1e6' },
        'ciphertext': '5318b4d5bcd28de64ee5559e671353e16f075ecae9f99c7a79a38af5f869aa46',
        'kdf': 'pbkdf2',
        'kdfparams': {
            'c': 262144,
            'dklen': 32,
            'prf': 'hmac-sha256',
            'salt': 'ae3cd4e7013836a3df6bd7241b12db061dbe2c6785853cce422d148a624ce0bd'
        },
        'mac': '517ead924a9d0dc3124507e3393d175ce3ff7c1e96529c6c555ce9e51205e9b2'
    },
    'id': '3198bc9c-6672-5ab3-d995-4942343ae5b6',
    'version': 3
}";

string password = "testpassword";
var service = new KeyStorePbkdf2Service();

byte[] privateKey = service.DecryptKeyStoreFromJson(password, pbkdf2KeyStoreJson);
Assert.Equal("7a28b5ba57c53603b0b07b56bba752f7784bf506fa95edc395f5cf6c7514fe9d", privateKey.ToHex());
```

### Example 5: Detect KDF Type Automatically

```csharp
using Nethereum.KeyStore;

string keystoreJson = File.ReadAllText("wallet.json");
var checker = new KeyStoreKdfChecker();

if (checker.IsScryptKdf(keystoreJson))
{
    var service = new KeyStoreScryptService();
    byte[] privateKey = service.DecryptKeyStoreFromJson(password, keystoreJson);
}
else if (checker.IsPbkdf2Kdf(keystoreJson))
{
    var service = new KeyStorePbkdf2Service();
    byte[] privateKey = service.DecryptKeyStoreFromJson(password, keystoreJson);
}
```

### Example 6: Encrypt with Custom Scrypt Parameters

```csharp
using Nethereum.KeyStore;
using Nethereum.KeyStore.Model;
using Nethereum.Hex.HexConvertors.Extensions;

var service = new KeyStoreScryptService();

// Custom Scrypt parameters (higher = more secure but slower)
var scryptParams = new ScryptParams
{
    N = 1048576,  // CPU cost (default: 262144)
    R = 8,        // Memory cost (default: 1)
    P = 1,        // Parallelization (default: 8)
    Dklen = 32    // Derived key length
};

byte[] privateKey = "...".HexToByteArray();
byte[] salt = new byte[32]; // Generate random salt
string password = "secure-password";

var crypto = service.EncryptAndGenerateKeyStore(
    password,
    privateKey,
    scryptParams.N,
    scryptParams.R,
    scryptParams.P,
    salt
);

// Convert to JSON
var keyStore = new KeyStore<ScryptParams>
{
    Crypto = crypto,
    Id = Guid.NewGuid().ToString(),
    Version = 3
};
```

## API Reference

### KeyStoreScryptService

Scrypt-based keystore (recommended - more secure).

```csharp
public class KeyStoreScryptService
{
    // Encrypt and generate JSON
    public string EncryptAndGenerateKeyStoreAsJson(string password, byte[] privateKey, string address);

    // Decrypt from JSON
    public byte[] DecryptKeyStoreFromJson(string password, string json);

    // Decrypt from object
    public byte[] DecryptKeyStore(string password, KeyStore<ScryptParams> keyStore);

    // Deserialize JSON to object
    public KeyStore<ScryptParams> DeserializeKeyStoreFromJson(string json);
}
```

### KeyStorePbkdf2Service

PBKDF2-based keystore (faster, less secure).

```csharp
public class KeyStorePbkdf2Service
{
    // Encrypt and generate JSON
    public string EncryptAndGenerateKeyStoreAsJson(string password, byte[] privateKey, string address);

    // Decrypt from JSON
    public byte[] DecryptKeyStoreFromJson(string password, string json);

    // Decrypt from object
    public byte[] DecryptKeyStore(string password, KeyStore<Pbkdf2Params> keyStore);

    // Deserialize JSON to object
    public KeyStore<Pbkdf2Params> DeserializeKeyStoreFromJson(string json);
}
```

### KeyStoreKdfChecker

Detect KDF type from JSON.

```csharp
public class KeyStoreKdfChecker
{
    public bool IsScryptKdf(string json);
    public bool IsPbkdf2Kdf(string json);
}
```

## Important Notes

### Scrypt vs PBKDF2

| Feature | Scrypt | PBKDF2 |
|---------|--------|--------|
| **Security** | Higher (memory-hard) | Lower |
| **Speed** | Slower | Faster |
| **Default** | ✅ Use this | Legacy |
| **Resistance** | ASIC-resistant | ASIC-vulnerable |

**Recommendation:** Always use **Scrypt** unless you need compatibility with very old systems.

### Password Strength

```csharp
// ❌ WEAK - Easy to brute force
string password = "12345";

// ✅ STRONG - Hard to crack
string password = "MyV3ry$ecur3P@ssw0rd!2024";
```

Scrypt parameters make brute-forcing expensive, but **strong passwords are still critical**.

### File Naming Convention

Ethereum wallets use this naming convention:
```
UTC--<created_at UTC ISO8601>--<address hex>
```

Example:
```
UTC--2024-01-15T10-30-45.123Z--0x12890d2cce102216644c59dae5baed380d84830c
```

### Never Store Passwords

```csharp
// ❌ WRONG - Password in code
string password = "hardcoded-password";

// ✅ CORRECT - Get from user
string password = Console.ReadLine();
```

Keystore files are **only as secure as the password**. Never hardcode or store passwords.

## Related Packages

### Used By (Consumers)
- **Nethereum.Accounts** - Account management with keystore support
- **Nethereum.Wallet** - Multi-wallet management

### Dependencies
- **Nethereum.Hex** - Hex encoding

## Additional Resources

- [Web3 Secret Storage Definition](https://github.com/ethereum/wiki/wiki/Web3-Secret-Storage-Definition)
- [Scrypt Paper](https://www.tarsnap.com/scrypt/scrypt.pdf)
- [PBKDF2 Specification](https://tools.ietf.org/html/rfc2898)
- [Nethereum Documentation](http://docs.nethereum.com/)

## License

This package is part of the Nethereum project and follows the same MIT license.
