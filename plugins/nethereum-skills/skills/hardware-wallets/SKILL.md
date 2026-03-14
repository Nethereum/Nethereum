---
name: hardware-wallets
description: Sign Ethereum transactions with Ledger and Trezor hardware wallets using Nethereum. Use this skill whenever the user asks about Ledger signing, Trezor signing, hardware wallet integration, HSM device signing, external signers, or hardware security modules for Ethereum in C#/.NET.
user-invocable: true
---

# Hardware Wallets with Nethereum

Private key never leaves the device. Both use the `ExternalAccount` pattern.

## Ledger

NuGet: `Nethereum.Signer.Ledger`

```bash
dotnet add package Nethereum.Signer.Ledger
```

```csharp
using Nethereum.Ledger;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;

var ledgerManagerFactory = new NethereumLedgerManagerBrokerFactory();
var signer = new LedgerExternalSigner(ledgerManagerFactory, accountIndex: 0);

var externalAccount = new ExternalAccount(signer, chainId: 1);
await externalAccount.InitialiseAsync();

var web3 = new Web3(externalAccount, "https://your-rpc-url");

// Send transaction (same API as regular Account)
var receipt = await web3.Eth.GetEtherTransferService()
    .TransferEtherAndWaitForReceiptAsync(toAddress, 0.1m);
```

### Legacy Path (Electrum/Ledger)
```csharp
var signer = new LedgerExternalSigner(ledgerManagerFactory, accountIndex: 0, legacyPath: true);
// Uses m/44'/60'/0'/x instead of m/44'/60'/0'/0/x
```

## Trezor

NuGet: `Nethereum.Signer.Trezor`

```bash
dotnet add package Nethereum.Signer.Trezor
```

Requires a PIN prompt handler:

```csharp
using Nethereum.Trezor;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;

public class ConsolePinHandler : ITrezorPromptHandler
{
    public Task<string> PromptPin()
    {
        Console.Write("Enter PIN (numpad positions): ");
        return Task.FromResult(Console.ReadLine());
    }

    public Task<string> PromptPassphrase()
    {
        Console.Write("Enter passphrase (or empty): ");
        return Task.FromResult(Console.ReadLine());
    }
}

var pinHandler = new ConsolePinHandler();
var trezorManagerFactory = new NethereumTrezorManagerBrokerFactory();
var signer = new TrezorExternalSigner(trezorManagerFactory, pinHandler, accountIndex: 0);

var externalAccount = new ExternalAccount(signer, chainId: 1);
await externalAccount.InitialiseAsync();

var web3 = new Web3(externalAccount, "https://your-rpc-url");
```

### Personal Message Signing (Trezor only)
```csharp
var signature = await signer.SignAsync(
    System.Text.Encoding.UTF8.GetBytes("Hello Ethereum!"));
```

## Comparison

| Feature | Ledger | Trezor |
|---|---|---|
| EIP-712 signing | No | Yes |
| Personal message signing | No | Yes |
| PIN entry | On device | Via handler |
| Passphrase (25th word) | On device | Via handler |

For full documentation, see: https://docs.nethereum.com/docs/signing-and-key-management/guide-hardware-wallets
