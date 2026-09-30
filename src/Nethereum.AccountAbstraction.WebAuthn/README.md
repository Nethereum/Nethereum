# Nethereum.AccountAbstraction.WebAuthn

ERC-7579 WebAuthn (passkey / P-256) validator module integration for Nethereum Account Abstraction -
own an ERC-4337 smart account with a passkey instead of a private key.

## Overview

This package plugs a WebAuthn/passkey signer into the core
[Nethereum.AccountAbstraction](../Nethereum.AccountAbstraction/README.md) module through the
`IErc7579ValidatorModule` seam: `WebAuthnValidatorModule` supplies the validator-address signature
prefixing and the bundler gas-estimation stub for the Rhinestone `WebAuthnValidator` (core-modules)
on-chain module, exactly like the built-in ECDSA validator module does for secp256k1.

The core AA module has no dependency on WebAuthn/P-256 - it only knows `IErc7579ValidatorModule` and
`IAccountSigningService`. This package is the separate, optional integration that adds WebAuthn on
top of the platform-agnostic P-256 primitives in [Nethereum.WebAuthn](../Nethereum.WebAuthn/README.md).
A passkey-owned account is created and driven through the exact same generic on-ramp
(`IAAClient.CreateAccountAsync(signingService, validator, initData)`) as any other non-ECDSA signer -
hardware, KMS/HSM, MPC.

### Key Features

- **`WebAuthnValidatorModule`** - `IErc7579ValidatorModule` implementation for the on-chain Rhinestone
  `WebAuthnValidator`
- **`WebAuthnValidatorConfig`** - `IModuleConfig` for installing WebAuthn credentials on a modular
  NethereumAccount
- **`WebAuthnAccountInitDataBuilder.BuildWebAuthn`** - builds the `validator ‖ validatorInitData` the
  NethereumAccountFactory expects for a WebAuthn-owned account
- **Generated `WebAuthnValidatorService`** - typed contract service for the vendored Rhinestone
  `WebAuthnValidator.sol`

## Installation

```bash
dotnet add package Nethereum.AccountAbstraction.WebAuthn
```

### Dependencies

- **Nethereum.AccountAbstraction** - core ERC-4337/ERC-7579 types (`IErc7579ValidatorModule`,
  `AccountInitDataBuilder`, `IAAClient`)
- **Nethereum.WebAuthn** - platform-agnostic P-256/WebAuthn signing primitives
  (`WebAuthnAccountSigningService`, `WebAuthnValidatorFormat`, `SoftwareWebAuthnAuthenticator`)

## On-chain verification: precompile vs. fallback

The Rhinestone `WebAuthnValidator` supports two P-256 verification paths on-chain, and both
`WebAuthnAccountSigningService` and `WebAuthnValidatorModule` take a matching `usePrecompile` flag
that must agree with each other and with how the validator is deployed:

- **`usePrecompile: true`** - verifies via the EIP-7951 `P256VERIFY` precompile at address `0x100`,
  active from the Osaka hardfork. Cheap, but only usable on a chain that has activated it.
- **`usePrecompile: false`** (the default) - verifies via the pure-Solidity FreshCryptoLib fallback
  the same validator contract also implements. Works everywhere, at a higher gas cost. Use this on a
  devchain or any chain not explicitly configured for Osaka.

`WebAuthnValidatorModule.GetVerificationGasBuffer()` adds a matching gas buffer on top of the bundler's
stub-signature estimate (since the estimation stub short-circuits before the SHA-256 hashing and the
real P-256 verification run) - 15,000 gas for the precompile path, 400,000 for the FreshCryptoLib
fallback (the pure-Solidity verification is far costlier than the precompile).

## Quick Start: create a passkey-owned account and send through it

This is the same flow the Account Abstraction example app's passkey tab uses
(`PasskeyAccountViewModel` in `Nethereum.AccountAbstraction.Example.Core`), with a
`SoftwareWebAuthnAuthenticator` standing in for a real platform passkey - swap it for
`BlazorWebAuthnAuthenticator` ([Nethereum.WebAuthn.Blazor](../Nethereum.WebAuthn.Blazor/README.md)) or
`WindowsWebAuthnAuthenticator` ([Nethereum.WebAuthn.Windows](../Nethereum.WebAuthn.Windows/README.md))
and nothing else below changes - both implement the same `IWebAuthnCredentialFactory` /
`IWebAuthnAuthenticator` seams.

