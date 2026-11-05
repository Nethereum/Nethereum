# Nethereum.X402

A .NET implementation of the [x402 protocol](https://github.com/coinbase/x402) (protocol **version 2**) for accepting HTTP 402 (Payment Required) stablecoin payments with signed, gasless token authorizations.

## Overview

The x402 protocol adds a payment layer to HTTP APIs. When a client requests a protected resource, the server returns HTTP 402 with payment requirements. The client signs a payment authorization off-chain (no gas), retries the request with a signed payment header, and the server or a facilitator settles the transfer on-chain.

Nethereum.X402 implements the x402 **v2** "exact" scheme with two EVM asset-transfer methods and provides:

- **`X402HttpClient`** — Client that handles the 402 flow automatically (detect → sign → retry)
- **`X402Middleware`** — ASP.NET Core middleware for protecting API endpoints
- **Two EIP-3009 payment processors** — TransferWithAuthorization (facilitator submits) and ReceiveWithAuthorization (receiver submits)
- **Permit2 asset-transfer method** — the "exact" scheme settled via the canonical Permit2 + x402ExactPermit2Proxy, for any ERC-20 (see [Permit2 Asset-Transfer Method](#permit2-asset-transfer-method))
- **Upfront payment flow** — a route can settle before its handler runs (`extra.paymentFlow = "upfront"`), see [Upfront Payment Flow](#upfront-payment-flow)
- **Gas-sponsoring extensions** — `eip2612GasSponsoring` and `erc20ApprovalGasSponsoring` let a payer who has never approved Permit2 pay without sending an approval transaction, see [Permit2 Gas Sponsoring](#permit2-gas-sponsoring)
- **Facilitator support** — Verify/settle locally, or proxy through a third-party facilitator service
- **Multi-chain support** — networks are identified by CAIP-2 ids (`eip155:<chainId>`); only an RPC endpoint per chain is configured

> **Protocol version 2.** This library targets x402 v2 only. Payments carry `x402Version = 2`, the chosen requirement is echoed in the payload's `accepted` field, and networks use CAIP-2 identifiers (e.g. `eip155:8453` for Base). The [Interoperability](#interoperability) section lists the scenarios in which the reference clients and facilitators are run against this library.

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
| `PAYMENT-RESPONSE` | 200 response (every handler response in the upfront flow), or 402 when settlement failed | `SettlementResponse` (on-chain result) |

The `PaymentRequired` body is also returned in the 402 response body as a fallback. The decoders for all three headers accept standard base64 with or without trailing `=` padding and reject anything that is not base64.

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

The signed authorization expires at now plus the smaller of the requirement's `MaxTimeoutSeconds` and `X402HttpClientOptions.MaxAuthorizationValiditySeconds` (`int`, 3600 by default, must be positive): `validBefore` for EIP-3009 and `deadline` for Permit2. A server cannot make the client sign a long-lived authorization, and the manual flow applies the same default cap.

`X402HttpClientOptions.MaxResponseBytes` (`int`, 1 MiB by default, must be positive) bounds how much of a 402 response the automatic client reads: a larger `Content-Length`, body or `PAYMENT-REQUIRED` header fails the request with `InvalidDataException`.

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

Set `Policy.AllowedAssets` whenever you can: without it the client signs for whichever token the server names. Leave `AllowedRecipients` empty when you don't know payees ahead of time (the common agent case) but still want to pin the network and asset. Address comparisons are checksum-insensitive; networks are CAIP-2 ids. The policy is enforced together with the `MaxAmount` cap — see [Security](#security).

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

### Choosing a Requirement

When a 402 offers several `accepts`, the client asks `X402HttpClientOptions.Selector` which one to pay. The default is `DefaultPaymentRequirementsSelector`; replace it with your own `IPaymentRequirementsSelector`:

```csharp
public interface IPaymentRequirementsSelector
{
    PaymentRequirements SelectRequirements(
        IEnumerable<PaymentRequirements> availableRequirements,
        string preferredNetwork,
        string preferredScheme);
}
```

`DefaultPaymentRequirementsSelector` keeps only the requirements this library supports (scheme `exact`, flow `authorization` or `upfront`, method `eip3009` or `permit2`) whose network and scheme equal `PreferredNetwork` and `PreferredScheme` (case-insensitive), and returns the first with the `authorization` flow, otherwise the first remaining one. It throws `InvalidOperationException` when the list is empty ("No payment requirements available") or nothing matches ("No payment requirements found for network '…' and scheme '…'…"). `X402HttpClientOptions.Validate` requires `PreferredNetwork` and a positive-integer `MaxAmount`; `PreferredScheme` defaults to `"exact"`.

### Client DI Registration

```csharp
using Nethereum.X402.Extensions;

builder.Services.AddX402Client(
    privateKey: payerPrivateKey);
```

`AddX402Client(privateKey)` (and the `IConfigurationSection` overload, which reads the `PrivateKey` key) registers a transient `X402HttpClient` built with `new X402HttpClient(httpClient, privateKey)`: the **manual** mode. Its `GetAsync(uri)`, `PostAsync`, `PutAsync`, `DeleteAsync` and `SendAsync` throw `InvalidOperationException` ("This method requires automatic payment mode…"); only `GetAsync(uri, requirements)` works. There is no DI registration for the automatic mode: construct the client yourself with `new X402HttpClient(httpClient, payerPrivateKey, options)` (see [Automatic Payment Flow](#automatic-payment-flow)).

## Server: Protect API Endpoints

Register a facilitator client and add the middleware with route-based payment requirements:

```csharp
using Nethereum.X402.AspNetCore;
using Nethereum.X402.Server;
using Nethereum.X402.Models;

var builder = WebApplication.CreateBuilder(args);

// Register the facilitator client used by the middleware to verify/settle
builder.Services.AddX402Services("https://x402.org/facilitator");

var app = builder.Build();

app.UseX402(options =>
{
    options.FacilitatorUrl = "https://x402.org/facilitator";
    options.Routes.Add(new RoutePaymentConfig("/api/premium/*", new PaymentRequirements
    {
        Scheme = "exact",
        Network = "eip155:8453",   // Base
        Amount = "1000000",        // $1.00 USDC (atomic units)
        Asset = "0x833589fCD6eDb6E08f4c7C32D4f71b54bdA02913",
        PayTo = "0xYourReceiverAddress",
        MaxTimeoutSeconds = 60,
        Extra = new ExactSchemeExtra { AssetTransferMethod = "eip3009", Name = "USD Coin", Version = "2" }
    }));
});

app.MapGet("/api/premium/content", () => Results.Ok(new { data = "Premium content" }));
app.Run();
```

When a matching request has no `PAYMENT-SIGNATURE`, the middleware returns 402 with a `PaymentRequired` (emitted in the `PAYMENT-REQUIRED` header and the body). When a payment is present the behaviour depends on the route's [payment flow](#upfront-payment-flow). In the default `authorization` flow the middleware verifies through the facilitator, reserves the payment identity for [replay protection](#replay-protection-serve-before-settle), runs the endpoint with its response buffered, and settles only when the endpoint returned a status below 400; an endpoint response of 400 or above is returned as-is without settlement. A successful settlement adds `PAYMENT-RESPONSE` to the response and makes its `Cache-Control` private: `public` and `s-maxage` directives the endpoint set are removed, a qualified `private="..."` directive is removed, a bare `private` is added and the other directives are kept; a failed one replaces it with a 402. In the `upfront` flow it settles first and never verifies.

A route can offer several options at once: set `RoutePaymentConfig.Accepts` to a list of `PaymentRequirements` (for example one `eip3009` and one `permit2` option) and the 402's `accepts` carries the whole list; without `Accepts` the single `Requirements` is the one offer. A payment is accepted only when its `accepted` equals one of the offers, compared as JSON values (key order ignored, `extra` and unknown properties included). The matching offer, not the payload's copy, is what the middleware sends to verify and settle and what replay protection is keyed on. `X402FacilitatorProxyProcessor.FindMatchingRequirements(route, payment)` does that lookup. `PaymentRequirements` and `ResourceInfo` keep properties they do not model in `ExtensionData`, and `X402HttpClient` echoes `accepted` and `resource` unchanged, unknown properties included.

### Failure Responses and Response Buffering

When the request is rejected before the endpoint runs, the 402, 400 and 412 responses carry a `PaymentRequired` body whose `error` is the reason, with `Cache-Control: no-store`; the 500 returns only the JSON body shown and the 503 returns its JSON body with `Cache-Control: no-store`:

| Status | When | Reason |
|--------|------|--------|
| 402 | No `PAYMENT-SIGNATURE` header | `payment_required` |
| 402 | The payment's `accepted` does not equal any of the route's offers (every field, `extra` and unknown fields included, compared as JSON values with key order ignored); no facilitator call is made | `No matching payment requirements` (`X402ErrorMessages.NoMatchingPaymentRequirements`) |
| 402 | Verification failed | the processor's `InvalidReason` (`unexpected_verify_error` if it has none) |
| 402 | The same payment is replayed | `invalid_exact_evm_nonce_already_used` |
| 400 | Replay protection is on and no payment identity can be derived from the payload | `invalid_payload` |
| 400 | The header is not valid base64 (unpadded is accepted) / JSON, the decoded JSON repeats a property name at any depth (case-insensitively for the payment, `accepted`, `payload`, `resource` and the objects inside them; exactly, case-sensitively, inside `extensions`, `extra` and unknown members), or verification reports `invalid_payload` | `invalid_payload` |
| 412 | Verification reports `permit2_allowance_required` | `permit2_allowance_required` |
| 500 | The facilitator's verify call throws | body `{"error":"facilitator_error"}` |
| 503 | The replay store is full (`PaymentReplayStoreFullException`) | body `{"error":"replay_store_full"}`, `Cache-Control: no-store`; the endpoint does not run |

A settlement failure returns 402 with the failed `SettlementResponse` in `PAYMENT-RESPONSE`.

In the `authorization` flow the middleware buffers the endpoint's response until settlement succeeds. `X402Options.MaxBufferedResponseBytes` (`long`, default 1 MiB) caps that buffer; when the endpoint writes more, the middleware returns 500 and does not settle. The `upfront` flow does not buffer: the handler's response streams straight through after settlement.

### Upfront Payment Flow

By default a route uses the `authorization` flow: the middleware verifies the payment, runs your endpoint, and settles on-chain only when the endpoint returned a success status. A route can instead declare `extra.paymentFlow = "upfront"` (`X402PaymentFlows.Upfront`) to settle **before** the handler runs. Omitting `paymentFlow` means `authorization` (`X402PaymentFlows.Authorization`). Both flows are supported for the EIP-3009 and Permit2 asset-transfer methods; any other `paymentFlow` value makes `X402Options.Validate` throw `InvalidOperationException` ("paymentFlow '…' is not supported; this middleware implements the authorization and upfront flows.").

```csharp
using Nethereum.X402.AspNetCore;
using Nethereum.X402.Models;
using Nethereum.X402.Server;

app.UseX402(options =>
{
    options.FacilitatorUrl = "https://x402.org/facilitator";
    options.Routes.Add(new RoutePaymentConfig("/api/generate", new PaymentRequirements
    {
        Scheme = "exact",
        Network = "eip155:84532",
        Amount = "10000",
        Asset = usdcAddress,
        PayTo = receiverAddress,
        MaxTimeoutSeconds = 60,
        Extra = new ExactSchemeExtra
        {
            AssetTransferMethod = X402AssetTransferMethods.Eip3009,
            PaymentFlow = X402PaymentFlows.Upfront,
            Name = "USDC",
            Version = "2"
        }
    }));
});
```

For an upfront route the middleware:

- verifies the payment through the facilitator first, exactly as the authorization flow does (one extra facilitator round trip), and only then reserves the payment identity and settles; an invalid or failing verify is answered as in the authorization flow and reserves nothing;
- returns `402` with a `PAYMENT-RESPONSE` header (`Cache-Control: no-store`, body `{}`) when settlement fails, or when it throws (`settlement_pending`: the outcome is unknown, so the replay reservation is kept until `PaymentReplayRetention` expires; an `HttpRequestException` that provably never reached the facilitator, with `HttpRequestError` `ConnectionError` or `NameResolutionError` found anywhere in its inner chain (a socket error inside a mid-response read failure does not count), is a definite failure, `unexpected_settle_error`, in both flows and releases it), and the handler does not run;
- on success runs the handler, and every response it produces, including a handler `4xx`/`5xx`, carries the `PAYMENT-RESPONSE` header and a `Cache-Control` that includes `private`;
- still applies [replay protection](#replay-protection-serve-before-settle): the payment identity is reserved after verification and before settling, so a replayed payment is rejected with `invalid_exact_evm_nonce_already_used` and is not settled again.

Because the payer has already been charged when the handler runs, a handler failure does not undo the settlement. The `402` response advertises the flow in each `accepts[].extra.paymentFlow`.

**Client** — no configuration is needed. The default requirement selector only considers requirements this library supports (`exact`, flow `authorization` or `upfront`, method `eip3009` or `permit2`), so a requirement with any other flow is skipped and never paid. When a server offers both flows for the same network and scheme the client picks the `authorization` one; when only `upfront` is offered it pays that.

### Facilitator-side processors

These register an `IX402PaymentProcessor` that verifies and settles on-chain with only an RPC endpoint per chain id (token, chain and domain come from each requirement). It is consumed by the hosted facilitator controllers (see [Host a Facilitator](#host-a-facilitator)). A server protected with `UseX402` still needs `AddX402Services(facilitatorUrl)`; that URL can point at your own facilitator host.

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
| `X402ExactPermit2Service` | Permit2 | Facilitator | `x402ExactPermit2Proxy.settle` (`settleWithPermit` with EIP-2612 sponsoring) |
| `X402ExactSchemeProcessor` | dispatcher | — | routes to EIP-3009 or Permit2 by the requirement's `assetTransferMethod`; a payload of the other shape is rejected with `unsupported_payload_type` |

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

`VerifyPaymentAsync` binds the signed authorization to the route requirement (recipient must equal `PayTo`, value must equal `Amount`) before trusting it, then checks the signature, balance, nonce and time window. Signature verification falls back to ERC-1271 when the payer is a contract wallet. It then dry-runs the settlement (`eth_estimateGas`), so a transfer that would revert is rejected at verify. `SettlePaymentAsync` verifies first and then broadcasts. Each processor constructor also accepts an optional `ILogger` for the swallowed verify/settle exceptions. See [Security](#security) for details.

The **ReceiveWithAuthorization** variant additionally requires the authorization's `To` to equal the receiver and settles via `receiveWithAuthorization` (only the designated receiver can submit).

`X402TransferWithAuthorisation3009Service` can also void a signed but unsettled authorization by submitting EIP-3009 `cancelAuthorization` from the facilitator account. The token requires the **authorizer's own EIP-712 `CancelAuthorization` signature** over `(authorizer, nonce)`, so the authorizer signs first (`TransferWithAuthorisationSigner.SignCancelWithPrivateKeyAsync`, or `SignCancelWithWeb3Async` to sign through a Web3 account, both returning an `EthECDSASignature`; `CancelAuthorisationBuilder.GetTypedDataForCancellation` builds the typed data) and passes the hex signature:

```csharp
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.X402.Signers;

var signer = new TransferWithAuthorisationSigner();
var nonce = authorization.Nonce.HexToByteArray();

var cancelSignature = EthECDSASignature.CreateStringSignature(
    await signer.SignCancelWithPrivateKeyAsync(
        payerAddress, nonce, "USDC", "2", chainId, usdcAddress, payerPrivateKey));

var cancel = await service.CancelAuthorizationAsync(
    payerAddress, nonce, cancelSignature, "eip155:84532", usdcAddress);
```

```csharp
public Task<CancelAuthorizationResponse> CancelAuthorizationAsync(
    string authorizerAddress,
    byte[] nonce,
    string authorizerSignature,
    string network,
    string tokenAddress,
    CancellationToken cancellationToken = default)

public Task<EthECDSASignature> SignCancelWithPrivateKeyAsync(
    string authorizer,
    byte[] nonce,
    string tokenName,
    string tokenVersion,
    BigInteger chainId,
    string verifyingContract,
    string privateKey)

public class CancelAuthorizationResponse
{
    public bool Success { get; set; }
    public string ErrorReason { get; set; }
    public string Transaction { get; set; }
    public string Network { get; set; }
}
```

The cancellation is simulated first. `ErrorReason` is `invalid_network` when the network is not configured, `invalid_transaction_state` when the simulation reverts (for example a signature from another key) or the transaction is mined with a failed status, and `cancellation_error` for any other exception. Once cancelled, the authorization can no longer be verified or settled.

The older overload `CancelAuthorizationAsync(authorizerAddress, nonce, network, tokenAddress, cancellationToken)` is `[Obsolete]`: it sends a zero signature, which a conformant token always rejects, so it can never succeed. Use the overload above. The cancellation is exercised against a dev chain token and, in `Permit2/X402Eip3009CancelForkTests.cs`, against real Base Sepolia USDC.

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
  proxy deployed, payer balance, Permit2 allowance) and settles via `x402ExactPermit2Proxy.settle`. The Permit2
  allowance is required unless the payment carries a [gas-sponsoring extension](#permit2-gas-sponsoring) that
  supplies it, in which case the facilitator settles via `x402ExactPermit2Proxy.settleWithPermit` (EIP-2612) or
  relays the payer's signed approval first (ERC-20 approval). When a settlement reverts, the Permit2 custom error is decoded — via the code-generated Permit2 error collection
  (`Permit2Service.FindCustomError`) — into the specific reason (`InvalidNonce` → `permit2_invalid_nonce`,
  `SignatureExpired` → `permit2_deadline_expired`, `AllowanceExpired`/`InsufficientAllowance` → `permit2_allowance_required`,
  the signature errors → `invalid_permit2_signature`, `InvalidAmount` → `permit2_invalid_amount`, `InvalidDestination` → `permit2_invalid_destination`,
  `InvalidOwner` → `permit2_invalid_owner`, `PaymentTooEarly` → `permit2_payment_too_early`,
  `Permit2612AmountMismatch` → `permit2_2612_amount_mismatch`) instead of a
  generic failure. Decoding happens at the pre-settle simulation, and for a mined revert a best-effort
  transaction replay recovers the reason (subject to archive-state availability).
- **Dispatch** — `X402ExactSchemeProcessor` routes by the requirement's `assetTransferMethod` (permit2 to the permit2 processor, otherwise the EIP-3009 processor), and rejects with `unsupported_payload_type` a payload that carries a
  `permit2Authorization` for an EIP-3009 requirement or no `permit2Authorization` for a Permit2 one. Register both with `AddX402ExactProcessor`.

The Permit2 and proxy addresses are canonical constants (`X402Permit2Addresses`), the same on every EVM chain.
The underlying Permit2 signing primitives live in `Nethereum.Signer.EIP712` / `Nethereum.ABI`, and the
contract in `Nethereum.Contracts` (`web3.Eth.GetPermit2Service()`).

## Permit2 Gas Sponsoring

Permit2 needs the payer to have approved the token to the Permit2 contract once, which costs gas. Two x402 extensions let the facilitator cover that step so a payer holding only the token can still pay. Both apply to the permit2 method only and are declared by the server in the `PaymentRequired.Extensions` of the 402 (body and `PAYMENT-REQUIRED` header) through `RoutePaymentConfig.Extensions`. The extension keys are `X402ExtensionKeys.Eip2612GasSponsoring` (`"eip2612GasSponsoring"`) and `X402ExtensionKeys.Erc20ApprovalGasSponsoring` (`"erc20ApprovalGasSponsoring"`). Both are used only while the payer's Permit2 allowance is below the payment amount.

### eip2612GasSponsoring

For tokens that implement EIP-2612 `permit`, the payer signs a permit for Permit2 and the facilitator submits it together with the payment through `x402ExactPermit2Proxy.settleWithPermit`.

**Server** — declare the extension on the route with `X402GasSponsoringExtensions.DeclareEip2612()`. The route's `extra` must carry the token's EIP-712 `Name` and `Version`, which the client needs to sign the permit:

```csharp
using Nethereum.X402.AspNetCore;
using Nethereum.X402.Models;
using Nethereum.X402.Server;

app.UseX402(options =>
{
    options.FacilitatorUrl = "https://x402.org/facilitator";
    options.Routes.Add(new RoutePaymentConfig("/api/premium-permit2", new PaymentRequirements
    {
        Scheme = "exact",
        Network = "eip155:84532",
        Amount = "10000",
        Asset = usdcAddress,
        PayTo = receiverAddress,
        MaxTimeoutSeconds = 60,
        Extra = new ExactSchemeExtra
        {
            AssetTransferMethod = X402AssetTransferMethods.Permit2,
            Name = "USDC",
            Version = "2"
        }
    })
    {
        Extensions = new Dictionary<string, object>
        {
            [X402ExtensionKeys.Eip2612GasSponsoring] = X402GasSponsoringExtensions.DeclareEip2612()
        }
    });
});
```

**Client** — give `X402HttpClientOptions.RpcClientsByChainId` (`Dictionary<int, IClient>?`, null by default) an RPC client for the payment's chain. The RPC is used only for sponsoring:

```csharp
using Nethereum.JsonRpc.Client;
using Nethereum.X402.Client;

var httpClient = new HttpClient();
var options = new X402HttpClientOptions
{
    MaxAmount = "100000",
    PreferredNetwork = "eip155:84532",
    PreferredScheme = "exact",
    RpcClientsByChainId = new Dictionary<int, IClient>
    {
        [84532] = new RpcClient(new Uri("https://sepolia.base.org"))
    }
};

var x402Client = new X402HttpClient(httpClient, payerPrivateKey, options);
```

The client attaches a signed permit (`Eip2612GasSponsoringInfo`) only when the selected requirement is permit2, the server declared the extension, `extra.name` and `extra.version` are present, an RPC client is configured for the chain, and the payer's Permit2 allowance is below the amount (an allowance read that fails counts as not covered). The permit's `value` equals the payment amount, its spender is the canonical Permit2 and its deadline is the Permit2 deadline. The attached extension is the server's declaration with the client's `info` fields added: values the server declared win, and the client only adds missing fields (nested objects are merged field by field).

**Facilitator** — `X402ExactPermit2Service` advertises the extension in `GetSupportedAsync().Extensions` on every instance, and `X402ExactSchemeProcessor` lists it once:

```csharp
using Nethereum.JsonRpc.Client;
using Nethereum.X402.Blockchain;

var clients = new Dictionary<int, IClient>
{
    [84532] = new RpcClient(new Uri("https://sepolia.base.org"))
};
var permit2Service = new X402ExactPermit2Service(facilitatorPrivateKey, clients);

var supported = await permit2Service.GetSupportedAsync();
```

When the payer's Permit2 allowance is below the amount, verification checks the extension and simulates `settleWithPermit`; settlement then uses `settleWithPermit`. When the allowance already covers the amount the extension is ignored and the normal `settle` is used, so a malformed extension next to a sufficient allowance is not rejected. Rejections:

| Code | Cause |
|------|-------|
| `permit2_allowance_required` | allowance short and no complete `eip2612GasSponsoring` info (also when the permit signature is invalid and there is no prior allowance) |
| `invalid_eip2612_extension_format` | a field does not match the declared schema patterns |
| `eip2612_from_mismatch` / `eip2612_asset_mismatch` | `from` is not the payer / `asset` is not the payment token |
| `eip2612_spender_not_permit2` | `spender` is not the canonical Permit2 |
| `eip2612_deadline_expired` | `deadline` is less than six seconds from now |
| `permit2_2612_amount_mismatch` | the permit `value` differs from the permitted amount |

### erc20ApprovalGasSponsoring

For tokens without EIP-2612, the payer pre-signs an EIP-1559 `approve(Permit2, MaxUint256)` transaction and the facilitator broadcasts it, funding the payer's gas if needed. Because the facilitator spends its own ETH, this is **opt-in** and capped.

**Server** — declare it with `X402GasSponsoringExtensions.DeclareErc20Approval()`:

```csharp
using Nethereum.X402.AspNetCore;
using Nethereum.X402.Models;
using Nethereum.X402.Server;

app.UseX402(options =>
{
    options.FacilitatorUrl = "https://x402.org/facilitator";
    options.Routes.Add(new RoutePaymentConfig("/api/premium-permit2", new PaymentRequirements
    {
        Scheme = "exact",
        Network = "eip155:84532",
        Amount = "10000",
        Asset = usdcAddress,
        PayTo = receiverAddress,
        MaxTimeoutSeconds = 60,
        Extra = new ExactSchemeExtra { AssetTransferMethod = X402AssetTransferMethods.Permit2 }
    })
    {
        Extensions = new Dictionary<string, object>
        {
            [X402ExtensionKeys.Erc20ApprovalGasSponsoring] = X402GasSponsoringExtensions.DeclareErc20Approval()
        }
    });
});
```

**Client** — the same `RpcClientsByChainId` option. The client signs the approval only when the server declared the extension, an RPC client is configured for the chain, the Permit2 allowance is below the amount, and the eip2612 permit was not attached (eip2612 takes precedence). The transaction is `approve(Permit2, MaxUint256)` on the asset, gas limit 70000, nonce = the payer's pending transaction count, and `maxPriorityFeePerGas` from the node's `eth_maxPriorityFeePerGas` clamped to between 1 wei and 100000000 wei, and `maxFeePerGas` of 1.5 times the latest base fee (rounded up) plus that priority (falling back to a `maxFeePerGas` of 1 gwei and `maxPriorityFeePerGas` of 0.1 gwei when the node cannot suggest). The result is an `Erc20ApprovalGasSponsoringInfo` merged into the payload extensions like the permit. The approved value is `MaxUint256` unless `X402HttpClientOptions.CapSponsoredApprovalToPaymentAmount` (`bool`, `false` by default) is `true`, in which case it is the requirement's amount.

**Facilitator** — opt in with `Erc20ApprovalGasSponsoringOptions`:

| Member | Meaning |
|--------|---------|
| `bool Enabled` | `false` by default; the extension is advertised in `/supported` and accepted only when `true` |
| `BigInteger MaxSponsoredGasCost` | cap in wei on `gasLimit × maxFeePerGas` of the payer's approval transaction; must be positive when `Enabled`, otherwise `Validate()` (called by the service constructor) throws `InvalidOperationException` |
| `int MinimumPermitDeadlineSeconds` | `120` by default; with a sponsored approval the Permit2 `deadline` must be at least this many seconds in the future, otherwise `permit2_deadline_expired`; `Validate()` throws `InvalidOperationException` when negative |
| `IErc20ApprovalSponsoredSettlement? Settlement` | the settlement strategy; `null` uses `Erc20ApprovalSponsoredSettlement` |

`AddX402ExactProcessor` does not take these options; build the processor yourself with the `X402ExactPermit2Service` constructor overloads that accept `Erc20ApprovalGasSponsoringOptions` (for a private key or an `IAccount`) and register it:

```csharp
using Nethereum.JsonRpc.Client;
using Nethereum.X402.Blockchain;
using Nethereum.X402.Processors;

var clients = new Dictionary<int, IClient>
{
    [84532] = new RpcClient(new Uri("https://sepolia.base.org"))
};

var sponsoring = new Erc20ApprovalGasSponsoringOptions
{
    Enabled = true,
    MaxSponsoredGasCost = Nethereum.Util.UnitConversion.Convert.ToWei(0.001m)
};

var processor = new X402ExactSchemeProcessor(
    new X402TransferWithAuthorisation3009Service(facilitatorPrivateKey, clients),
    new X402ExactPermit2Service(facilitatorPrivateKey, clients, sponsoring));

builder.Services.AddSingleton<IX402PaymentProcessor>(processor);
```

Verification, when the allowance is short and no complete eip2612 info is present, decodes the signed transaction and rejects it unless every rule holds:

| Code | Rule |
|------|------|
| `invalid_erc20_approval_extension_format` | fields match the declared schema patterns |
| `erc20_approval_from_mismatch` / `erc20_approval_asset_mismatch` / `erc20_approval_spender_not_permit2` | `from` is the payer, `asset` the payment token, `spender` the canonical Permit2 |
| `erc20_approval_tx_parse_failed` / `erc20_approval_tx_invalid_signature` | the signed transaction decodes and has a recoverable signer whose signature has a low `s` (at most half the curve order) |
| `erc20_approval_tx_wrong_target` / `erc20_approval_tx_wrong_selector` | the transaction targets the asset and calls `approve` |
| `erc20_approval_tx_unsupported_type` | the signed transaction is EIP-1559 (type `0x02`) |
| `erc20_approval_tx_invalid_calldata` / `erc20_approval_tx_wrong_spender` / `erc20_approval_tx_wrong_amount` | calldata is a full `approve` whose spender word has zero upper bytes, the spender is Permit2, the amount equals `info.amount` |
| `erc20_approval_tx_signer_mismatch` | the transaction is signed by the payer |
| `erc20_approval_tx_chain_id_mismatch` / `erc20_approval_tx_value_not_zero` | the transaction declares the requirement's chain id and sends no value |
| `erc20_approval_tx_gas_cost_exceeds_cap` | `gasLimit × maxFeePerGas` is within `MaxSponsoredGasCost` |
| `erc20_approval_tx_gas_limit_too_high` | `gasLimit` is at most the smaller of the latest block's gas limit and 16777216, and at most the larger of 70000 and 1.5 times the `eth_estimateGas` of the `approve`, rounded up |
| `erc20_approval_tx_priority_fee_too_high` | `maxPriorityFeePerGas` is at most the larger of twice the node's `eth_maxPriorityFeePerGas` and 100000000 wei |
| `erc20_approval_tx_max_fee_too_high` | `maxFeePerGas` is at most the larger of 3 times the latest base fee plus `maxPriorityFeePerGas` and 1000000000 wei |
| `erc20_approval_tx_priority_fee_too_low` | `maxPriorityFeePerGas` is at least 1 wei |
| `erc20_approval_amount_below_required` | the approved amount is at least the payment amount |
| `erc20_approval_tx_nonce_mismatch` / `erc20_approval_tx_max_fee_below_base_fee` | the nonce equals the payer's pending transaction count and `maxFeePerGas` is at least the latest block's base fee times 1.125, rounded up |
| `erc20_approval_tx_in_flight` | no other settle holds the same chain, payer (canonical 20-byte address, with or without `0x`) and approval nonce; the lock is held from verification until the settle returns and is released on every other exit |
| `erc20_approval_tx_gas_limit_too_low` / `erc20_approval_tx_simulation_failed` | `eth_estimateGas` of the `approve` from the payer succeeds and does not exceed the signed gas limit |

With sponsoring not enabled, a payment that relies on the extension is rejected with `permit2_allowance_required`. Verification also checks the payer balance and the deployed proxy, but cannot simulate the three-step sequence on a plain node.

Settlement is **sequential, not atomic**: `Erc20ApprovalSponsoredSettlement` first sends the payer the missing ETH if its balance is below the approval's gas cost (`gas_funding_failed` on failure), then broadcasts the signed approval and waits for its receipt (`erc20_approval_broadcast_failed` when the node rejected the transaction or it provably never reached the node, `erc20_approval_tx_failed`, or `settlement_pending` when no receipt is obtained or the broadcast outcome is unknown), then settles through `x402ExactPermit2Proxy.settle`. The returned transaction is the settle transaction. If the settle fails after the approval has mined, the approval stands. Every receipt wait is bounded by the requirement's `maxTimeoutSeconds` (60 when it is not positive) and is not tied to the caller's cancellation token: once the funding transfer is broadcast the approval and the settle run to completion, and a wait that expires returns `settlement_pending` with the broadcast hash. The facilitator sends its transactions one at a time per account address and chain id across the process, reading the pending nonce on the payment's chain before each send. `Erc20ApprovalSponsoredSettlementRequest.MaxTimeoutSeconds` carries the bound to a custom settlement. Implement `IErc20ApprovalSponsoredSettlement` and set `Settlement` to replace this sequence (for example with a bundler).

**Residual risk.** The payer can spend the ETH it was just funded with through a conflicting transaction at the same nonce, which costs the facilitator up to `MaxSponsoredGasCost` per request. The cap bounds the exposure per request. A front-runner cannot divert the payment, because the Permit2 witness fixes the recipient to `payTo` and the spender is the proxy.

## Authorization Building (EIP-3009)

`TransferWithAuthorisationBuilder` and `ReceiveWithAuthorisationBuilder` create `Authorization` objects from payment requirements:

```csharp
using Nethereum.X402.Signers;

var builder = new TransferWithAuthorisationBuilder();

// Build from payment requirements (random nonce; validAfter = 10 minutes ago, validBefore = now + MaxTimeoutSeconds, or 1 hour when that is not positive)
var authorization = builder.BuildFromPaymentRequirements(requirements, fromAddress: payerAddress);

var signer = new TransferWithAuthorisationSigner();
var signature = await signer.SignWithPrivateKeyAsync(
    authorization, "USDC", "2", chainId, usdcAddress, payerPrivateKey);
```

The EIP-712 signing domain comes from the requirement: the chain id from its CAIP-2 network, the verifying contract from `asset`, and the domain name/version from `extra`. `SignWithWeb3Async` (and `SignReceiveWithWeb3Async`, `SignCancelWithWeb3Async`) sign through `web3.Eth.AccountSigning.SignTypedDataV4`, so the key can live in any account the `Web3` instance uses, and return the same signature as the private-key overload. They throw `ArgumentException` for `signerAddress` when the `Web3` account address differs from `signerAddress`:

```csharp
using Nethereum.Web3;
using Nethereum.Web3.Accounts;

var web3 = new Web3(new Account(payerPrivateKey, chainId));

var signature = await signer.SignWithWeb3Async(
    authorization, "USDC", "2", chainId, usdcAddress, web3, payerAddress);
```

## Facilitator


The facilitator API is JSON over UTF-8: the hosted controllers answer 400 `invalid_payload` to a POST whose `Content-Type` declares a charset other than UTF-8, whose body is not valid UTF-8 JSON, or whose JSON repeats a property name (case-insensitively for the request, `paymentPayload`, `paymentRequirements` and the typed objects inside them; case-sensitively inside `extensions`, `extra` and unknown members) or contains an unreadable one (such as a lone surrogate escape) at any depth, before the request is bound.
A facilitator verifies and settles payments on behalf of API servers, so servers need no blockchain infrastructure.

### Client

```csharp
using Nethereum.X402.Facilitator;

var facilitatorClient = new HttpFacilitatorClient(httpClient, "https://x402.org/facilitator");

var verification = await facilitatorClient.VerifyAsync(paymentPayload, requirements);
var settlement = await facilitatorClient.SettleAsync(paymentPayload, requirements);
var supported = await facilitatorClient.GetSupportedAsync();
```

`HttpFacilitatorClient` bounds each call itself: verify by `VerifyTimeout` (30 seconds by default, a `TimeoutException` when it expires) and settle by `3 x maxTimeoutSeconds` of the requirement plus `SettleTimeoutGrace` (30 seconds by default). A settle that outlives its bound returns a failed `SettlementResponse` with `settlement_pending` and no transaction hash, because the client never learns the hash of a settlement the facilitator was still working on. The `HttpClient` registered by `AddX402Services` and `AddX402FacilitatorProxy` (the named client `HttpFacilitatorClient.HttpClientName`) has `Timeout` set to infinite so these bounds govern; an `HttpClient` you pass to the constructor must have a `Timeout` above the settle bound, or its own timeout fires first. Response bodies of verify, settle and supported are read through `HttpFacilitatorClient.MaxResponseBytes` (1 MiB by default); a larger body throws `InvalidDataException`, which the middleware answers as `facilitator_error` on verify and as `settlement_pending` on settle. `GetSupportedAsync` is bounded the same way: by `VerifyTimeout`, or by the `HttpClient.Timeout` when that is finite and shorter, and throws `TimeoutException` when it expires. The hosted `FacilitatorController` rejects a request body that repeats a property name at any depth with 400 `invalid_payload`.

When the facilitator answers a non-success HTTP status with a JSON body that carries `isValid` (verify) or `success` (settle), `HttpFacilitatorClient` returns that body as the failed `VerificationResponse` / `SettlementResponse` with its `InvalidReason` / `ErrorReason` kept (`IsValid` / `Success` forced to `false`); a settle answer is a definite failure only for status 400, 401, 403, 404, 409, 413, 415 or 422 (and a body-declared failure on 2xx); any other status (every 3xx, 402, 408, 425, 429, 499, 5xx) is an unknown outcome, because the facilitator received the request and may have broadcast, so its `ErrorReason` becomes `settlement_pending` and the middleware keeps the replay reservation. A settle answer is released as a failure only when it is well formed: `success` false, a non-empty `errorReason` other than `settlement_pending`, and (over HTTP) a definite status; anything else is an unknown outcome (`settlement_pending`, reservation kept), including a 2xx body without a `success` member, `{}`, `null`, an unparseable body, a missing or empty `errorReason`, or any exception, and the middleware applies the same rule to any `IFacilitatorClient`. A `null` verify answer is an invalid verification (`unexpected_verify_error`). A `PaymentRequirements.Extra` that does not parse as `ExactSchemeExtra` is answered by the exact processor as `unsupported_payload_type`, and `ExactSchemeExtra.FromRequirements` returns null for it instead of throwing. Any other non-success answer still throws `HttpRequestException`, classified by the same status list in the middleware. `VerificationResponse.InvalidMessage` and `SettlementResponse.ErrorMessage` carry the optional human-readable cause (`invalidMessage` / `errorMessage` on the wire), are omitted when null, and pass through the client and the hosted controllers.

`HttpFacilitatorClient` implements `IFacilitatorClient`, the seam `X402Middleware` calls; implement it to put your own facilitator behind the middleware:

```csharp
public interface IFacilitatorClient
{
    Task<VerificationResponse> VerifyAsync(
        PaymentPayload paymentPayload,
        PaymentRequirements requirements,
        CancellationToken cancellationToken = default);

    Task<SettlementResponse> SettleAsync(
        PaymentPayload paymentPayload,
        PaymentRequirements requirements,
        CancellationToken cancellationToken = default);

    Task<SupportedPaymentKindsResponse> GetSupportedAsync(
        CancellationToken cancellationToken = default);
}
```

`AddX402Services(facilitatorUrl)` registers an `IFacilitatorClient` (an `HttpFacilitatorClient` for that URL). `AddX402FacilitatorProxy(facilitatorUrl, routes)` registers the same client plus a singleton `X402FacilitatorProxyProcessor` built from the given `RoutePaymentConfig` list. The processor, which the middleware also builds internally from `X402Options.Routes`, matches requests to routes and forwards to the facilitator:

```csharp
public X402FacilitatorProxyProcessor(IFacilitatorClient facilitator, IEnumerable<RoutePaymentConfig> routes)

public RoutePaymentConfig? FindMatchingRoute(string path, string? method = null)
public static PaymentRequirements? FindMatchingRequirements(RoutePaymentConfig route, PaymentPayload payment)
public static PaymentPayload DecodePaymentHeader(string header)
public async Task<VerificationResponse> VerifyPaymentAsync(PaymentPayload payment, PaymentRequirements requirements, CancellationToken cancellationToken = default)
public async Task<SettlementResponse> SettlePaymentAsync(PaymentPayload payment, PaymentRequirements requirements, CancellationToken cancellationToken = default)
public static string EncodeSettlementResponse(SettlementResponse response)
```

`FindMatchingRoute` compares case-insensitively with the invariant culture, ranks routes by specificity, and ignores `Method` when it is `null`. The path is the decoded request path (`HttpContext.Request.Path.Value`), and both the pattern and the path are normalised the same way: repeated slashes collapse and a trailing slash is ignored, so `/reports/` and `/reports` match each other. Routes are ranked segment by segment after normalisation: a literal segment outranks a `:name` or `[name]` parameter segment, which outranks a segment containing `*`; a pattern ending in `/*` ranks below any pattern without a wildcard; the higher-ranked pattern wins at the first differing segment, more literal segments win next, and ties keep declaration order (so `/api/vip` is charged ahead of `/api/:identifier` whatever the declaration order, and `/premium/:id` ahead of `/premium/*`). A route also protects every spelling of its path that the host may resolve to the same resource: `%2F` is read as `/`, `%5C` and `\` as `/`, trailing dots and spaces on a segment are dropped, and `.` and `..` segments are resolved, so `/files/secret%5Cdata.txt` and `/files/secret./data.txt` are charged like `/files/secret/data.txt`. Route patterns are matched with non-backtracking regular expressions, so a pattern with several `*` cannot be made to stall on a long path. A trailing `/*` also matches the bare prefix (`/api/premium/*` covers `/api/premium`, `/api/premium/` and `/api/premium/a/b`), `*` spans segments, and `[id]` or `:id` match exactly one segment.

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
- `POST /facilitator/verify` — Verify a payment (the `facilitator` prefix is `X402FacilitatorOptions.RoutePrefix`)
- `POST /facilitator/settle` — Settle a payment on-chain
- `GET /facilitator/supported` — List supported payment kinds: each (`x402Version`, `scheme`, `network`) once and without an `extra`, as the `/supported` example of the x402 v2 specification shows. This holds for every registration shown here (the EIP-3009 services and `X402ExactSchemeProcessor`); a bare `X402ExactPermit2Service` used as the only processor returns `Extra` = `{ assetTransferMethod: "permit2" }` from `GetSupportedAsync`

These endpoints hold the key that pays gas, so they must not be exposed unauthenticated: set `X402FacilitatorOptions.AuthorizationPolicy` (below) or protect them at the host.

### Facilitator Options

`X402FacilitatorOptions` carries the guards every facilitator transaction passes through:

```csharp
using Nethereum.X402.Extensions;
using Nethereum.X402.Facilitator;

builder.Services.AddX402FacilitatorOptions(options =>
{
    options.MaxSettlementGas = X402FacilitatorOptions.DefaultMaxSettlementGas;   // 500000
    options.AllowedAssets["eip155:8453"] = new List<string> { "0x833589fCD6eDb6E08f4c7C32D4f71b54bdA02913" };
    options.AuthorizationPolicy = "x402-facilitator";
    options.RoutePrefix = X402FacilitatorOptions.DefaultRoutePrefix;                // "facilitator"; "" serves /verify, /settle, /supported
    options.RequiredConfirmations = 1;
    options.MaxReceiptWaitSeconds = X402FacilitatorOptions.DefaultMaxReceiptWaitSeconds;   // 3600
});

builder.Services.AddAuthorization(authorization => authorization.AddPolicy("x402-facilitator", policy => policy.RequireAuthenticatedUser()));
```

| Member | Meaning |
|--------|---------|
| `long MaxSettlementGas` | 500000 by default. Every facilitator transaction estimates gas first and is refused with `settlement_gas_exceeds_limit` when the estimate is above this ceiling; it is sent with an explicit gas limit of 120% of the estimate, never above the ceiling. Verification of an EIP-3009 payment refuses the same way |
| `Dictionary<string, List<string>> AllowedAssets` | per CAIP-2 network, the assets the facilitator accepts; any other asset is rejected with `invalid_exact_evm_asset_not_allowed` before any RPC. A network without an entry is unrestricted. Networks are matched on the parsed chain id, so a non-canonical spelling of a configured network is never admitted by it |
| `string RoutePrefix` | `"facilitator"` by default, the path prefix of the hosted `verify`, `settle` and `supported` endpoints; `""` mounts them at the root (`/verify`, `/settle`, `/supported`) as the x402 specification shows. Must not be `null` |
| `string? AuthorizationPolicy` | the name of an ASP.NET Core authorization policy that the hosted `/facilitator` endpoints require; `null` leaves them open. An unauthenticated caller gets 401 and a caller failing the policy 403 |
| `int RequiredConfirmations` | 1 by default; settlement waits until the receipt's block has this many confirmations, within the same bound, otherwise `settlement_pending` with the hash; a transaction recovered after a front-run is held to the same count |
| `List<string> FacilitatorAddresses` | empty by default; the addresses of every facilitator signer in the deployment. Front-run recovery never reports a transaction sent by this facilitator's account or by any listed address, compared canonically, so in a deployment with several facilitator keys list them all here; the recovered-settlement registry is per process |
| `int MaxReceiptWaitSeconds` | 3600 by default; the receipt wait is `maxTimeoutSeconds` clamped to between 1 second and this value |
| `void Validate()` | throws `InvalidOperationException` for a non-positive ceiling, fewer than one confirmation, a wait below one second or a `null` `RoutePrefix`, and `ArgumentException` for an `AllowedAssets` key that is not a canonical `eip155:<chainId>` CAIP-2 id (also thrown by `AddX402FacilitatorOptions`) |
| `bool IsAssetAllowed(string network, string? asset)` | whether the allowlist admits the asset on the network |

Pass the options to the processors through the constructors that take them (`new X402TransferWithAuthorisation3009Service(key, clients, options)`, `new X402ReceiveWithAuthorisation3009Service(key, clients, options)`, `new X402ExactPermit2Service(key, clients, sponsoring, options)`), or register them with `AddX402FacilitatorOptions` and the `AddX402TransferProcessor`, `AddX402ExactProcessor` and `AddX402ReceiveProcessor` registrations use them.

The facilitator serialises its transactions per account address and chain id across the whole process, so an EIP-3009 service and a Permit2 service built for the same account never race for a nonce. The receipt bound (`maxTimeoutSeconds`, clamped by `MaxReceiptWaitSeconds`) starts once the send lock is acquired; waiting for the lock has its own bound of the same length, and a settlement that times out there broadcast nothing and returns `unexpected_settle_error`. If a settle reverts because a third party already submitted the payer's signed payment, the facilitator reports success with that transaction's hash when the chain proves it: EIP-3009 `authorizationState` is used and an `AuthorizationUsed` log exists in the blocks since verification, or the Permit2 nonce bit is set and a matching `Transfer` from the payer to `payTo` exists; the transfer check is applied to that transaction. The found transaction must also belong to this payment: for EIP-3009 the asset's `AuthorizationUsed(payer, nonce)` log must be immediately followed by the asset's `Transfer(payer, payTo, value)` with the authorized value, and for Permit2 the transaction must be a `settle` or `settleWithPermit` call to the x402 exact Permit2 proxy whose owner, token, nonce, amount and witness recipient are this payment's. Otherwise the original failure stands. A transaction sent by the facilitator's own account is never recovered, and a process-wide registry (bounded, entries expire at the authorization's `validBefore` or `deadline`) refuses to report the same authorization as settled by recovery twice; in both cases the original failure stands: `invalid_transaction_state` for Permit2, and for EIP-3009 `unexpected_settle_error` when the send reverted or `invalid_exact_evm_transaction_failed` when the transaction was mined and reverted. A recovered transaction is held to `RequiredConfirmations` within the same bound as the facilitator's own, and short of them the result is `settlement_pending` with its hash.

## Security

### Client payment allow-list

`X402HttpClient` auto-signs whatever a 402 demands up to `MaxAmount`; `X402HttpClientOptions.Policy`
adds a network/asset/recipient allow-list so a compromised server cannot redirect the payment, swap
the asset, or switch chains. Refused before signing. See [Client Payment Policy](#client-payment-policy-allow-list).

### Facilitator endpoint authentication is a hosting responsibility

`AddX402FacilitatorControllers` exposes `/facilitator/verify`, `/facilitator/settle` and
`/facilitator/supported`, open unless `X402FacilitatorOptions.AuthorizationPolicy` names an ASP.NET Core
authorization policy. The facilitator holds the key that pays gas and submits settlements, so the
endpoints must not be exposed unauthenticated: set the policy, or protect them at the host — API
keys, mTLS, a network ACL, or an allow-list of resource-server origins — as for any privileged
internal API.

### Replay protection (serve-before-settle)

The middleware serves the protected resource before settlement confirms on-chain, so the same signed
payment must not drive the endpoint more than once. After verify and before running the endpoint,
`X402Middleware` reserves the payment identity via `IPaymentReplayStore`: network, transfer method, asset, payer (20 bytes, lower-case hex) and canonical nonce (EIP-3009: 32-byte lower-case hex; Permit2: a decimal uint256), derived from the same typed payload the facilitator deserialises. An EIP-3009 nonce must be exactly 32 bytes (`0x` followed by 64 hex characters); a shorter or longer one is rejected at verify with `invalid_payload` and yields no identity. A payment from which no identity can be derived is rejected with 400 / `invalid_payload`; a replay is
rejected with 402 / `invalid_exact_evm_nonce_already_used` before the endpoint runs. On by default
(`X402Options.EnablePaymentReplayProtection`, with `PaymentReplayRetention`, which `Validate` requires to be greater than zero while protection is on).

On an authorization route the reservation is released unless the payment ends in a successful settlement or a `settlement_pending` result: a handler response of 400 or above, a failed settlement, an exception from the handler and a cancelled request all release it, while a settlement that throws keeps it (the outcome is unknown); it expires with `PaymentReplayRetention`. When the store is full, the expired sweep runs at most once per second.

On an upfront route the reservation is released through `IPaymentReplayStore.ReleaseAsync` unless the payment ends in a successful settlement, a `settlement_pending` result or an exception while settling (the outcome is unknown, so the reservation is kept until it expires); a definite settle failure releases it so the payment can be retried. The default `InMemoryPaymentReplayStore` is single-instance and bounded: `new InMemoryPaymentReplayStore(int maxEntries)` sets `MaxEntries` (100000 by default); when it is full, expired reservations are evicted first, and if it is still full `TryReserveAsync` throws `PaymentReplayStoreFullException` and the middleware answers 503 with `replay_store_full` (fail closed). For a horizontally-scaled deployment,
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
on-chain. (Standard EIP-3009 tokens validate `transferWithAuthorization` with `ecrecover` only. Verify dry-runs
the settlement with the `v`, `r`, `s` overload, so a contract-wallet payment on the raw EIP-3009 path
is rejected with `invalid_exact_evm_transaction_simulation_failed` unless the token validates ERC-1271
inside that overload.)

An EIP-3009 signature that is not exactly 65 bytes is never truncated into `v`, `r`, `s`: it is valid only as an ERC-1271 signature of `from` (a payer without contract code is rejected with `invalid_exact_evm_payload_undeployed_smart_wallet`, a deployed wallet that rejects the signature with `invalid_exact_evm_signature`), and settlement then calls the token's `transferWithAuthorization(..., bytes signature)` overload (`receiveWithAuthorization` for `X402ReceiveWithAuthorisation3009Service`) with the whole signature. A token without that overload fails the verify simulation with `invalid_exact_evm_transaction_simulation_failed`. A 65-byte signature keeps the `v`, `r`, `s` overload.

## Known Deviations and Nethereum Extensions

- **ERC-20 approval sponsoring is sequential, not atomic.** The x402 `erc20_gas_sponsoring.md` extension describes an atomic batch of the approval and the settle; `Erc20ApprovalSponsoredSettlement` broadcasts the approval, waits for its receipt, then settles. Supply an atomic sender (for example a bundler) by implementing `IErc20ApprovalSponsoredSettlement` and setting `Erc20ApprovalGasSponsoringOptions.Settlement`.
- **Sponsored approvals must be EIP-1559 transactions.** A legacy or other signed approval is rejected with `erc20_approval_tx_unsupported_type`.
- **Nethereum extensions that are not in the x402 specification:** the `ResourceInfo` fields `serviceName`, `tags` and `iconUrl`, and `extra.paymentFlow = "upfront"` (settle before the handler runs).

## Network Configuration

`NetworkConfiguration` provides pre-configured RPC endpoints, USDC addresses and chain ids for common networks:

```csharp
using Nethereum.X402.Blockchain;

var config = NetworkConfiguration.Default;
var rpcUrl = config.GetRpcEndpoint("base");
var usdcAddress = config.GetUSDCAddress("base");
var chainId = config.GetChainId("base");
```

CAIP-2 network ids are parsed/formatted with `Caip2` (`Caip2.FormatEip155(chainId)`, `Caip2.TryParseEip155ChainId(network, out chainId)`). Only the canonical form parses: `eip155:` followed by a decimal chain id with no sign, no leading zero, no whitespace and at most 78 digits that also fit a 32-bit signed integer (`eip155:0` is invalid); the `eip155` namespace is matched case-insensitively; anything else is rejected at verify with `invalid_network` before any RPC.

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
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }   // unknown JSON properties, round-tripped and part of equality
}
```

### ExactSchemeExtra

Scheme-specific data carried in `PaymentRequirements.Extra`:

```csharp
public class ExactSchemeExtra
{
    public string? AssetTransferMethod { get; set; }  // "eip3009" (default) or "permit2"
    public string? PaymentFlow { get; set; }          // "authorization" (default when omitted) or "upfront"
    public string Name { get; set; }                  // EIP-712 domain name (EIP-3009 / EIP-2612 path)
    public string Version { get; set; }               // EIP-712 domain version

    public static ExactSchemeExtra? FromRequirements(PaymentRequirements requirements)
}
```

### RoutePaymentConfig

A protected route (`Nethereum.X402.Server`):

```csharp
public class RoutePaymentConfig
{
    public string PathPattern { get; set; }                   // supports wildcards, e.g. "/api/data/*"; X402Options.Validate requires "*" (every path) or a leading '/', and no '?', '#' or "://"
    public string? Method { get; set; }                       // null = any HTTP method
    public PaymentRequirements Requirements { get; set; }     // the single offer, used when Accepts is not set
    public List<PaymentRequirements>? Accepts { get; set; }   // the route's offers; when set it replaces Requirements in the 402
    public IReadOnlyList<PaymentRequirements> GetOffers()     // Accepts, or Requirements as a one-element list
    public ResourceInfo? Resource { get; set; }
    public Dictionary<string, object>? Extensions { get; set; } // served in PaymentRequired.Extensions
}
```

### Gas-sponsoring extension info

The `info` object a client attaches under the extension key in `PaymentPayload.Extensions` (JSON property names in comments):

```csharp
public class Eip2612GasSponsoringInfo       // X402ExtensionKeys.Eip2612GasSponsoring
{
    public string From { get; set; }        // "from"
    public string Asset { get; set; }       // "asset"
    public string Spender { get; set; }     // "spender" (canonical Permit2)
    public string Amount { get; set; }      // "amount"
    public string Nonce { get; set; }       // "nonce" (the token's EIP-2612 nonce)
    public string Deadline { get; set; }    // "deadline"
    public string Signature { get; set; }   // "signature" (65 bytes, hex)
    public string Version { get; set; }     // "version"
}

public class Erc20ApprovalGasSponsoringInfo // X402ExtensionKeys.Erc20ApprovalGasSponsoring
{
    public string From { get; set; }              // "from"
    public string Asset { get; set; }             // "asset"
    public string Spender { get; set; }           // "spender" (canonical Permit2)
    public string Amount { get; set; }            // "amount"
    public string SignedTransaction { get; set; } // "signedTransaction" (RLP-encoded signed EIP-1559 transaction)
    public string Version { get; set; }           // "version"
}
```

### ResourceInfo

Describes the protected resource in a 402 (`RoutePaymentConfig.Resource`, `PaymentRequired.Resource`, `PaymentPayload.Resource`):

```csharp
public class ResourceInfo
{
    public string Url { get; set; }
    public string? Description { get; set; }
    public string? MimeType { get; set; }
    public string? ServiceName { get; set; }
    public List<string>? Tags { get; set; }
    public string? IconUrl { get; set; }
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }   // unknown JSON properties, round-tripped
}
```

`X402Options.Validate` rejects a route's `Resource` (`InvalidOperationException`) unless `ServiceName` is printable ASCII (0x20-0x7E) of at most 32 characters, `Tags` has at most 5 entries each printable ASCII of at most 32 characters, and `IconUrl` is an absolute `http` or `https` URL of at most 2048 characters.

When the route's `Resource.Url` is empty the 402 uses the request's `scheme://host/path`.

### PaymentRequired

The 402 body (and `PAYMENT-REQUIRED` header):

```csharp
public class PaymentRequired
{
    public int X402Version { get; set; } = 2;
    public string? Error { get; set; }
    public ResourceInfo Resource { get; set; }
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
    public string? InvalidMessage { get; set; } // human-readable cause, omitted when null
    public string Payer { get; set; }
    public object? Extensions { get; set; }
    public object? Extra { get; set; }
}

public class SettlementResponse
{
    public bool Success { get; set; }
    public string? ErrorReason { get; set; }    // X402ErrorCodes value
    public string? ErrorMessage { get; set; }   // human-readable cause, omitted when null
    public string Transaction { get; set; } = string.Empty;   // TX hash
    public string Network { get; set; }
    public string Payer { get; set; }
    public string? Amount { get; set; }
    public object? Extensions { get; set; }
}
```

## Public API Reference

The remaining public types of the package, grouped by area. Signatures are copied from the source.

### Constants

```csharp
public static class X402Headers
{
    public const string PaymentRequired = "PAYMENT-REQUIRED";
    public const string PaymentSignature = "PAYMENT-SIGNATURE";
    public const string PaymentResponse = "PAYMENT-RESPONSE";
}

public static class X402Schemes
{
    public const string Exact = "exact";
}

public static class X402Protocol
{
    public const int Version = 2;
}
```

`X402AssetTransferMethods` (`Eip3009` = `"eip3009"`, `Permit2` = `"permit2"`) and `X402PaymentFlows` (`Authorization` = `"authorization"`, `Upfront` = `"upfront"`) are the other constant classes.

`X402RequirementSupport` decides what a requirement asks for and whether this library handles it:

```csharp
public static class X402RequirementSupport
{
    public static bool IsAuthorizationFlow(PaymentRequirements requirements)
    public static bool IsUpfrontFlow(PaymentRequirements requirements)
    public static string ResolveAssetTransferMethod(PaymentRequirements requirements)
    public static bool IsSupportedByThisLibrary(PaymentRequirements requirements)
}
```

`IsAuthorizationFlow` is true when `paymentFlow` is omitted or `authorization`; `ResolveAssetTransferMethod` returns `eip3009` when `assetTransferMethod` is omitted; `IsSupportedByThisLibrary` requires scheme `exact`, an authorization or upfront flow and an `eip3009` or `permit2` method.

### Payload Models

```csharp
public class Authorization
{
    public string From { get; set; }          // "from"
    public string To { get; set; }            // "to"
    public string Value { get; set; }         // "value", atomic units
    public string ValidAfter { get; set; }    // "validAfter", unix seconds
    public string ValidBefore { get; set; }   // "validBefore", unix seconds
    public string Nonce { get; set; }         // "nonce", 0x + 64 hex characters
}

public class Permit2Authorization
{
    public string From { get; set; }                       // "from"
    public Permit2TokenPermissions Permitted { get; set; } // "permitted"
    public string Spender { get; set; }                    // "spender" (the x402ExactPermit2Proxy)
    public string Nonce { get; set; }                      // "nonce"
    public string Deadline { get; set; }                   // "deadline"
    public Permit2WitnessData Witness { get; set; }        // "witness"
}

public class Permit2TokenPermissions
{
    public string Token { get; set; }    // "token"
    public string Amount { get; set; }   // "amount"
}

public class Permit2WitnessData
{
    public string To { get; set; }           // "to" (the recipient, equal to PayTo)
    public string ValidAfter { get; set; }   // "validAfter"
}

public class PaymentKind
{
    public int X402Version { get; set; }   // "x402Version"
    public string Scheme { get; set; }     // "scheme"
    public string Network { get; set; }    // "network"
    public object? Extra { get; set; }     // "extra", omitted when null
}

public class SupportedPaymentKindsResponse
{
    public List<PaymentKind> Kinds { get; set; }                    // "kinds"
    public List<string> Extensions { get; set; } = new();           // "extensions"
    public Dictionary<string, List<string>> Signers { get; set; } = new();   // "signers"
}
```

### Client Types

`X402HttpClient` exposes the payer address, decodes a `PAYMENT-SIGNATURE` header value, and has these constructors and request methods:

```csharp
public string Address { get; }

public X402HttpClient(HttpClient httpClient, string privateKey)
public X402HttpClient(HttpClient httpClient, string privateKey, X402HttpClientOptions options)

public async Task<HttpResponseMessage> GetAsync(
    string uri,
    PaymentRequirements requirements,
    CancellationToken cancellationToken = default)

public async Task<HttpResponseMessage> GetAsync(
    string uri,
    CancellationToken cancellationToken = default)

public async Task<HttpResponseMessage> PostAsync(
    string uri,
    HttpContent? content = null,
    CancellationToken cancellationToken = default)

public async Task<HttpResponseMessage> PutAsync(
    string uri,
    HttpContent? content = null,
    CancellationToken cancellationToken = default)

public async Task<HttpResponseMessage> DeleteAsync(
    string uri,
    CancellationToken cancellationToken = default)

public async Task<HttpResponseMessage> SendAsync(
    HttpRequestMessage request,
    CancellationToken cancellationToken = default)

public static PaymentPayload DecodePaymentHeader(string header)

public class X402PaymentExceedsMaximumException : Exception
{
    public string RequestedAmount { get; }
    public string MaximumAllowed { get; }
    public X402PaymentExceedsMaximumException(string requestedAmount, string maximumAllowed)
}
```

The payment result is read from the `PAYMENT-RESPONSE` header of the response:

```csharp
public static class HttpResponseMessageExtensions
{
    public static SettlementResponse? GetSettlementResponse(this HttpResponseMessage response)
    public static bool HasPaymentResponse(this HttpResponseMessage response)
    public static string? GetTransactionHash(this HttpResponseMessage response)
    public static string? GetPayerAddress(this HttpResponseMessage response)
    public static bool IsPaymentSuccessful(this HttpResponseMessage response)
    public static string? GetPaymentError(this HttpResponseMessage response)
}
```

`GetSettlementResponse` returns `null` when the header is missing or unreadable; `GetPaymentError` returns the settlement's `ErrorReason` when the settlement failed, otherwise `null`.

`X402HttpClientOptions.Policy` is a `PaymentPolicy`:

```csharp
public class PaymentPolicy
{
    public List<string> AllowedNetworks { get; }
    public List<string> AllowedAssets { get; }
    public List<string> AllowedRecipients { get; }
    public void Assert(PaymentRequirements requirement)
}

public enum PaymentPolicyDimension
{
    Network,
    Asset,
    Recipient
}

public class X402PaymentPolicyViolationException : Exception
{
    public PaymentPolicyDimension Dimension { get; }
    public string RejectedValue { get; }
    public X402PaymentPolicyViolationException(PaymentPolicyDimension dimension, string rejectedValue)
}
```

`Assert` throws `X402PaymentPolicyViolationException` naming the `Dimension` that refused the payment and the `RejectedValue`. The client calls it before signing.

### Server Types

`RoutePaymentConfig` has three constructors:

```csharp
public RoutePaymentConfig()
public RoutePaymentConfig(string pathPattern, PaymentRequirements requirements, string? method = null)
public RoutePaymentConfig(string pathPattern, PaymentRequirements requirements, ResourceInfo resource, string? method = null)
```

The middleware and the service registrations are extension methods:

```csharp
public static class X402MiddlewareExtensions
{
    public static IApplicationBuilder UseX402(
        this IApplicationBuilder app,
        Action<X402Options> configureOptions)

    public static IServiceCollection AddX402Services(
        this IServiceCollection services,
        string facilitatorUrl)
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddX402Client(
        this IServiceCollection services,
        string privateKey,
        Action<HttpClient>? configureHttpClient = null)

    public static IServiceCollection AddX402Client(
        this IServiceCollection services,
        IConfigurationSection configuration,
        Action<HttpClient>? configureHttpClient = null)
}

public static class ServiceCollectionExtensionsServer
{
    public static IServiceCollection AddX402FacilitatorProxy(
        this IServiceCollection services,
        string facilitatorUrl,
        IEnumerable<RoutePaymentConfig> routes)

    public static IServiceCollection AddX402TransferProcessor(
        this IServiceCollection services,
        string facilitatorPrivateKey,
        Dictionary<int, string> rpcEndpointsByChainId)

    public static IServiceCollection AddX402TransferProcessor(
        this IServiceCollection services,
        IAccount facilitatorAccount,
        Dictionary<int, string> rpcEndpointsByChainId)

    public static IServiceCollection AddX402TransferProcessor(
        this IServiceCollection services,
        Func<IServiceProvider, IAccount> accountFactory,
        Dictionary<int, string> rpcEndpointsByChainId)

    public static IServiceCollection AddX402ExactProcessor(
        this IServiceCollection services,
        string facilitatorPrivateKey,
        Dictionary<int, string> rpcEndpointsByChainId)

    public static IServiceCollection AddX402ExactProcessor(
        this IServiceCollection services,
        Func<IServiceProvider, IAccount> accountFactory,
        Dictionary<int, string> rpcEndpointsByChainId)

    public static IServiceCollection AddX402ExactProcessor(
        this IServiceCollection services,
        IAccount facilitatorAccount,
        Dictionary<int, string> rpcEndpointsByChainId)

    public static IServiceCollection AddX402ReceiveProcessor(
        this IServiceCollection services,
        string receiverPrivateKey,
        Dictionary<int, string> rpcEndpointsByChainId)

    public static IServiceCollection AddX402ReceiveProcessor(
        this IServiceCollection services,
        IAccount receiverAccount,
        Dictionary<int, string> rpcEndpointsByChainId)

    public static IServiceCollection AddX402ReceiveProcessor(
        this IServiceCollection services,
        Func<IServiceProvider, IAccount> accountFactory,
        Dictionary<int, string> rpcEndpointsByChainId)
}

public static class ServiceCollectionExtensionsFacilitator
{
    public static IMvcBuilder AddX402FacilitatorControllers(
        this IMvcBuilder builder)

    public static IServiceCollection AddX402FacilitatorOptions(
        this IServiceCollection services,
        Action<X402FacilitatorOptions> configure)
}
```

`PaymentReplayIdentity` derives the key the replay store reserves:

```csharp
public static class PaymentReplayIdentity
{
    public static string? Extract(PaymentPayload payment)
    public static string? Extract(PaymentPayload payment, PaymentRequirements requirements)
}
```

The overload with `requirements` is the one the middleware uses: `{network}:{method}:{asset}:{payer}:{nonce}` with the network lower-cased, the method `eip3009` or `permit2`, and the asset and payer as canonical 20-byte addresses. It returns `null` when no identity can be derived. The overload without `requirements` returns `{payer}:{nonce}`.

`InMemoryPaymentReplayStore` has a parameterless constructor (`DefaultMaxEntries` = 100000) and one taking `maxEntries`; `PaymentReplayStoreFullException(int maxEntries)` is thrown by `TryReserveAsync`, and its `MaxEntries` carries the limit that was reached. `IPaymentReplayStore.ReleaseAsync` has a default implementation that does nothing.

### Hosted Facilitator Types

```csharp
public class FacilitatorController : ControllerBase
{
    public FacilitatorController(IX402PaymentProcessor processor)

    public async Task<ActionResult<VerificationResponse>> Verify(
        [FromBody] FacilitatorVerifyRequest request,
        CancellationToken cancellationToken = default)

    public async Task<ActionResult<SettlementResponse>> Settle(
        [FromBody] FacilitatorSettleRequest request,
        CancellationToken cancellationToken = default)

    public async Task<ActionResult<SupportedPaymentKindsResponse>> GetSupported(
        CancellationToken cancellationToken = default)
}

public class FacilitatorVerifyRequest
{
    public int X402Version { get; set; } = X402Protocol.Version;   // "x402Version"
    public PaymentPayload PaymentPayload { get; set; }
    public PaymentRequirements PaymentRequirements { get; set; }
}

public class FacilitatorSettleRequest
{
    public int X402Version { get; set; } = X402Protocol.Version;   // "x402Version"
    public PaymentPayload PaymentPayload { get; set; }
    public PaymentRequirements PaymentRequirements { get; set; }
}
```

`Verify` and `Settle` answer 400 with `{"error":"PaymentPayload and PaymentRequirements are required"}` when either is missing. Each action is behind the `X402FacilitatorOptions.AuthorizationPolicy` filter.

### Blockchain Processor Building Blocks

`X402TransferWithAuthorisation3009Service` and `X402ReceiveWithAuthorisation3009Service` share one base class, which implements the three `IX402PaymentProcessor` operations:

```csharp
public abstract class ExactScheme3009ServiceBase : IX402PaymentProcessor
{
    public async Task<VerificationResponse> VerifyPaymentAsync(
        PaymentPayload paymentPayload,
        PaymentRequirements requirements,
        CancellationToken cancellationToken = default)

    public async Task<SettlementResponse> SettlePaymentAsync(
        PaymentPayload paymentPayload,
        PaymentRequirements requirements,
        CancellationToken cancellationToken = default)

    public Task<SupportedPaymentKindsResponse> GetSupportedAsync(CancellationToken cancellationToken = default)
}
```

The signature, funds, nonce and time checks of an EIP-3009 authorization are in `PaymentAuthorizationValidator`; the ERC-1271 check of a contract wallet signature is `Erc1271SignatureVerifier`:

```csharp
public class PaymentAuthorizationValidator
{
    public PaymentAuthorizationValidator()

    public async Task<ValidationResult> ValidateAsync(
        Authorization authorization,
        string signatureHex,
        string tokenAddress,
        string tokenName,
        string tokenVersion,
        int chainId,
        IClient client,
        CancellationToken cancellationToken = default)

    public async Task<ValidationResult> ValidateFundsNonceTimeAsync(
        Authorization authorization,
        string tokenAddress,
        IClient client,
        CancellationToken cancellationToken = default)
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public string InvalidReason { get; set; }         // an X402ErrorCodes value
    public BigInteger BlockNumber { get; set; }       // the latest block the check read

    public static ValidationResult Valid()
    public static ValidationResult Valid(BigInteger blockNumber)
    public static ValidationResult Invalid(string reason)
}

public static class Erc1271SignatureVerifier
{
    public static async Task<bool> IsValidContractSignatureAsync(IWeb3 web3, string signer, byte[] digest, byte[] signature)
}
```

A settlement that was broadcast but whose receipt was not obtained surfaces as `X402SettlementPendingException`; so does any failure while broadcasting that does not prove nothing was sent (a lost answer, a reset connection, a timeout after sending), with an empty `TransactionHash`. A JSON-RPC error reply from the node means the node rejected the transaction and is a definite failure (for example "insufficient funds for gas", a nonce error), except a reply saying the transaction is already known to the node (a reply containing "already known", "known transaction" or "existing txn with same hash"), which is pending. A failure to connect (an `HttpRequestException` with `HttpRequestError` `ConnectionError` or `NameResolutionError` in the chain) and the send-lock wait timeout are also definite:

```csharp
public class X402SettlementPendingException : Exception
{
    public string TransactionHash { get; }
    public X402SettlementPendingException(string transactionHash, Exception innerException)
}
```

The processor turns it into `settlement_pending` with `Transaction` set to `TransactionHash`.

`TransferWithAuthorisationBuilder`, `ReceiveWithAuthorisationBuilder` and `CancelAuthorisationBuilder` derive from one base class:

```csharp
public abstract class Eip3009AuthorisationBuilderBase
{
    public byte[] GenerateNonce()

    public Authorization BuildFromPaymentRequirements(
        PaymentRequirements requirements,
        string fromAddress,
        BigInteger? validAfterTimestamp = null,
        BigInteger? validBeforeTimestamp = null)
}
```

`GenerateNonce` returns 32 random bytes. When the timestamps are omitted `validAfter` is 10 minutes ago and `validBefore` is now plus `MaxTimeoutSeconds` (1 hour when that is not positive).

`TransferWithAuthorisationSigner` also signs and recovers the receive variant and computes the digests:

```csharp
public Task<EthECDSASignature> SignWithPrivateKeyAsync(
    Authorization authorization, string tokenName, string tokenVersion,
    BigInteger chainId, string verifyingContract, string privateKey)

public Task<EthECDSASignature> SignWithWeb3Async(
    Authorization authorization, string tokenName, string tokenVersion,
    BigInteger chainId, string verifyingContract, IWeb3 web3, string signerAddress)

public string RecoverAddress(
    Authorization authorization, string tokenName, string tokenVersion,
    BigInteger chainId, string verifyingContract, EthECDSASignature signature)

public Task<EthECDSASignature> SignReceiveWithPrivateKeyAsync(
    Authorization authorization, string tokenName, string tokenVersion,
    BigInteger chainId, string verifyingContract, string privateKey)

public Task<EthECDSASignature> SignReceiveWithWeb3Async(
    Authorization authorization, string tokenName, string tokenVersion,
    BigInteger chainId, string verifyingContract, IWeb3 web3, string signerAddress)

public string RecoverReceiveAddress(
    Authorization authorization, string tokenName, string tokenVersion,
    BigInteger chainId, string verifyingContract, EthECDSASignature signature)

public byte[] ComputeTransferDigest(
    Authorization authorization, string tokenName, string tokenVersion,
    BigInteger chainId, string verifyingContract)

public byte[] ComputeReceiveDigest(
    Authorization authorization, string tokenName, string tokenVersion,
    BigInteger chainId, string verifyingContract)

public Task<EthECDSASignature> SignCancelWithWeb3Async(
    string authorizer, byte[] nonce, string tokenName, string tokenVersion,
    BigInteger chainId, string verifyingContract, IWeb3 web3, string signerAddress)
```

For a token whose `transferWithAuthorization` / `receiveWithAuthorization` takes a `bytes` signature (the ERC-1271 path for a signature that is not 65 bytes), the calls are these function messages:

```csharp
[Function("transferWithAuthorization")]
public class TransferWithAuthorizationBytesSignatureFunction : FunctionMessage
{
    public virtual string AuthorisationFrom { get; set; }       // from
    public virtual string AuthorisationTo { get; set; }         // to
    public virtual BigInteger Value { get; set; }               // value
    public virtual BigInteger ValidAfter { get; set; }          // validAfter
    public virtual BigInteger ValidBefore { get; set; }         // validBefore
    public virtual byte[] AuthorisationNonce { get; set; }      // nonce
    public virtual byte[] Signature { get; set; }               // signature
}

[Function("receiveWithAuthorization")]
public class ReceiveWithAuthorizationBytesSignatureFunction : FunctionMessage
{
    public virtual string From { get; set; }                    // from
    public virtual string To { get; set; }                      // to
    public virtual BigInteger Value { get; set; }               // value
    public virtual BigInteger ValidAfter { get; set; }          // validAfter
    public virtual BigInteger ValidBefore { get; set; }         // validBefore
    public virtual byte[] AuthorisationNonce { get; set; }      // nonce
    public virtual byte[] Signature { get; set; }               // signature
}
```

### Permit2 Building Blocks

The package also ships the generated EIP-3009 token bindings in the `Nethereum.Contracts.EIP3009.EIP3009` namespace (`Eip3009Service`, the `ContractDefinition` namespace with `Eip3009Deployment`, the functions `TransferWithAuthorization1Function`, `ReceiveWithAuthorization1Function`, `CancelAuthorization1Function` and `AuthorizationStateFunction`, the events `AuthorizationUsedEventDTO` and `AuthorizationCanceledEventDTO`, and `AuthorizationStateOutputDTO`), which the processors use to call the token.

The Permit2 witness transfer is signed by `Permit2WitnessSigner`, and an EIP-2612 permit by `Eip2612PermitSigner`:

```csharp
public class Permit2WitnessSigner
{
    public TypedData<DomainWithNameChainIdAndVerifyingContract> GetTypedData(BigInteger chainId, string permit2Address)
    public string Sign(PermitWitnessTransferFrom message, BigInteger chainId, string permit2Address, EthECKey key)
    public string RecoverSigner(PermitWitnessTransferFrom message, BigInteger chainId, string permit2Address, string signature)
    public byte[] ComputeDigest(PermitWitnessTransferFrom message, BigInteger chainId, string permit2Address)
}

public class Eip2612PermitSigner
{
    public TypedData<Domain> GetTypedData(string tokenName, string tokenVersion, BigInteger chainId, string tokenAddress)
    public string Sign(Permit permit, string tokenName, string tokenVersion, BigInteger chainId, string tokenAddress, EthECKey key)
    public string RecoverSigner(Permit permit, string tokenName, string tokenVersion, BigInteger chainId, string tokenAddress, string signature)
}

[Struct("PermitWitnessTransferFrom")]
public class PermitWitnessTransferFrom
{
    public virtual TokenPermissions Permitted { get; set; }
    public virtual string Spender { get; set; }
    public virtual BigInteger Nonce { get; set; }
    public virtual BigInteger Deadline { get; set; }
    public virtual Witness Witness { get; set; }
}

[Struct("Witness")]
public class Witness
{
    public virtual string To { get; set; }
    public virtual BigInteger ValidAfter { get; set; }
}
```

A `Permit2Authorization` maps to the contract messages with `Permit2AuthorizationMapper`, and a revert of the proxy is mapped to a reason with `Permit2SettlementErrorMapper`:

```csharp
public static class Permit2AuthorizationMapper
{
    public static PermitWitnessTransferFrom ToWitnessMessage(this Permit2Authorization auth)
    public static SettleFunction ToSettleFunction(this Permit2Authorization auth, string signatureHex)
    public static SettleWithPermitFunction ToSettleWithPermitFunction(
        this Permit2Authorization auth, string signatureHex, Eip2612PermitParams permit2612)
}

public static class Permit2SettlementErrorMapper
{
    public static string MapReason(SmartContractCustomErrorRevertException exception)
}
```

The x402ExactPermit2Proxy calls and custom errors, as message and error DTOs:

```csharp
[Function("settle")]
public class SettleFunction : FunctionMessage
{
    public virtual PermitTransferFrom Permit { get; set; }
    public virtual string Owner { get; set; }
    public virtual Witness Witness { get; set; }
    public virtual byte[] Signature { get; set; }
}

[Function("settleWithPermit")]
public class SettleWithPermitFunction : FunctionMessage
{
    public virtual Eip2612PermitParams Permit2612 { get; set; }
    public virtual PermitTransferFrom Permit { get; set; }
    public virtual string Owner { get; set; }
    public virtual Witness Witness { get; set; }
    public virtual byte[] Signature { get; set; }
}

[Struct("EIP2612Permit")]
public class Eip2612PermitParams
{
    public virtual BigInteger Value { get; set; }
    public virtual BigInteger Deadline { get; set; }
    public virtual byte[] R { get; set; }
    public virtual byte[] S { get; set; }
    public virtual byte V { get; set; }
}

[Function("nonces", "uint256")]
public class Eip2612NoncesFunction : FunctionMessage
{
    public virtual string Owner { get; set; }
}

[Error("Permit2612AmountMismatch")]
public class Permit2612AmountMismatchError : IErrorDTO { }

[Error("InvalidDestination")]
public class InvalidDestinationError : IErrorDTO { }

[Error("InvalidOwner")]
public class InvalidOwnerError : IErrorDTO { }

[Error("PaymentTooEarly")]
public class PaymentTooEarlyError : IErrorDTO { }
```

## Error Codes

`X402ErrorCodes` defines the reason strings. Verification failures arrive in `VerificationResponse.InvalidReason` and settlement failures in `SettlementResponse.ErrorReason`; both carry an `X402ErrorCodes` value. Every code a processor, the middleware or the cancel call can return:

| Code | Meaning |
|------|---------|
| `invalid_x402_version` | `PaymentPayload.X402Version` is not 2 |
| `invalid_exact_evm_scheme` | The payload's accepted scheme or the requirement's scheme is not `exact` |
| `unsupported_payload_type` | The processor does not handle the requirement's asset-transfer method (the EIP-3009 processors need `eip3009`, `X402ExactPermit2Service` needs `permit2`) |
| `invalid_exact_evm_network_mismatch` | The payload's accepted network differs from the requirement's network |
| `invalid_payload` | The payload lacks a field, is malformed JSON, carries an EIP-3009 nonce that is not `0x` followed by 64 hex characters, or carries a number that is not plain ASCII digits of at most 78 characters; the middleware also returns it (HTTP 400) for an undecodable `PAYMENT-SIGNATURE` header and when replay protection cannot derive a payment identity |
| `invalid_network` | Unsupported / unconfigured network: not `eip155:<chainId>`, or no RPC client for that chain id |
| `invalid_payment_requirements` | Asset missing on the EIP-3009 path |
| `invalid_exact_evm_missing_eip712_domain` | `extra` missing its EIP-712 `name` / `version` on the EIP-3009 path |
| `payment_required` | The `error` of the 402 returned when the request has no `PAYMENT-SIGNATURE` |
| `invalid_exact_evm_recipient_mismatch` | Recipient doesn't match `PayTo` |
| `invalid_exact_evm_authorization_value` | Authorized value differs from the requirement `Amount` |
| `invalid_exact_evm_signature` | EIP-712 signature verification failed, or the signature is longer than 65 bytes, the payer has code and the wallet rejects it as an ERC-1271 signature |
| `invalid_exact_evm_payload_undeployed_smart_wallet` | The signature is longer than 65 bytes and the payer has no contract code |
| `invalid_exact_evm_token_name_mismatch` / `invalid_exact_evm_token_version_mismatch` | After a failed settlement simulation, the token's on-chain `name()` / `version()` differs from `extra.name` / `extra.version` (an unrecoverable signature that names another token name or version returns `invalid_exact_evm_signature` without reading the token domain) |
| `invalid_exact_evm_eip3009_not_supported` | The token has no `authorizationState` |
| `invalid_exact_evm_insufficient_balance` | Payer doesn't have enough tokens |
| `invalid_exact_evm_nonce_already_used` | Nonce already consumed (EIP-3009 authorization state, Permit2 nonce bitmap, or a replay rejected by the middleware) |
| `invalid_exact_evm_payload_authorization_valid_after` | The latest block's timestamp is earlier than `validAfter` |
| `invalid_exact_evm_payload_authorization_valid_before` | `validBefore` is not more than 6 seconds after the latest block's timestamp |
| `invalid_exact_evm_transaction_simulation_failed` | Settlement dry-run (`eth_estimateGas`) reverted |
| `settlement_gas_exceeds_limit` | The settlement's gas estimate is above `X402FacilitatorOptions.MaxSettlementGas`; nothing is sent |
| `invalid_exact_evm_asset_not_allowed` | The asset is not in the `AllowedAssets` list configured for the network |
| `invalid_exact_evm_transfer_event_mismatch` | The settlement receipt does not contain exactly one matching `Transfer` |
| `invalid_exact_evm_transaction_failed` | The EIP-3009 settlement transaction was mined with a failed status |
| `invalid_transaction_state` | The Permit2 settlement (or cancel) transaction was mined with a failed status |
| `settlement_pending` | The settlement transaction was broadcast but no receipt was obtained; `Transaction` carries its hash; the middleware and `HttpFacilitatorClient` also use it when the outcome is unknown (a settle that throws, times out or answers a status outside the definite list) |
| `replay_store_full` | The replay store holds its maximum of live reservations; the middleware answers 503 and the endpoint does not run |
| `unexpected_verify_error` | Unhandled error while verifying; the middleware also uses it when an invalid verification has no reason |
| `unexpected_settle_error` | Unhandled error while settling; the middleware also uses it when a failure has no reason |
| `facilitator_error` | The `error` of the HTTP 500 the middleware returns when the facilitator's verify throws |
| `cancellation_error` | `CancelAuthorizationAsync` threw |
| `invalid_permit2_spender` | Permit2 spender is not the x402ExactPermit2Proxy |
| `invalid_permit2_recipient_mismatch` | Permit2 witness `to` doesn't match `PayTo` |
| `permit2_amount_mismatch` / `permit2_token_mismatch` | Permit2 amount/token doesn't match the requirement |
| `permit2_invalid_nonce` / `permit2_invalid_amount` | Settlement reverted with the Permit2 `InvalidNonce` / the proxy or Permit2 `InvalidAmount` |
| `permit2_invalid_destination` / `permit2_invalid_owner` / `permit2_payment_too_early` | Settlement reverted with the proxy's `InvalidDestination` / `InvalidOwner` / `PaymentTooEarly` |
| `permit2_allowance_required` | Payer hasn't approved Permit2 for the asset and no enabled gas-sponsoring extension covers it |
| `invalid_permit2_signature` | Permit2 EIP-712 signature verification failed |
| `permit2_deadline_expired` | Permit2 deadline is less than 6 seconds away, or settlement reverted with `SignatureExpired` |
| `permit2_not_yet_valid` | The witness `validAfter` is in the future |
| `permit2_insufficient_balance` | Payer's token balance is below the amount |
| `permit2_proxy_not_deployed` | The x402ExactPermit2Proxy has no code on the chain |
| `permit2_simulation_failed` | The `settleWithPermit` simulation failed and diagnosis found no specific cause |
| `permit2_2612_amount_mismatch` | EIP-2612 permit `value` differs from the Permit2 permitted amount (`settleWithPermit` revert or simulation) |
| `invalid_eip2612_extension_format` | `eip2612GasSponsoring` info does not match the declared schema |
| `eip2612_from_mismatch` / `eip2612_asset_mismatch` | Permit `from` is not the payer / `asset` is not the payment token |
| `eip2612_spender_not_permit2` | Permit spender is not the canonical Permit2 |
| `eip2612_deadline_expired` | Permit deadline is less than six seconds away |
| `invalid_erc20_approval_extension_format` | `erc20ApprovalGasSponsoring` info does not match the declared schema |
| `erc20_approval_from_mismatch` / `erc20_approval_asset_mismatch` | Approval `from` is not the payer / `asset` is not the payment token |
| `erc20_approval_spender_not_permit2` | Approval info spender is not the canonical Permit2 |
| `erc20_approval_tx_parse_failed` / `erc20_approval_tx_invalid_signature` | Signed approval transaction cannot be decoded / has no recoverable signer |
| `erc20_approval_tx_wrong_target` / `erc20_approval_tx_wrong_selector` | Transaction does not target the asset / does not call `approve` |
| `erc20_approval_tx_invalid_calldata` | Calldata is shorter than a full `approve` call, cannot be decoded, or its spender word has non-zero upper bytes |
| `erc20_approval_tx_wrong_spender` / `erc20_approval_tx_wrong_amount` | Decoded spender is not Permit2 / decoded amount differs from `info.amount` |
| `erc20_approval_tx_signer_mismatch` | Transaction is not signed by the payer |
| `erc20_approval_tx_chain_id_mismatch` / `erc20_approval_tx_value_not_zero` | Transaction chain id differs from the requirement / it sends value |
| `erc20_approval_tx_gas_cost_exceeds_cap` | `gasLimit × maxFeePerGas` exceeds `MaxSponsoredGasCost` |
| `erc20_approval_tx_nonce_mismatch` | Nonce is not the payer's pending transaction count |
| `erc20_approval_tx_max_fee_below_base_fee` | `maxFeePerGas` is below the latest block's base fee times 1.125, rounded up |
| `erc20_approval_tx_gas_limit_too_high` | The signed `gasLimit` is above the smaller of the latest block's gas limit and 16777216, or above the larger of 70000 and 1.5 times the estimated gas of the `approve` |
| `erc20_approval_tx_priority_fee_too_high` | The signed `maxPriorityFeePerGas` is above the larger of twice the node's suggestion and 100000000 wei |
| `erc20_approval_tx_max_fee_too_high` | The signed `maxFeePerGas` is above the larger of 3 times the latest base fee plus the priority fee and 1000000000 wei |
| `erc20_approval_tx_priority_fee_too_low` | The signed `maxPriorityFeePerGas` is below 1 wei |
| `erc20_approval_amount_below_required` | Approved amount is below the payment amount |
| `erc20_approval_tx_unsupported_type` | Signed approval is not an EIP-1559 transaction |
| `erc20_approval_tx_in_flight` | Another settle is already relaying an approval with the same payer and nonce |
| `erc20_approval_tx_gas_limit_too_low` | `eth_estimateGas` of the `approve` exceeds the signed gas limit |
| `erc20_approval_tx_simulation_failed` | `eth_estimateGas` of the `approve` from the payer failed |
| `gas_funding_failed` | Funding the payer's approval gas failed |
| `erc20_approval_broadcast_failed` / `erc20_approval_tx_failed` | Relaying the approval failed to broadcast / mined with a failed status |
| `unsupported_scheme` / `invalid_exact_evm_payload_authorization_value_mismatch` | Defined in `X402ErrorCodes` for API compatibility but never returned by this library: an unsupported scheme is reported as `invalid_exact_evm_scheme` and a value mismatch as `invalid_exact_evm_authorization_value` |

## Payment Flow (v2)

The default `authorization` flow:

```
1. Client → Server:   GET /api/premium (no PAYMENT-SIGNATURE)
2. Server → Client:   402 + PaymentRequired (PAYMENT-REQUIRED header + body)
3. Client:            Selects a requirement, signs it off-chain (EIP-3009 or Permit2), no gas
4. Client → Server:   GET /api/premium + PAYMENT-SIGNATURE (base64 PaymentPayload)
5. Server/Facilitator: Verifies signature, binding, balance, time window
6. Server:            Reserves the payment identity (replay protection) and runs the handler, response buffered
7. Server/Facilitator: If the handler status is below 400, settles on-chain (transferWithAuthorization / receiveWithAuthorization / proxy.settle / proxy.settleWithPermit); a handler status of 400 or above is returned as-is without settlement
8. Server → Client:   200 OK + content + PAYMENT-RESPONSE (base64 SettlementResponse); a failed settlement returns 402 + PAYMENT-RESPONSE instead
```

The `upfront` flow (`extra.paymentFlow = "upfront"`) settles before the handler runs, after verifying the payment:

```
1. Client → Server:   GET /api/generate (no PAYMENT-SIGNATURE)
2. Server → Client:   402 + PaymentRequired (accepts[].extra.paymentFlow = "upfront")
3. Client:            Selects a requirement (prefers "authorization" when both are offered), signs it off-chain
4. Client → Server:   GET /api/generate + PAYMENT-SIGNATURE (base64 PaymentPayload)
4a. Server → Facilitator: Verifies; on failure: 402 or 400, nothing is reserved or settled
5. Server/Facilitator: Settles on-chain; on failure: 402 + PAYMENT-RESPONSE, the handler does not run
6. Server:            Runs the handler
7. Server → Client:   The handler response (any status) + PAYMENT-RESPONSE + Cache-Control including private
```

## Interoperability

The scenarios in which the coinbase/x402 reference clients and facilitators are run against this library on an Anvil fork of Base Sepolia (chain 84532) are in `tests/Nethereum.X402.IntegrationTests/Integration/X402Reference*InteropTests.cs` (category `LiveNetwork`: they need the fork and the reference implementations):

| Direction | Reference | Covered |
|-----------|-----------|---------|
| Our client → reference facilitator | TypeScript (`@x402` 2.28.0) | EIP-3009, Permit2, Permit2 + `eip2612GasSponsoring`, Permit2 + `erc20ApprovalGasSponsoring` |
| Reference client → our server and facilitator | TypeScript client (`@x402` 2.28.0 and 2.10.0) | the same four, plus an `upfront` route |
| Reference client → our server and facilitator | Go client | EIP-3009, Permit2 + `eip2612GasSponsoring`, `upfront` |
| Reference client → our server and facilitator | Python client | EIP-3009, Permit2 + `eip2612GasSponsoring`, `upfront` |
| Our client → reference facilitator | Go facilitator | EIP-3009, Permit2 + `eip2612GasSponsoring` |

## References

- [x402 Protocol (coinbase/x402)](https://github.com/coinbase/x402)
- [EIP-3009: Transfer With Authorization](https://eips.ethereum.org/EIPS/eip-3009)
- [Uniswap Permit2](https://github.com/Uniswap/permit2)
- [CAIP-2: Blockchain ID Specification](https://standards.chainagnostic.org/CAIPs/caip-2)
