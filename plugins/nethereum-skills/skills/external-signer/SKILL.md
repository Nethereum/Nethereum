---
name: external-signer
description: "Help users own an ERC-4337 smart account whose ECDSA key never lives in the application process — sign UserOperations with AWS KMS, Azure Key Vault, an HSM, a Ledger, or a Trezor instead of an in-memory private key, using Nethereum.AccountAbstraction and IEthExternalSigner. Use whenever the user mentions KMS signing, Azure Key Vault, AWS Key Management Service, HSM-backed wallets, custody, Ledger/Trezor hardware wallet signing for a smart account, MPC signers, or 'the key must never be in memory' requirements, in .NET/C#."
user-invocable: true
---

# External Signers: Own a Smart Account with a KMS, HSM, or Hardware Wallet

For institutional and custody use cases, an in-memory `EthECKey` isn't acceptable — the signing key has to live in a cloud Key Management Service, a hardware security module, or a physical device, and must never be readable by the application process itself. `IEthExternalSigner` is Nethereum's seam for that: signing happens as a remote or hardware operation instead of an in-process ECDSA computation.

The account itself doesn't change. It is still a plain ECDSA-owned `NethereumSmartAccount`, installed with the same `EcdsaValidatorModule` used everywhere else in Account Abstraction. Only *where the key lives* and *who computes the signature* changes — the generic on-ramp doesn't care which `IAccountSigningService` you hand it, so wiring an external signer in is exactly the same shape as wiring a raw key, one level removed.

## When to Use This

- User needs a smart account's **signing key in a KMS/HSM**, never in application memory
- User mentions **AWS Key Management Service, Azure Key Vault**, custody infrastructure
- User wants a **Ledger or Trezor** hardware wallet to control a smart account
- User is building **institutional custody** or compliance-driven signing
- User wants to plug in a **custom/MPC signer** without changing the rest of the AA stack

## Packages

```bash
dotnet add package Nethereum.Web3
dotnet add package Nethereum.AccountAbstraction
```

Plus whichever signer package matches the backend:

| Backend | Package | Type |
|---------|---------|------|
| AWS Key Management Service | `Nethereum.Signer.AWSKeyManagement` | `Nethereum.Signer.AWSKeyManagement.AWSKeyManagementExternalSigner` |
| Azure Key Vault | `Nethereum.Signer.AzureKeyVault` | `Nethereum.Signer.AzureKeyVault.AzureKeyVaultExternalSigner` |
| Ledger hardware wallet | `Nethereum.Signer.Ledger` | `Nethereum.Ledger.LedgerExternalSigner` |
| Trezor hardware wallet | `Nethereum.Signer.Trezor` | `Nethereum.Signer.Trezor.TrezorExternalSigner` |

Each derives from `Nethereum.Signer.EthExternalSignerBase`. You also need `IAAClient` registered (the standard `AddNethereumAccountAbstraction` setup) and the same `NethereumAccountFactory` + `ECDSAValidator` pair deployed on the target chain that any ECDSA-owned account uses — an external signer installs the identical validator, so there's nothing extra to deploy on-chain for it.

## The Simple Way

Wrap any `IEthExternalSigner` in `AccountSigningExternalService`, then drive it through the same generic on-ramp as any other signer:

```csharp
using Nethereum.Accounts.AccountMessageSigning;
using Nethereum.AccountAbstraction;
using Nethereum.AccountAbstraction.Client;
using Nethereum.AccountAbstraction.ERC7579.Modules;
using Nethereum.AccountAbstraction.Signing;
using Nethereum.Signer;

// externalSigner is any IEthExternalSigner - a KMS, HSM, or hardware-wallet signer whose private
// key never lives in this process.
IEthExternalSigner externalSigner = /* your KMS/HSM/hardware-wallet signer */;
var ownerAddress = await externalSigner.GetAddressAsync();

var signingService = new AccountSigningExternalService(externalSigner);
var validator = new EcdsaValidatorModule(deploymentAddresses.EcdsaValidatorAddress);
var initData = AccountInitDataBuilder.BuildEcdsa(deploymentAddresses.EcdsaValidatorAddress, ownerAddress);

var account = await aaClient.CreateAccountAsync(signingService, validator, initData);   // NethereumSmartAccount

myToken.UseAccountAbstraction(account, aaClient);
var receipt = (AATransactionReceipt)await myToken.TransferRequestAndWaitForReceiptAsync(recipient, amount);
Console.WriteLine($"UserOp success: {receipt.UserOpSuccess}, sender: {receipt.Sender}");
```