```csharp
using Nethereum.AccountAbstraction;
using Nethereum.AccountAbstraction.Client;
using Nethereum.AccountAbstraction.WebAuthn.Client;
using Nethereum.WebAuthn;

// 1. Register the passkey. SoftwareWebAuthnAuthenticator implements both IWebAuthnCredentialFactory
//    and IWebAuthnAuthenticator (a real platform authenticator - Blazor/Windows - does too); a DI
//    composition root typically injects each interface separately from the same registered instance.
var authenticator = new SoftwareWebAuthnAuthenticator(requireUV: false);

var created = await authenticator.CreateCredentialAsync(new WebAuthnCredentialCreationOptions
{
    RpId = "nethereum.local",
    RpName = "Nethereum AA Demo",
    UserName = "alice",
    RequireUserVerification = false
});

// 2. Create the account through the WebAuthn counterpart of the generic on-ramp - it builds the
//    init data, the WebAuthnValidatorModule and the WebAuthnAccountSigningService from a SINGLE
//    usePrecompile flag, so the signature's on-chain verification path and the validator's gas
//    buffer can never disagree, and threads the two credential ids (on-chain vs. the authenticator's
//    platform handle - see Nethereum.WebAuthn's two-id model) for you. Counterfactual: deploys on
//    first op.
var account = await aaClient.CreateWebAuthnAccountAsync(
    created, authenticator, webAuthnValidatorAddress, rpId: "nethereum.local", usePrecompile: false);

// 3. Drive any generated contract service through it exactly like an ECDSA-owned account.
myToken.UseAccountAbstraction(account, aaClient);
var receipt = (AATransactionReceipt)await myToken.TransferRequestAndWaitForReceiptAsync(recipient, amount);
Console.WriteLine($"UserOp Hash: {receipt.UserOpHash}, Success: {receipt.UserOpSuccess}");
```

The raw constructors (`WebAuthnAccountInitDataBuilder.BuildWebAuthn`, `WebAuthnValidatorModule`,
`WebAuthnAccountSigningService`, then `IAAClient.CreateAccountAsync(signingService, validator,
initData)` directly) remain available for advanced control - e.g. building the validator and signing
service at different times - but only `CreateWebAuthnAccountAsync` guarantees their `usePrecompile`
stays paired.

`aaClient` here is the `IAAClient` from
[Nethereum.AccountAbstraction](../Nethereum.AccountAbstraction/README.md)'s
`AddNethereumAccountAbstraction` DI registration - see that package's README for wiring it up
(bundler URL, EntryPoint, factory addresses) and for the rest of the on-ramp
(`GetAccount`, `Configure`, batching, paymasters, gas config).

### Attaching to an already-deployed passkey account

Once deployed, attach the same way as any other modular account - no factory/initData needed. The
`GetWebAuthnAccount` counterpart keeps the same single-`usePrecompile`/two-id guarantee:

```csharp
var account = aaClient.GetWebAuthnAccount(
    existingAccountAddress, created, authenticator, webAuthnValidatorAddress, rpId: "nethereum.local", usePrecompile: false);

// or, with the raw constructors:
var account = aaClient.GetAccount(existingAccountAddress, signingService, validator);
```

## Related Packages

### Dependencies
- **[Nethereum.AccountAbstraction](../Nethereum.AccountAbstraction/README.md)** - core ERC-4337/ERC-7579 framework
- **[Nethereum.WebAuthn](../Nethereum.WebAuthn/README.md)** - platform-agnostic WebAuthn/P-256 signing, the two-id model, `SoftwareWebAuthnAuthenticator`

### Platform authenticators (either implements the seams this package consumes)
- **[Nethereum.WebAuthn.Blazor](../Nethereum.WebAuthn.Blazor/README.md)** - browser passkeys via `navigator.credentials`
- **[Nethereum.WebAuthn.Windows](../Nethereum.WebAuthn.Windows/README.md)** - Windows Hello passkeys via `webauthn.dll`

## Additional Resources

- [ERC-7579: Minimal Modular Smart Accounts](https://eips.ethereum.org/EIPS/eip-7579)
- [EIP-7951: Precompile for secp256r1 Curve Support](https://eips.ethereum.org/EIPS/eip-7951)
- [WebAuthn Level 2 (W3C)](https://www.w3.org/TR/webauthn-2/)
- [Nethereum Documentation](https://docs.nethereum.com)
