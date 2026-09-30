# Nethereum.WebAuthn

**Nethereum.WebAuthn** is a platform-agnostic library for signing ERC-4337 UserOperations with a
WebAuthn (passkey / P-256) credential, producing signatures that Rhinestone's on-chain
`WebAuthnValidator` (ERC-7579) accepts. The core library has no browser, OS credential store, or
`fido2-net-lib` dependency - it defines the seams a platform authenticator implements, plus a
software authenticator that satisfies them without any real hardware.

This package covers the passkey primitives only. To own an Account Abstraction smart account with
one, see [Nethereum.AccountAbstraction.WebAuthn](../Nethereum.AccountAbstraction.WebAuthn/README.md)
for the validator module and the end-to-end create/send flow.

## The two seams

Everything platform-specific sits behind two interfaces. A software authenticator, a browser, and
Windows Hello all implement the same two contracts:

```csharp
public interface IWebAuthnCredentialFactory
{
    // The registration ceremony (navigator.credentials.create / WebAuthNAuthenticatorMakeCredential):
    // mints a new P-256 passkey and returns its public key + handle.
    Task<WebAuthnCreatedCredential> CreateCredentialAsync(WebAuthnCredentialCreationOptions options);
}

public interface IWebAuthnAuthenticator
{
    // The assertion ceremony (navigator.credentials.get / WebAuthNAuthenticatorGetAssertion):
    // signs a challenge with an existing passkey.
    Task<WebAuthnAssertion> GetAssertionAsync(byte[] challenge, byte[] credentialId, string rpId);
}
```

| Implementation | Package | What it talks to |
|---|---|---|
| `SoftwareWebAuthnAuthenticator` | this package | an in-process P-256 key - no OS/browser involved |
| `BlazorWebAuthnAuthenticator` | [Nethereum.WebAuthn.Blazor](../Nethereum.WebAuthn.Blazor/README.md) | the browser's `navigator.credentials`, via JS interop |
| `WindowsWebAuthnAuthenticator` | [Nethereum.WebAuthn.Windows](../Nethereum.WebAuthn.Windows/README.md) | Windows Hello, via `webauthn.dll` |

Swapping between them is a one-line DI change (`AddNethereumWebAuthnBlazor()` vs.
`AddNethereumWebAuthnWindows()`) - every consumer of `IWebAuthnCredentialFactory` /
`IWebAuthnAuthenticator` is unaffected.

## The two-id model

A created credential carries two distinct identifiers, and mixing them up is the single most common
mistake when wiring a passkey account together:

```csharp
public class WebAuthnCreatedCredential
{
    public byte[] PlatformCredentialId { get; set; }   // the authenticator's own opaque handle
    public BigInteger PubKeyX { get; set; }
    public BigInteger PubKeyY { get; set; }
    public bool RequireUserVerification { get; set; }

    // keccak256(abi.encode(pubKeyX, pubKeyY)) - derived, not stored by the authenticator
    public byte[] OnChainCredentialId { get; }
}
```

- **`PlatformCredentialId`** is the authenticator's own handle for the passkey (a browser credential
  ID, a Windows Hello credential ID, ...). Pass it back into `GetAssertionAsync` so the OS/browser
  knows *which* passkey to sign with.
- **`OnChainCredentialId`** is `keccak256(abi.encode(pubKeyX, pubKeyY))` - the id the on-chain
  `WebAuthnValidator` stores and looks up, and the id encoded into every UserOperation signature.

`WebAuthnAccountSigningService` needs both: the on-chain id to encode into the signature, and the
platform id to select the passkey when asking the authenticator to sign.

## Signing: `WebAuthnAccountSigningService`

```csharp
public WebAuthnAccountSigningService(
    IWebAuthnAuthenticator authenticator,
    byte[] credentialId,           // the ON-CHAIN id: created.OnChainCredentialId
    string rpId,
    bool usePrecompile = false,    // must match the validator module - see the AA.WebAuthn README
    byte[] platformCredentialId = null)   // defaults to credentialId if omitted
```

It is a third `IAccountSigningService`, the same seam `AccountSigningOfflineService` (an offline
`EthECKey`) and `AccountSigningExternalService` (KMS/HSM/MPC) implement. Its `SignTypedDataV4` takes
the EIP-712 hash of the UserOperation, keccak-hashes it into a WebAuthn `challenge`, asks the
authenticator for an assertion, and ABI-encodes the result into the `WebAuthnValidator`'s expected
signature shape - not a 65-byte ECDSA signature. `PersonalSign` throws `NotSupportedException`:
WebAuthn has no equivalent of a bare-digest `personal_sign`.

