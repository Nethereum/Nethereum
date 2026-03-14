# Nethereum.X402

A .NET implementation of the [x402 protocol](https://github.com/x402/x402) for accepting HTTP 402 (Payment Required) cryptocurrency payments using EIP-3009 signed token authorizations.

## Overview

The x402 protocol adds a payment layer to HTTP APIs. When a client requests a protected resource, the server returns HTTP 402 with payment requirements. The client signs an EIP-3009 authorization (off-chain, no gas), retries the request with the signed payment header, and the server or facilitator settles the transfer on-chain.

Nethereum.X402 provides:

- **`X402HttpClient`** — Client that handles the 402 flow automatically (detect → sign → retry)
- **`X402Middleware`** — ASP.NET Core middleware for protecting API endpoints
- **Two EIP-3009 payment processors** — TransferWithAuthorization (facilitator submits) and ReceiveWithAuthorization (receiver submits)
- **Facilitator support** — Proxy payments through a third-party facilitator service
- **Multi-chain support** — Pre-configured for Base, Ethereum, Polygon, Arbitrum, Optimism, Avalanche

## Installation

```bash
dotnet add package Nethereum.X402
```

## Client: Pay for API Requests

`X402HttpClient` wraps `HttpClient` and handles the full 402 payment flow automatically.

### Automatic Payment Flow

The client detects 402 responses, signs an EIP-3009 authorization, and retries with the payment header:

```csharp
using Nethereum.X402.Client;

var httpClient = new HttpClient();
var options = new X402HttpClientOptions
{
    MaxPaymentAmount = 0.1m,       // Max USDC per request (safety limit)
    PreferredNetwork = "base",
    TokenName = "USD Coin",
    TokenVersion = "2",
    ChainId = 8453,
    TokenAddress = "0x833589fCD6eDb6E08f4c7C32D4f71b54bdA02913"
};

var x402Client = new X402HttpClient(httpClient, privateKey, options);

// Automatic: detects 402 → signs EIP-3009 → retries with payment
var response = await x402Client.GetAsync("https://api.example.com/premium/content");
var content = await response.Content.ReadAsStringAsync();

// Check payment result from response headers
if (response.HasPaymentResponse())
{
    var txHash = response.GetTransactionHash();
    var payer = response.GetPayerAddress();
    var success = response.IsPaymentSuccessful();
}
```

All HTTP methods are supported: `GetAsync`, `PostAsync`, `PutAsync`, `DeleteAsync`, `SendAsync`.

If the requested amount exceeds `MaxPaymentAmount`, the client throws `X402PaymentExceedsMaximumException` instead of signing.

### Manual Payment Flow

For full control, pass explicit `PaymentRequirements`:

```csharp
var x402Client = new X402HttpClient(httpClient, privateKey, "USD Coin", "2", 8453, usdcAddress);

var requirements = new PaymentRequirements
{
    Scheme = "exact",
    Network = "base",
    MaxAmountRequired = "1000000",  // $1.00 USDC (6 decimals)
    Asset = "USDC",
    PayTo = "0xReceiverAddress",
    Resource = "/api/premium",
    Description = "Premium content access",
    MaxTimeoutSeconds = 60
};

var response = await x402Client.GetAsync("https://api.example.com/premium", requirements);
```

### Client DI Registration

```csharp
using Nethereum.X402.Extensions;

builder.Services.AddX402Client(
    privateKey: Environment.GetEnvironmentVariable("PAYER_PRIVATE_KEY"),
    tokenName: "USD Coin",
    tokenVersion: "2",
    chainId: 8453,
    tokenAddress: "0x833589fCD6eDb6E08f4c7C32D4f71b54bdA02913");
```

## Server: Protect API Endpoints

Use `X402Middleware` to gate endpoints behind payment. Define route-based payment requirements:

```csharp
using Nethereum.X402.AspNetCore;
using Nethereum.X402.Server;
using Nethereum.X402.Models;

var builder = WebApplication.CreateBuilder(args);

// Register x402 services with a facilitator
builder.Services.AddX402Services("https://facilitator.x402.org");

var app = builder.Build();

// Add x402 middleware with route-specific payment requirements
app.UseX402(options =>
{
    options.Routes.Add(new RoutePaymentConfig("/api/premium/*", new PaymentRequirements
    {
        Scheme = "exact",
        Network = "base",
        MaxAmountRequired = "1000000",  // $1.00 USDC
        Asset = "USDC",
        PayTo = "0xYourReceiverAddress",
        Resource = "/api/premium",
        Description = "Premium API access",
        MaxTimeoutSeconds = 60
    }));
});

app.MapGet("/api/premium/content", () => Results.Ok(new { data = "Premium content" }));
app.Run();
```

The middleware intercepts requests matching route patterns. If no `X-Payment` header is present, it returns 402 with `PaymentRequirements`. If a payment header is present, it verifies and settles through the facilitator before forwarding to the endpoint.

### Self-Facilitated Server

Instead of using an external facilitator, process payments directly on-chain:

```csharp
using Nethereum.X402.Extensions;

// Register a Transfer processor (facilitator pays gas)
builder.Services.AddX402TransferProcessor(
    facilitatorPrivateKey: Environment.GetEnvironmentVariable("FACILITATOR_KEY"),
    rpcEndpoints: new Dictionary<string, string> { ["base"] = "https://mainnet.base.org" },
    tokenAddresses: new Dictionary<string, string> { ["base"] = "0x833589fCD6eDb6E08f4c7C32D4f71b54bdA02913" },
    chainIds: new Dictionary<string, int> { ["base"] = 8453 },
    tokenNames: new Dictionary<string, string> { ["base"] = "USD Coin" },
    tokenVersions: new Dictionary<string, string> { ["base"] = "2" });

// Or register a Receive processor (receiver pays gas)
builder.Services.AddX402ReceiveProcessor(
    receiverPrivateKey: Environment.GetEnvironmentVariable("RECEIVER_KEY"),
    rpcEndpoints: new Dictionary<string, string> { ["base"] = "https://mainnet.base.org" },
    tokenAddresses: new Dictionary<string, string> { ["base"] = "0x833589fCD6eDb6E08f4c7C32D4f71b54bdA02913" },
    chainIds: new Dictionary<string, int> { ["base"] = 8453 },
    tokenNames: new Dictionary<string, string> { ["base"] = "USD Coin" },
    tokenVersions: new Dictionary<string, string> { ["base"] = "2" });
```

## Payment Processors

The `IX402PaymentProcessor` interface defines three operations:

```csharp
public interface IX402PaymentProcessor
{
    Task<VerificationResponse> VerifyPaymentAsync(PaymentPayload payload, PaymentRequirements requirements, CancellationToken ct = default);
    Task<SettlementResponse> SettlePaymentAsync(PaymentPayload payload, PaymentRequirements requirements, CancellationToken ct = default);
    Task<SupportedPaymentKindsResponse> GetSupportedAsync(CancellationToken ct = default);
}
```

Two implementations are provided:

| Processor | Who Submits TX | Who Pays Gas | EIP-3009 Function |
|-----------|---------------|-------------|-------------------|
| `X402TransferWithAuthorisation3009Service` | Facilitator | Facilitator | `transferWithAuthorization` |
| `X402ReceiveWithAuthorisation3009Service` | Receiver | Receiver | `receiveWithAuthorization` |

### TransferWithAuthorization (Facilitator Model)

The payer signs an authorization, and a facilitator submits the on-chain transfer:

```csharp
using Nethereum.X402.Blockchain;
using Nethereum.X402.Models;
using Nethereum.X402.Signers;

// Create the service
var service = new X402TransferWithAuthorisation3009Service(
    facilitatorPrivateKey: "0x...",
    rpcEndpoints: new Dictionary<string, string> { ["base-sepolia"] = "https://sepolia.base.org" },
    tokenAddresses: new Dictionary<string, string> { ["base-sepolia"] = usdcAddress },
    chainIds: new Dictionary<string, int> { ["base-sepolia"] = 84532 },
    tokenNames: new Dictionary<string, string> { ["base-sepolia"] = "USDC" },
    tokenVersions: new Dictionary<string, string> { ["base-sepolia"] = "2" });

// Build and sign an authorization
var builder = new TransferWithAuthorisationBuilder();
var signer = new TransferWithAuthorisationSigner();

var requirements = new PaymentRequirements
{
    Scheme = "exact",
    Network = "base-sepolia",
    MaxAmountRequired = "1000000",
    PayTo = receiverAddress
};

var authorization = builder.BuildFromPaymentRequirements(requirements, payerAddress);

var signature = await signer.SignWithPrivateKeyAsync(
    authorization, "USDC", "2", 84532, usdcAddress, payerPrivateKey);

// Encode signature to hex (r + s + v)
var signatureBytes = new byte[signature.R.Length + signature.S.Length + signature.V.Length];
signature.R.CopyTo(signatureBytes, 0);
signature.S.CopyTo(signatureBytes, signature.R.Length);
signature.V.CopyTo(signatureBytes, signature.R.Length + signature.S.Length);
var signatureHex = signatureBytes.ToHex(true);

// Create the payment payload
var paymentPayload = new PaymentPayload
{
    X402Version = 1,
    Scheme = "exact",
    Network = "base-sepolia",
    Payload = new ExactSchemePayload
    {
        Authorization = authorization,
        Signature = signatureHex
    }
};

// Verify the payment (checks signature, balance, timestamps)
var verification = await service.VerifyPaymentAsync(paymentPayload, requirements);
if (verification.IsValid)
{
    // Settle on-chain
    var settlement = await service.SettlePaymentAsync(paymentPayload, requirements);
    Console.WriteLine($"TX: {settlement.Transaction}, Payer: {settlement.Payer}");
}
```

### ReceiveWithAuthorization (Receiver Model)

The payer signs an authorization, and the receiver submits the on-chain transfer (receiver pays gas):

```csharp
using Nethereum.X402.Blockchain;
using Nethereum.X402.Signers;

var service = new X402ReceiveWithAuthorisation3009Service(
    receiverPrivateKey: "0x...",
    rpcEndpoints: new Dictionary<string, string> { ["base-sepolia"] = "https://sepolia.base.org" },
    tokenAddresses: new Dictionary<string, string> { ["base-sepolia"] = usdcAddress },
    chainIds: new Dictionary<string, int> { ["base-sepolia"] = 84532 },
    tokenNames: new Dictionary<string, string> { ["base-sepolia"] = "USDC" },
    tokenVersions: new Dictionary<string, string> { ["base-sepolia"] = "2" });

// Build authorization using Receive builder
var builder = new ReceiveWithAuthorisationBuilder();
var signer = new TransferWithAuthorisationSigner();

var authorization = builder.BuildFromPaymentRequirements(requirements, payerAddress);

// Sign with the Receive-specific method
var signature = await signer.SignReceiveWithPrivateKeyAsync(
    authorization, "USDC", "2", 84532, usdcAddress, payerPrivateKey);
```

The key difference: `ReceiveWithAuthorisation3009Service` validates that the authorization's `To` address matches the receiver's address and uses `receiveWithAuthorization` on-chain (only the designated receiver can submit).

### Cancel an Authorization

```csharp
var service = new X402TransferWithAuthorisation3009Service(...);

var cancelResponse = await service.CancelAuthorizationAsync(
    authorizerAddress: payerAddress,
    nonce: authorizationNonce,
    network: "base-sepolia");
```

## Authorization Building

`TransferWithAuthorisationBuilder` and `ReceiveWithAuthorisationBuilder` create `Authorization` objects from payment requirements:

```csharp
var builder = new TransferWithAuthorisationBuilder();

// Generate a cryptographically random nonce
byte[] nonce = builder.GenerateNonce();

// Build from payment requirements (auto-generates nonce, sets time window)
var authorization = builder.BuildFromPaymentRequirements(
    requirements,
    fromAddress: payerAddress,
    validAfterTimestamp: null,   // Default: 10 minutes ago
    validBeforeTimestamp: null); // Default: 1 hour from now
```

### EIP-712 Typed Data

Get the EIP-712 typed data structure for custom signing flows:

```csharp
var typedData = builder.GetTypedDataForAuthorization(
    tokenName: "USD Coin",
    tokenVersion: "2",
    chainId: 8453,
    verifyingContract: usdcAddress);
```

### Signing with Web3 (External Signers)

Sign with any Nethereum Web3 account (hardware wallets, KMS, etc.):

```csharp
var signer = new TransferWithAuthorisationSigner();

// Sign with Web3 account (supports external signers)
var signature = await signer.SignWithWeb3Async(
    authorization, "USD Coin", "2", 8453, usdcAddress, web3, signerAddress);

// Recover signer address from signature
var recovered = signer.RecoverAddress(
    authorization, "USD Coin", "2", 8453, usdcAddress, signature);
```

## Facilitator

A facilitator is a service that verifies and settles payments on behalf of API servers, so servers don't need blockchain infrastructure.

### Client

```csharp
using Nethereum.X402.Facilitator;

var facilitatorClient = new HttpFacilitatorClient(httpClient, "https://facilitator.x402.org");

var verification = await facilitatorClient.VerifyAsync(paymentPayload, requirements);
var settlement = await facilitatorClient.SettleAsync(paymentPayload, requirements);
var supported = await facilitatorClient.GetSupportedAsync();
```

### Host a Facilitator

Expose a facilitator as an ASP.NET Core REST API:

```csharp
using Nethereum.X402.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Register a payment processor
builder.Services.AddX402TransferProcessor(...);

// Add facilitator controller endpoints
builder.Services.AddControllers().AddX402FacilitatorControllers();

var app = builder.Build();
app.MapControllers();
app.Run();
```

This exposes:
- `POST /facilitator/verify` — Verify a payment
- `POST /facilitator/settle` — Settle a payment on-chain
- `GET /facilitator/supported` — List supported payment kinds

## Network Configuration

`NetworkConfiguration` provides pre-configured RPC endpoints, token addresses, and chain IDs for common networks:

```csharp
using Nethereum.X402.Blockchain;

var config = NetworkConfiguration.Default;

var rpcUrl = config.GetRpcEndpoint("base");
var usdcAddress = config.GetUSDCAddress("base");
var chainId = config.GetChainId("base");
```

Supported networks include Base, Ethereum, Polygon, Avalanche, Arbitrum, and Optimism (mainnet and testnet variants).

## Models

### PaymentRequirements

Server's payment request (returned in 402 response):

```csharp
public class PaymentRequirements
{
    public string Scheme { get; set; }             // "exact"
    public string Network { get; set; }            // "base", "ethereum", etc.
    public string MaxAmountRequired { get; set; }  // Amount in atomic units (string)
    public string Asset { get; set; }              // "USDC"
    public string PayTo { get; set; }              // Receiver address
    public string Resource { get; set; }           // Protected resource path
    public string Description { get; set; }        // Human-readable description
    public string? MimeType { get; set; }
    public object? OutputSchema { get; set; }
    public int MaxTimeoutSeconds { get; set; }
    public object? Extra { get; set; }
}
```

### Authorization

EIP-3009 transfer authorization:

```csharp
public class Authorization
{
    public string From { get; set; }        // Payer address
    public string To { get; set; }          // Receiver address
    public string Value { get; set; }       // Amount in atomic units
    public string ValidAfter { get; set; }  // Unix timestamp
    public string ValidBefore { get; set; } // Unix timestamp
    public string Nonce { get; set; }       // Hex-encoded 32-byte nonce
}
```

### PaymentPayload

Client's signed payment (sent in X-Payment header):

```csharp
public class PaymentPayload
{
    public int X402Version { get; set; }  // 1
    public string Scheme { get; set; }    // "exact"
    public string Network { get; set; }
    public object Payload { get; set; }   // ExactSchemePayload
}

public class ExactSchemePayload
{
    public string Signature { get; set; }            // Hex-encoded EIP-712 signature
    public Authorization Authorization { get; set; }
}
```

### Response Types

```csharp
public class VerificationResponse
{
    public bool IsValid { get; set; }
    public string? InvalidReason { get; set; }  // X402ErrorCodes value
    public string Payer { get; set; }
}

public class SettlementResponse
{
    public bool Success { get; set; }
    public string? ErrorReason { get; set; }
    public string Transaction { get; set; }  // TX hash
    public string Network { get; set; }
    public string Payer { get; set; }
}
```

## Error Codes

`X402ErrorCodes` defines standard error strings:

| Code | Meaning |
|------|---------|
| `insufficient_funds` | Payer doesn't have enough tokens |
| `invalid_exact_evm_payload_signature` | EIP-712 signature verification failed |
| `invalid_exact_evm_payload_authorization_valid_after` | Authorization not yet valid |
| `invalid_exact_evm_payload_authorization_valid_before` | Authorization expired |
| `invalid_exact_evm_payload_authorization_value` | Amount mismatch |
| `invalid_exact_evm_payload_recipient_mismatch` | Receiver address doesn't match (ReceiveWithAuthorization) |
| `invalid_exact_evm_payload_authorization_nonce_used` | Nonce already consumed |
| `invalid_network` | Unsupported network |
| `invalid_scheme` | Invalid payment scheme |

## Supported Tokens

Any EIP-3009 compliant token works. Common tokens with pre-configured addresses:

| Token | Network | Address |
|-------|---------|---------|
| USDC | Ethereum | `0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48` |
| USDC | Base | `0x833589fCD6eDb6E08f4c7C32D4f71b54bdA02913` |
| USDC | Polygon | `0x3c499c542cEF5E3811e1192ce70d8cC03d5c3359` |
| USDC | Arbitrum | `0xaf88d065e77c8cC2239327C5EDb3A432268e5831` |
| USDC | Optimism | `0x0b2C639c533813f4Aa9D7837CAf62653d097Ff85` |

## Payment Flow

```
1. Client → Server:  GET /api/premium (no payment header)
2. Server → Client:  402 + PaymentRequirements (amount, token, network, payTo)
3. Client:           Signs EIP-3009 authorization (off-chain, no gas)
4. Client → Server:  GET /api/premium + X-Payment header (PaymentPayload)
5. Server/Facilitator: Verifies signature, balance, timestamps
6. Server/Facilitator: Settles on-chain via transferWithAuthorization or receiveWithAuthorization
7. Server → Client:  200 OK + content + settlement response headers
```

## References

- [x402 Protocol Specification](https://github.com/x402/x402)
- [EIP-3009: Transfer With Authorization](https://eips.ethereum.org/EIPS/eip-3009)
- [USDC Developer Documentation](https://developers.circle.com/stablecoins/docs)
