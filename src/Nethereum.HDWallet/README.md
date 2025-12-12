# Nethereum.HDWallet

BIP32 and BIP39 hierarchical deterministic (HD) wallet implementation for generating Ethereum-compatible addresses from mnemonic seed phrases.

## Overview

Nethereum.HDWallet implements **BIP32** (Hierarchical Deterministic Wallets) and **BIP39** (Mnemonic Code for Generating Deterministic Keys) standards to generate an HD tree of Ethereum addresses from a single mnemonic seed phrase. This is the **same standard** used by MetaMask, TREZOR, MyEtherWallet, Ledger, Jaxx, Exodus, and most hardware/software wallets.

**Key Features:**
- Generate 12/15/18/21/24-word mnemonic seed phrases (BIP39)
- Derive unlimited Ethereum addresses from a single seed
- Two standard derivation paths: `m/44'/60'/0'/0/x` (default) and `m/44'/60'/0'/x` (Ledger/Electrum)
- Compatible with MetaMask, TREZOR, Ledger, MyEtherWallet
- Watch-only public wallets (no private key exposure)
- Direct integration with Web3.Account for transactions

**Use Cases:**
- Multi-account wallet applications
- Hardware wallet compatibility
- Backup and recovery using mnemonic phrases
- Secure key management without exposing master private key
- Account abstraction and wallet-as-a-service

## Installation

```bash
dotnet add package Nethereum.HDWallet
```

## Dependencies

**External:**
- **NBitcoin** (v7.0.6) - Bitcoin and cryptographic primitives (BIP32/BIP39 implementation)

**Nethereum:**
- **Nethereum.Web3** - Web3 client and account integration

## Quick Start

```csharp
using Nethereum.HdWallet;
using NBitcoin;

// Generate new wallet with random 12-word seed
var wallet = new Wallet(Wordlist.English, WordCount.Twelve);

// Get first 5 addresses
var addresses = wallet.GetAddresses(5);

// Get account for index 0 (for signing transactions)
var account = wallet.GetAccount(0);

// Use with Web3
var web3 = new Web3.Web3(account, "https://mainnet.infura.io/v3/YOUR-PROJECT-ID");
```

## BIP32/BIP39 Standards

### BIP39 (Mnemonic Codes)
- Converts random entropy into human-readable words
- Supports 12, 15, 18, 21, or 24 words
- Each word list contains 2048 words
- Optional passphrase for additional security (TREZOR-style)

### BIP32 (Hierarchical Deterministic Wallets)
- Derives child keys from master key
- Two derivation paths supported:
  - `m/44'/60'/0'/0/x` - **Default** (MetaMask, TREZOR App, MyEtherWallet, Jaxx, Exodus)
  - `m/44'/60'/0'/x` - **Electrum/Ledger** (Electrum, MyEtherWallet Ledger, Ledger Chrome, imToken)

## Usage Examples

### Example 1: Generate Wallet with Random Seed

```csharp
using Nethereum.HdWallet;
using NBitcoin;

// Generate wallet with 12-word mnemonic (most common)
var wallet = new Wallet(Wordlist.English, WordCount.Twelve);

Console.WriteLine("Mnemonic: " + string.Join(" ", wallet.Words));
// Example: "army van defense carry jealous true garbage claim echo media make crunch"

Console.WriteLine("Seed: " + wallet.Seed);
// Hex-encoded seed derived from mnemonic

Console.WriteLine("Checksum Valid: " + wallet.IsMnemonicValidChecksum);
// true

// Get first address
var firstAddress = wallet.GetAccount(0).Address;
Console.WriteLine("Address 0: " + firstAddress);
```

### Example 2: Restore Wallet from Existing Mnemonic (Real Test Example)

```csharp
using Nethereum.HdWallet;

// Restore wallet from mnemonic (e.g., from backup)
const string words = "ripple scissors kick mammal hire column oak again sun offer wealth tomorrow wagon turn fatal";
const string password = "TREZOR"; // Optional password (leave empty string if unused)

var wallet = new Wallet(words, password);

// Wallet is now restored with all the same addresses
var account = wallet.GetAccount(0);

// Use with Web3 for transactions
var web3 = new Web3.Web3(account, "https://mainnet.infura.io/v3/YOUR-PROJECT-ID");

// All addresses derived from this seed will match the original wallet
```

### Example 3: Generate Multiple Addresses

```csharp
using Nethereum.HdWallet;
using NBitcoin;

var wallet = new Wallet(Wordlist.English, WordCount.Twelve);

// Get first 20 addresses (default)
string[] addresses = wallet.GetAddresses(20);

for (int i = 0; i < addresses.Length; i++)
{
    Console.WriteLine($"Account {i}: {addresses[i]}");
}

// Output:
// Account 0: 0x742d35Cc6634C0532925a3b844Bc454e4438f44e
// Account 1: 0x5aeda56215b167893e80b4fe645ba6d5bab767de
// Account 2: 0x6330a553fc93768f612722bb8c2ec78ac90b3bbc
// ...
```

