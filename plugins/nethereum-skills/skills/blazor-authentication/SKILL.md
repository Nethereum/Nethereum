---
name: blazor-authentication
description: Implement Sign-In with Ethereum (SIWE / EIP-4361) authentication in Blazor with JWT tokens, session management, and AuthorizeView (.NET/C#). Use this skill when the user asks about SIWE, Sign-In with Ethereum, Blazor authentication with Ethereum wallets, JWT with SIWE, EthereumAuthenticationStateProvider, or wallet-based login.
user-invocable: true
---

# SIWE Authentication in Blazor

Implement Sign-In with Ethereum (EIP-4361) authentication in Blazor. Supports Blazor Server (direct signing) and Blazor WASM (REST API + JWT).

NuGet: `Nethereum.Siwe`, `Nethereum.Blazor`

A complete starter template is available:

```bash
dotnet new install Nethereum.Templates.Pack
dotnet new nethereum-siwe
```

Template repo: https://github.com/Nethereum/Nethereum.Templates.Pack (templates/Nethereum.Templates.Siwe)

## SIWE Flow

1. Server creates a `SiweMessage` with a random nonce
2. Client signs the message with their wallet
3. Server verifies signature matches the claimed address
4. Server issues a JWT (WASM) or stores session (Server)
5. Blazor `<AuthorizeView>` checks claims

## Blazor Server (Simplest)

Everything runs in one process. No REST API needed.

### Setup (Program.cs)

```csharp
builder.Services.AddSingleton<SiweMessageService>();
builder.Services.AddSingleton<NethereumSiweAuthenticatorService>();
builder.Services.AddScoped<IAccessTokenService, ProtectedSessionStorageAccessTokenService>();
builder.Services.AddScoped<AuthenticationStateProvider, SiweAuthenticationServerStateProvider>();
```

### Authentication Flow

The `SiweAuthenticationServerStateProvider` orchestrates the full flow:

```csharp
public async Task AuthenticateAsync(string address = null)
{
    if (string.IsNullOrEmpty(address))
        address = await EthereumHostProvider.GetProviderSelectedAccountAsync();

    var siweMessage = new DefaultSiweMessage();
    siweMessage.Address = address.ConvertToEthereumChecksumAddress();
    siweMessage.SetExpirationTime(DateTime.UtcNow.AddMinutes(10));
    siweMessage.SetNotBefore(DateTime.UtcNow);

    var fullMessage = await nethereumSiweAuthenticatorService.AuthenticateAsync(siweMessage);
    await _accessTokenService.SetAccessTokenAsync(SiweMessageStringBuilder.BuildMessage(fullMessage));
    await MarkUserAsAuthenticated();
}
```

The `NethereumSiweAuthenticatorService` (from `Nethereum.UI`) handles:
- Building the message string with nonce
- Prompting the wallet to sign via `IEthereumHostProvider`
- Verifying the signature

## Blazor WASM + REST API

### REST API: Generate SIWE Message

```csharp
[AllowAnonymous]
[HttpPost("newsiwemessage")]
public IActionResult GenerateNewSiweMessage([FromBody] string address)
{
    var message = new DefaultSiweMessage();
    message.SetExpirationTime(DateTime.UtcNow.AddMinutes(10));
    message.SetNotBefore(DateTime.UtcNow);
    message.Address = address.ConvertToEthereumChecksumAddress();
    return Ok(_siweMessageService.BuildMessageToSign(message));
}
```

### REST API: Authenticate and Issue JWT

```csharp
[AllowAnonymous]
[HttpPost("authenticate")]
public async Task<IActionResult> Authenticate(AuthenticateRequest request)
{
    var siweMessage = SiweMessageParser.Parse(request.SiweEncodedMessage);

    if (!await _siweMessageService.IsUserAddressRegistered(siweMessage))
        return Unauthorized("Invalid User");

    if (!await _siweMessageService.IsMessageSignatureValid(siweMessage, request.Signature))
        return Unauthorized("Invalid Signature");

    if (!_siweMessageService.IsMessageTheSameAsSessionStored(siweMessage))
        return Unauthorized("Nonce mismatch");

    if (!_siweMessageService.HasMessageDateStartedAndNotExpired(siweMessage))
        return Unauthorized("Expired");

    var token = _siweJwtAuthorisationService.GenerateToken(siweMessage, request.Signature);
    return Ok(new AuthenticateResponse { Address = siweMessage.Address, Jwt = token });
}
```

### WASM Client: Sign and Authenticate

```csharp
public async Task AuthenticateAsync(string address)
{
    var siweMessage = await _siweUserLoginService.GenerateNewSiweMessage(address);
    var signedMessage = await EthereumHostProvider.SignMessageAsync(siweMessage);
    var response = await _siweUserLoginService.Authenticate(
        SiweMessageParser.Parse(siweMessage), signedMessage);

    if (response.Jwt != null)
    {
        await _accessTokenService.SetAccessTokenAsync(response.Jwt);
        await MarkUserAsAuthenticated();
    }
}
```

## Using AuthorizeView

Two roles are available:

| Role | Meaning |
|------|---------|
| `EthereumConnected` | Wallet is connected (from `EthereumAuthenticationStateProvider`) |
| `SiweAuthenticated` | SIWE signature verified (from `SiweAuthentication*StateProvider`) |

```razor
<AuthorizeView Roles="EthereumConnected">
    <Authorized>
        <p>Wallet connected: @context.User.Identity?.Name</p>
        <AuthorizeView Roles="SiweAuthenticated">
            <NotAuthorized>
                <button @onclick="LoginAsync">Sign In</button>
            </NotAuthorized>
        </AuthorizeView>
    </Authorized>
    <NotAuthorized>
        <button @onclick="ConnectWalletAsync">Connect Wallet</button>
    </NotAuthorized>
</AuthorizeView>

<AuthorizeView Roles="SiweAuthenticated">
    <Authorized>
        <p>Authenticated as @context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value</p>
        <MyProtectedComponent />
    </Authorized>
</AuthorizeView>
```

## NFT-Gated Access

Use `ERC721BalanceEthereumUserService` to require users hold a specific NFT:

```csharp
var userService = new ERC721BalanceEthereumUserService(web3, nftContractAddress);
var siweService = new SiweMessageService(
    new InMemorySessionNonceStorage(),
    userService);
```

`IsUserAddressRegistered` will check the user's NFT balance before allowing authentication.

## Smart Contract Wallets (ERC-1271 / ERC-6492)

Pass a `Web3` instance to `SiweMessageService` for smart contract wallet signature verification:

```csharp
var siweService = new SiweMessageService(
    new InMemorySessionNonceStorage(),
    ethereumUserService: null,
    web3ForERC1271Validation: web3);
```

`IsMessageSignatureValid` automatically falls back to ERC-1271 on-chain verification, and supports ERC-6492 for counterfactual (not-yet-deployed) smart wallets.

## SiweMessageService API

| Method | Purpose |
|--------|---------|
| `BuildMessageToSign(message)` | Build message string, assign nonce, store in session |
| `IsMessageSignatureValid(message, signature)` | Verify ECDSA or ERC-1271/ERC-6492 signature |
| `IsValidMessage(message, signature)` | Full validation: timing + session + signature |
| `IsUserAddressRegistered(message)` | Check user via `IEthereumUserService` |
| `HasMessageDateStartedAndNotExpired(message)` | Check NotBefore and ExpirationTime |
| `IsMessageTheSameAsSessionStored(message)` | Verify message matches stored nonce |
| `InvalidateSession(message)` | Remove session (logout) |

## Claims Structure

```csharp
new Claim(ClaimTypes.Name, userName);
new Claim(ClaimTypes.NameIdentifier, ethereumAddress);
new Claim(ClaimTypes.Role, "EthereumConnected");
new Claim(ClaimTypes.Role, "SiweAuthenticated");
```

For full documentation, see: https://docs.nethereum.com/docs/blazor-dapp-integration/guide-blazor-authentication
