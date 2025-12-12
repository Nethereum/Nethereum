# Nethereum.KeyStore

Password-encrypted private key storage using the Web3 Secret Storage Definition standard.

## Overview

Nethereum.KeyStore implements the [Web3 Secret Storage Definition](https://github.com/ethereum/wiki/wiki/Web3-Secret-Storage-Definition) for encrypting and storing Ethereum private keys. This is a standard format for encrypted key storage used across the Ethereum ecosystem.

**Key Features:**
- AES-128-CTR encryption with password-derived keys
- Scrypt KDF (memory-hard, ASIC-resistant)
- PBKDF2 KDF (legacy, faster but less secure)
- Configurable KDF parameters for performance tuning
- JSON serialization/deserialization

**Use Cases:**
- Encrypted local key storage
- Wallet file generation
- Key import/export between applications
- Performance-tuned encryption for constrained environments (WASM, mobile)

## Installation

```bash
dotnet add package Nethereum.KeyStore
```

## Dependencies

**Nethereum:**
- **Nethereum.Hex** - Hex encoding/decoding

**External:**
- **BouncyCastle.Cryptography** or **Portable.BouncyCastle** (conditional) - Cryptographic operations

## Quick Start

```csharp
using Nethereum.KeyStore;
using Nethereum.Signer;
using Nethereum.Hex.HexConvertors.Extensions;

// Generate a new key
var ecKey = EthECKey.GenerateKey();

// Encrypt and generate keystore JSON
var service = new KeyStoreScryptService();
string password = "testPassword";
string json = service.EncryptAndGenerateKeyStoreAsJson(
    password,
    ecKey.GetPrivateKeyAsBytes(),
    ecKey.GetPublicAddress()
);

// Decrypt later
byte[] privateKey = service.DecryptKeyStoreFromJson(password, json);
```

## Usage Examples

### Example 1: Generate Key and Create Keystore (Scrypt)

```csharp
using Nethereum.KeyStore;
using Nethereum.Signer;
using Nethereum.Hex.HexConvertors.Extensions;

var ecKey = EthECKey.GenerateKey();
var keyStoreScryptService = new KeyStoreScryptService();
string password = "testPassword";

// Encrypt and serialize to JSON
string json = keyStoreScryptService.EncryptAndGenerateKeyStoreAsJson(
    password,
    ecKey.GetPrivateKeyAsBytes(),
    ecKey.GetPublicAddress()
);

// Save to file
File.WriteAllText($"keystore-{ecKey.GetPublicAddress()}.json", json);

// Decrypt to verify
byte[] key = keyStoreScryptService.DecryptKeyStoreFromJson(password, json);
Assert.Equal(ecKey.GetPrivateKey(), key.ToHex(true));
```

### Example 2: Custom Scrypt Parameters (Performance Tuning)

```csharp
using Nethereum.KeyStore;
using Nethereum.KeyStore.Model;
using Nethereum.Signer;
using Nethereum.Hex.HexConvertors.Extensions;

var keyStoreService = new KeyStoreScryptService();

// Lower N for faster encryption (WASM, mobile, testing)
// Default: N=262144, R=1, P=8, Dklen=32
var scryptParams = new ScryptParams { Dklen = 32, N = 32, R = 1, P = 8 };

var ecKey = EthECKey.GenerateKey();
string password = "testPassword";

// Encrypt with custom parameters
var keyStore = keyStoreService.EncryptAndGenerateKeyStore(
    password,
    ecKey.GetPrivateKeyAsBytes(),
    scryptParams.N,
    scryptParams.R,
    scryptParams.P,
    null // salt (null = auto-generate)
);

// Serialize to JSON
string json = keyStoreService.SerializeKeyStoreToJson(keyStore);

// Decrypt
byte[] decryptedKey = keyStoreService.DecryptKeyStoreFromJson(password, json);
```

### Example 3: Decrypt Existing Keystore (Scrypt)

```csharp
using Nethereum.KeyStore;
using Nethereum.Hex.HexConvertors.Extensions;

var scryptKeyStoreJson = @"{
    ""crypto"" : {
        ""cipher"" : ""aes-128-ctr"",
        ""cipherparams"" : {
            ""iv"" : ""83dbcc02d8ccb40e466191a123791e0e""
        },
        ""ciphertext"" : ""d172bf743a674da9cdad04534d56926ef8358534d458fffccd4e6ad2fbde479c"",
        ""kdf"" : ""scrypt"",
        ""kdfparams"" : {
            ""dklen"" : 32,
            ""n"" : 262144,
            ""r"" : 1,
            ""p"" : 8,
            ""salt"" : ""ab0c7876052600dd703518d6fc3fe8984592145b591fc8fb5c6d43190334ba19""
        },
        ""mac"" : ""2103ac29920d71da29f15d75b4a16dbe95cfd7ff8faea1056c33131d846e3097""
    },
    ""id"" : ""3198bc9c-6672-5ab3-d995-4942343ae5b6"",
    ""version"" : 3
}";

string password = "testpassword";
var keyStoreScryptService = new KeyStoreScryptService();

// Deserialize and decrypt
var keyStore = keyStoreScryptService.DeserializeKeyStoreFromJson(scryptKeyStoreJson);
byte[] privateKey = keyStoreScryptService.DecryptKeyStore(password, keyStore);

Console.WriteLine($"Private Key: {privateKey.ToHex()}");
// Output: 7a28b5ba57c53603b0b07b56bba752f7784bf506fa95edc395f5cf6c7514fe9d
```

### Example 4: PBKDF2 Keystore (Legacy)

```csharp
using Nethereum.KeyStore;
using Nethereum.Signer;
using Nethereum.Hex.HexConvertors.Extensions;

var ecKey = EthECKey.GenerateKey();
var keyStorePbkdf2Service = new KeyStorePbkdf2Service();
string password = "testPassword";

// Encrypt with PBKDF2 (faster but less secure than Scrypt)
string json = keyStorePbkdf2Service.EncryptAndGenerateKeyStoreAsJson(
    password,
    ecKey.GetPrivateKeyAsBytes(),
    ecKey.GetPublicAddress()
);

// Decrypt
byte[] key = keyStorePbkdf2Service.DecryptKeyStoreFromJson(password, json);
Assert.Equal(ecKey.GetPrivateKey(), key.ToHex(true));
```

### Example 5: Detect KDF Type

```csharp
using Nethereum.KeyStore;

string keystoreJson = File.ReadAllText("wallet.json");
var keyStoreKdfChecker = new KeyStoreKdfChecker();

var kdfType = keyStoreKdfChecker.GetKeyStoreKdfType(keystoreJson);

if (kdfType == KeyStoreKdfChecker.KdfType.scrypt)
{
    var service = new KeyStoreScryptService();
    byte[] privateKey = service.DecryptKeyStoreFromJson(password, keystoreJson);
}
else if (kdfType == KeyStoreKdfChecker.KdfType.pbkdf2)
{
    var service = new KeyStorePbkdf2Service();
    byte[] privateKey = service.DecryptKeyStoreFromJson(password, keystoreJson);
}
```

### Example 6: Default Keystore Service

```csharp
using Nethereum.KeyStore;
using Nethereum.Signer;
using Nethereum.Hex.HexConvertors.Extensions;

var ecKey = EthECKey.GenerateKey();
var keyStoreService = new KeyStoreService();
string password = "testPassword";

// Uses default Scrypt parameters
string json = keyStoreService.EncryptAndGenerateDefaultKeyStoreAsJson(
    password,
    ecKey.GetPrivateKeyAsBytes(),
    ecKey.GetPublicAddress()
);

byte[] key = keyStoreService.DecryptKeyStoreFromJson(password, json);
Assert.Equal(ecKey.GetPrivateKey(), key.ToHex(true));
```

## API Reference

### KeyStoreScryptService

Scrypt-based keystore encryption (recommended).

```csharp
public class KeyStoreScryptService
{
    // Encrypt and generate JSON (default parameters)
    public string EncryptAndGenerateKeyStoreAsJson(string password, byte[] privateKey, string address);

    // Encrypt with custom Scrypt parameters
    public KeyStore<ScryptParams> EncryptAndGenerateKeyStore(
        string password,
        byte[] privateKey,
        int n, int r, int p,
        byte[] salt = null
    );

    // Serialize keystore to JSON
    public string SerializeKeyStoreToJson(KeyStore<ScryptParams> keyStore);

    // Deserialize JSON to keystore
    public KeyStore<ScryptParams> DeserializeKeyStoreFromJson(string json);

    // Decrypt from JSON
    public byte[] DecryptKeyStoreFromJson(string password, string json);

    // Decrypt from keystore object
    public byte[] DecryptKeyStore(string password, KeyStore<ScryptParams> keyStore);
}
```

**Default Scrypt Parameters:**
```csharp
N = 262144  // CPU/memory cost (2^18)
R = 1       // Block size
P = 8       // Parallelization
Dklen = 32  // Derived key length
```

### KeyStorePbkdf2Service

PBKDF2-based keystore encryption (legacy).

```csharp
public class KeyStorePbkdf2Service
{
    // Same methods as KeyStoreScryptService but using PBKDF2
    public string EncryptAndGenerateKeyStoreAsJson(string password, byte[] privateKey, string address);
    public KeyStore<Pbkdf2Params> DeserializeKeyStoreFromJson(string json);
    public byte[] DecryptKeyStoreFromJson(string password, string json);
    public byte[] DecryptKeyStore(string password, KeyStore<Pbkdf2Params> keyStore);
}
```

### KeyStoreService

Unified service with default encryption.

```csharp
public class KeyStoreService
{
    // Encrypt with default Scrypt parameters
    public string EncryptAndGenerateDefaultKeyStoreAsJson(string password, byte[] privateKey, string address);

    // Decrypt (auto-detects KDF type)
    public byte[] DecryptKeyStoreFromJson(string password, string json);
}
```

### KeyStoreKdfChecker

Detect KDF type from JSON.

```csharp
public class KeyStoreKdfChecker
{
    public enum KdfType { scrypt, pbkdf2 }

    public KdfType GetKeyStoreKdfType(string json);
    public bool IsScryptKdf(string json);
    public bool IsPbkdf2Kdf(string json);
}
```

### Model Classes

```csharp
public class ScryptParams
{
    public int Dklen { get; set; }  // Derived key length (32)
    public int N { get; set; }      // CPU/memory cost (262144)
    public int R { get; set; }      // Block size (1)
    public int P { get; set; }      // Parallelization (8)
    public string Salt { get; set; } // Random salt (hex)
}

public class Pbkdf2Params
{
    public int Dklen { get; set; }  // Derived key length (32)
    public int C { get; set; }      // Iteration count (262144)
    public string Prf { get; set; } // PRF algorithm (hmac-sha256)
    public string Salt { get; set; } // Random salt (hex)
}

public class KeyStore<TKdfParams>
{
    public CryptoInfo<TKdfParams> Crypto { get; set; }
    public string Id { get; set; }      // UUID
    public int Version { get; set; }    // Always 3
    public string Address { get; set; } // Ethereum address (optional)
}
```

## Scrypt Parameter Tuning

### Default Parameters (Desktop/Server)

```csharp
N = 262144  // 2^18 - Strong security, ~100ms encryption
R = 1
P = 8
```

**Use for:** Desktop applications, servers, production wallets

### Low-Cost Parameters (WASM/Mobile/Testing)

```csharp
N = 32      // 2^5 - Fast encryption, weaker security
R = 1
P = 8
```

**Use for:** Browser WASM, mobile apps, development/testing

### High-Security Parameters

```csharp
N = 1048576  // 2^20 - Very strong security, ~3s encryption
R = 8
P = 1
```

**Use for:** Cold storage, high-value accounts, paranoid security

### Parameter Effects

| Parameter | Effect | Security Impact | Performance Impact |
|-----------|--------|-----------------|-------------------|
| **N** | CPU/memory cost | Exponential | Exponential |
| **R** | Block size | Linear | Linear |
| **P** | Parallelization | Linear | Linear (if parallel) |

**N dominates:** Doubling N doubles time and memory. N=262144 uses ~256MB RAM.

## Web3 Secret Storage Format

Keystore JSON structure:

```json
{
  "crypto": {
    "cipher": "aes-128-ctr",
    "cipherparams": { "iv": "..." },
    "ciphertext": "...",
    "kdf": "scrypt",
    "kdfparams": {
      "dklen": 32,
      "n": 262144,
      "r": 1,
      "p": 8,
      "salt": "..."
    },
    "mac": "..."
  },
  "id": "3198bc9c-6672-5ab3-d995-4942343ae5b6",
  "version": 3
}
```

**Fields:**
- **cipher**: Always `aes-128-ctr`
- **ciphertext**: Encrypted private key
- **kdf**: `scrypt` or `pbkdf2`
- **kdfparams**: KDF configuration
- **mac**: HMAC for integrity verification
- **version**: Always `3`

## Important Notes

### Scrypt vs PBKDF2

| Feature | Scrypt | PBKDF2 |
|---------|--------|--------|
| **Security** | Memory-hard, ASIC-resistant | CPU-only, ASIC-vulnerable |
| **Speed** | Slower (~100ms default) | Faster (~50ms) |
| **Recommendation** | Use this | Legacy only |

**Use Scrypt** unless you need compatibility with very old systems.

### File Naming Convention

Standard naming convention used across Ethereum tools:

```
UTC--<created_at UTC ISO8601>--<address hex>
```

Example:
```
UTC--2024-01-15T10-30-45.123Z--0x12890d2cce102216644c59dae5baed380d84830c
```

### Security Considerations

1. **Password strength is critical** - No KDF can protect weak passwords
2. **N parameter tradeoff** - Higher N = more secure but slower
3. **Salt is auto-generated** - Uses cryptographically secure random bytes
4. **MAC prevents tampering** - Detects modified ciphertext
5. **AES-128-CTR** - Standard encryption mode, secure when properly implemented

## Related Packages

### Used By
- **Nethereum.Accounts** - Account management with keystore loading

### Dependencies
- **Nethereum.Hex** - Hex encoding/decoding

## Additional Resources

- [Web3 Secret Storage Definition](https://github.com/ethereum/wiki/wiki/Web3-Secret-Storage-Definition) - Official specification
- [Scrypt Paper](https://www.tarsnap.com/scrypt/scrypt.pdf) - Original Scrypt algorithm
- [PBKDF2 RFC 2898](https://tools.ietf.org/html/rfc2898) - PBKDF2 specification
- [Nethereum Documentation](http://docs.nethereum.com/)