```csharp
using Nethereum.Accounts.AccountAbstraction;
using Nethereum.WebAuthn;

// SoftwareWebAuthnAuthenticator satisfies both seams - handy for tests/headless code with no real
// passkey hardware. Swap it for BlazorWebAuthnAuthenticator or WindowsWebAuthnAuthenticator to use
// a real one; nothing else below changes.
var authenticator = new SoftwareWebAuthnAuthenticator(requireUV: false, origin: "https://nethereum.local");

var created = await authenticator.CreateCredentialAsync(new WebAuthnCredentialCreationOptions
{
    RpId = "nethereum.local",
    RpName = "My Nethereum App",
    UserName = "alice",
    RequireUserVerification = false
});

var signingService = new WebAuthnAccountSigningService(
    authenticator, created.OnChainCredentialId, rpId: "nethereum.local",
    platformCredentialId: created.PlatformCredentialId);

// Any IAccountSigningService-backed account can now use this signer, e.g. a bare read-only account:
var account = new AccountAbstractionAccount(smartAccountAddress, signingService);
```

For a real Account Abstraction smart account - a validator module, install data, and an end-to-end
create + send through `IAAClient` - see
[Nethereum.AccountAbstraction.WebAuthn](../Nethereum.AccountAbstraction.WebAuthn/README.md).

## Parsing platform responses: `WebAuthnResponseParser`

A real authenticator hands back DER signatures, SPKI/COSE public keys, and raw `authenticatorData`
buffers. `WebAuthnResponseParser` decodes all of them into the shapes `WebAuthnAssertion` and
`WebAuthnCreatedCredential` already use (fixed 32-byte big-endian `(r, s)` / `(x, y)`, low-S
normalized), so a platform adapter (Blazor, Windows) never re-implements this parsing itself:

- `DecodeDerEcdsaSignatureToLowS(byte[] der)` - DER `ECDSA-Sig-Value` -> `(r, s)`
- `DecodeP256PublicKeyFromSpki(byte[] spki)` / `DecodeP256PublicKeyFromCose(byte[] coseKey)` - public key -> `(x, y)`
- `ExtractCosePublicKeyFromAuthenticatorData` / `ExtractCredentialId` - pull fields out of a
  registration's `authenticatorData`
- `ExtractAuthDataFromAttestationObject` - unwrap a CBOR `attestationObject` down to its `authData`

`WebAuthnClientData.BuildGet` / `BuildCreate` build the `clientDataJSON` string every authenticator in
this module constructs identically - the on-chain validator hashes this exact byte sequence, so field
order and quoting must stay stable across every authenticator that builds it itself.

## Wire types: `WebAuthnAssertion` and `WebAuthnCredential`

```csharp
public class WebAuthnAssertion
{
    public byte[] CredentialId { get; set; }
    public byte[] AuthenticatorData { get; set; }
    public string ClientDataJSON { get; set; }
    public byte[] R { get; set; }
    public byte[] S { get; set; }
}
```

`WebAuthnAssertion` is what `IWebAuthnAuthenticator.GetAssertionAsync` returns: the raw materials
`WebAuthnValidatorFormat.EncodeUserOpSignature` (below) needs to build the on-chain signature - the
authenticator data, the `clientDataJSON` the signature was made over, and the low-S-normalized
`(R, S)` ECDSA signature.

```csharp
public class WebAuthnCredential
{
    public BigInteger PubKeyX { get; set; }
    public BigInteger PubKeyY { get; set; }
    public bool RequireUV { get; set; }

    public WebAuthnCredential() { }
    public WebAuthnCredential(BigInteger pubKeyX, BigInteger pubKeyY, bool requireUV);
}
```

`WebAuthnCredential` is the already-registered, on-chain-shaped counterpart of `WebAuthnCreatedCredential`
above - it drops `PlatformCredentialId` (nothing off-chain needs it once a credential is installed) and
is what `WebAuthnValidatorFormat.EncodeInstallData` takes a list of.

## On-chain wire format: `WebAuthnValidatorFormat`

```csharp
public static class WebAuthnValidatorFormat
{
    public static byte[] GenerateCredentialId(BigInteger pubKeyX, BigInteger pubKeyY);
    public static byte[] EncodeInstallData(BigInteger threshold, IReadOnlyList<WebAuthnCredential> credentials);
    public static byte[] EncodeUserOpSignature(IReadOnlyList<byte[]> credentialIds, bool usePrecompile, IReadOnlyList<WebAuthnAssertion> assertions);
    public static byte[] EncodeUserOpSignature(byte[] credentialId, bool usePrecompile, WebAuthnAssertion assertion);
}
```

