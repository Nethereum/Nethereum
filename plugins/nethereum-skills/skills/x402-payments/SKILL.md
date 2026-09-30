---
name: x402-payments
description: Accept HTTP 402 cryptocurrency payments using Nethereum (.NET/C#). Use this skill whenever the user asks about x402 protocol (version 2), HTTP 402 payments, pay-per-request APIs, EIP-3009 transfer authorization, Permit2 payments, USDC payments, crypto API monetization, payment middleware, or accepting cryptocurrency payments in ASP.NET with C# or .NET.
user-invocable: true
---

# x402: Crypto Payments (protocol v2)

The x402 protocol implements HTTP 402 (Payment Required) for pay-per-request APIs. A client pays with a signed authorization — off-chain (no gas) — and the server or a facilitator settles on-chain. Nethereum's `Nethereum.X402` package provides `X402HttpClient` for automatic payments and `X402Middleware` for ASP.NET Core endpoint protection.

This library targets **x402 version 2**: payments carry `x402Version = 2`, the chosen requirement is echoed in the payload's `accepted` field, and networks use **CAIP-2** identifiers (`eip155:<chainId>`, e.g. `eip155:8453` for Base). The "exact" scheme supports two EVM asset-transfer methods: **EIP-3009** (default) and **Permit2**.

NuGet: `Nethereum.X402`

```bash
dotnet add package Nethereum.X402
```

The v2 wire uses three base64 headers: `PAYMENT-REQUIRED` (402 response), `PAYMENT-SIGNATURE` (request), `PAYMENT-RESPONSE` (200 response).

## Client: Pay Automatically

`X402HttpClient` handles the full 402 flow — detect requirement, select one, sign it, retry with the payment header. The signing domain (token, chain id, name/version) is derived from the server's `PaymentRequirements`, so the client is not pre-configured with a token:

```csharp
using Nethereum.X402.Client;

var options = new X402HttpClientOptions
{
    MaxAmount = "100000",           // Safety limit per request (USDC)
    PreferredNetwork = "eip155:8453",  // CAIP-2 (Base)
    PreferredScheme = "exact"
};

var x402Client = new X402HttpClient(httpClient, privateKey, options);
var response = await x402Client.GetAsync("https://api.example.com/premium/content");

// Check payment result from the PAYMENT-RESPONSE header
if (response.HasPaymentResponse())
{
    var txHash = response.GetTransactionHash();
    var payer = response.GetPayerAddress();
    var success = response.IsPaymentSuccessful();
}
```

Supports `GetAsync`, `PostAsync`, `PutAsync`, `DeleteAsync`, `SendAsync`. Throws `X402PaymentExceedsMaximumException` if the requested amount exceeds `MaxAmount`. The client automatically produces the right payload for the selected requirement's method (EIP-3009, or Permit2 when `extra.assetTransferMethod == "permit2"`).

### Manual Payment Flow

Construct with just the payer key and pass explicit `PaymentRequirements`:

```csharp
var x402Client = new X402HttpClient(httpClient, privateKey);

var requirements = new PaymentRequirements
{
    Scheme = "exact",
    Network = "eip155:84532",   // Base Sepolia
    Amount = "1000000",         // $1.00 USDC (6 decimals), atomic units
    Asset = usdcAddress,
    PayTo = receiverAddress,
    MaxTimeoutSeconds = 60,
    Extra = new ExactSchemeExtra { Name = "USDC", Version = "2" }
};

var response = await x402Client.GetAsync("https://api.example.com/premium", requirements);
```

## Server: Protect Endpoints

Register a facilitator client, then add route-based middleware:

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
        Network = "eip155:8453",
        Amount = "1000000",
        Asset = "0x833589fCD6eDb6E08f4c7C32D4f71b54bdA02913",
        PayTo = "0xYourAddress",
        MaxTimeoutSeconds = 60,
        Extra = new ExactSchemeExtra { AssetTransferMethod = "eip3009", Name = "USDC", Version = "2" }
    }));
});
```

### Self-Facilitated (No External Facilitator)

Settle on-chain directly. Only an RPC endpoint per chain id is configured (token/chain/domain come from each requirement):

```csharp
using Nethereum.X402.Extensions;

var rpcByChainId = new Dictionary<int, string> { [8453] = "https://mainnet.base.org" };

// EIP-3009 only (facilitator pays gas):
builder.Services.AddX402TransferProcessor(facilitatorPrivateKey, rpcByChainId);

// EIP-3009 AND Permit2 (routes by payload shape) — recommended:
builder.Services.AddX402ExactProcessor(facilitatorPrivateKey, rpcByChainId);

