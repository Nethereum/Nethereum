---
name: hd-wallets
description: Derive multiple Ethereum accounts from a mnemonic seed phrase using Nethereum HD wallets. Use this skill whenever the user asks about mnemonic phrases, seed phrases, BIP-39, BIP-32, BIP-44, HD wallets, deriving accounts from a seed, wallet recovery, 12-word or 24-word phrases, derivation paths, or generating multiple addresses from one key in C#/.NET.
user-invocable: true
---

# HD Wallets with Nethereum

Two implementations available:

## Full HD Wallet (NBitcoin-based)

NuGet: `Nethereum.HDWallet`

```bash
dotnet add package Nethereum.HDWallet
```

### Generate New Wallet
```csharp
using Nethereum.HdWallet;
using NBitcoin;

var wallet = new Wallet(Wordlist.English, WordCount.Twelve);
var mnemonic = string.Join(" ", wallet.Words);
```

### Restore from Mnemonic
```csharp
var wallet = new Wallet("rapid squeeze excess salute ...", seedPassword: null);
var account = wallet.GetAccount(0, chainId: 1);
var web3 = new Web3(account, "https://your-rpc-url");
```

### Derivation Paths
```csharp
// Default: m/44'/60'/0'/0/x (MetaMask, TREZOR, MEW)
var wallet = new Wallet(words, null, Wallet.DEFAULT_PATH);

// Ledger/Electrum: m/44'/60'/0'/x
var wallet = new Wallet(words, null, Wallet.ELECTRUM_LEDGER_PATH);
```

### Key Methods
```csharp
var addresses = wallet.GetAddresses(10);     // First 10 addresses
var privateKey = wallet.GetPrivateKey(0);     // Private key bytes
var ethKey = wallet.GetEthereumKey(0);        // EthECKey for signing
var account = wallet.GetAccount(0, chainId: 1); // Account for Web3
```

### Watch-Only (Public Wallet)
```csharp
var xPub = wallet.GetMasterExtPubKey();
var publicWallet = new PublicWallet(xPub);
var addresses = publicWallet.GetAddresses(10);
```

## Light HD Wallet (Zero Dependencies)

Included in `Nethereum.Accounts` — no extra package needed. Uses only `System.Security.Cryptography`. Modern .NET only (net6.0+).

```csharp
using Nethereum.Accounts.Bip32;

// Generate mnemonic
var mnemonic = Bip39.GenerateMnemonic(12);

// Create wallet
var wallet = new MinimalHDWallet(mnemonic);

// Derive keys (m/44'/60'/0'/0/{index})
var key = wallet.GetEthereumKey(0);
var address = wallet.GetEthereumAddress(0);

// Custom path
var key = wallet.GetKeyFromPath("m/44'/60'/0'/0/5");
```

## When to Use Which

| Feature | `Nethereum.HDWallet` | `MinimalHDWallet` |
|---|---|---|
| Dependencies | NBitcoin | None |
| Frameworks | net452+ | net6.0+ |
| Extended keys (xPub) | Yes | No |
| Key caching | Yes | No |
| Best for | Full wallet apps | Embedded, mobile, WASM |

For full documentation, see: https://docs.nethereum.com/docs/signing-and-key-management/guide-hd-wallets
