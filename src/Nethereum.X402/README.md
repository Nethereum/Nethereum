# Nethereum.X402

A .NET implementation of the [x402 protocol](https://github.com/coinbase/x402) (protocol **version 2**) for accepting HTTP 402 (Payment Required) stablecoin payments with signed, gasless token authorizations.

## Overview

The x402 protocol adds a payment layer to HTTP APIs. When a client requests a protected resource, the server returns HTTP 402 with payment requirements. The client signs a payment authorization off-chain (no gas), retries the request with a signed payment header, and the server or a facilitator settles the transfer on-chain.

Nethereum.X402 implements the x402 **v2** "exact" scheme with two EVM asset-transfer methods and provides:

- **`X402HttpClient`** — Client that handles the 402 flow automatically (detect → sign → retry)
- **`X402Middleware`** — ASP.NET Core middleware for protecting API endpoints
- **Two EIP-3009 payment processors** — TransferWithAuthorization (facilitator submits) and ReceiveWithAuthorization (receiver submits)
- **Permit2 asset-transfer method** — the "exact" scheme settled via the canonical Permit2 + x402ExactPermit2Proxy, for any ERC-20 (see [Permit2 Asset-Transfer Method](#permit2-asset-transfer-method))
- **Facilitator support** — Verify/settle locally, or proxy through a third-party facilitator service
- **Multi-chain support** — networks are identified by CAIP-2 ids (`eip155:<chainId>`); only an RPC endpoint per chain is configured

> **Protocol version 2.** This library targets x402 v2 only. Payments carry `x402Version = 2`, the chosen requirement is echoed in the payload's `accepted` field, and networks use CAIP-2 identifiers (e.g. `eip155:8453` for Base). The wire format and signatures are interop-verified against the coinbase/x402 reference implementation (both directions).

## Installation

```bash
dotnet add package Nethereum.X402
```

## HTTP Headers

x402 v2 uses three base64-encoded headers:

| Header | Direction | Payload |
|--------|-----------|---------|
| `PAYMENT-REQUIRED` | 402 response | `PaymentRequired` (accepted requirements + resource info) |
| `PAYMENT-SIGNATURE` | request | `PaymentPayload` (the signed payment) |
| `PAYMENT-RESPONSE` | 200 response | `SettlementResponse` (on-chain result) |

The `PaymentRequired` body is also returned in the 402 response body as a fallback.

## Client: Pay for API Requests

`X402HttpClient` wraps `HttpClient`. The signing domain (token, chain id, EIP-712 name/version) is derived from the `PaymentRequirements` the server returns — the client is not pre-configured with a token.

### Automatic Payment Flow

The client detects 402 responses, selects a requirement, signs it, and retries with the payment header:

```csharp
using Nethereum.X402.Client;

var httpClient = new HttpClient();
var options = new X402HttpClientOptions
{
    MaxAmount = "100000",          // Max USDC per request (safety limit)
    PreferredNetwork = "eip155:8453", // CAIP-2 (Base); used by the requirement selector
    PreferredScheme = "exact"
};

var x402Client = new X402HttpClient(httpClient, payerPrivateKey, options);

// Automatic: detects 402 → selects requirement → signs → retries with PAYMENT-SIGNATURE
var response = await x402Client.GetAsync("https://api.example.com/premium/content");
var content = await response.Content.ReadAsStringAsync();

// Read the settlement result from the PAYMENT-RESPONSE header
if (response.HasPaymentResponse())
{
    var txHash = response.GetTransactionHash();
    var payer = response.GetPayerAddress();
    var success = response.IsPaymentSuccessful();
}
```

All HTTP methods are supported: `GetAsync`, `PostAsync`, `PutAsync`, `DeleteAsync`, `SendAsync`.

If the requested amount exceeds `MaxAmount`, the client throws `X402PaymentExceedsMaximumException` instead of signing.

The client automatically produces the correct payload for the selected requirement's asset-transfer method: EIP-3009 by default, or a Permit2 payload when `extra.assetTransferMethod == "permit2"`.

### Client Payment Policy (allow-list)

The client auto-signs whatever a 402 response demands (up to `MaxAmount`). If you pay many servers — say an AI agent calling third-party APIs — a compromised or malicious server could return a 402 that redirects payment to **its own** wallet, swaps in a **different token**, or moves the payment to **another chain**, all under your amount cap. `Policy` is an allow-list that pins what the client is willing to pay; anything outside it is refused with `X402PaymentPolicyViolationException` **before anything is signed** — no authorization ever leaves the client.

Each list is empty by default (**unrestricted**), so you opt into exactly the constraints you want:

```csharp
var options = new X402HttpClientOptions
{
    PreferredNetwork = "eip155:8453",
    MaxAmount = "1000000"            // ≤ 1 USDC per call (atomic units)
};

// Only ever pay USDC, on Base, to my provider — refuse everything else:
options.Policy.AllowedNetworks.Add("eip155:8453");
options.Policy.AllowedAssets.Add("0x833589fCD6eDb6E08f4c7C32D4f71b54bdA02913"); // USDC on Base
options.Policy.AllowedRecipients.Add("0xMyTrustedProvider");                     // optional

var client = new X402HttpClient(httpClient, payerPrivateKey, options);
```

Leave `AllowedRecipients` empty when you don't know payees ahead of time (the common agent case) but still want to pin the network and asset. Address comparisons are checksum-insensitive; networks are CAIP-2 ids. The policy is enforced together with the `MaxAmount` cap — see [Security](#security).

### Manual Payment Flow

For full control, construct the client with just the payer key and pass explicit `PaymentRequirements` per request:

```csharp
var x402Client = new X402HttpClient(httpClient, payerPrivateKey);

var requirements = new PaymentRequirements
{
    Scheme = "exact",
    Network = "eip155:84532",   // Base Sepolia
    Amount = "1000000",         // $1.00 USDC (6 decimals), atomic units
    Asset = usdcAddress,
    PayTo = "0xReceiverAddress",
    MaxTimeoutSeconds = 60,
    Extra = new ExactSchemeExtra { Name = "USDC", Version = "2" }
};

var response = await x402Client.GetAsync("https://api.example.com/premium", requirements);
```

### Client DI Registration

```csharp
using Nethereum.X402.Extensions;

builder.Services.AddX402Client(
    privateKey: Environment.GetEnvironmentVariable("PAYER_PRIVATE_KEY"));
```

## Server: Protect API Endpoints

Register a facilitator client and add the middleware with route-based payment requirements:

```csharp
using Nethereum.X402.AspNetCore;
using Nethereum.X402.Server;
using Nethereum.X402.Models;

var builder = WebApplication.CreateBuilder(args);

// Register the facilitator client used by the middleware to verify/settle
builder.Services.AddX402Services("https://facilitator.x402.org");

var app = builder.Build();

app.UseX402(options =>
{
    options.Routes.Add(new RoutePaymentConfig("/api/premium/*", new PaymentRequirements
    {
        Scheme = "exact",
        Network = "eip155:8453",   // Base
        Amount = "1000000",        // $1.00 USDC (atomic units)
        Asset = "0x833589fCD6eDb6E08f4c7C32D4f71b54bdA02913",
        PayTo = "0xYourReceiverAddress",
        MaxTimeoutSeconds = 60,
        Extra = new ExactSchemeExtra { AssetTransferMethod = "eip3009", Name = "USDC", Version = "2" }
    }));
});

app.MapGet("/api/premium/content", () => Results.Ok(new { data = "Premium content" }));
app.Run();
```

When a matching request has no `PAYMENT-SIGNATURE`, the middleware returns 402 with a `PaymentRequired` (emitted in the `PAYMENT-REQUIRED` header and the body). When a payment is present, it verifies and settles through the facilitator before forwarding.

### Facilitator-side processors

These register an `IX402PaymentProcessor` that verifies and settles on-chain with only an RPC endpoint per chain id (token, chain and domain come from each requirement). It is consumed by the hosted facilitator controllers (see Hosting a Facilitator). A server protected with `UseX402` still needs `AddX402Services(facilitatorUrl)`; that URL can point at your own facilitator host.

```csharp
using Nethereum.X402.Extensions;

var rpcByChainId = new Dictionary<int, string>
{
    [8453]  = "https://mainnet.base.org",
    [84532] = "https://sepolia.base.org"
};

// EIP-3009 only (facilitator pays gas):
builder.Services.AddX402TransferProcessor(facilitatorPrivateKey, rpcByChainId);

// EIP-3009 AND Permit2 (routes by payload shape) — recommended:
builder.Services.AddX402ExactProcessor(facilitatorPrivateKey, rpcByChainId);

// Receiver-submitted model (receiver pays gas):
builder.Services.AddX402ReceiveProcessor(receiverPrivateKey, rpcByChainId);
```

Each overload also accepts an `IAccount` (for external signers/KMS) or an `IServiceProvider` factory.

## Payment Processors

The `IX402PaymentProcessor` interface defines three operations:

```csharp
public interface IX402PaymentProcessor
{
    Task<VerificationResponse> VerifyPaymentAsync(PaymentPayload paymentPayload, PaymentRequirements requirements, CancellationToken cancellationToken = default);
    Task<SettlementResponse> SettlePaymentAsync(PaymentPayload paymentPayload, PaymentRequirements requirements, CancellationToken cancellationToken = default);
    Task<SupportedPaymentKindsResponse> GetSupportedAsync(CancellationToken cancellationToken = default);
}
```

Implementations:

| Processor | Method | Who Submits | On-chain call |
|-----------|--------|-------------|---------------|
| `X402TransferWithAuthorisation3009Service` | EIP-3009 | Facilitator | `transferWithAuthorization` |
| `X402ReceiveWithAuthorisation3009Service` | EIP-3009 | Receiver | `receiveWithAuthorization` |
| `X402ExactPermit2Service` | Permit2 | Facilitator | `x402ExactPermit2Proxy.settle` |
| `X402ExactSchemeProcessor` | dispatcher | — | routes to EIP-3009 or Permit2 by payload shape |

Each blockchain processor is constructed with a facilitator key (or `IAccount`) and a `Dictionary<int, IClient>` keyed by chain id:

```csharp
using Nethereum.JsonRpc.Client;

var clients = new Dictionary<int, IClient> { [84532] = new RpcClient(new Uri("https://sepolia.base.org")) };
var service = new X402TransferWithAuthorisation3009Service(facilitatorPrivateKey, clients);

var verification = await service.VerifyPaymentAsync(paymentPayload, requirements);
if (verification.IsValid)
{
    var settlement = await service.SettlePaymentAsync(paymentPayload, requirements);
    Console.WriteLine($"TX: {settlement.Transaction}, Payer: {settlement.Payer}, Amount: {settlement.Amount}");
}
```

`VerifyPaymentAsync` binds the signed authorization to the route requirement (recipient must equal `PayTo`, value must equal `Amount`) before trusting it, then checks the signature, balance, nonce and time window. Signature verification falls back to ERC-1271 when the payer is a contract wallet. `SettlePaymentAsync` dry-runs the settlement (`eth_estimateGas`) before broadcasting. Each processor constructor also accepts an optional `ILogger` for the swallowed verify/settle exceptions. See [Security](#security) for details.

The **ReceiveWithAuthorization** variant additionally requires the authorization's `To` to equal the receiver and settles via `receiveWithAuthorization` (only the designated receiver can submit).

## Permit2 Asset-Transfer Method

The v2 "exact" scheme supports two EVM asset-transfer methods: the default **EIP-3009**
(`transferWithAuthorization`) and **permit2**. Permit2 works with any ERC-20 the payer has approved to the
canonical [Permit2](https://github.com/Uniswap/permit2) contract (no EIP-3009 support needed in the token).
The payer signs a Permit2 `PermitWitnessTransferFrom` naming the **x402ExactPermit2Proxy** as spender and the
route recipient as the witness; the facilitator settles by calling `proxy.settle`, which enforces that funds
can only go to that recipient.

A route selects permit2 by setting `extra.assetTransferMethod = "permit2"`:

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
```

- **Client** — `X402HttpClient` automatically signs and emits a permit2 payload when the selected requirement
  declares `extra.assetTransferMethod == "permit2"` (no extra configuration).
- **Facilitator** — `X402ExactPermit2Service` verifies the payment (binds spender = proxy, `witness.to` = `payTo`,
  exact amount and token, deadline/validAfter window, EIP-712 signer recovery, and the on-chain prerequisites:
  proxy deployed, payer balance, Permit2 allowance) and settles via `x402ExactPermit2Proxy.settle`. When a
  settlement reverts, the Permit2 custom error is decoded — via the code-generated Permit2 error collection
  (`Permit2Service.FindCustomError`) — into the specific reason (`InvalidNonce` → `invalid_exact_evm_nonce_already_used`,
  `SignatureExpired` → `permit2_deadline_expired`, `AllowanceExpired`/`InsufficientAllowance` → `permit2_allowance_required`,
  the signature errors → `invalid_permit2_signature`, `InvalidAmount` → `permit2_amount_mismatch`) instead of a
  generic failure. Decoding happens at the pre-settle simulation, and for a mined revert a best-effort
  transaction replay recovers the reason (subject to archive-state availability).
- **Dispatch** — `X402ExactSchemeProcessor` routes to the permit2 processor when the payload carries a
  `permit2Authorization`, otherwise to the EIP-3009 processor. Register both with `AddX402ExactProcessor`.

The Permit2 and proxy addresses are canonical constants (`X402Permit2Addresses`), the same on every EVM chain.
The underlying Permit2 signing primitives live in `Nethereum.Signer.EIP712` / `Nethereum.ABI`, and the
contract in `Nethereum.Contracts` (`web3.Eth.GetPermit2Service()`).

## Authorization Building (EIP-3009)

`TransferWithAuthorisationBuilder` and `ReceiveWithAuthorisationBuilder` create `Authorization` objects from payment requirements:

```csharp
using Nethereum.X402.Signers;

var builder = new TransferWithAuthorisationBuilder();

// Build from payment requirements (auto-generates nonce, sets the time window)
var authorization = builder.BuildFromPaymentRequirements(requirements, fromAddress: payerAddress);

var signer = new TransferWithAuthorisationSigner();
var signature = await signer.SignWithPrivateKeyAsync(
    authorization, "USDC", "2", chainId, usdcAddress, payerPrivateKey);
```

The EIP-712 signing domain comes from the requirement: the chain id from its CAIP-2 network, the verifying contract from `asset`, and the domain name/version from `extra`. `SignWithWeb3Async` supports external signers (hardware wallets, KMS).

## Facilitator

A facilitator verifies and settles payments on behalf of API servers, so servers need no blockchain infrastructure.

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

// Register a processor (EIP-3009 + Permit2 here)
builder.Services.AddX402ExactProcessor(facilitatorAccount, rpcByChainId);

builder.Services.AddControllers().AddX402FacilitatorControllers();

var app = builder.Build();
app.MapControllers();
app.Run();
```

Endpoints:
- `POST /facilitator/verify` — Verify a payment
- `POST /facilitator/settle` — Settle a payment on-chain
- `GET /facilitator/supported` — List supported payment kinds

## Security

### Client payment allow-list

`X402HttpClient` auto-signs whatever a 402 demands up to `MaxAmount`; `X402HttpClientOptions.Policy`
adds a network/asset/recipient allow-list so a compromised server cannot redirect the payment, swap
the asset, or switch chains. Refused before signing. See [Client Payment Policy](#client-payment-policy-allow-list).

### Facilitator endpoint authentication is a hosting responsibility

`AddX402FacilitatorControllers` exposes `/facilitator/verify`, `/facilitator/settle` and
`/facilitator/supported` as open endpoints. The facilitator holds the key that pays gas and submits
settlements, so the **host must protect these endpoints** — API keys, mTLS, a network ACL, or an
allow-list of resource-server origins — as it would any privileged internal API. The library does
not bake in an auth scheme by design; apply your standard ASP.NET Core authentication/authorization
to the controllers, or place them behind a gateway.

### Replay protection (serve-before-settle)

The middleware serves the protected resource before settlement confirms on-chain, so the same signed
payment must not drive the endpoint more than once. After verify and before running the endpoint,
`X402Middleware` reserves the payment identity (payer + nonce) via `IPaymentReplayStore`; a replay is
rejected with 402 / `invalid_exact_evm_nonce_already_used` before the endpoint runs. On by default
(`X402Options.EnablePaymentReplayProtection`, with `PaymentReplayRetention`).

The default `InMemoryPaymentReplayStore` is single-instance. For a horizontally-scaled deployment,
register a distributed `IPaymentReplayStore` (e.g. Redis-backed) so reservations are shared:

```csharp
builder.Services.AddSingleton<IPaymentReplayStore, MyRedisPaymentReplayStore>();
```

### Pre-settle simulation

Before broadcasting, the EIP-3009 and Permit2 facilitators `eth_estimateGas` the settlement call. A
token that would revert the transfer for a reason verify cannot see (a paused or blocklisting token,
a fee-on-transfer shortfall) is rejected with `invalid_exact_evm_transaction_simulation_failed`
instead of the facilitator burning gas on a doomed transaction.

### ERC-1271 smart-contract wallets

When plain ECDSA recovery does not match the payer, the facilitators fall back to ERC-1271: if the
payer address holds contract code, the wallet's `isValidSignature` is consulted. On the Permit2 path
this settles end-to-end, because the canonical Permit2 contract performs the same ERC-1271 check
on-chain. (Standard EIP-3009 tokens validate `transferWithAuthorization` with `ecrecover` only, so a
contract-wallet payment on the raw EIP-3009 path is accepted at verify but needs an ERC-1271-aware
token to settle.)

## Network Configuration

`NetworkConfiguration` provides pre-configured RPC endpoints, USDC addresses and chain ids for common networks:

```csharp
using Nethereum.X402.Blockchain;

var config = NetworkConfiguration.Default;
var rpcUrl = config.GetRpcEndpoint("base");
var usdcAddress = config.GetUSDCAddress("base");
var chainId = config.GetChainId("base");
```

CAIP-2 network ids are parsed/formatted with `Caip2` (`Caip2.FormatEip155(chainId)`, `Caip2.TryParseEip155ChainId(network, out chainId)`).

## Models

### PaymentRequirements

A single accepted payment option:

```csharp
public class PaymentRequirements
{
    public string Scheme { get; set; }            // "exact"
    public string Network { get; set; }           // CAIP-2, e.g. "eip155:8453"
    public string Amount { get; set; }            // atomic units (string)
    public string Asset { get; set; }             // token contract address
    public string PayTo { get; set; }             // receiver address
    public int MaxTimeoutSeconds { get; set; }
    public object? Extra { get; set; }            // ExactSchemeExtra
}
```

### ExactSchemeExtra

Scheme-specific data carried in `PaymentRequirements.Extra`:

```csharp
public class ExactSchemeExtra
{
    public string? AssetTransferMethod { get; set; }  // "eip3009" (default) or "permit2"
    public string Name { get; set; }                  // EIP-712 domain name (EIP-3009 / EIP-2612 path)
    public string Version { get; set; }               // EIP-712 domain version
}
```

### PaymentRequired

The 402 body (and `PAYMENT-REQUIRED` header):

```csharp
public class PaymentRequired
{
    public int X402Version { get; set; } = 2;
    public string? Error { get; set; }
    public ResourceInfo Resource { get; set; }        // { Url, Description?, MimeType? }
    public List<PaymentRequirements> Accepts { get; set; }
    public object? Extensions { get; set; }
}
```

### PaymentPayload

The client's signed payment (`PAYMENT-SIGNATURE` header):

```csharp
public class PaymentPayload
{
    public int X402Version { get; set; } = 2;
    public ResourceInfo? Resource { get; set; }
    public PaymentRequirements Accepted { get; set; }  // the chosen requirement
    public object Payload { get; set; }                // ExactSchemePayload or Permit2SchemePayload
    public object? Extensions { get; set; }
}

// EIP-3009 payload
public class ExactSchemePayload
{
    public string Signature { get; set; }
    public Authorization Authorization { get; set; }
}

// Permit2 payload (detected by the presence of Permit2Authorization)
public class Permit2SchemePayload
{
    public string Signature { get; set; }
    public Permit2Authorization Permit2Authorization { get; set; }  // from, permitted, spender, nonce, deadline, witness
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
    public string? ErrorReason { get; set; }    // X402ErrorCodes value
    public string Transaction { get; set; }     // TX hash
    public string Network { get; set; }
    public string Payer { get; set; }
    public string? Amount { get; set; }
    public object? Extensions { get; set; }
}
```

## Error Codes

`X402ErrorCodes` defines the standard reason strings (matching the coinbase/x402 reference). Selected values:

| Code | Meaning |
|------|---------|
| `invalid_exact_evm_insufficient_balance` | Payer doesn't have enough tokens |
| `invalid_exact_evm_signature` | EIP-712 signature verification failed |
| `invalid_exact_evm_authorization_value` | Amount below the requirement |
| `invalid_exact_evm_recipient_mismatch` | Recipient doesn't match `PayTo` |
| `invalid_exact_evm_nonce_already_used` | Nonce already consumed (also returned for a rejected replay) |
| `invalid_exact_evm_transaction_simulation_failed` | Settlement dry-run (`eth_estimateGas`) reverted |
| `invalid_network` | Unsupported / unconfigured network |
| `unsupported_payload_type` | Scheme not supported |
| `invalid_payment_requirements` | Requirement missing/invalid (e.g. no `extra`) |
| `invalid_permit2_spender` | Permit2 spender is not the x402ExactPermit2Proxy |
| `invalid_permit2_recipient_mismatch` | Permit2 witness `to` doesn't match `PayTo` |
| `permit2_amount_mismatch` / `permit2_token_mismatch` | Permit2 amount/token doesn't match the requirement |
| `permit2_allowance_required` | Payer hasn't approved Permit2 for the asset |
| `invalid_permit2_signature` | Permit2 EIP-712 signature verification failed |

## Payment Flow (v2)

```
1. Client → Server:   GET /api/premium (no PAYMENT-SIGNATURE)
2. Server → Client:   402 + PaymentRequired (PAYMENT-REQUIRED header + body)
3. Client:            Selects a requirement, signs it off-chain (EIP-3009 or Permit2), no gas
4. Client → Server:   GET /api/premium + PAYMENT-SIGNATURE (base64 PaymentPayload)
5. Server/Facilitator: Verifies signature, binding, balance, time window
6. Server/Facilitator: Settles on-chain (transferWithAuthorization / receiveWithAuthorization / proxy.settle)
7. Server → Client:   200 OK + content + PAYMENT-RESPONSE (base64 SettlementResponse)
```

## Interoperability

The v2 wire format and signatures are validated against the coinbase/x402 TypeScript reference implementation in both directions — our client's payloads verify and settle on the reference facilitator, and reference-client payloads verify and settle through our facilitator — for both the EIP-3009 and Permit2 methods.

## References

- [x402 Protocol (coinbase/x402)](https://github.com/coinbase/x402)
- [EIP-3009: Transfer With Authorization](https://eips.ethereum.org/EIPS/eip-3009)
- [Uniswap Permit2](https://github.com/Uniswap/permit2)
- [CAIP-2: Blockchain ID Specification](https://standards.chainagnostic.org/CAIPs/caip-2)
