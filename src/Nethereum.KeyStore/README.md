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

// Encrypt with custom parameters — pass address and ScryptParams object
var keyStore = keyStoreService.EncryptAndGenerateKeyStore(
    password,
    ecKey.GetPrivateKeyAsBytes(),
    ecKey.GetPublicAddress(),
    scryptParams
);

// Or use the JSON shortcut directly
string json = keyStoreService.EncryptAndGenerateKeyStoreAsJson(
    password, ecKey.GetPrivateKeyAsBytes(), ecKey.GetPublicAddress(), scryptParams);

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

### KeyStoreServiceBase&lt;T&gt;

`KeyStoreScryptService` and `KeyStorePbkdf2Service` are thin subclasses of this abstract base
(`T : KdfParams`) — almost every method below is inherited, not redeclared per-KDF. Note the
`EncryptAndGenerateKeyStoreAsJson` overloads take the address parameter as `addresss` (a real
typo in the shipped signature, kept for source compatibility); the object-returning
`EncryptAndGenerateKeyStore` overloads spell it correctly as `address`.

```csharp
public abstract class KeyStoreServiceBase<T> : IKeyStoreService<T> where T : KdfParams
{
    public const int CurrentVersion = 3;

    // Key store (private key) encryption
    public KeyStore<T> EncryptAndGenerateKeyStore(string password, byte[] privateKey, string address);
    public KeyStore<T> EncryptAndGenerateKeyStore(string password, byte[] privateKey, string address, T kdfParams);
    public string EncryptAndGenerateKeyStoreAsJson(string password, byte[] privateKey, string addresss);
    public string EncryptAndGenerateKeyStoreAsJson(string password, byte[] privateKey, string addresss, T kdfParams);

    public byte[] DecryptKeyStoreFromJson(string password, string json);
    public virtual byte[] DecryptKeyStore(string password, KeyStore<T> keyStore);

    public abstract KeyStore<T> DeserializeKeyStoreFromJson(string json);   // overridden per-KDF
    public abstract string SerializeKeyStoreToJson(KeyStore<T> keyStore);  // overridden per-KDF
    public abstract string GetKdfType();                                  // "scrypt" / "pbkdf2"
    public virtual string GetCipherType(); // "aes-128-ctr"

    // Generic payload (arbitrary byte[]/string) encryption - same KDF/cipher, no "address" field
    public CryptoStore<T> EncryptAndGenerateCryptoStore(string password, byte[] payload);
    public CryptoStore<T> EncryptAndGenerateCryptoStore(string password, byte[] payload, T kdfParams);
    public string EncryptAndGenerateCryptoStoreAsJson(string password, byte[] payload);
    public string EncryptAndGenerateCryptoStoreFromStringAsJson(string password, string payload);
    public byte[] DecryptCryptoStoreFromJson(string password, string json);
    public byte[] DecryptCryptoStore(string password, CryptoStore<T> cryptoStore);
}
```

### KeyStoreScryptService : KeyStoreServiceBase&lt;ScryptParams&gt;

Scrypt-based keystore encryption (recommended). Adds only the KDF-specific pieces; everything
else above is inherited unchanged.

```csharp
public class KeyStoreScryptService : KeyStoreServiceBase<ScryptParams>
{
    public const string KdfType = "scrypt";
}
```

**Default Scrypt Parameters** (`GetDefaultParams()`):
```csharp
N = 262144  // CPU/memory cost (2^18)
R = 1       // Block size
P = 8       // Parallelization
Dklen = 32  // Derived key length
```

### KeyStorePbkdf2Service : KeyStoreServiceBase&lt;Pbkdf2Params&gt;

PBKDF2-based keystore encryption (legacy). Same relationship to the base class as
`KeyStoreScryptService`.

```csharp
public class KeyStorePbkdf2Service : KeyStoreServiceBase<Pbkdf2Params>
{
    public const string KdfType = "pbkdf2";
}
```

