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

```csharp
public class TypedData<TDomain>
{
    public TDomain Domain { get; set; }                           // Domain separator
    public Dictionary<string, MemberDescription[]> Types { get; set; }  // Type definitions
    public string PrimaryType { get; set; }                       // Main message type
    public object Message { get; set; }                           // Actual data
}
```

## Quick Start

```csharp
using Nethereum.Signer;
using Nethereum.Signer.EIP712;
using Nethereum.ABI.EIP712;

// 1. Define your message type
public class Mail
{
    public Person From { get; set; }
    public Person To { get; set; }
    public string Contents { get; set; }
}

public class Person
{
    public string Name { get; set; }
    public string Wallet { get; set; }
}

// 2. Create domain
var domain = new Domain
{
    Name = "Ether Mail",
    Version = "1",
    ChainId = 1,
    VerifyingContract = "0xCcCCccccCCCCcCCCCCCcCcCccCcCCCcCcccccccC"
};

// 3. Create typed data
var mail = new Mail
{
    From = new Person { Name = "Alice", Wallet = "0x..." },
    To = new Person { Name = "Bob", Wallet = "0x..." },
    Contents = "Hello Bob!"
};

// 4. Sign
var signer = new Eip712TypedDataSigner();
var key = new EthECKey("YOUR_PRIVATE_KEY");
string signature = signer.SignTypedData(mail, domain, "Mail", key);
```

## Usage Examples

### Example 1: Simple Typed Message (Real Test Example)

```csharp
using Nethereum.Signer.EIP712;
using Nethereum.ABI.EIP712;
using Nethereum.Signer;
using System.Collections.Generic;

// Define domain
var domain = new Domain
{
    Name = "Ether Mail",
    Version = "1",
    ChainId = 1,
    VerifyingContract = "0xCcCCccccCCCCcCCCCCCcCcCccCcCCCcCcccccccC"
};

// Define type schema
var typedData = new TypedData<Domain>
{
    Domain = domain,
    Types = new Dictionary<string, MemberDescription[]>
    {
        ["EIP712Domain"] = new[]
        {
            new MemberDescription { Name = "name", Type = "string" },
            new MemberDescription { Name = "version", Type = "string" },
            new MemberDescription { Name = "chainId", Type = "uint256" },
            new MemberDescription { Name = "verifyingContract", Type = "address" }
        },
        ["Mail"] = new[]
        {
            new MemberDescription { Name = "from", Type = "Person" },
            new MemberDescription { Name = "to", Type = "Person[]" },
            new MemberDescription { Name = "contents", Type = "string" }
        },
        ["Person"] = new[]
        {
            new MemberDescription { Name = "name", Type = "string" },
            new MemberDescription { Name = "wallets", Type = "address[]" }
        }
    },
    PrimaryType = "Mail",
    Message = new[]
    {
        new MemberValue
        {
            TypeName = "Person",
            Value = new[]
            {
                new MemberValue { TypeName = "string", Value = "Cow" },
                new MemberValue { TypeName = "address[]", Value = new List<string>
                {
                    "0xCD2a3d9F938E13CD947Ec05AbC7FE734Df8DD826",
                    "0xDeaDbeefdEAdbeefdEadbEEFdeadbeEFdEaDbeeF"
                }}
            }
        },
        new MemberValue
        {
            TypeName = "Person[]",
            Value = new List<MemberValue[]>
            {
                new[]
                {
                    new MemberValue { TypeName = "string", Value = "Bob" },
                    new MemberValue { TypeName = "address[]", Value = new List<string>
                    {
                        "0xbBbBBBBbbBBBbbbBbbBbbbbBBbBbbbbBbBbbBBbB"
                    }}
                }
            }
        },
        new MemberValue { TypeName = "string", Value = "Hello, Bob!" }
    }
};

// Sign
var signer = new Eip712TypedDataSigner();
var key = new EthECKey("94e001d6adf3a3275d5dd45971c2a5f6637d3e9c51f9693f2e678f649e164fa5");
string signature = signer.SignTypedDataV4(typedData, key);

Console.WriteLine($"Signature: {signature}");

// Verify
string recoveredAddress = signer.RecoverFromSignatureV4(typedData, signature);
Console.WriteLine($"Signer: {recoveredAddress}");
Console.WriteLine($"Match: {key.GetPublicAddress() == recoveredAddress}");
```

