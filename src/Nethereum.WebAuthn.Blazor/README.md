# Nethereum.WebAuthn.Blazor

**Nethereum.WebAuthn.Blazor** implements [Nethereum.WebAuthn](../Nethereum.WebAuthn/README.md)'s
`IWebAuthnAuthenticator` / `IWebAuthnCredentialFactory` over the browser's real passkey store
(`navigator.credentials`), so a Blazor Server or WebAssembly app gets OS-native passkeys - Windows
Hello, Touch ID, Android biometrics, security keys - with almost no app code.

## Installation

```bash
dotnet add package Nethereum.WebAuthn.Blazor
```

## Registering the authenticator

```csharp
// Program.cs
builder.Services.AddNethereumWebAuthnBlazor();
```

```csharp
public static IServiceCollection AddNethereumWebAuthnBlazor(
    this IServiceCollection services,
    bool requireUserVerificationForAssertion = true)
```

This registers a single scoped `BlazorWebAuthnAuthenticator` under both `IWebAuthnAuthenticator` and
`IWebAuthnCredentialFactory` - scoped so it shares one JS module instance per Blazor circuit (Server)
or app lifetime (WebAssembly), matching the lifetime of the injected `IJSRuntime` itself.
`requireUserVerificationForAssertion` controls the user-verification requirement passed to
`navigator.credentials.get` for every assertion (registration's requirement is set per-call, via
`WebAuthnCredentialCreationOptions.RequireUserVerification` - see below).

## Usage

Inject `IWebAuthnCredentialFactory` and `IWebAuthnAuthenticator` into a component or service - both
resolve to the same registered `BlazorWebAuthnAuthenticator`:

```csharp
using System.Threading.Tasks;
using Nethereum.WebAuthn;

public class PasskeyService
{
    private readonly IWebAuthnCredentialFactory _credentialFactory;
    private readonly IWebAuthnAuthenticator _authenticator;

    public PasskeyService(IWebAuthnCredentialFactory credentialFactory, IWebAuthnAuthenticator authenticator)
    {
        _credentialFactory = credentialFactory;
        _authenticator = authenticator;
    }

    public async Task<WebAuthnAccountSigningService> RegisterAsync()
    {
        var created = await _credentialFactory.CreateCredentialAsync(new WebAuthnCredentialCreationOptions
        {
            RpId = "nethereum.local",
            RpName = "My Nethereum App",
            UserName = "alice",
            RequireUserVerification = true
        });

        // credentialId is the ON-CHAIN id (keccak of the public key); platformCredentialId is the
        // browser's own credential handle used to select the passkey for the assertion - see
        // Nethereum.WebAuthn's two-id model.
        return new WebAuthnAccountSigningService(
            _authenticator, created.OnChainCredentialId, rpId: "nethereum.local",
            platformCredentialId: created.PlatformCredentialId);
    }
}
```

This is the browser-backed half of a passkey account. For the ERC-7579 validator module, the account
init data, and the full create + send flow through `IAAClient`, see
[Nethereum.AccountAbstraction.WebAuthn](../Nethereum.AccountAbstraction.WebAuthn/README.md)'s Quick
Start - it uses `SoftwareWebAuthnAuthenticator` for a runnable example, and swapping it for the
`BlazorWebAuthnAuthenticator` registered here is a one-line change; nothing else in that flow differs.

## RP-ID and origin scoping

`RpId` is the relying-party id the browser scopes the passkey to - it must be the page's own domain
(or a registrable parent of it) or `navigator.credentials` rejects the call outright. A passkey
registered under one RP ID cannot be asserted under another. Keep the `RpId` you pass to
`CreateCredentialAsync` and the `rpId` you pass to `WebAuthnAccountSigningService` identical, and
matching the on-chain validator's expectations for that account (the on-chain `WebAuthnValidator`
does not itself check the RP ID - the browser does, before the assertion is ever produced).

## Blazor Server vs. WebAssembly

`BlazorWebAuthnAuthenticator` works unmodified under either render mode - it talks to the browser
exclusively through `IJSRuntime` interop and never touches the DOM directly:

- **Blazor Server**: interop round-trips over the SignalR circuit. The scoped registration means one
  authenticator instance (and one imported JS module) per circuit.
- **WebAssembly**: interop is an in-process JS call. The scoped registration is effectively a
  singleton for the app's lifetime, since a WASM app has one "scope."

All byte[] payloads cross the JS interop boundary as base64/base64url strings, since Blazor does not
marshal `byte[]` uniformly across Server and WebAssembly render modes.

## Notes

- All parsing (DER signatures, SPKI/COSE public keys, authenticatorData) is delegated to
  `Nethereum.WebAuthn.WebAuthnResponseParser` - this package only bridges JS interop.
- The JS module lives at `wwwroot/nethereumWebAuthn.js` and is served from
  `_content/Nethereum.WebAuthn.Blazor/nethereumWebAuthn.js` (the standard Razor class library static
  web assets path) - no manual `<script>` reference is needed.
- The live browser passkey path (an actual OS prompt) cannot run in a headless test suite; it is
  exercised manually via the Blazor AA demo. The unit tests in
  `tests/Nethereum.WebAuthn.Blazor.UnitTests` instead verify the C# <-> JS marshalling against
  real BCL-generated P-256 keys/signatures fed through a fake `IJSRuntime`.

## Related Packages

- **[Nethereum.WebAuthn](../Nethereum.WebAuthn/README.md)** - the platform-agnostic seams, the two-id model, `SoftwareWebAuthnAuthenticator`
- **[Nethereum.AccountAbstraction.WebAuthn](../Nethereum.AccountAbstraction.WebAuthn/README.md)** - the ERC-7579 validator module + end-to-end account create/send flow
- **[Nethereum.WebAuthn.Windows](../Nethereum.WebAuthn.Windows/README.md)** - the desktop (Windows Hello) equivalent of this package

## Additional Resources

- [WebAuthn Level 2 (W3C)](https://www.w3.org/TR/webauthn-2/)
- [Nethereum Documentation](https://docs.nethereum.com)