**Default PBKDF2 Parameters** (`GetDefaultParams()`):
```csharp
Count = 262144      // Iteration count
Prf = "hmac-sha256"
Dklen = 32          // Derived key length
```

### KeyStoreService

Unified service: wraps a `KeyStoreScryptService` + `KeyStorePbkdf2Service` + `KeyStoreKdfChecker`
internally, and also carries the file-naming/address-extraction and raw-payload-encryption helpers
that are NOT on `KeyStoreServiceBase<T>`.

```csharp
public class KeyStoreService
{
    public KeyStoreService();
    public KeyStoreService(KeyStoreKdfChecker keyStoreKdfChecker, KeyStoreScryptService keyStoreScryptService,
        KeyStorePbkdf2Service keyStorePbkdf2Service);

    // Encrypt with default Scrypt parameters
    public string EncryptAndGenerateDefaultKeyStoreAsJson(string password, byte[] key, string address);

    // Decrypt (auto-detects KDF type via KeyStoreKdfChecker)
    public byte[] DecryptKeyStoreFromJson(string password, string json);
#if !PCL
    public byte[] DecryptKeyStoreFromFile(string password, string filePath);
#endif

    // Read the "address" field straight out of a keystore JSON document, without deserializing it fully
    public string GetAddressFromKeyStore(string json);

    // "UTC--<ISO8601 with ':' replaced by '-'>--<address without 0x>", the standard Ethereum
    // keystore file-naming convention
    public string GenerateUTCFileName(string address);

    // Generic payload encryption (delegates to the internal KeyStoreScryptService's
    // CryptoStore<ScryptParams> methods - no address field, just password + payload)
    public string EncryptPayloadAsJson(string password, byte[] data);
    public string EncryptPayloadFromStringAsJson(string password, string data);
    public byte[] DecryptPayloadFromJson(string password, string json);
    public string DecryptPayloadToUtf8String(string password, string json);
}
```

### KeyStoreKdfChecker

Detect KDF type from JSON. This is the only detection API — there is no `IsScryptKdf`/`IsPbkdf2Kdf`.

```csharp
public class KeyStoreKdfChecker
{
    public enum KdfType { scrypt, pbkdf2 }

    // Throws if "crypto.kdf" is missing or is neither "scrypt" nor "pbkdf2"
    public KdfType GetKeyStoreKdfType(string json);
}
```

### Model Classes

`ScryptParams` and `Pbkdf2Params` both derive from `KdfParams`, which carries `Dklen`/`Salt`;
`N`/`R`/`P` and `Count`/`Prf` live on the subclasses. Note `Pbkdf2Params.Count` (JSON `"c"`), not `C`.

```csharp
public class KdfParams
{
    [JsonProperty("dklen")]
    public int Dklen { get; set; }  // Derived key length (32)

    [JsonProperty("salt")]
    public string Salt { get; set; } // Random salt (hex)
}

public class ScryptParams : KdfParams
{
    [JsonProperty("n")]
    public int N { get; set; }      // CPU/memory cost (262144)

    [JsonProperty("r")]
    public int R { get; set; }      // Block size (1)

    [JsonProperty("p")]
    public int P { get; set; }      // Parallelization (8)
}

public class Pbkdf2Params : KdfParams
{
    [JsonProperty("c")]
    public int Count { get; set; }  // Iteration count (262144)

    [JsonProperty("prf")]
    public string Prf { get; set; } // PRF algorithm (hmac-sha256)
}

public class KeyStore<TKdfParams> where TKdfParams : KdfParams
{
    public CryptoInfo<TKdfParams> Crypto { get; set; }
    public string Id { get; set; }      // UUID
    public string Address { get; set; } // Ethereum address (optional)
    public int Version { get; set; }    // Always 3
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

**N dominates:** Doubling N doubles time and memory. Scrypt needs about 128 x N x R bytes, so the default N=262144 with R=1 uses ~32MB RAM.

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
