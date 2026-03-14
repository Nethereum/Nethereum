---
name: x402-payments
description: Accept HTTP 402 cryptocurrency payments using Nethereum (.NET/C#). Use this skill whenever the user asks about x402 protocol, HTTP 402 payments, pay-per-request APIs, EIP-3009 transfer authorization, USDC payments, crypto API monetization, payment middleware, or accepting cryptocurrency payments in ASP.NET with C# or .NET.
user-invocable: true
---

# x402: Crypto Payments

The x402 protocol implements HTTP 402 (Payment Required) for pay-per-request APIs. Clients pay with signed EIP-3009 USDC authorizations — the payer signs off-chain (no gas), and the server or facilitator settles on-chain. Nethereum's `Nethereum.X402` package provides `X402HttpClient` for automatic payments and `X402Middleware` for ASP.NET Core endpoint protection.

NuGet: `Nethereum.X402`

```bash
dotnet add package Nethereum.X402
```

## Client: Pay Automatically

`X402HttpClient` handles the full 402 flow — detect payment requirement, sign EIP-3009 authorization, retry with payment:

```csharp
using Nethereum.X402.Client;

var options = new X402HttpClientOptions
{
    MaxPaymentAmount = 0.1m,       // Safety limit per request
    PreferredNetwork = "base",
    TokenName = "USD Coin",
    TokenVersion = "2",
    ChainId = 8453,
    TokenAddress = "0x833589fCD6eDb6E08f4c7C32D4f71b54bdA02913"
};

var x402Client = new X402HttpClient(httpClient, privateKey, options);
var response = await x402Client.GetAsync("https://api.example.com/premium/content");

// Check payment result from response headers
if (response.HasPaymentResponse())
{
    var txHash = response.GetTransactionHash();
    var payer = response.GetPayerAddress();
    var success = response.IsPaymentSuccessful();
}
```

Supports `GetAsync`, `PostAsync`, `PutAsync`, `DeleteAsync`, `SendAsync`. Throws `X402PaymentExceedsMaximumException` if requested amount exceeds `MaxPaymentAmount`.

### Manual Payment Flow

Pass explicit `PaymentRequirements` for full control:

```csharp
var x402Client = new X402HttpClient(httpClient, privateKey, "USD Coin", "2", 8453, usdcAddress);

var requirements = new PaymentRequirements
{
    Scheme = "exact",
    Network = "base",
    MaxAmountRequired = "1000000",  // $1.00 USDC (6 decimals)
    PayTo = receiverAddress,
    Resource = "/api/premium",
    Description = "Premium content",
    MaxTimeoutSeconds = 60
};

var response = await x402Client.GetAsync("https://api.example.com/premium", requirements);
```

## Server: Protect Endpoints

Use route-based middleware to gate endpoints:

```csharp
using Nethereum.X402.AspNetCore;
using Nethereum.X402.Server;
using Nethereum.X402.Models;

builder.Services.AddX402Services("https://facilitator.x402.org");

app.UseX402(options =>
{
    options.Routes.Add(new RoutePaymentConfig("/api/premium/*", new PaymentRequirements
    {
        Scheme = "exact",
        Network = "base",
        MaxAmountRequired = "1000000",
        Asset = "USDC",
        PayTo = "0xYourAddress",
        Resource = "/api/premium",
        Description = "Premium API access",
        MaxTimeoutSeconds = 60
    }));
});
```

### Self-Facilitated (No External Facilitator)

Process payments on-chain directly:

```csharp
using Nethereum.X402.Extensions;

// Transfer model: facilitator account pays gas
builder.Services.AddX402TransferProcessor(
    facilitatorPrivateKey: Environment.GetEnvironmentVariable("KEY"),
    rpcEndpoints: new Dictionary<string, string> { ["base"] = "https://mainnet.base.org" },
    tokenAddresses: new Dictionary<string, string> { ["base"] = usdcAddress },
    chainIds: new Dictionary<string, int> { ["base"] = 8453 },
    tokenNames: new Dictionary<string, string> { ["base"] = "USD Coin" },
    tokenVersions: new Dictionary<string, string> { ["base"] = "2" });

// Or Receive model: receiver pays gas
builder.Services.AddX402ReceiveProcessor(
    receiverPrivateKey: Environment.GetEnvironmentVariable("KEY"),
    ...same config dictionaries...);
```

## Two Payment Models

| Model | Service | Who Submits TX | Who Pays Gas |
|-------|---------|---------------|-------------|
| Transfer | `X402TransferWithAuthorisation3009Service` | Facilitator | Facilitator |
| Receive | `X402ReceiveWithAuthorisation3009Service` | Receiver | Receiver |

Both implement `IX402PaymentProcessor` with `VerifyPaymentAsync`, `SettlePaymentAsync`, `GetSupportedAsync`.

## Build Authorizations Directly

For custom payment flows:

```csharp
using Nethereum.X402.Signers;

var builder = new TransferWithAuthorisationBuilder();
var signer = new TransferWithAuthorisationSigner();

var authorization = builder.BuildFromPaymentRequirements(requirements, payerAddress);
// Default: validAfter = 10 min ago, validBefore = 1 hour from now

var signature = await signer.SignWithPrivateKeyAsync(
    authorization, "USD Coin", "2", chainId, usdcAddress, payerPrivateKey);

// Or sign with Web3 account (hardware wallets, KMS)
var signature = await signer.SignWithWeb3Async(
    authorization, "USD Coin", "2", chainId, usdcAddress, web3, signerAddress);
```

## Error Codes

| Code | Meaning |
|------|---------|
| `insufficient_funds` | Payer doesn't have enough tokens |
| `invalid_exact_evm_payload_signature` | Signature verification failed |
| `invalid_exact_evm_payload_authorization_valid_before` | Authorization expired |
| `invalid_exact_evm_payload_recipient_mismatch` | Receiver mismatch (Receive model) |
| `invalid_exact_evm_payload_authorization_nonce_used` | Nonce already used |

## Supported Tokens

| Token | Network | Address |
|-------|---------|---------|
| USDC | Ethereum | `0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48` |
| USDC | Base | `0x833589fCD6eDb6E08f4c7C32D4f71b54bdA02913` |
| USDC | Polygon | `0x3c499c542cEF5E3811e1192ce70d8cC03d5c3359` |
| USDC | Arbitrum | `0xaf88d065e77c8cC2239327C5EDb3A432268e5831` |
| USDC | Optimism | `0x0b2C639c533813f4Aa9D7837CAf62653d097Ff85` |

For full documentation, see: https://docs.nethereum.com/docs/defi/guide-x402-payments