### Example 2: ERC-2612 Permit (Gasless Approval)

```csharp
using Nethereum.Signer.EIP712;
using Nethereum.ABI.EIP712;
using Nethereum.Signer;
using System.Numerics;

// ERC-20 Permit allows approvals via signature (no gas cost)
public class Permit
{
    public string Owner { get; set; }
    public string Spender { get; set; }
    public BigInteger Value { get; set; }
    public BigInteger Nonce { get; set; }
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
using System.Numerics;

public class MetaTransaction
{
    public BigInteger Nonce { get; set; }
    public string From { get; set; }
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
using System.Numerics;

public class Order
{
    public string MakerAddress { get; set; }
    public string TakerAddress { get; set; }
    public string MakerAssetAddress { get; set; }
    public string TakerAssetAddress { get; set; }
    public BigInteger MakerAssetAmount { get; set; }
    public BigInteger TakerAssetAmount { get; set; }
    public BigInteger ExpirationTimeSeconds { get; set; }
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

public class Vote
{
    public string From { get; set; }
    public string Space { get; set; }
    public long Timestamp { get; set; }
    public string Proposal { get; set; }
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

### Example 6: Sign from JSON (Real Test Example)

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
using System.Numerics;

public class LazyMint
{
    public BigInteger TokenId { get; set; }
    public string TokenURI { get; set; }
    public string Creator { get; set; }
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
using System.Numerics;

public class SessionKey
{
    public string SessionPublicKey { get; set; }
    public BigInteger ExpiresAt { get; set; }
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

## API Reference

### Eip712TypedDataSigner

Main class for EIP-712 signing operations.

```csharp
public class Eip712TypedDataSigner
{
    // Sign typed data (generates schema automatically)
    public string SignTypedData<T, TDomain>(T data, TDomain domain, string primaryTypeName, EthECKey key);

    // Sign pre-defined typed data
    public string SignTypedData<TDomain>(TypedData<TDomain> typedData, EthECKey key);

    // Sign for eth_signTypedData_v4 compatibility
    public string SignTypedDataV4<TDomain>(TypedData<TDomain> typedData, EthECKey key);
    public string SignTypedDataV4(string json, EthECKey key);
    public string SignTypedDataV4<T, TDomain>(T message, TypedData<TDomain> typedData, EthECKey key);

    // Sign with external signer (hardware wallet, etc.)
    public Task<string> SignTypedDataV4<TDomain>(TypedData<TDomain> typedData, IEthExternalSigner signer);

    // Recover signer address from signature
    public string RecoverFromSignatureV4<TDomain>(TypedData<TDomain> typedData, string signature);
    public string RecoverFromSignatureV4(string json, string signature);
    public string RecoverFromSignatureV4(byte[] encodedData, string signature);

    // Encode typed data (for custom workflows)
    public byte[] EncodeTypedData<TDomain>(TypedData<TDomain> typedData);
    public byte[] EncodeTypedData(string json);

    // Singleton instance
    public static Eip712TypedDataSigner Current { get; }
}
```

## Related Packages

### Used By (Consumers)
- **Nethereum.Accounts** - Account signing with EIP-712
- **Nethereum.Contracts.Standards** - ERC-2612 Permit, EIP-3009
- **Nethereum.X402** - HTTP 402 payment authorization

### Dependencies
- **Nethereum.ABI** - EIP-712 encoding engine
- **Nethereum.Signer** - ECDSA signing primitives
- **Nethereum.Util** - Keccak hashing
- **Nethereum.Hex** - Hex encoding

## Important Notes

### MetaMask Compatibility

Always use `SignTypedDataV4` for MetaMask compatibility:

```csharp
// CORRECT - Works with MetaMask
string signature = signer.SignTypedDataV4(typedData, key);

// WRONG - Old format, not recommended
string signature = signer.SignTypedData(typedData, key);
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
