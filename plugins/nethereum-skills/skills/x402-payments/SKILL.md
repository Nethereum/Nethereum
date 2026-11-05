---
name: x402-payments
description: Accept HTTP 402 cryptocurrency payments using Nethereum (.NET/C#). Use this skill whenever the user asks about x402 protocol (version 2), HTTP 402 payments, pay-per-request APIs, EIP-3009 transfer authorization, Permit2 payments, upfront (settle-before-handler) payment flow, EIP-2612 or ERC-20 approval gas sponsoring, USDC payments, crypto API monetization, payment middleware, or accepting cryptocurrency payments in ASP.NET with C# or .NET.
user-invocable: true
---

# x402: Crypto Payments (protocol v2)

The x402 protocol implements HTTP 402 (Payment Required) for pay-per-request APIs. A client pays with a signed authorization — off-chain (no gas) — and the server or a facilitator settles on-chain. Nethereum's `Nethereum.X402` package provides `X402HttpClient` for automatic payments and `X402Middleware` for ASP.NET Core endpoint protection.

This library targets **x402 version 2**: payments carry `x402Version = 2`, the chosen requirement is echoed in the payload's `accepted` field, and networks use **CAIP-2** identifiers (`eip155:<chainId>`, e.g. `eip155:8453` for Base). The "exact" scheme supports two EVM asset-transfer methods: **EIP-3009** (default) and **Permit2**, and two payment flows: **authorization** (default: verify, run the handler, then settle) and **upfront** (settle before the handler runs). Permit2 payers who have never approved Permit2 can be helped by two **gas-sponsoring extensions** (`eip2612GasSponsoring`, `erc20ApprovalGasSponsoring`).

NuGet: `Nethereum.X402`

```bash
dotnet add package Nethereum.X402
```

The v2 wire uses three base64 headers: `PAYMENT-REQUIRED` (402 response), `PAYMENT-SIGNATURE` (request), `PAYMENT-RESPONSE` (200 response, every handler response in the upfront flow, or 402 when settlement failed). The decoders accept standard base64 with or without `=` padding and reject anything that is not base64.

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

builder.Services.AddX402Services("https://x402.org/facilitator");

app.UseX402(options =>
{
    options.FacilitatorUrl = "https://x402.org/facilitator";
    options.Routes.Add(new RoutePaymentConfig("/api/premium/*", new PaymentRequirements
    {
        Scheme = "exact",
        Network = "eip155:8453",
        Amount = "1000000",
        Asset = "0x833589fCD6eDb6E08f4c7C32D4f71b54bdA02913",
        PayTo = "0xYourAddress",
        MaxTimeoutSeconds = 60,
        Extra = new ExactSchemeExtra { AssetTransferMethod = "eip3009", Name = "USD Coin", Version = "2" }
    }));
});
```

The EIP-712 `Name` must be the token's own domain name: `"USD Coin"` for USDC on Base mainnet (`eip155:8453`), `"USDC"` for USDC on Base Sepolia (`eip155:84532`).

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
| Permit2 | `X402ExactPermit2Service` | `x402ExactPermit2Proxy.settle` (`settleWithPermit` with EIP-2612 sponsoring) |
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

The Permit2/proxy addresses are canonical constants (`X402Permit2Addresses`), the same on every EVM chain. The underlying Permit2 signing lives in `Nethereum.Signer.EIP712`/`Nethereum.ABI`, and the contract in `Nethereum.Contracts` (`web3.Eth.GetPermit2Service()`). The facilitator requires the payer's Permit2 allowance unless a gas-sponsoring extension (below) supplies it.

## Upfront Payment Flow

By default a route uses the `authorization` flow: verify, run the endpoint, settle only if the endpoint succeeded. Set `extra.paymentFlow = X402PaymentFlows.Upfront` (`"upfront"`) to settle **before** the handler runs; omitting `paymentFlow` means `authorization`. Supported for the EIP-3009 and Permit2 methods; any other flow value makes `X402Options.Validate` throw `InvalidOperationException`.

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

- The middleware calls only the facilitator's settle (never verify); replay protection still reserves the payment before settling.
- Settlement failure (or exception, `unexpected_settle_error`): `402` with `PAYMENT-RESPONSE` and `Cache-Control: no-store`; the handler does not run.
- On success the handler runs and every response it produces (including `4xx`/`5xx`) carries `PAYMENT-RESPONSE` and a `Cache-Control` that includes `private`. The payer is already charged, so a handler failure does not undo the settlement.
- Client: no configuration. The selector skips requirements with an unrecognised flow, prefers `authorization` when both flows are offered for the same network and scheme, and pays `upfront` when it is the only one.

## Permit2 Gas Sponsoring

Permit2 normally needs a one-time ERC-20 approval from the payer. Two extensions let the facilitator cover it. The server declares them in `RoutePaymentConfig.Extensions` (`Dictionary<string, object>?`, served in `PaymentRequired.Extensions`); keys are `X402ExtensionKeys.Eip2612GasSponsoring` and `X402ExtensionKeys.Erc20ApprovalGasSponsoring`. Both are used only while the payer's Permit2 allowance is below the payment amount.

### eip2612GasSponsoring (tokens with EIP-2612 permit)

The payer signs a permit; the facilitator settles through `x402ExactPermit2Proxy.settleWithPermit`. Declare it with `X402GasSponsoringExtensions.DeclareEip2612()`; the route's `extra` must carry the token's `Name` and `Version`:

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

The client needs an RPC client for the payment's chain in `X402HttpClientOptions.RpcClientsByChainId` (`Dictionary<int, IClient>?`, null by default; used only for sponsoring):

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

The client attaches the permit (`Eip2612GasSponsoringInfo`, `value` = the payment amount) only when the requirement is permit2, the server declared the extension, `extra.name`/`extra.version` are present, an RPC client is configured for the chain, and the allowance is below the amount. The attached extension is the server's declaration with the client's `info` fields added (server-declared values win; the client only adds missing fields). The facilitator (`X402ExactPermit2Service`) always advertises `eip2612GasSponsoring` in `GetSupportedAsync().Extensions`:

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

It uses `settleWithPermit` only when the allowance is short; with a sufficient allowance the extension is ignored. Rejections: `invalid_eip2612_extension_format`, `eip2612_from_mismatch`, `eip2612_asset_mismatch`, `eip2612_spender_not_permit2`, `eip2612_deadline_expired` (deadline less than 6 seconds away), `permit2_2612_amount_mismatch` (permit `value` differs from the permitted amount), and `permit2_allowance_required` when no complete extension is present.

### erc20ApprovalGasSponsoring (any ERC-20, opt-in)

The payer pre-signs an EIP-1559 `approve(Permit2, MaxUint256)` transaction (gas 70000, nonce = pending count, node fee suggestion with a 1 gwei / 0.1 gwei fallback); the facilitator broadcasts it, funding the payer's gas if needed. The client uses the same `RpcClientsByChainId` option and signs it only when the server declared the extension, the allowance is short and the eip2612 permit was not attached (eip2612 wins). Declare it with `X402GasSponsoringExtensions.DeclareErc20Approval()`:

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

The facilitator spends its own ETH, so it is off by default. Opt in with `Erc20ApprovalGasSponsoringOptions` (`Enabled` = false by default; `MaxSponsoredGasCost` in wei, must be positive when enabled or the constructor throws `InvalidOperationException`; `Settlement` = an `IErc20ApprovalSponsoredSettlement`, default `Erc20ApprovalSponsoredSettlement`). `AddX402ExactProcessor` does not take the options, so compose the processor with the `X402ExactPermit2Service` overloads that do:

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

`erc20ApprovalGasSponsoring` is advertised in `/supported` only when `Enabled`; with sponsoring disabled a payment that relies on it is rejected with `permit2_allowance_required`. Verification decodes the signed transaction and checks: target = asset, selector `approve`, spender = Permit2, amount = `info.amount` and at least the payment amount, signer = payer, chain id = requirement chain, value 0, `gasLimit x maxFeePerGas` within `MaxSponsoredGasCost`, nonce = payer's pending count, `maxFeePerGas` at least 1.125 times the latest base fee, rounded up (codes in the table below).

Settlement is **sequential, not atomic**: fund the payer's gas deficit, relay the approval and wait for its receipt, then settle through `proxy.settle`; the returned transaction is the settle transaction, and the approval stands if the settle then fails. **Residual risk:** the payer can spend the funded ETH through a conflicting transaction at the same nonce, costing the facilitator up to `MaxSponsoredGasCost` per request; the payment itself cannot be diverted (the witness fixes the recipient).

## Failure Responses and Response Buffering

Rejections before the endpoint runs return a `PaymentRequired` body whose `error` is the reason, with `Cache-Control: no-store`: **402** (no `PAYMENT-SIGNATURE` = `payment_required`; verification failed = the processor's `InvalidReason`, `unexpected_verify_error` if none; a replayed payment = `invalid_exact_evm_nonce_already_used`), **400** (`invalid_payload`, including a header that is not valid base64/JSON), **412** (`permit2_allowance_required`), and **500** with body `{"error":"facilitator_error"}` when the facilitator's verify throws, and **503** with body `{"error":"replay_store_full"}` and `Cache-Control: no-store` when the replay store is full (the endpoint does not run). A failed settlement returns 402 with the failed `SettlementResponse` in `PAYMENT-RESPONSE`.

In the `authorization` flow the middleware runs the endpoint with its response buffered and settles only when the status is below 400 (a 4xx/5xx endpoint response is returned as-is, unsettled). `X402Options.MaxBufferedResponseBytes` (`long`, default 1 MiB) caps the buffer; a larger response gives 500 and no settlement. The `upfront` flow does not buffer. `ResourceInfo` (on `RoutePaymentConfig.Resource`) also has `ServiceName` (printable ASCII, at most 32 characters), `Tags` (at most 5, each printable ASCII of at most 32 characters) and `IconUrl` (absolute http/https URL of at most 2048 characters); `X402Options.Validate` throws `InvalidOperationException` otherwise.

## Client Payment Policy (allow-list)

The automatic client signs whatever a 402 demands up to `MaxAmount`. `X402HttpClientOptions.Policy` pins what it will pay; each list is empty (unrestricted) by default, and anything outside a non-empty list throws `X402PaymentPolicyViolationException` before anything is signed:

```csharp
using Nethereum.X402.Client;

var options = new X402HttpClientOptions
{
    PreferredNetwork = "eip155:8453",
    MaxAmount = "1000000"
};

options.Policy.AllowedNetworks.Add("eip155:8453");
options.Policy.AllowedAssets.Add("0x833589fCD6eDb6E08f4c7C32D4f71b54bdA02913");
options.Policy.AllowedRecipients.Add("0xMyTrustedProvider");

var client = new X402HttpClient(httpClient, payerPrivateKey, options);
```

When a 402 offers several `accepts`, `X402HttpClientOptions.Selector` (default `DefaultPaymentRequirementsSelector`, replaceable with any `IPaymentRequirementsSelector`) picks one: supported requirements matching `PreferredNetwork`/`PreferredScheme` (default `"exact"`), `authorization` flow first. `AddX402Client(privateKey)` registers a manual-mode client only (`GetAsync(uri, requirements)`); the automatic `GetAsync(uri)`, `PostAsync` and the rest throw unless you construct `new X402HttpClient(httpClient, payerPrivateKey, options)` yourself.

## Replay Protection

The middleware reserves each payment identity (payer + nonce) through `IPaymentReplayStore` after verify and before the endpoint (or, upfront, before settling); a replay gets 402 / `invalid_exact_evm_nonce_already_used`. On by default (`X402Options.EnablePaymentReplayProtection`, `PaymentReplayRetention` default 1 hour). The default `InMemoryPaymentReplayStore` is single-instance: for several instances register a shared store with `builder.Services.AddSingleton<IPaymentReplayStore, MyRedisPaymentReplayStore>();`.

## Facilitator: Client and Hosting

`IFacilitatorClient` (`VerifyAsync`, `SettleAsync`, `GetSupportedAsync`) is what `X402Middleware` calls; `HttpFacilitatorClient(httpClient, baseUrl)` implements it over HTTP. `AddX402Services(url)` registers one; `AddX402FacilitatorProxy(url, routes)` also registers an `X402FacilitatorProxyProcessor` (route matching via `FindMatchingRoute`, header decoding via `DecodePaymentHeader`). To host your own facilitator over an `IX402PaymentProcessor`:

```csharp
using Nethereum.X402.Extensions;

builder.Services.AddX402ExactProcessor(facilitatorAccount, rpcByChainId);
builder.Services.AddControllers().AddX402FacilitatorControllers();

app.MapControllers();
```

This exposes `POST /facilitator/verify`, `POST /facilitator/settle` and `GET /facilitator/supported`. They are open unless `X402FacilitatorOptions.AuthorizationPolicy` names an ASP.NET Core authorization policy (401 for an unauthenticated caller, 403 for a caller failing the policy); the facilitator key pays gas, so set the policy or protect the endpoints at the host (API keys, mTLS, network ACL). `X402TransferWithAuthorisation3009Service.CancelAuthorizationAsync` voids a signed but unsettled EIP-3009 authorization. The token needs the authorizer's own EIP-712 `CancelAuthorization` signature, so sign it first (`TransferWithAuthorisationSigner.SignCancelWithPrivateKeyAsync(authorizer, nonce, tokenName, tokenVersion, chainId, verifyingContract, privateKey)`, or `SignCancelWithWeb3Async`; both return an `EthECDSASignature`):

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

The signature overload is `CancelAuthorizationAsync(authorizerAddress, nonce, authorizerSignature, network, tokenAddress, cancellationToken)`, returning a `CancelAuthorizationResponse` (`Success`, `ErrorReason`, `Transaction`, `Network`). It simulates first: `invalid_network` for an unconfigured network, `invalid_transaction_state` when the simulation reverts (e.g. a signature from another key) or the transaction fails, `cancellation_error` for any other exception. The older overload without a signature is `[Obsolete]`: it sends a zero signature and can never succeed.

## Network Configuration

`NetworkConfiguration.Default` provides RPC endpoints, USDC addresses and chain ids by network name:

```csharp
using Nethereum.X402.Blockchain;

var config = NetworkConfiguration.Default;
var rpcUrl = config.GetRpcEndpoint("base");
var usdcAddress = config.GetUSDCAddress("base");
var chainId = config.GetChainId("base");
```

CAIP-2 ids are parsed/formatted with `Caip2.FormatEip155(chainId)` and `Caip2.TryParseEip155ChainId(network, out chainId)`.

## Build EIP-3009 Authorizations Directly

For custom flows:

```csharp
using Nethereum.X402.Signers;

var builder = new TransferWithAuthorisationBuilder();
var signer = new TransferWithAuthorisationSigner();

var authorization = builder.BuildFromPaymentRequirements(requirements, payerAddress);
```

The default window is `validAfter` = 10 minutes ago and `validBefore` = now + `requirements.MaxTimeoutSeconds` (1 hour when that is not positive). Sign with a private key:

```csharp
var signature = await signer.SignWithPrivateKeyAsync(
    authorization, "USD Coin", "2", chainId, usdcAddress, payerPrivateKey);
```

Or sign through a Web3 account (`web3.Eth.AccountSigning.SignTypedDataV4`), which returns the same signature; `ArgumentException` for `signerAddress` if the Web3 account address differs from `signerAddress`:

```csharp
using Nethereum.Web3;
using Nethereum.Web3.Accounts;

var web3 = new Web3(new Account(payerPrivateKey, chainId));

var signature = await signer.SignWithWeb3Async(
    authorization, "USDC", "2", chainId, usdcAddress, web3, payerAddress);
```

## Several Offers per Route (`Accepts`)

Set `RoutePaymentConfig.Accepts` to a list of `PaymentRequirements` (for example one `eip3009` and one `permit2` option) and the 402's `accepts` carries the whole list; `GetOffers()` returns `Accepts`, or `Requirements` as a one-element list. A payment is accepted only when its `accepted` equals one of the offers, compared as JSON values (key order ignored, `extra` and unknown properties included). The matching offer, not the payload's copy, is what the middleware sends to verify and settle and what replay protection is keyed on; a payment matching none gets 402 `No matching payment requirements` (`X402ErrorMessages.NoMatchingPaymentRequirements`) and no facilitator call. `PaymentRequirements` and `ResourceInfo` keep unknown JSON properties in `ExtensionData`, and `X402HttpClient` echoes `accepted` and `resource` unchanged.

## Client Options

`X402HttpClientOptions`: `MaxAmount` (required positive integer string), `PreferredNetwork` (required), `PreferredScheme` (`"exact"`), `Selector`, `Policy`, `RpcClientsByChainId` (`Dictionary<int, IClient>?`, null; used only for gas sponsoring), `CapSponsoredApprovalToPaymentAmount` (`bool`, `false`: the sponsored ERC-20 approval is for `MaxUint256` unless `true`, then for the payment amount) and `MaxAuthorizationValiditySeconds` (`int`, 3600, must be positive). The signed authorization expires at now plus the smaller of the requirement's `MaxTimeoutSeconds` and `MaxAuthorizationValiditySeconds` (`validBefore` for EIP-3009, `deadline` for Permit2), so a server cannot make the client sign a long-lived authorization. `X402HttpClient.Address` is the payer address.

## Facilitator Options (`X402FacilitatorOptions`)

The guards every facilitator transaction passes through. Register them with `AddX402FacilitatorOptions` (the `AddX402TransferProcessor`, `AddX402ExactProcessor` and `AddX402ReceiveProcessor` registrations use them), or pass them to the processor constructors:

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

- `MaxSettlementGas` (`long`, 500000): a facilitator transaction whose gas estimate is above it is refused with `settlement_gas_exceeds_limit` (also at EIP-3009 verify); it is sent with a gas limit of 120% of the estimate, never above the ceiling.
- `AllowedAssets` (`Dictionary<string, List<string>>`): per CAIP-2 network the assets accepted; any other asset gets `invalid_exact_evm_asset_not_allowed` before any RPC; a network without an entry is unrestricted; networks match on the parsed chain id.
- `RoutePrefix` (`"facilitator"`): path prefix of the hosted endpoints; `""` mounts them at the root; must not be `null`.
- `AuthorizationPolicy` (`string?`): the authorization policy the hosted endpoints require; `null` leaves them open.
- `RequiredConfirmations` (1) and `MaxReceiptWaitSeconds` (3600): settlement waits for the receipt's block to have that many confirmations, within `maxTimeoutSeconds` clamped to between 1 second and `MaxReceiptWaitSeconds`, otherwise `settlement_pending` with the hash.
- `FacilitatorAddresses` (empty): the addresses of every facilitator signer in the deployment, so front-run recovery never reports a transaction sent by one of them.
- `Validate()` throws `InvalidOperationException` for a non-positive ceiling, fewer than one confirmation, a wait below one second or a `null` `RoutePrefix`, and `ArgumentException` for an `AllowedAssets` key that is not a canonical `eip155:<chainId>` id (also thrown by `AddX402FacilitatorOptions`). `IsAssetAllowed(network, asset)` tells whether the allowlist admits an asset.

The facilitator sends its transactions one at a time per account address and chain id across the process. If a settle reverts because a third party already submitted the payer's signed payment, it reports success with that transaction's hash only when the chain proves the transaction belongs to this payment; otherwise the original failure stands (`invalid_transaction_state` for Permit2, `unexpected_settle_error` or `invalid_exact_evm_transaction_failed` for EIP-3009).

## Contract-Wallet Payers (ERC-1271)

When ECDSA recovery does not match the payer, verify falls back to ERC-1271 `isValidSignature` if the payer holds contract code. An EIP-3009 signature that is not exactly 65 bytes is never truncated into `v`, `r`, `s`: it is valid only as an ERC-1271 signature of `from`. A payer without contract code gets `invalid_exact_evm_payload_undeployed_smart_wallet`, a deployed wallet that rejects it gets `invalid_exact_evm_signature`, and settlement calls the token's `transferWithAuthorization(..., bytes signature)` overload (`receiveWithAuthorization` for the receive processor); a token without that overload fails the verify simulation with `invalid_exact_evm_transaction_simulation_failed`. A 65-byte signature keeps the `v`, `r`, `s` overload, and because verify dry-runs the settlement a contract-wallet payment on that overload passes only if the token validates ERC-1271 inside it. On the Permit2 path the canonical Permit2 contract performs the ERC-1271 check on-chain.

## Results, Messages and `/supported`

`VerificationResponse.InvalidMessage` and `SettlementResponse.ErrorMessage` carry an optional human-readable cause next to `InvalidReason` / `ErrorReason`; they are omitted when null and pass through `HttpFacilitatorClient` and the hosted controllers. When the facilitator answers a non-success HTTP status with a JSON body carrying `isValid` or `success`, `HttpFacilitatorClient` returns it as the failed response (reason kept); any other non-success answer throws `HttpRequestException`. `GET /facilitator/supported` lists each (`x402Version`, `scheme`, `network`) once and without an `extra`, as the x402 v2 specification's example shows. `HttpFacilitatorClient` bounds verify by `VerifyTimeout` (30 seconds, `TimeoutException`) and settle by 3 times the requirement's `maxTimeoutSeconds` plus `SettleTimeoutGrace` (30 seconds), returning `unexpected_settle_error` when the bound expires; the HttpClient registered by `AddX402Services` has an infinite own timeout so these bounds govern.

## Known Deviations and Nethereum Extensions

- ERC-20 approval sponsoring is **sequential, not atomic**: the x402 `erc20_gas_sponsoring.md` extension describes an atomic batch of the approval and the settle. Implement `IErc20ApprovalSponsoredSettlement` and set `Erc20ApprovalGasSponsoringOptions.Settlement` to supply an atomic sender (for example a bundler).
- Sponsored approvals must be EIP-1559 transactions; anything else gets `erc20_approval_tx_unsupported_type`.
- Not in the x402 specification: the `ResourceInfo` fields `serviceName`, `tags` and `iconUrl`, and `extra.paymentFlow = "upfront"`.
- `unsupported_scheme` and `invalid_exact_evm_payload_authorization_value_mismatch` are defined in `X402ErrorCodes` but never returned.

## Error Codes (`X402ErrorCodes`)

Verification failures arrive in `VerificationResponse.InvalidReason` and settlement failures in `SettlementResponse.ErrorReason`; both carry an `X402ErrorCodes` value. Every code a processor, the middleware or the cancel call can return:

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
| `settlement_pending` | The settlement transaction was broadcast but no receipt was obtained; `Transaction` carries its hash |
| `replay_store_full` | The replay store holds its maximum of live reservations; the middleware answers 503 and the endpoint does not run |
| `unexpected_verify_error` | Unhandled error while verifying; the middleware also uses it when an invalid verification has no reason |
| `unexpected_settle_error` | Unhandled error while settling; the middleware also uses it when settlement throws or a failure has no reason, and `HttpFacilitatorClient` returns it when the settle bound expires |
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