This is the exact ABI wire format Rhinestone's `WebAuthnValidator` (core-modules) expects, mirroring
`WebAuthnValidator.sol`'s `generateCredentialId`, `onInstall` and `_validateSignatureWithConfig`
byte-for-byte: `GenerateCredentialId` is `keccak256(abi.encode(pubKeyX, pubKeyY))` (the same value
exposed as `WebAuthnCreatedCredential.OnChainCredentialId`); `EncodeInstallData` ABI-encodes
`(threshold, WebAuthnCredential[])` for `onInstall`, sorting the credentials ascending by their
generated credential id since the validator reverts with `NotSorted` otherwise; and
`EncodeUserOpSignature` ABI-encodes `(bytes32[] credentialIds, bool usePrecompile, WebAuthnAuth[] auth)`
for the userOp/ERC-1271 signature, deriving each assertion's `challengeIndex`/`typeIndex` from its
`clientDataJSON`. `credentialIds` and `assertions` must already be in the same ascending-by-credential-id
order; the single-credential overload is the common case.

## `Base64UrlEncoder`

```csharp
public static class Base64UrlEncoder
{
    public static string Encode(byte[] data);
    public static byte[] Decode(string base64Url);
}
```

RFC 4648 §5 base64url, unpadded, as used throughout the WebAuthn spec (`clientDataJSON.challenge`,
credential ids, COSE keys).

## `WebAuthnSignTypedDataV4` and `WebAuthnPersonalSign`

`WebAuthnAccountSigningService`'s two properties are backed by two small adapter classes, usable
directly wherever an `IEthSignTypedDataV4`/`IEthPersonalSign` is needed on its own:

```csharp
public class WebAuthnSignTypedDataV4 : IEthSignTypedDataV4
{
    public WebAuthnSignTypedDataV4(IWebAuthnAuthenticator authenticator, byte[] credentialId, string rpId, bool usePrecompile = false, byte[] platformCredentialId = null);
}

public class WebAuthnPersonalSign : IEthPersonalSign
{
    // Both overloads throw NotSupportedException: WebAuthn has no equivalent of EIP-191
    // personal_sign over an arbitrary raw digest.
}
```

`WebAuthnSignTypedDataV4.SendRequestAsync` keccak-hashes the EIP-712 pre-image
(`Eip712TypedDataSigner.EncodeTypedData`) into the WebAuthn `challenge`, asks the authenticator for an
assertion using `platformCredentialId` (defaulting to `credentialId` when omitted), and returns
`WebAuthnValidatorFormat.EncodeUserOpSignature(credentialId, usePrecompile, assertion)` as a hex
string.

## `SoftwareWebAuthnAuthenticator`'s own members

Beyond the two seams it implements, `SoftwareWebAuthnAuthenticator` exposes:

```csharp
public class SoftwareWebAuthnAuthenticator : IWebAuthnAuthenticator, IWebAuthnCredentialFactory
{
    public byte[] CredentialId { get; }

    public SoftwareWebAuthnAuthenticator(byte[] credentialId = null, bool requireUV = false, string origin = "https://nethereum.local");

    public (BigInteger X, BigInteger Y) GetPublicKey();
}
```

`CredentialId` is the platform credential id this authenticator was constructed with (or a random
`Guid`-derived one, if none was passed); `GetPublicKey()` exports the in-process P-256 key's public
point directly, without going through `CreateCredentialAsync`.

## Installation

```bash
dotnet add package Nethereum.WebAuthn
```

## Related Packages

- **[Nethereum.AccountAbstraction.WebAuthn](../Nethereum.AccountAbstraction.WebAuthn/README.md)** - the ERC-7579 validator module + end-to-end account create/send flow
- **[Nethereum.WebAuthn.Blazor](../Nethereum.WebAuthn.Blazor/README.md)** - browser passkeys via `navigator.credentials`
- **[Nethereum.WebAuthn.Windows](../Nethereum.WebAuthn.Windows/README.md)** - Windows Hello passkeys via `webauthn.dll`
- **[Nethereum.AccountAbstraction](../Nethereum.AccountAbstraction/README.md)** - core ERC-4337 framework

## Additional Resources

- [WebAuthn Level 2 (W3C)](https://www.w3.org/TR/webauthn-2/)
- [ERC-4337: Account Abstraction](https://eips.ethereum.org/EIPS/eip-4337)
- [Nethereum Documentation](https://docs.nethereum.com)