// Receiver-submitted model (receiver pays gas):
builder.Services.AddX402ReceiveProcessor(receiverPrivateKey, rpcByChainId);
```

Each overload also accepts an `IAccount` (external signers/KMS) or an `IServiceProvider` factory.

## Asset-Transfer Methods

| Method | Processor | On-chain call |
|--------|-----------|---------------|
| EIP-3009 (default) | `X402TransferWithAuthorisation3009Service` | `transferWithAuthorization` |
| EIP-3009 (receiver) | `X402ReceiveWithAuthorisation3009Service` | `receiveWithAuthorization` |
| Permit2 | `X402ExactPermit2Service` | `x402ExactPermit2Proxy.settle` |
| dispatcher | `X402ExactSchemeProcessor` | routes by payload shape |

All implement `IX402PaymentProcessor` (`VerifyPaymentAsync`, `SettlePaymentAsync`, `GetSupportedAsync`). A blockchain processor is constructed with a facilitator key (or `IAccount`) and a `Dictionary<int, IClient>` keyed by chain id.

### Permit2 Method

Permit2 works with any ERC-20 the payer has approved to the canonical Permit2 contract (no EIP-3009 support needed). A route opts in with `extra.assetTransferMethod = "permit2"`; the client signs a Permit2 `PermitWitnessTransferFrom` (spender = the x402ExactPermit2Proxy, witness = the recipient) and the facilitator settles via `proxy.settle`, which enforces that funds go only to that recipient:

```csharp
options.Routes.Add(new RoutePaymentConfig("/api/premium-permit2", new PaymentRequirements
{
    Scheme = "exact",
    Network = "eip155:84532",
    Amount = "10000",
    Asset = usdcAddress,
    PayTo = receiverAddress,
    MaxTimeoutSeconds = 60,
    Extra = new ExactSchemeExtra { AssetTransferMethod = "permit2" }
}));

// Facilitator: X402ExactPermit2Service (or AddX402ExactProcessor to serve both methods)
```

The Permit2/proxy addresses are canonical constants (`X402Permit2Addresses`), the same on every EVM chain. The underlying Permit2 signing lives in `Nethereum.Signer.EIP712`/`Nethereum.ABI`, and the contract in `Nethereum.Contracts` (`web3.Eth.GetPermit2Service()`).

## Build EIP-3009 Authorizations Directly

For custom flows:

```csharp
using Nethereum.X402.Signers;

var builder = new TransferWithAuthorisationBuilder();
var signer = new TransferWithAuthorisationSigner();

var authorization = builder.BuildFromPaymentRequirements(requirements, payerAddress);
// Default window: validAfter = 10 min ago, validBefore = 1 hour from now

var signature = await signer.SignWithPrivateKeyAsync(
    authorization, "USD Coin", "2", chainId, usdcAddress, payerPrivateKey);

// Or sign with a Web3 account (hardware wallets, KMS)
var signature = await signer.SignWithWeb3Async(
    authorization, "USD Coin", "2", chainId, usdcAddress, web3, signerAddress);
```

## Error Codes (`X402ErrorCodes`)

| Code | Meaning |
|------|---------|
| `invalid_exact_evm_insufficient_balance` | Payer doesn't have enough tokens |
| `invalid_exact_evm_signature` | EIP-3009 signature verification failed |
| `invalid_exact_evm_authorization_value` | Amount below the requirement |
| `invalid_exact_evm_recipient_mismatch` | Receiver mismatch |
| `invalid_exact_evm_nonce_already_used` | Nonce already used |
| `invalid_network` / `unsupported_payload_type` | Network/scheme not supported |
| `invalid_permit2_recipient_mismatch` | Permit2 witness recipient ≠ `PayTo` |
| `permit2_amount_mismatch` / `permit2_token_mismatch` | Permit2 amount/token mismatch |
| `permit2_allowance_required` | Payer hasn't approved Permit2 for the asset |
| `invalid_permit2_signature` | Permit2 signature verification failed |

## Supported Tokens

Any EIP-3009 token (for the eip3009 method) or any ERC-20 approved to Permit2 (for the permit2 method). Common USDC addresses:

| Token | Network | Address |
|-------|---------|---------|
| USDC | Ethereum | `0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48` |
| USDC | Base | `0x833589fCD6eDb6E08f4c7C32D4f71b54bdA02913` |
| USDC | Base Sepolia | `0x036CbD53842c5426634e7929541eC2318f3dCF7e` |
| USDC | Polygon | `0x3c499c542cEF5E3811e1192ce70d8cC03d5c3359` |
| USDC | Arbitrum | `0xaf88d065e77c8cC2239327C5EDb3A432268e5831` |
| USDC | Optimism | `0x0b2C639c533813f4Aa9D7837CAf62653d097Ff85` |

For full documentation, see: https://docs.nethereum.com/docs/defi/guide-x402-payments