That's it — no code downstream of `CreateAccountAsync` knows or cares that the owner key lives in a KMS instead of memory. Every signature over a UserOperation hash is produced by a call out to `externalSigner`, never by an in-process private key.

## Mental Model: Same ECDSA Account, Key Lives Elsewhere

An external-signer account is **not** a distinct account type — it's an ordinary ECDSA-owned `NethereumSmartAccount` with the same `EcdsaValidatorModule` and `AccountInitDataBuilder.BuildEcdsa` init data as a raw-key account. On-chain, the deployed `ECDSAValidator` reports the external signer's address as owner — indistinguishable from an account owned by an in-process key, because it's the same signature scheme (secp256k1/ECDSA) either way.

What changes is entirely client-side: `AccountSigningExternalService` wraps an `IEthExternalSigner` instead of an `EthECKey`, and its `SignTypedDataV4`/`PersonalSign` are delegated out to that signer — `SignTypedDataJsonAsync` for typed data, `SignEthereumMessageAsync` for personal-sign — as a KMS API call or HSM operation rather than an in-memory computation. Contrast this with a WebAuthn passkey (see the `webauthn-passkeys` skill): a passkey is a *different* signature scheme (P-256), whereas an external signer is still plain secp256k1 ECDSA — just computed somewhere else.

## Constructing a Signer per Backend

```csharp
using Amazon;
using Nethereum.Signer.AWSKeyManagement;

// keyId identifies an asymmetric ECC_SECG_P256K1 key already created in AWS KMS.
IEthExternalSigner externalSigner = new AWSKeyManagementExternalSigner(
    keyId: "arn:aws:kms:...:key/...", region: RegionEndpoint.EUWest1);
```

```csharp
using Azure.Identity;              // DefaultAzureCredential - add the Azure.Identity NuGet package
using Nethereum.Signer.AzureKeyVault;

// keyName identifies an EC-secp256k1 key already created in the given Key Vault.
IEthExternalSigner externalSigner = new AzureKeyVaultExternalSigner(
    keyName: "my-signing-key", vaultUri: "https://my-vault.vault.azure.net/", credential: new DefaultAzureCredential());
```

An MPC (multi-party computation) signer isn't shipped out of the box, but plugs in the exact same way: implement `IEthExternalSigner` (or derive from `EthExternalSignerBase`) against the MPC network's signing API, and hand it to `AccountSigningExternalService` unchanged.

The **hardware-wallet signers** (`LedgerExternalSigner`, `TrezorExternalSigner`) implement the same `IEthExternalSigner`, need a connected device, and are typically used from a desktop app rather than a server process — see each package's README for device-transport setup. Signing a UserOperation's EIP-712 typed data through a Ledger specifically is not covered by an automated test in this codebase; verify on a real device before relying on it for AA (Ledger does not override the base typed-data signing path, so confirm the produced signature recovers to the owner under `ECDSAValidator`).

## Common Mistakes

- **Trying to drive EIP-7702 with an external signer.** `IAAClient.ConfigureEip7702` takes an `EthECKey ownerKey` directly, because the 7702 authorisation tuple's secp256k1 signature has to be produced by the same key path Nethereum's transaction signing uses, not through the generic `IAccountSigningService` seam. An external signer can own a CREATE2-deployed modular account, but it cannot drive the 7702 upgrade-in-place tier — use the CREATE2 on-ramp shown above instead.
- **Not accounting for signing latency/availability as a distinct failure mode.** Every signature is now a network round-trip (KMS throttling, an unplugged Ledger, a network partition to Azure) instead of a microsecond in-memory computation — a failed `SignAsync`/`SignTypedDataJsonAsync` surfaces as an exception before a UserOperation is ever sent, not as a bundler rejection.
- **Assuming the signer exposes key material.** It doesn't need to, and Nethereum never asks for it — `AccountSigningExternalService` only calls `GetAddressAsync()` once (to build init data) plus the two signing methods.

## Decision Guide

| Scenario | Approach |
|----------|----------|
| Signing key must live in a cloud KMS | `AWSKeyManagementExternalSigner` or `AzureKeyVaultExternalSigner` |
| Signing key must live on a physical device | `LedgerExternalSigner` / `TrezorExternalSigner` |
| Custom custody backend (MPC network, internal HSM API) | Implement `IEthExternalSigner` yourself |
| Device-native biometric passkey instead of ECDSA | Use the `webauthn-passkeys` skill instead — different signature scheme |
| Upgrading an existing EOA in place (EIP-7702) | Needs a raw `EthECKey` — external signers can't drive this tier |

For full documentation, see: https://docs.nethereum.com/docs/account-abstraction/guide-external-signer
