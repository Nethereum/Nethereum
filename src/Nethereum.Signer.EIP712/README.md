# Nethereum.Signer.EIP712

EIP-712 typed structured data signing for secure off-chain message authentication compatible with MetaMask's eth_signTypedData_v4.

## Overview

Nethereum.Signer.EIP712 implements [EIP-712](https://eips.ethereum.org/EIPS/eip-712), the standard for hashing and signing typed structured data. This enables signing complex objects (not just strings) in a way that's **human-readable** in MetaMask and other wallets, preventing phishing attacks where users unknowingly sign malicious transactions.

**Key Features:**
- Sign complex typed data structures (objects, arrays, nested types)
- Compatible with MetaMask's `eth_signTypedData_v4`
- Human-readable signature prompts in wallets (shows fields, not raw hex)
- Domain separation prevents replay attacks across different dApps
- Type-safe C# API with automatic schema generation
- Signature recovery to verify signers

**Use Cases:**
- Gasless meta-transactions (user signs intent, relayer pays gas)
- Off-chain order books (DEX orders, NFT listings)
- Permit functionality (ERC-20 approvals via signature)
- DAO voting (off-chain vote aggregation)
- Session keys and delegated permissions

## Installation

```bash
dotnet add package Nethereum.Signer.EIP712
```

Or via Package Manager Console:

```powershell
Install-Package Nethereum.Signer.EIP712
```

## Dependencies

**Nethereum:**
- **Nethereum.ABI** - EIP-712 encoding implementation
- **Nethereum.Signer** - Core ECDSA signing
- **Nethereum.Util** - Keccak hashing
- **Nethereum.Hex** - Hex encoding

## Key Concepts

### EIP-712 vs Regular Message Signing

| Aspect | Regular (EIP-191) | EIP-712 |
|--------|-------------------|---------|
| **Data** | Arbitrary bytes/string | Typed structured data |
| **Wallet Display** | Hex hash (unreadable) | Human-readable fields |
| **Type Safety** | None | Full type checking |
| **Phishing Protection** | Weak | Strong (user sees what they sign) |
| **Use Cases** | Simple messages | Complex objects, transactions |

### Domain Separator

The domain separator prevents signatures from being valid across different:
- **Name**: Application name
- **Version**: Schema version
- **ChainId**: Network (prevents mainnet/testnet replay)
- **VerifyingContract**: Contract address that will verify the signature

### TypedData Structure

`TypedData<TDomain>` derives from `TypedDataRaw`, which is where `Types` and
`Message` actually live:

```csharp
public class TypedDataRaw
{
    public IDictionary<string, MemberDescription[]> Types { get; set; }  // Type definitions
    public string PrimaryType { get; set; }                              // Main message type
    public MemberValue[] Message { get; set; }                           // Actual data, EIP-712-typed values
    public MemberValue[] DomainRawValues { get; set; }
}

public class TypedData<TDomain> : TypedDataRaw
{
    public TDomain Domain { get; set; }                                  // Domain separator
}
```

`Message` is not populated by hand for the common case — pass your POCO and a
`TypedData<TDomain>` schema straight to `SignTypedDataV4(message, typedData, key)`
and it is converted to `MemberValue[]` for you.

## Quick Start

**Every message type MUST carry `[Struct]` on the class and `[Parameter]` on
each member (Solidity type, name, order).** These attributes are what the
EIP-712 encoder uses to build the type schema — without them the schema is
empty and the signature is silently wrong.

```csharp
using Nethereum.Signer;
using Nethereum.Signer.EIP712;
using Nethereum.ABI.EIP712;
using Nethereum.ABI.FunctionEncoding.Attributes;

// 1. Define your message type
[Struct("Person")]
public class Person
{
    [Parameter("string", "name", 1)]
    public string Name { get; set; }

    [Parameter("address", "wallet", 2)]
    public string Wallet { get; set; }
}

[Struct("Mail")]
public class Mail
{
    [Parameter("tuple", "from", 1, "Person")]
    public Person From { get; set; }

    [Parameter("tuple", "to", 2, "Person")]
    public Person To { get; set; }

    [Parameter("string", "contents", 3)]
    public string Contents { get; set; }
}

// 2. Create domain
var domain = new Domain
{
    Name = "Ether Mail",
    Version = "1",
    ChainId = 1,
    VerifyingContract = "0xCcCCccccCCCCcCCCCCCcCcCccCcCCCcCcccccccC"
};

// 3. Build the schema from the attributes above and the message value
var typedData = new TypedData<Domain>
{
    Domain = domain,
    Types = MemberDescriptionFactory.GetTypesMemberDescription(typeof(Domain), typeof(Mail), typeof(Person)),
    PrimaryType = nameof(Mail)
};

var mail = new Mail
{
    From = new Person { Name = "Alice", Wallet = "0xCD2a3d9F938E13CD947Ec05AbC7FE734Df8DD826" },
    To = new Person { Name = "Bob", Wallet = "0xbBbBBBBbbBBBbbbBbbBbbbbBBbBbbbbBbBbbBBbB" },
    Contents = "Hello Bob!"
};

// 4. Sign
var signer = new Eip712TypedDataSigner();
var key = new EthECKey("YOUR_PRIVATE_KEY");
string signature = signer.SignTypedDataV4(mail, typedData, key);
```

## Usage Examples

### Example 1: Simple Typed Message (Real Test Example)

```csharp
using Nethereum.Signer.EIP712;
using Nethereum.ABI.EIP712;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Signer;
using Nethereum.Util;

[Struct("Person")]
public class Person
{
    [Parameter("string", "name", 1)]
    public string Name { get; set; }

    [Parameter("address", "wallet", 2)]
    public string Wallet { get; set; }
}

[Struct("Mail")]
public class Mail
{
    [Parameter("tuple", "from", 1, "Person")]
    public Person From { get; set; }

    [Parameter("tuple", "to", 2, "Person")]
    public Person To { get; set; }

    [Parameter("string", "contents", 3)]
    public string Contents { get; set; }
}

// Define domain
var domain = new Domain
{
    Name = "Ether Mail",
    Version = "1",
    ChainId = 1,
    VerifyingContract = "0xCcCCccccCCCCcCCCCCCcCcCccCcCCCcCcccccccC"
};

// Build the schema from the [Struct]/[Parameter] attributes on Mail and Person
var typedData = new TypedData<Domain>
{
    Domain = domain,
    Types = MemberDescriptionFactory.GetTypesMemberDescription(typeof(Domain), typeof(Mail), typeof(Person)),
    PrimaryType = nameof(Mail)
};

var mail = new Mail
{
    From = new Person { Name = "Cow", Wallet = "0xCD2a3d9F938E13CD947Ec05AbC7FE734Df8DD826" },
    To = new Person { Name = "Bob", Wallet = "0xbBbBBBBbbBBBbbbBbbBbbbbBBbBbbbbBbBbbBBbB" },
    Contents = "Hello, Bob!"
};

// Sign
var signer = new Eip712TypedDataSigner();
var key = new EthECKey("94e001d6adf3a3275d5dd45971c2a5f6637d3e9c51f9693f2e678f649e164fa5");
string signature = signer.SignTypedDataV4(mail, typedData, key);

Console.WriteLine($"Signature: {signature}");

// Verify
string recoveredAddress = signer.RecoverFromSignatureV4(mail, typedData, signature);
Console.WriteLine($"Signer: {recoveredAddress}");
Console.WriteLine($"Match: {key.GetPublicAddress() == recoveredAddress}");
```

### Example 2: ERC-2612 Permit (Gasless Approval)

```csharp
using Nethereum.Signer.EIP712;
using Nethereum.ABI.EIP712;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Signer;
using System.Numerics;

// ERC-20 Permit allows approvals via signature (no gas cost)
// [Struct]/[Parameter] attributes are required: SignTypedData uses them to
// build the EIP-712 type schema, and a message without them signs the wrong hash.
[Struct("Permit")]
public class Permit
{
    [Parameter("address", "owner", 1)]
    public string Owner { get; set; }

    [Parameter("address", "spender", 2)]
    public string Spender { get; set; }

    [Parameter("uint256", "value", 3)]
    public BigInteger Value { get; set; }

    [Parameter("uint256", "nonce", 4)]
    public BigInteger Nonce { get; set; }

    [Parameter("uint256", "deadline", 5)]
    public BigInteger Deadline { get; set; }
}

var domain = new Domain
{
    Name = "USD Coin",
    Version = "2",
    ChainId = 1,
    VerifyingContract = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48" // USDC
};

var permit = new Permit
{
    Owner = "0x5B38Da6a701c568545dCfcB03FcB875f56beddC4",
    Spender = "0xAb8483F64d9C6d1EcF9b849Ae677dD3315835cb2",
    Value = BigInteger.Parse("1000000000"), // 1000 USDC (6 decimals)
    Nonce = 0,
    Deadline = 1735689600 // Unix timestamp
};

var signer = new Eip712TypedDataSigner();
var key = new EthECKey("YOUR_PRIVATE_KEY");

// This signature can be submitted by anyone to approve the spender
string signature = signer.SignTypedData(permit, domain, "Permit", key);

// The spender can now call: token.permit(owner, spender, value, deadline, v, r, s)
// No gas cost for the owner!
```

### Example 3: Meta-Transaction (Gasless Transaction)

```csharp
using Nethereum.Signer.EIP712;
using Nethereum.ABI.EIP712;
using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Numerics;

[Struct("MetaTransaction")]
public class MetaTransaction
{
    [Parameter("uint256", "nonce", 1)]
    public BigInteger Nonce { get; set; }

    [Parameter("address", "from", 2)]
    public string From { get; set; }

    [Parameter("bytes", "functionSignature", 3)]
    public string FunctionSignature { get; set; }
}

var domain = new Domain
{
    Name = "My dApp",
    Version = "1",
    ChainId = 137, // Polygon
    VerifyingContract = "0x..." // Your contract address
};

var metaTx = new MetaTransaction
{
    Nonce = 0,
    From = "0x...", // User address
    FunctionSignature = "0x..." // Encoded function call
};

var signer = new Eip712TypedDataSigner();
var key = new EthECKey("USER_PRIVATE_KEY");
string signature = signer.SignTypedData(metaTx, domain, "MetaTransaction", key);

// Relayer submits this to: contract.executeMetaTransaction(from, functionSignature, signature)
// User doesn't pay gas - relayer does!
```

### Example 4: DEX Order (0x Protocol Style)

```csharp
using Nethereum.Signer.EIP712;
using Nethereum.ABI.EIP712;
using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Numerics;

[Struct("Order")]
public class Order
{
    [Parameter("address", "makerAddress", 1)]
    public string MakerAddress { get; set; }

    [Parameter("address", "takerAddress", 2)]
    public string TakerAddress { get; set; }

    [Parameter("address", "makerAssetAddress", 3)]
    public string MakerAssetAddress { get; set; }

    [Parameter("address", "takerAssetAddress", 4)]
    public string TakerAssetAddress { get; set; }

    [Parameter("uint256", "makerAssetAmount", 5)]
    public BigInteger MakerAssetAmount { get; set; }

    [Parameter("uint256", "takerAssetAmount", 6)]
    public BigInteger TakerAssetAmount { get; set; }

    [Parameter("uint256", "expirationTimeSeconds", 7)]
    public BigInteger ExpirationTimeSeconds { get; set; }

    [Parameter("uint256", "salt", 8)]
    public BigInteger Salt { get; set; }
}

var domain = new Domain
{
    Name = "0x Protocol",
    Version = "3.0.0",
    ChainId = 1,
    VerifyingContract = "0x..." // Exchange contract
};

var order = new Order
{
    MakerAddress = "0x...",
    TakerAddress = "0x0000000000000000000000000000000000000000", // Anyone can fill
    MakerAssetAddress = "0x...", // WETH
    TakerAssetAddress = "0x...", // DAI
    MakerAssetAmount = BigInteger.Parse("1000000000000000000"), // 1 WETH
    TakerAssetAmount = BigInteger.Parse("2000000000000000000000"), // 2000 DAI
    ExpirationTimeSeconds = 1735689600,
    Salt = BigInteger.Parse("12345")
};

var signer = new Eip712TypedDataSigner();
var key = new EthECKey("MAKER_PRIVATE_KEY");
string signature = signer.SignTypedData(order, domain, "Order", key);

// Order is signed off-chain, submitted to relayer, filled on-chain
```

### Example 5: DAO Vote (Snapshot Style)

```csharp
using Nethereum.Signer.EIP712;
using Nethereum.ABI.EIP712;
using Nethereum.ABI.FunctionEncoding.Attributes;

[Struct("Vote")]
public class Vote
{
    [Parameter("address", "from", 1)]
    public string From { get; set; }

    [Parameter("string", "space", 2)]
    public string Space { get; set; }

    [Parameter("uint64", "timestamp", 3)]
    public long Timestamp { get; set; }

    [Parameter("string", "proposal", 4)]
    public string Proposal { get; set; }

    [Parameter("uint32", "choice", 5)]
    public int Choice { get; set; } // 1 = For, 2 = Against, 3 = Abstain
}

var domain = new Domain
{
    Name = "snapshot",
    Version = "0.1.4"
};

var vote = new Vote
{
    From = "0x...", // Voter address
    Space = "aave.eth",
    Timestamp = 1735689600,
    Proposal = "0x...", // Proposal ID
    Choice = 1 // Vote "For"
};

var signer = new Eip712TypedDataSigner();
var key = new EthECKey("VOTER_PRIVATE_KEY");
string signature = signer.SignTypedData(vote, domain, "Vote", key);

// Vote is aggregated off-chain, no gas cost for voters
```

### Example 6: Sign from JSON

```csharp
using Nethereum.Signer.EIP712;
using Nethereum.Signer;

// Sign typed data directly from JSON (useful for frontend integration)
var typedDataJson = @"{
    'domain': {
        'chainId': 1,
        'name': 'Ether Mail',
        'verifyingContract': '0xCcCCccccCCCCcCCCCCCcCcCccCcCCCcCcccccccC',
        'version': '1'
    },
    'message': {
        'contents': 'Hello, Bob!',
        'from': {
            'name': 'Cow',
            'wallets': [
                '0xCD2a3d9F938E13CD947Ec05AbC7FE734Df8DD826',
                '0xDeaDbeefdEAdbeefdEadbEEFdeadbeEFdEaDbeeF'
            ]
        },
        'to': [{
            'name': 'Bob',
            'wallets': ['0xbBbBBBBbbBBBbbbBbbBbbbbBBbBbbbbBbBbbBBbB']
        }]
    },
    'primaryType': 'Mail',
    'types': {
        'EIP712Domain': [
            {'name': 'name', 'type': 'string'},
            {'name': 'version', 'type': 'string'},
            {'name': 'chainId', 'type': 'uint256'},
            {'name': 'verifyingContract', 'type': 'address'}
        ],
        'Mail': [
            {'name': 'from', 'type': 'Person'},
            {'name': 'to', 'type': 'Person[]'},
            {'name': 'contents', 'type': 'string'}
        ],
        'Person': [
            {'name': 'name', 'type': 'string'},
            {'name': 'wallets', 'type': 'address[]'}
        ]
    }
}";

var signer = new Eip712TypedDataSigner();
var key = new EthECKey("94e001d6adf3a3275d5dd45971c2a5f6637d3e9c51f9693f2e678f649e164fa5");

// Sign JSON directly
string signature = signer.SignTypedDataV4(typedDataJson, key);

// Recover signer from JSON + signature
string recoveredAddress = signer.RecoverFromSignatureV4(typedDataJson, signature);
Console.WriteLine($"Signer: {recoveredAddress}");
```

### Example 7: NFT Lazy Minting

```csharp
using Nethereum.Signer.EIP712;
using Nethereum.ABI.EIP712;
using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Numerics;

[Struct("LazyMint")]
public class LazyMint
{
    [Parameter("uint256", "tokenId", 1)]
    public BigInteger TokenId { get; set; }

    [Parameter("string", "tokenURI", 2)]
    public string TokenURI { get; set; }

    [Parameter("address", "creator", 3)]
    public string Creator { get; set; }

    [Parameter("uint256", "royaltyBps", 4)]
    public BigInteger RoyaltyBps { get; set; } // Basis points (100 = 1%)
}

var domain = new Domain
{
    Name = "LazyNFT",
    Version = "1",
    ChainId = 1,
    VerifyingContract = "0x..." // NFT contract
};

var lazyMint = new LazyMint
{
    TokenId = 12345,
    TokenURI = "ipfs://QmYx...",
    Creator = "0x...", // Artist address
    RoyaltyBps = 1000 // 10% royalty
};

var signer = new Eip712TypedDataSigner();
var key = new EthECKey("ARTIST_PRIVATE_KEY");
string signature = signer.SignTypedData(lazyMint, domain, "LazyMint", key);

// NFT is not minted until someone buys it
// Buyer pays gas to mint + purchase in one transaction
// contract.buyAndMint(tokenId, tokenURI, creator, royaltyBps, signature)
```

### Example 8: Session Key Authorization

```csharp
using Nethereum.Signer.EIP712;
using Nethereum.ABI.EIP712;
using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Numerics;

[Struct("SessionKey")]
public class SessionKey
{
    [Parameter("address", "sessionPublicKey", 1)]
    public string SessionPublicKey { get; set; }

    [Parameter("uint256", "expiresAt", 2)]
    public BigInteger ExpiresAt { get; set; }

    [Parameter("address[]", "allowedContracts", 3)]
    public string[] AllowedContracts { get; set; }
}

var domain = new Domain
{
    Name = "GameSession",
    Version = "1",
    ChainId = 137,
    VerifyingContract = "0x..." // Game contract
};

var sessionKey = new SessionKey
{
    SessionPublicKey = "0x...", // Temporary key for gaming session
    ExpiresAt = DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeSeconds(),
    AllowedContracts = new[] { "0x...", "0x..." } // Game contracts
};

var signer = new Eip712TypedDataSigner();
var mainKey = new EthECKey("MAIN_WALLET_PRIVATE_KEY");
string signature = signer.SignTypedData(sessionKey, domain, "SessionKey", mainKey);

// Session key can now make transactions within constraints
// User doesn't need to approve each action - better UX for games
```

### Example 9: Verify Signature Without Private Key

```csharp
using Nethereum.Signer.EIP712;
using Nethereum.ABI.EIP712;
using Nethereum.Util;

// You have a signature and need to verify who signed it
var typedData = new TypedData<Domain>
{
    Domain = new Domain { Name = "MyApp", Version = "1", ChainId = 1 },
    // ... rest of typed data
};

string receivedSignature = "0x...";
string expectedSigner = "0x...";

var signer = new Eip712TypedDataSigner();

// Recover the address that created the signature
string recoveredAddress = signer.RecoverFromSignatureV4(typedData, receivedSignature);

// Verify it matches expected signer
bool isValid = expectedSigner.IsTheSameAddress(recoveredAddress);

if (isValid)
{
    Console.WriteLine("Signature is valid!");
    // Process the signed message
}
else
{
    Console.WriteLine($"Invalid signature!");
    Console.WriteLine($"Expected: {expectedSigner}");
    Console.WriteLine($"Got: {recoveredAddress}");
}
```

### Example 10: Uniswap Permit2

`PermitSigner` signs the Uniswap [Permit2](https://github.com/Uniswap/permit2) messages against the
Permit2 domain (name `"Permit2"`, the chain id, and the Permit2 verifying contract). The message types
live in `Nethereum.ABI.EIP712.Permit2`; the on-chain call tuples live in `Nethereum.Contracts.Standards.Permit2`.

**AllowanceTransfer** — `PermitSingle` / `PermitBatch` carry `spender` as a real field:

```csharp
using Nethereum.ABI.EIP712.Permit2;
using Nethereum.Signer.EIP712.Permit2;

var permitSingle = new PermitSingle
{
    Details = new PermitDetails { Token = token, Amount = amount, Expiration = expiration, Nonce = nonce },
    Spender = spender,
    SigDeadline = sigDeadline
};
string signature = PermitSigner.SignPermitSingle(chainId, permit2Address, permitSingle, key);
```

**SignatureTransfer** — Permit2 hashes the caller (`msg.sender`) as the `spender`, so the *signed*
message must include it even though the on-chain `permitTransferFrom` tuple does not. Sign the
`PermitTransferFromWithSpender` model (the `Spender` is the address that will call Permit2):

```csharp
var signature = PermitSigner.SignPermitTransferFrom(
    chainId, permit2Address,
    new PermitTransferFromWithSpender
    {
        Permitted = new TokenPermissions { Token = token, Amount = amount },
        Spender = spender,      // the caller; hashed as msg.sender on-chain
        Nonce = nonce,
        Deadline = deadline
    },
    key);
```

`SignPermitBatch`, `SignPermitBatchTransferFrom` and `HashPermitSingle` follow the same pattern.
`PermitSigner` only signs and hashes — it has no recover/verify methods; recover a Permit2 signature
with `Eip712TypedDataSigner.RecoverFromSignatureV4` against the same typed data used to sign. For the
x402 witness variant (`permitWitnessTransferFrom`) see `Nethereum.X402`.

## API Reference

### Eip712TypedDataSigner

Main class for EIP-712 signing operations.

```csharp
public class Eip712TypedDataSigner
{
    // Sign a flat message, auto-generating the schema from its [Parameter] attributes.
    // For flat messages only - a message with reference-type (nested struct) fields
    // needs the TypedData<TDomain> + SignTypedDataV4(message, typedData, key) overload below.
    public string SignTypedData<T, TDomain>(T data, TDomain domain, string primaryTypeName, EthECKey key);

    // Sign a pre-built TypedData<TDomain> (Message must already be populated)
    public string SignTypedData<TDomain>(TypedData<TDomain> typedData, EthECKey key);

    // Sign for eth_signTypedData_v4 compatibility (identical hashing/signing to SignTypedData - only the encoding source differs)
    public string SignTypedDataV4<TDomain>(TypedData<TDomain> typedData, EthECKey key);
    public string SignTypedDataV4(string json, EthECKey key);
    public string SignTypedDataV4<TDomain>(string json, EthECKey key, string messageKeySelector = "message");
    public string SignTypedDataV4<T, TDomain>(T message, TypedData<TDomain> typedData, EthECKey key);

    // Sign with external signer (hardware wallet, etc.)
    public Task<string> SignTypedDataV4<TDomain>(TypedData<TDomain> typedData, IEthExternalSigner ethExternalSigner);

    // Recover signer address from signature
    public string RecoverFromSignatureV4<T, TDomain>(T message, TypedData<TDomain> typedData, string signature);
    public string RecoverFromSignatureV4<TDomain>(TypedData<TDomain> typedData, string signature);
    public string RecoverFromSignatureV4(string json, string signature, string messageKeySelector = "message");
    public string RecoverFromSignatureV4(byte[] encodedData, string signature);
    public string RecoverFromSignatureHashV4(byte[] hash, string signature);

    // Encode typed data (for custom workflows)
    public byte[] EncodeTypedData<TDomain>(TypedData<TDomain> typedData);
    public byte[] EncodeTypedDataRaw(TypedDataRaw typedData);
    public byte[] EncodeTypedData(string json);
    public byte[] EncodeTypedData<TDomain>(string json, string messageKeySelector = "message");
    public byte[] EncodeTypedData<T, TDomain>(T message, TypedData<TDomain> typedData);

    // Singleton instance
    public static Eip712TypedDataSigner Current { get; }
}
```

## Related Packages

### Used By (Consumers)
- **Nethereum.Accounts** - Account signing with EIP-712
- **Nethereum.GnosisSafe** - Safe transaction typed-data signing
- **Nethereum.Uniswap** - Permit2 signing
- **Nethereum.WebAuthn** - EIP-712 typed data with passkeys
- **Nethereum.X402** - HTTP 402 payment authorization (uses `Eip712TypedDataSigner`)

### Dependencies
- **Nethereum.ABI** - EIP-712 encoding engine
- **Nethereum.Signer** - ECDSA signing primitives
- **Nethereum.Util** - Keccak hashing
- **Nethereum.Hex** - Hex encoding

## Important Notes

### MetaMask Compatibility

Prefer `SignTypedDataV4` for parity with `eth_signTypedData_v4`. For the same
`TypedData<TDomain>`, `SignTypedData` and `SignTypedDataV4` encode the message
identically (`EncodeTypedData`) and produce the **same signature** — there is
no behavioral difference between them for a `TypedData<TDomain>` overload.
`SignTypedDataV4` is the name to reach for by convention, and it is the only
one of the two with JSON and external-signer overloads:

```csharp
string signature = signer.SignTypedDataV4(typedData, key);
```

### Domain Separator is Critical

Always include proper domain to prevent cross-app replay:

```csharp
// CORRECT - Unique per app and chain
var domain = new Domain
{
    Name = "My dApp",
    Version = "1",
    ChainId = 1, // REQUIRED for replay protection
    VerifyingContract = "0x..." // REQUIRED
};

// WRONG - Missing chainId allows replay attacks
var domain = new Domain
{
    Name = "My dApp",
    Version = "1"
};
```

### Type Order Matters

Member order in type definitions must match exactly:

```csharp
// CORRECT - Consistent order
new MemberDescription { Name = "name", Type = "string" },
new MemberDescription { Name = "wallet", Type = "address" }

// WRONG - Different order produces different hash
new MemberDescription { Name = "wallet", Type = "address" },
new MemberDescription { Name = "name", Type = "string" }
```

### Frontend Integration

JSON format matches JavaScript exactly:

```javascript
// Frontend (JavaScript)
const signature = await ethereum.request({
  method: 'eth_signTypedData_v4',
  params: [account, JSON.stringify(typedData)]
});

// Backend (.NET) - Same JSON structure
string signature = signer.SignTypedDataV4(jsonString, key);
```

## Additional Resources

- [EIP-712: Typed Structured Data Hashing and Signing](https://eips.ethereum.org/EIPS/eip-712)
- [MetaMask eth_signTypedData_v4](https://docs.metamask.io/wallet/how-to/sign-data/#use-eth_signtypeddata_v4)
- [ERC-2612: Permit Extension](https://eips.ethereum.org/EIPS/eip-2612)
- [EIP-3009: Transfer With Authorization](https://eips.ethereum.org/EIPS/eip-3009)
- [Nethereum Documentation](http://docs.nethereum.com/)
