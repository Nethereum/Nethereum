# Nethereum.Siwe

Full Sign-In with Ethereum (EIP-4361) implementation with session management, signature verification, and pluggable user validation. Built on top of [Nethereum.Siwe.Core](../Nethereum.Siwe.Core/README.md).

## Key Components

| Class | Purpose |
|---|---|
| `SiweMessageService` | Orchestrates the full SIWE flow: message building with nonce, signature verification, session matching, expiry checks |
| `ISessionStorage` | Interface for storing SIWE sessions keyed by nonce |
| `InMemorySessionNonceStorage` | Default in-memory session store (uses `ConcurrentDictionary`) |
| `IEthereumUserService` | Pluggable user validation (e.g. check NFT balance, database lookup) |
| `ERC721BalanceEthereumUserService` | Validates users hold an ERC-721 token |
| `RandomNonceBuilder` | Generates cryptographically random nonces for SIWE messages |

## Quick Start

### Server-side SIWE authentication

```csharp
// 1. Create the service (in-memory sessions, no user validation)
var siweService = new SiweMessageService();

// 2. Build a message for the user to sign
var message = new SiweMessage
{
    Domain = "example.com",
    Address = userAddress.ConvertToEthereumChecksumAddress(),
    Statement = "Sign in to Example",
    Uri = "https://example.com",
    Version = "1",
    ChainId = "1"
};
message.SetExpirationTime(DateTime.UtcNow.AddMinutes(10));
message.SetNotBefore(DateTime.UtcNow);
string messageToSign = siweService.BuildMessageToSign(message);
// Send messageToSign to the client for signing

// 3. Verify the signed message
var parsed = SiweMessageParser.Parse(messageToSign);
bool valid = await siweService.IsValidMessage(parsed, signature);
```

### With NFT-gated access

```csharp
var userService = new ERC721BalanceEthereumUserService(nftContractAddress, rpcUrl);
var siweService = new SiweMessageService(
    new InMemorySessionNonceStorage(),
    userService);

// IsUserAddressRegistered checks the user holds the NFT
bool registered = await siweService.IsUserAddressRegistered(parsed);
```

### Smart contract wallet support (ERC-1271 / ERC-6492)

```csharp
var siweService = new SiweMessageService(
    new InMemorySessionNonceStorage(),
    ethereumUserService: null,
    web3ForERC1271Validation: web3);

// IsMessageSignatureValid automatically falls back to ERC-1271/ERC-6492
// for smart contract wallets
bool valid = await siweService.IsMessageSignatureValid(parsed, signature);
```

## SiweMessageService API Reference

### Constructor

```csharp
// Default: in-memory sessions, no user validation, EOA signatures only
var service = new SiweMessageService();

// Full: custom session storage, user validation, smart contract wallet support
var service = new SiweMessageService(
    sessionStorage: new InMemorySessionNonceStorage(),
    ethereumUserService: myUserService,         // optional
    web3ForERC1271Validation: web3);             // optional
```

### Methods

| Method | Description |
|---|---|
| `BuildMessageToSign(SiweMessage)` | Assigns a new nonce, stores the session, and returns the canonical string for signing |
| `IsValidMessage(SiweMessage, string)` | Full validation: signature + dates + session match + user registration |
| `IsMessageSignatureValid(SiweMessage, string)` | Validates signature only (EOA via `EthereumMessageSigner`, or ERC-1271/ERC-6492 for smart wallets) |
| `HasMessageDateStartedAndNotExpired(SiweMessage)` | Checks `NotBefore` and `ExpirationTime` against current UTC time |
| `IsMessageTheSameAsSessionStored(SiweMessage)` | Verifies the message matches the session stored under its nonce |
| `IsUserAddressRegistered(SiweMessage)` | Delegates to the configured `IEthereumUserService` (returns `true` if none configured) |
| `AssignNewNonce(SiweMessage)` | Generates a random nonce via `RandomNonceBuilder` and stores the session |
| `InvalidateSession(SiweMessage)` | Removes the session entry for the given message's nonce |

## RandomNonceBuilder

Generates cryptographically secure nonces by signing a timestamped challenge with a random key and hashing the result:

```csharp
string nonce = RandomNonceBuilder.GenerateNewNonce();
```

## Session Storage

Implement `ISessionStorage` to customize session persistence (e.g. Redis, database):

```csharp
public interface ISessionStorage
{
    void AddOrUpdate(SiweMessage siweMessage);
    SiweMessage GetSiweMessage(SiweMessage siweMessage);
    void Remove(SiweMessage siweMessage);
    void Remove(string nonce);
}
```

The default `InMemorySessionNonceStorage` uses a `ConcurrentDictionary<string, SiweMessage>` keyed by nonce.

## User Validation

Implement `IEthereumUserService` to add custom user authorization checks:

```csharp
public interface IEthereumUserService
{
    Task<bool> IsUserAddressRegistered(string address);
}
```

The built-in `ERC721BalanceEthereumUserService` checks whether the user holds at least one token from an ERC-721 contract:

```csharp
// Provide the NFT contract address and an RPC URL
var userService = new ERC721BalanceEthereumUserService(nftContractAddress, rpcUrl);
```

## Authentication (REST API)

For .NET 5.0+ applications, `SiweApiUserLoginService<TUser>` provides a REST API client for SIWE-based authentication flows with JWT tokens:

```csharp
var loginService = new SiweApiUserLoginService<User>(
    new HttpClient { BaseAddress = new Uri("https://api.example.com") });

// 1. Request a fresh SIWE message from the server
string messageToSign = await loginService.GenerateNewSiweMessage(ethereumAddress);

// 2. User signs the message (client-side), then authenticate
var response = await loginService.Authenticate(parsedMessage, signature);
// response.Jwt — JWT token for subsequent API calls
// response.Address — authenticated Ethereum address

// 3. Get user details
var user = await loginService.GetUser(response.Jwt);

// 4. Logout
await loginService.Logout(response.Jwt);
```

### User Model

```csharp
public class User
{
    public string EthereumAddress { get; set; }
    public string UserName { get; set; }
    public string Email { get; set; }
}
```

## Full Template

A complete Blazor + REST API SIWE authentication template is available at [`Nethereum.Templates.Siwe`](https://github.com/Nethereum/Nethereum.Templates.Pack), providing:

- **REST API** — Nonce generation, authentication endpoint, JWT creation and middleware validation
- **Blazor WebAssembly** — MetaMask signing, JWT local storage, `SiweAuthenticationWasmStateProvider`
- **Blazor Server** — Direct signing via `NethereumSiweAuthenticatorService`, protected session storage
- **Blazor `<AuthorizeView>`** — Role-based access with `EthereumConnected` and `SiweAuthenticated` claims

## Relationship to Other Packages

- **[Nethereum.Siwe.Core](../Nethereum.Siwe.Core/README.md)** — Message model, parser, string builder, and RECAP capabilities
- **Nethereum.UI** — `NethereumSiweAuthenticatorService` for Blazor Server SIWE flows
- **Nethereum.Signer** — `EthereumMessageSigner` used for signature recovery
- **Nethereum.Contracts** — ERC-1271 and ERC-6492 signature validation for smart contract wallets
