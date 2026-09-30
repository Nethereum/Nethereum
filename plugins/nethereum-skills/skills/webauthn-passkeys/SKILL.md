---
name: webauthn-passkeys
description: "Help users own an ERC-4337 smart account with a WebAuthn passkey instead of a private key — passkey login, FIDO2, P-256/secp256r1 credentials, Windows Hello, Touch ID/Face ID, security keys, phishing-resistant authentication, and biometric wallet access using Nethereum.AccountAbstraction.WebAuthn and Nethereum.WebAuthn. Use whenever the user mentions passkey, WebAuthn, FIDO2, biometric login, Windows Hello, Touch ID, Face ID, security key, 'no private key' / 'no seed phrase' wallets, or wanting a smart account a device's secure enclave controls, in .NET/C#."
user-invocable: true
---

# WebAuthn Passkeys: Own a Smart Account with No Private Key

A [WebAuthn](https://www.w3.org/TR/webauthn-2/) passkey is a P-256 (secp256r1) key pair generated and held inside a device's secure enclave — a phone, a security key, Windows Hello — that never exports its private key. There is no seed phrase to write down and no key file to leak; every signature requires the physical device (usually plus a biometric or PIN), which is what makes passkeys phishing-resistant in a way a copy-pasteable private key never can be.

Use this when the user wants an account owned by a passkey instead of an ECDSA key — e.g. "let the user log in with Face ID and control their wallet," or "no seed phrase, no private key management." It builds on the same generic on-ramp every AA signer uses (see the `account-abstraction`/`modular-accounts` skills): a passkey-owned account is just another `IAccountSigningService` + `IErc7579ValidatorModule` pair through `Nethereum.AccountAbstraction.WebAuthn`'s `WebAuthnValidatorModule` — an [ERC-7579](../modular-accounts/SKILL.md) validator for the Rhinestone `WebAuthnValidator` contract — paired with the platform-agnostic P-256 primitives in `Nethereum.WebAuthn`.

## When to Use This

- User wants to **own a smart account with a passkey / FIDO2 credential** instead of a private key
- User wants **"no seed phrase" / "no private key" onboarding**
- User mentions **Windows Hello, Touch ID, Face ID, or a security key** controlling a wallet
- User wants **phishing-resistant** account authentication
- User is adding a passkey as a **second factor** alongside an existing ECDSA-owned account

## Packages

```bash
dotnet add package Nethereum.Web3
dotnet add package Nethereum.AccountAbstraction
dotnet add package Nethereum.AccountAbstraction.WebAuthn
```

For a real device instead of the software test authenticator, add whichever platform package matches the app:

```bash
dotnet add package Nethereum.WebAuthn.Blazor    # browser passkeys (navigator.credentials) for Blazor
dotnet add package Nethereum.WebAuthn.Windows   # Windows Hello via webauthn.dll, for desktop apps
```

You also need `IAAClient` registered (the standard `AddNethereumAccountAbstraction` setup used by every AA skill) and the Rhinestone `WebAuthnValidator` (core-modules) contract deployed on the target chain.

## The Simple Way

```csharp
using Nethereum.AccountAbstraction;
using Nethereum.AccountAbstraction.Client;
using Nethereum.AccountAbstraction.WebAuthn.Client;
using Nethereum.WebAuthn;

// 1. Register the passkey. SoftwareWebAuthnAuthenticator stands in for a real device here; swap it
//    for BlazorWebAuthnAuthenticator or WindowsWebAuthnAuthenticator and nothing else changes.
var authenticator = new SoftwareWebAuthnAuthenticator(requireUV: false);
var credential = await authenticator.CreateCredentialAsync(new WebAuthnCredentialCreationOptions
{
    RpId = "nethereum.local",
    RpName = "Nethereum AA Demo",
    UserName = "alice"
});

// 2. Create the passkey-owned account through the WebAuthn on-ramp — one usePrecompile flag drives
//    both the validator and the signing service, so they can never disagree.
var account = await aaClient.CreateWebAuthnAccountAsync(
    credential, authenticator, webAuthnValidatorAddress, rpId: "nethereum.local");

// 3. Drive any generated contract service through it exactly like an ECDSA-owned account.
myToken.UseAccountAbstraction(account, aaClient);
var receipt = (AATransactionReceipt)await myToken.TransferRequestAndWaitForReceiptAsync(recipient, amount);
Console.WriteLine($"UserOp success: {receipt.UserOpSuccess}, sender: {receipt.Sender}");
```

That's it — no ECDSA key anywhere. `CreateWebAuthnAccountAsync` derives the counterfactual CREATE2 address, builds the validator's init data, and wires the `IEthSignTypedDataV4` that turns every UserOperation hash into a WebAuthn assertion. Deployment, gas estimation, and sending all go through the same `AAContractHandler` path as any other signer.

If the account is already deployed, attach to it instead — this skips deriving init data:

```csharp
var account = aaClient.GetWebAuthnAccount(
    existingAccountAddress, credential, authenticator, webAuthnValidatorAddress, rpId: "nethereum.local");
```

## Mental Model: Two Ids, One Credential

Registering a passkey (`IWebAuthnCredentialFactory.CreateCredentialAsync`) hands back a `WebAuthnCreatedCredential` carrying **two distinct ids**:

- **`PlatformCredentialId`** — the authenticator's own opaque handle, passed back to `IWebAuthnAuthenticator.GetAssertionAsync` so the OS/browser picks the right passkey to sign with.
- **`OnChainCredentialId`** — `keccak256(abi.encode(pubKeyX, pubKeyY))` (`WebAuthnCreatedCredential.OnChainCredentialId`), the id the on-chain `WebAuthnValidator` stores and looks up.

`CreateWebAuthnAccountAsync`/`GetWebAuthnAccount` thread both ids automatically — you don't need to reason about the split day-to-day; it matters mainly for a real platform authenticator, where the handle genuinely selects which stored passkey signs.

Signing flows through `WebAuthnAccountSigningService`, the `IAccountSigningService` for passkeys (alongside `AccountSigningOfflineService` for raw keys and `AccountSigningExternalService` for external signers — see the `external-signer` skill). Its `SignTypedDataV4` turns the UserOperation's EIP-712 hash into the WebAuthn `challenge`, gets an assertion from the authenticator, and ABI-encodes the result into the `WebAuthnValidator` signature format. **`PersonalSign` throws `NotSupportedException`** — WebAuthn has no equivalent of signing a raw digest outside a full assertion ceremony; only `SignTypedDataV4` (what UserOperation signing uses) is supported.

## Real Devices: Platform Authenticators

`SoftwareWebAuthnAuthenticator` is what the examples above use — it produces genuinely valid WebAuthn-shaped assertions with no browser or OS credential store, which is why it's usable in a headless process. For a real application, swap it for a platform authenticator; both implement the identical `IWebAuthnCredentialFactory`/`IWebAuthnAuthenticator` interfaces so nothing else in the flow changes:

| Authenticator | Package | Device |
|---|---|---|
| `SoftwareWebAuthnAuthenticator` | `Nethereum.WebAuthn` | None — in-process P-256, for tests/servers |
| `BlazorWebAuthnAuthenticator` | `Nethereum.WebAuthn.Blazor` | Browser passkeys via `navigator.credentials` |
| `WindowsWebAuthnAuthenticator` | `Nethereum.WebAuthn.Windows` | Windows Hello via `webauthn.dll` |

## Decision: Precompile vs. Fallback Verification

`WebAuthnAccountSigningService` and `WebAuthnValidatorModule` both take a matching `usePrecompile` flag that selects how the on-chain `WebAuthnValidator` verifies the P-256 signature:

| | `usePrecompile: true` | `usePrecompile: false` (default) |
|---|---|---|
| Verification path | [EIP-7951](https://eips.ethereum.org/EIPS/eip-7951) `P256VERIFY` precompile at `0x100` | Pure-Solidity FreshCryptoLib fallback |
| Chain requirement | Osaka hardfork or later | Any chain, including a devchain not configured for Osaka |
| Gas | Cheap | Materially more expensive |
| Verification-gas buffer | 15,000 (`WebAuthnValidatorModule.GetVerificationGasBuffer()`) | 400,000 |

**Default to `usePrecompile: false`** unless the target chain is known to have activated EIP-7951 — there is no fallback if the precompile is missing; `WebAuthn.verify` returns false outright rather than retrying against FreshCryptoLib. Either way, never compute the verification-gas buffer yourself — `WebAuthnValidatorModule.GetVerificationGasBuffer()` adds it automatically on top of the bundler's stub-signature estimate, because the estimation stub's challenge can never match the real UserOp hash.

## Passkey as a Second Validator Alongside ECDSA

A passkey can be installed as a **second** validator on an account that already has an ECDSA validator — the "add a passkey to an existing wallet" flow — signed by the account's existing validator via a self-call:

```csharp
using Nethereum.AccountAbstraction;
using Nethereum.AccountAbstraction.Client;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccount;
using Nethereum.AccountAbstraction.WebAuthn.Client;
using Nethereum.AccountAbstraction.WebAuthn.ERC7579.Modules;
using Nethereum.WebAuthn;

// The account already exists, owned by an ECDSA validator.
var accountService = new NethereumAccountService(web3, account.Address);
accountService.UseAccountAbstraction(account, aaClient);

// Register the passkey and install it as a second validator, signed by the EXISTING ECDSA validator.
var authenticator = new SoftwareWebAuthnAuthenticator(requireUV: false, origin: "https://nethereum.local");
var credential = await authenticator.CreateCredentialAsync(
    new WebAuthnCredentialCreationOptions { RequireUserVerification = false });
var moduleConfig = new WebAuthnValidatorConfig(webAuthnValidatorAddress, threshold: 1, credential.ToCredential());

var installReceipt = await accountService.InstallModuleAndWaitForReceiptAsync(moduleConfig);

// From here, the SAME address can be driven by a passkey-signed NethereumSmartAccount.
var passkeyAccount = aaClient.GetWebAuthnAccount(
    account.Address, credential, authenticator, webAuthnValidatorAddress, rpId: "nethereum.local");

aaClient.Configure(myToken, passkeyAccount);
var receipt = (AATransactionReceipt)await myToken.CountRequestAndWaitForReceiptAsync();
```

## Common Mistakes

- **`usePrecompile` mismatched between signer and validator** — it's baked into the signature by `WebAuthnAccountSigningService` and into the gas buffer by `WebAuthnValidatorModule` independently. Always build both through `CreateWebAuthnAccountAsync`/`GetWebAuthnAccount` (a single flag drives both) unless you have a specific reason to construct them by hand.
- **`usePrecompile: true` on a pre-Osaka chain** — there is no fallback retry; the call fails outright.
- **Calling `PersonalSign`** on a WebAuthn-owned account — throws `NotSupportedException`. Only `SignTypedDataV4` works.
- **Mixing with EIP-7702** — a WebAuthn-owned account has no `IEip7702AuthSigner` and cannot authorize a 7702 delegation; use the counterfactual CREATE2 tier shown here instead.

## Decision Guide

| Scenario | Approach |
|----------|----------|
| Wallet with no seed phrase, device-native biometric login | This skill: `CreateWebAuthnAccountAsync` |
| Key must live in a cloud KMS/HSM/hardware wallet | See the `external-signer` skill instead |
| Add passkey as a backup alongside an existing key | "Passkey as a Second Validator" above |
| Devchain or unconfirmed hardfork | `usePrecompile: false` (default) |
| Confirmed Osaka+ chain, want cheaper verification gas | `usePrecompile: true` |

For full documentation, see: https://docs.nethereum.com/docs/account-abstraction/guide-webauthn-passkeys
