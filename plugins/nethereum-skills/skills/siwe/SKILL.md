---
name: siwe
description: Help users implement Sign-In with Ethereum (EIP-4361) authentication using Nethereum (.NET). Use this skill whenever the user mentions SIWE, sign-in with Ethereum, wallet authentication, EIP-4361, wallet login, crypto authentication, NFT-gated access, RECAP capabilities, or EIP-5573. Also use when building Blazor or ASP.NET apps that need Ethereum-based authentication.
user-invocable: true
---

# SIWE Authentication with Nethereum

## When to Use

- User wants to authenticate users via their Ethereum wallet
- Building a dApp that needs sign-in functionality
- Implementing NFT-gated or token-gated access
- Need to verify a user controls a specific Ethereum address
- Building REST API authentication with Ethereum wallets
- Need fine-grained capability authorization (RECAP/EIP-5573)

## Required Packages

```bash
dotnet add package Nethereum.Siwe
```

This includes `Nethereum.Siwe.Core` (message model, parser) automatically.

## Basic SIWE Flow

```csharp
using Nethereum.Siwe;
using Nethereum.Siwe.Core;

// 1. Create the service
var siweService = new SiweMessageService();

// 2. Build the message
var message = new SiweMessage
{
    Domain = "myapp.com",
    Address = userAddress.ConvertToEthereumChecksumAddress(),
    Statement = "Sign in to MyApp",
    Uri = "https://myapp.com",
    Version = "1",
    ChainId = "1"
};
message.SetIssuedAtNow();
message.SetExpirationTime(DateTime.UtcNow.AddMinutes(30));

// 3. Get the text for the wallet to sign (nonce auto-generated)
string messageToSign = siweService.BuildMessageToSign(message);

// 4. After user signs, verify
var parsed = SiweMessageParser.Parse(messageToSign);
bool valid = await siweService.IsValidMessage(parsed, signature);
// Checks: signature + dates + session nonce + user registration
```

## NFT-Gated Access

```csharp
var nftService = new ERC721BalanceEthereumUserService(
    nftContractAddress, rpcUrl);

var siweService = new SiweMessageService(
    new InMemorySessionNonceStorage(),
    nftService);

// IsValidMessage now also checks NFT ownership
bool valid = await siweService.IsValidMessage(parsed, signature);
```

## Smart Contract Wallet Support (ERC-1271)

```csharp
var siweService = new SiweMessageService(
    new InMemorySessionNonceStorage(),
    ethereumUserService: null,
    web3ForERC1271Validation: web3);

// Falls back to ERC-1271/ERC-6492 for smart contract wallets
bool valid = await siweService.IsMessageSignatureValid(parsed, signature);
```

## RECAP Capabilities (EIP-5573)

```csharp
using Nethereum.Siwe.Core.Recap;

var recapMessage = SiweRecapMsgBuilder.Init(message)
    .AddDefaultActions(new SiweNamespace("eip155"), new HashSet<string> { "sign" })
    .AddTargetActions(new SiweNamespace("https"), "https://api.myapp.com",
        new HashSet<string> { "read", "write" })
    .Build();

// Check permissions after verification
bool canWrite = recapMessage.HasPermissions(
    new SiweNamespace("https"), "https://api.myapp.com", "write");
```

## Custom Session Storage

```csharp
public class RedisSessionStorage : ISessionStorage
{
    public void AddOrUpdate(SiweMessage msg) { /* store by nonce */ }
    public SiweMessage GetSiweMessage(SiweMessage msg) { /* lookup by nonce */ }
    public void Remove(SiweMessage msg) { /* delete */ }
    public void Remove(string nonce) { /* delete */ }
}

var siweService = new SiweMessageService(new RedisSessionStorage());
```

## Custom User Validation

```csharp
public class MyUserService : IEthereumUserService
{
    public Task<bool> IsUserAddressRegistered(string address)
    {
        // Check database, allowlist, token balance, etc.
        return Task.FromResult(true);
    }
}
```

## REST API Authentication (.NET 5+)

```csharp
using Nethereum.Siwe.Authentication;

var loginService = new SiweApiUserLoginService<User>(httpClient);
string messageToSign = await loginService.GenerateNewSiweMessage(address);
var response = await loginService.Authenticate(parsedMessage, signature);
string jwt = response.Jwt;
var user = await loginService.GetUser(jwt);
await loginService.Logout(jwt);
```

## Key Decision Points

| Scenario | Approach |
|---|---|
| Simple EOA sign-in | `new SiweMessageService()` — default, no extras needed |
| NFT/token gating | Pass `ERC721BalanceEthereumUserService` or custom `IEthereumUserService` |
| Smart contract wallets | Pass `web3ForERC1271Validation` to constructor |
| Distributed servers | Implement `ISessionStorage` with Redis/DB |
| Fine-grained permissions | Use `SiweRecapMsgBuilder` for RECAP capabilities |
| Blazor Server auth | Use `NethereumSiweAuthenticatorService` from `Nethereum.UI` |
| Blazor WASM + API | Use `SiweApiUserLoginService<User>` with JWT |

## Important Notes

- Always use UTC for `SetExpirationTime` and `SetNotBefore`
- `BuildMessageToSign` generates a fresh nonce each call — don't reuse
- `InMemorySessionNonceStorage` doesn't survive restarts — use persistent storage in production
- Set `ChainId` to match the user's connected chain

For full documentation, see: https://docs.nethereum.com/docs/protocols/guide-siwe