### Example 4: Get Private Key by Index or Address

```csharp
using Nethereum.HdWallet;
using Nethereum.Hex.HexConvertors.Extensions;
using NBitcoin;

var wallet = new Wallet(Wordlist.English, WordCount.Twelve);

// Get private key by index
byte[] privateKey0 = wallet.GetPrivateKey(0);
Console.WriteLine("Private Key 0: " + privateKey0.ToHex());

// Get private key by address (useful for account recovery)
string targetAddress = "0x742d35Cc6634C0532925a3b844Bc454e4438f44e";
byte[] privateKeyByAddress = wallet.GetPrivateKey(targetAddress, maxIndexSearch: 20);

if (privateKeyByAddress != null)
{
    Console.WriteLine("Found private key for address!");
}
else
{
    Console.WriteLine("Address not found in first 20 accounts");
}
```

### Example 5: Use Electrum/Ledger Derivation Path

```csharp
using Nethereum.HdWallet;
using NBitcoin;

// Use Electrum/Ledger path instead of default
var wallet = new Wallet(
    Wordlist.English,
    WordCount.Twelve,
    seedPassword: null,
    path: Wallet.ELECTRUM_LEDGER_PATH // "m/44'/60'/0'/x"
);

// Addresses will match Ledger hardware wallet
var addresses = wallet.GetAddresses(5);
```

### Example 6: Generate Wallet with Seed Password (TREZOR-Style)

```csharp
using Nethereum.HdWallet;
using NBitcoin;

// Same mnemonic + different password = completely different addresses
const string mnemonic = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";

var wallet1 = new Wallet(mnemonic, seedPassword: "password123");
var wallet2 = new Wallet(mnemonic, seedPassword: "different_password");

var address1 = wallet1.GetAccount(0).Address;
var address2 = wallet2.GetAccount(0).Address;

Console.WriteLine("Wallet 1 Address: " + address1);
Console.WriteLine("Wallet 2 Address: " + address2);
// Completely different addresses from same mnemonic!

// This is the 25th word / passphrase feature
// Used for plausible deniability and additional security
```

### Example 7: Public Wallet (Watch-Only, No Private Keys)

```csharp
using Nethereum.HdWallet;
using NBitcoin;

// Full wallet (has private keys)
var fullWallet = new Wallet(Wordlist.English, WordCount.Twelve);

// Extract public wallet (no private keys exposed)
PublicWallet publicWallet = fullWallet.GetMasterPublicWallet();

// Can generate addresses but cannot sign transactions
string[] watchAddresses = publicWallet.GetAddresses(10);

Console.WriteLine("Watch-only addresses:");
foreach (var address in watchAddresses)
{
    Console.WriteLine(address);
}

// Use case: Server-side address generation without private key exposure
// Server can generate addresses, but cannot spend funds
```

### Example 8: Share Extended Public Key (xPub)

```csharp
using Nethereum.HdWallet;
using Nethereum.Hex.HexConvertors.Extensions;
using NBitcoin;

var wallet = new Wallet(Wordlist.English, WordCount.Twelve);

// Get extended public key (xPub)
var extPubKey = wallet.GetMasterExtPubKey();
byte[] xPubBytes = extPubKey.PubKey.ToBytes();

Console.WriteLine("Extended Public Key: " + xPubBytes.ToHex());

// Share xPub with third-party service
// They can generate addresses but cannot access private keys

// Recreate PublicWallet from xPub on another system
var remotePublicWallet = new PublicWallet(xPubBytes);
var remoteAddresses = remotePublicWallet.GetAddresses(5);

// Addresses match original wallet without exposing private keys
```

### Example 9: Using HDWallet with Web3 Transactions

```csharp
using Nethereum.HdWallet;
using Nethereum.Web3;
using Nethereum.Hex.HexTypes;
using NBitcoin;

// Create wallet
var wallet = new Wallet(Wordlist.English, WordCount.Twelve);

// Get account 0 with chain ID
var account = wallet.GetAccount(0, chainId: 1); // 1 = Ethereum mainnet

// Create Web3 instance with account
var web3 = new Web3(account, "https://mainnet.infura.io/v3/YOUR-PROJECT-ID");

// Send transaction
var toAddress = "0x742d35Cc6634C0532925a3b844Bc454e4438f44e";
var transactionHash = await web3.Eth.GetEtherTransferService()
    .TransferEtherAndWaitForReceiptAsync(toAddress, 0.01m);

Console.WriteLine("Transaction sent: " + transactionHash);

// Use different account from same wallet
var account5 = wallet.GetAccount(5, chainId: 1);
var web3Account5 = new Web3(account5, "https://mainnet.infura.io/v3/YOUR-PROJECT-ID");
```

## API Reference

### Wallet

Main class for HD wallet operations with private key access.

