# Nethereum.Siwe.Core

Core models and utilities for Sign-In with Ethereum (EIP-4361). This package provides the `SiweMessage` model, message parsing, string building, and validation helpers used by both `Nethereum.Siwe` and downstream authentication providers.

## Key Components

| Class | Purpose |
|---|---|
| `SiweMessage` | EIP-4361 message model with Domain, Address, Statement, URI, Nonce, expiration, and chain binding |
| `SiweMessageParser` | Parses a plain-text SIWE string back into a `SiweMessage` (regex and ABNF modes) |
| `SiweMessageStringBuilder` | Builds the canonical plain-text representation for signing |

## Usage

### Build a SIWE message string

```csharp
var message = new SiweMessage
{
    Domain = "example.com",
    Address = "0xAb5801a7D398351b8bE11C439e05C5B3259aeC9B",
    Statement = "Sign in to Example",
    Uri = "https://example.com",
    Version = "1",
    ChainId = "1"
};
message.SetIssuedAtNow();
message.SetExpirationTime(DateTime.UtcNow.AddMinutes(10));
message.SetNotBefore(DateTime.UtcNow);
message.Nonce = "randomnonce123";

string plainText = SiweMessageStringBuilder.BuildMessage(message);
```

### Parse a SIWE message

Two parsing modes are available — regex-based (default) and ABNF-based:

```csharp
var parsed = SiweMessageParser.Parse(plainText);
// parsed.Address, parsed.Domain, parsed.Nonce, etc.

// ABNF-based parser (stricter validation)
var parsedAbnf = SiweMessageParser.ParseUsingAbnf(plainText);
```

### Validate timing

```csharp
bool valid = message.HasMessageDateStartedAndNotExpired();
bool hasRequired = message.HasRequiredFields();

// Individual checks
bool started = message.HasMessageDateStarted();
bool expired = message.HasMessageDateExpired();
```

### Compare messages

```csharp
bool same = message.IsTheSame(otherMessage);
```

## SiweMessage Properties

| Property | Type | Description |
|---|---|---|
| `Domain` | `string` | RFC 4501 DNS authority (e.g. `"example.com"`) |
| `Address` | `string` | EIP-55 checksum Ethereum address |
| `Statement` | `string` | Human-readable assertion for the user to sign |
| `Uri` | `string` | RFC 3986 URI of the resource requesting authentication |
| `Version` | `string` | Message version (currently `"1"`) |
| `Nonce` | `string` | Randomized token (8+ alphanumeric characters) |
| `IssuedAt` | `string` | ISO 8601 datetime when the message was created |
| `ExpirationTime` | `string` | ISO 8601 datetime when the message expires (optional) |
| `NotBefore` | `string` | ISO 8601 datetime before which the message is not valid (optional) |
| `RequestId` | `string` | System-specific identifier (optional) |
| `ChainId` | `string` | EIP-155 chain ID to bind authentication to a specific chain |
| `Resources` | `List<string>` | List of RFC 3986 URIs the user is authorizing (optional) |

## SIWE RECAP (EIP-5573)

The RECAP (Resource Capability) system extends SIWE messages with fine-grained capability-based authorization. Instead of blanket sign-in, users can authorize specific actions on specific resources.

### Key RECAP Classes

| Class | Purpose |
|---|---|
| `SiweRecapMsgBuilder` | Fluent builder for constructing RECAP-enabled SIWE messages |
| `SiweRecapCapability` | Represents a set of permitted actions with optional targets |
| `SiweNamespace` | Validated namespace identifier (e.g. `"eip155"`, `"https"`) |
| `SiweRecapExtensions` | Extension methods for checking permissions on SIWE messages |

### Build a RECAP message

```csharp
var message = new SiweMessage
{
    Domain = "example.com",
    Address = "0xAb5801a7D398351b8bE11C439e05C5B3259aeC9B",
    Uri = "https://example.com",
    Version = "1",
    ChainId = "1"
};

// Use the fluent builder to add capabilities
var recapMessage = SiweRecapMsgBuilder.Init(message)
    .AddDefaultActions(
        new SiweNamespace("eip155"),
        new HashSet<string> { "sign", "send" })
    .AddTargetActions(
        new SiweNamespace("https"),
        "https://api.example.com",
        new HashSet<string> { "read", "write" })
    .Build();

string messageToSign = SiweMessageStringBuilder.BuildMessage(recapMessage);
```

### Check permissions

```csharp
// Check if the signed message grants a specific permission
bool canWrite = recapMessage.HasPermissions(
    new SiweNamespace("https"),
    "https://api.example.com",
    "write");

// Verify statement matches declared permissions
bool consistent = recapMessage.HasStatementMatchingPermissions();
```

### Decode a RECAP resource URN

```csharp
var capability = SiweRecapCapability.DecodeResourceUrn(resourceUrn);
bool hasAccess = capability.HasTargetPermission("https://api.example.com", "read");
bool hasDefault = capability.HasPermissionByDefault("sign");
```

## Relationship to Other Packages

- **[Nethereum.Siwe](../Nethereum.Siwe/README.md)** — Adds session/nonce management, signature verification, and user registration checks on top of these core models
- **Nethereum.UI** — `NethereumSiweAuthenticatorService` orchestrates SIWE authentication in Blazor Server using this package