```csharp
public class Wallet
{
    // Constants
    public const string DEFAULT_PATH = "m/44'/60'/0'/0/x"; // MetaMask, TREZOR, MEW
    public const string ELECTRUM_LEDGER_PATH = "m/44'/60'/0'/x"; // Electrum, Ledger

    // Constructors
    public Wallet(Wordlist wordList, WordCount wordCount, string seedPassword = null, string path = DEFAULT_PATH);
    public Wallet(string words, string seedPassword, string path = DEFAULT_PATH);
    public Wallet(byte[] seed, string path = DEFAULT_PATH);

    // Properties
    public string Seed { get; }
    public string[] Words { get; }
    public bool IsMnemonicValidChecksum { get; }
    public string Path { get; }

    // Methods
    public string[] GetAddresses(int numberOfAddresses = 20);
    public byte[] GetPrivateKey(int index);
    public byte[] GetPrivateKey(string address, int maxIndexSearch = 20);
    public byte[] GetPublicKey(int index);
    public Account GetAccount(int index, BigInteger? chainId = null);
    public Account GetAccount(string address, int maxIndexSearch = 20, BigInteger? chainId = null);

    // Advanced
    public ExtKey GetMasterExtKey();
    public ExtPubKey GetMasterExtPubKey();
    public PublicWallet GetMasterPublicWallet();
    public ExtKey GetExtKey(int index, bool hardened = false);
    public ExtPubKey GetExtPubKey(int index, bool hardened = false);
    public EthECKey GetEthereumKey(int index);
}
```

### PublicWallet

Watch-only wallet (no private key access).

```csharp
public class PublicWallet
{
    // Constructors
    public PublicWallet(ExtPubKey extPubKey);
    public PublicWallet(ExtKey extKey);
    public PublicWallet(byte[] extPublicKey);
    public PublicWallet(string extPublicKey);

    // Properties
    public ExtPubKey ExtPubKey { get; }

    // Methods
    public string[] GetAddresses(int numberOfAddresses = 20);
    public string GetAddress(int index);
    public ExtPubKey GetExtPubKey(int index);
    public PublicWallet GetChildPublicWallet(int index);
    public byte[] GetExtendedPublicKey();
}
```

## Important Notes


### Seed Passwords (25th Word)

```csharp
var wallet1 = new Wallet(mnemonic, seedPassword: "");        // No password
var wallet2 = new Wallet(mnemonic, seedPassword: "secret");  // With password

// SAME mnemonic + DIFFERENT password = DIFFERENT addresses
```

- Seed password acts as a "25th word"
- Provides plausible deniability (duress wallet)
- Adds extra security layer
- Must be remembered (not part of mnemonic backup)

### Derivation Paths

| Path | Format | Used By |
|------|--------|---------|
| **Default** | `m/44'/60'/0'/0/x` | MetaMask, TREZOR, MEW, Jaxx, Exodus |
| **Ledger** | `m/44'/60'/0'/x` | Ledger, Electrum, MEW (Ledger mode), imToken |

**Important:** Different paths = different addresses from same seed.

### Word Counts

| Words | Entropy | Security | Use Case |
|-------|---------|----------|----------|
| 12 | 128-bit | Standard | Most wallets (recommended) |
| 15 | 160-bit | Higher | Enhanced security |
| 18 | 192-bit | Higher | Enhanced security |
| 21 | 224-bit | Highest | Maximum security |
| 24 | 256-bit | Highest | Maximum security |

**Recommendation:** 12 words is standard and provides excellent security for most use cases.

### Account Indexing

```csharp
// MetaMask account numbering
var account0 = wallet.GetAccount(0); // MetaMask "Account 1"
var account1 = wallet.GetAccount(1); // MetaMask "Account 2"
var account2 = wallet.GetAccount(2); // MetaMask "Account 3"
```

**Note:** Index starts at 0, but user-facing UIs typically show "Account 1", "Account 2", etc.

### Public Wallet Use Cases

```csharp
// Use case 1: Payment processor
// Generate addresses on server without private keys
var publicWallet = GetPublicWalletFromSecureStorage();
var depositAddress = publicWallet.GetAddress(customerID);

// Use case 2: Accounting/auditing
// View all addresses and balances without spending ability

// Use case 3: Address generation service
// Generate addresses for users without key exposure
```

## Related Packages

### Used By (Consumers)
- **Nethereum.Accounts** - Account management
- Wallet applications
- Multi-sig services
- Payment processors

### Dependencies
- **Nethereum.Web3** - Web3 integration
- **Nethereum.Signer** - Transaction signing
- **NBitcoin** - BIP32/BIP39 implementation

## Additional Resources

- [BIP32 Specification](https://github.com/bitcoin/bips/blob/master/bip-0032.mediawiki)
- [BIP39 Specification](https://github.com/bitcoin/bips/blob/master/bip-0039.mediawiki)
- [BIP44 Specification](https://github.com/bitcoin/bips/blob/master/bip-0044.mediawiki)
- [Nethereum Documentation](http://docs.nethereum.com/)
- [Ian Coleman's BIP39 Tool](https://iancoleman.io/bip39/) - Test derivation paths
