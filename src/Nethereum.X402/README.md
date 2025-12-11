# Nethereum.X402

A .NET implementation of the x402 protocol for accepting HTTP 402 (Payment Required) payments using EIP-3009 USDC transfers on Ethereum-compatible blockchains.

## Overview

Nethereum.X402 enables seamless cryptocurrency payment integration for HTTP APIs by implementing the x402 protocol. It supports both self-facilitated and facilitated payment patterns, allowing services to accept USDC payments with minimal friction.

### Key Features

- **HTTP 402 Payment Required**: Standards-compliant implementation of the x402 protocol
- **EIP-3009 Integration**: Gasless token transfers using signed authorizations (USDC, PYUSD, EURC)
- **Flexible Architecture**: Support for both self-facilitated and third-party facilitated payment flows
- **ASP.NET Core Integration**: Middleware and filters for easy API integration
- **Type-Safe**: Full .NET type safety with comprehensive DTOs
- **Testable**: Built with dependency injection and testability in mind

### Supported Payment Patterns

1. **Self-Facilitated**: Service manages its own payment infrastructure
2. **Facilitated**: Leverage third-party facilitators for payment processing
3. **Hybrid**: Mix both patterns as needed

## Installation

```bash
dotnet add package Nethereum.X402
```

### Prerequisites

- .NET 8.0 or .NET 9.0
- Nethereum.Web3 (automatically included)
- Access to an Ethereum-compatible RPC endpoint

## Quick Start

### 1. Configure Services

```csharp
using Nethereum.X402;

var builder = WebApplication.CreateBuilder(args);

// Add x402 services
builder.Services.AddX402(options =>
{
    options.RpcUrl = "https://your-rpc-endpoint.com";
    options.ChainId = 1; // Mainnet
    options.UsdcAddress = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48";
    options.PaymentReceiverAddress = "0xYourAddress";
    options.FacilitatorUrl = "https://facilitator.x402.org"; // Optional
});

builder.Services.AddControllers();
var app = builder.Build();
app.MapControllers();
app.Run();
```

### 2. Protect API Endpoints

```csharp
using Microsoft.AspNetCore.Mvc;
using Nethereum.X402.Filters;

[ApiController]
[Route("api/[controller]")]
public class PremiumController : ControllerBase
{
    [HttpGet("content")]
    [X402PaymentRequired(
        amount: 1_000000, // $1.00 USDC (6 decimals)
        description: "Access to premium content"
    )]
    public IActionResult GetPremiumContent()
    {
        return Ok(new { data = "Premium content here" });
    }
}
```

### 3. Client Integration

```csharp
using Nethereum.X402.Client;

var httpClient = new HttpClient();
var x402Client = new X402Client(httpClient, web3, senderPrivateKey);

// Make payment-required request
var response = await x402Client.GetAsync(
    "https://api.example.com/api/premium/content"
);

if (response.IsSuccessStatusCode)
{
    var content = await response.Content.ReadAsStringAsync();
    Console.WriteLine($"Received: {content}");
}
```

## Architecture

### Core Components

#### X402Service
Central service for managing x402 payment flows. Handles payment verification, signature validation, and blockchain interaction.

```csharp
public class X402Service
{
    Task<bool> VerifyPaymentAsync(X402PaymentHeader paymentHeader);
    Task<X402PaymentProposal> CreatePaymentProposalAsync(PaymentRequest request);
    Task<TransactionReceipt> SubmitPaymentAsync(X402PaymentHeader payment);
}
```

#### X402PaymentHeader
Complete payment information including EIP-3009 authorization signature.

```csharp
public class X402PaymentHeader
{
    public string From { get; set; }
    public string To { get; set; }
    public decimal Amount { get; set; }
    public string Token { get; set; }
    public string Nonce { get; set; }
    public long ValidAfter { get; set; }
    public long ValidBefore { get; set; }
    public string Signature { get; set; } // EIP-3009 signature (v,r,s)
    public int ChainId { get; set; }
}
```

#### X402PaymentProposal
Server's payment request sent to clients via HTTP 402 response.

```csharp
public class X402PaymentProposal
{
    public string PaymentId { get; set; }
    public string To { get; set; }
    public decimal Amount { get; set; }
    public string Token { get; set; }
    public int ChainId { get; set; }
    public long ValidBefore { get; set; }
    public string Description { get; set; }
}
```

### Payment Flow

```
1. Client → Server: GET /api/premium/content
2. Server → Client: 402 Payment Required + X402PaymentProposal
3. Client: Signs EIP-3009 authorization
4. Client → Server: GET /api/premium/content + X-Payment header
5. Server: Verifies signature, submits to blockchain
6. Server → Client: 200 OK + content
```

## Configuration

### X402Options

```csharp
public class X402Options
{
    // Required: Blockchain configuration
    public string RpcUrl { get; set; }
    public int ChainId { get; set; }
    public string UsdcAddress { get; set; }

    // Self-facilitated mode
    public string PaymentReceiverAddress { get; set; }
    public string PaymentReceiverPrivateKey { get; set; }

    // Facilitated mode
    public string FacilitatorUrl { get; set; }
    public string FacilitatorApiKey { get; set; }

    // Payment defaults
    public long DefaultValidityWindow { get; set; } = 3600; // 1 hour
    public decimal MinimumPaymentAmount { get; set; } = 0.01m;
}
```

### appsettings.json

```json
{
  "X402": {
    "RpcUrl": "https://mainnet.infura.io/v3/YOUR-PROJECT-ID",
    "ChainId": 1,
    "UsdcAddress": "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48",
    "PaymentReceiverAddress": "0xYourAddress",
    "FacilitatorUrl": "https://facilitator.x402.org"
  }
}
```

## Usage Examples

### Self-Facilitated Payment Server

A complete example of a server managing its own payment infrastructure:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.X402;

var builder = WebApplication.CreateBuilder(args);

// Configure self-facilitated mode
builder.Services.AddX402(options =>
{
    options.RpcUrl = "https://mainnet.infura.io/v3/YOUR-PROJECT-ID";
    options.ChainId = 1;
    options.UsdcAddress = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48";
    options.PaymentReceiverAddress = "0xYourReceiverAddress";
    options.PaymentReceiverPrivateKey = Environment.GetEnvironmentVariable("PAYMENT_PRIVATE_KEY");
});

builder.Services.AddControllers();

var app = builder.Build();
app.MapControllers();
app.Run();
```

### Client Using Facilitator

Example client leveraging a facilitator for payment processing:

```csharp
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.X402.Client;

// Setup client with facilitator
var account = new Account(privateKey);
var web3 = new Web3(account, "https://mainnet.infura.io/v3/YOUR-PROJECT-ID");

var httpClient = new HttpClient();
var x402Client = new X402Client(httpClient, web3, privateKey)
{
    FacilitatorUrl = "https://facilitator.x402.org"
};

// Make request - payment handled automatically
var response = await x402Client.GetAsync("https://api.example.com/premium/data");
var data = await response.Content.ReadAsStringAsync();
```

### Custom Payment Validation

Implement custom validation logic for payments:

```csharp
services.AddX402(options => { /* ... */ })
    .AddPaymentValidator<CustomPaymentValidator>();

public class CustomPaymentValidator : IPaymentValidator
{
    private readonly ILogger<CustomPaymentValidator> _logger;

    public CustomPaymentValidator(ILogger<CustomPaymentValidator> logger)
    {
        _logger = logger;
    }

    public async Task<bool> ValidateAsync(X402PaymentHeader payment)
    {
        // Check minimum amount
        if (payment.Amount < 1.0m)
        {
            _logger.LogWarning("Payment amount {Amount} below minimum", payment.Amount);
            return false;
        }

        // Verify sender is not blacklisted
        if (await IsBlacklistedAsync(payment.From))
        {
            _logger.LogWarning("Payment from blacklisted address {Address}", payment.From);
            return false;
        }

        return true;
    }
}
```

### Dynamic Pricing

Implement dynamic pricing based on context:

```csharp
[HttpGet("content/{contentId}")]
[X402PaymentRequired(description: "Premium content")]
public async Task<IActionResult> GetPremiumContent(
    string contentId,
    [FromServices] X402Service x402Service,
    [FromServices] IPricingService pricingService)
{
    // Calculate price based on content, user, time, etc.
    var price = await pricingService.GetPriceAsync(contentId, User);

    if (!Request.Headers.ContainsKey("X-Payment"))
    {
        var proposal = await x402Service.CreatePaymentProposalAsync(
            new PaymentRequest
            {
                Amount = price,
                Description = $"Access to content {contentId}"
            });
        return StatusCode(402, proposal);
    }

    // Verify payment matches expected price
    var payment = ParsePaymentHeader(Request.Headers["X-Payment"]);
    if (payment.Amount < price)
    {
        return StatusCode(402, new { error = "Insufficient payment amount" });
    }

    var isValid = await x402Service.VerifyPaymentAsync(payment);
    if (!isValid)
    {
        return Unauthorized(new { error = "Invalid payment" });
    }

    await x402Service.SubmitPaymentAsync(payment);
    return Ok(await GetContentAsync(contentId));
}
```

### Facilitator Discovery

Automatically discover facilitators for a service:

```csharp
using Nethereum.X402.Client;

var discoveryClient = new FacilitatorDiscoveryClient(httpClient);

// Discover facilitator for a specific service
var facilitator = await discoveryClient.DiscoverFacilitatorAsync(
    "https://api.example.com"
);

Console.WriteLine($"Found facilitator: {facilitator.Url}");
Console.WriteLine($"Supported chains: {string.Join(", ", facilitator.SupportedChainIds)}");
Console.WriteLine($"Fee: {facilitator.FeePercentage}%");

// Use discovered facilitator
var x402Client = new X402Client(httpClient, web3, privateKey)
{
    FacilitatorUrl = facilitator.Url
};
```

### Payment Event Monitoring

Monitor and react to payment events:

```csharp
services.AddX402(options => { /* ... */ })
    .AddPaymentEventHandler<PaymentEventLogger>();

public class PaymentEventLogger : IPaymentEventHandler
{
    private readonly ILogger<PaymentEventLogger> _logger;
    private readonly IEmailService _emailService;

    public PaymentEventLogger(
        ILogger<PaymentEventLogger> logger,
        IEmailService emailService)
    {
        _logger = logger;
        _emailService = emailService;
    }

    public async Task OnPaymentReceivedAsync(PaymentReceivedEvent evt)
    {
        _logger.LogInformation(
            "Payment received: {Amount} USDC from {From} to {To}",
            evt.Amount, evt.From, evt.To);

        // Send notification
        await _emailService.SendAsync(
            to: "admin@example.com",
            subject: "Payment Received",
            body: $"Received ${evt.Amount} from {evt.From}");
    }

    public async Task OnPaymentFailedAsync(PaymentFailedEvent evt)
    {
        _logger.LogError(
            "Payment failed: {Reason} for payment from {From}",
            evt.Reason, evt.From);
    }
}
```

## EIP-3009 Integration

Nethereum.X402 uses Nethereum's EIP-3009 standard implementation for gasless token transfers.

### Overview

EIP-3009 enables token transfers to be executed by relaying a signed authorization, allowing gas fees to be paid by a third party. This is perfect for x402 payments where the facilitator can submit the transaction.

### Creating a Transfer Authorization

```csharp
using Nethereum.Contracts.Standards.EIP3009;
using Nethereum.Signer;
using Nethereum.Signer.EIP712;

// Get EIP-3009 service
var web3 = new Web3("https://rpc-url");
var eip3009Service = web3.Eth.EIP3009;
var usdcService = eip3009Service.GetContractService(usdcAddress);

// Create authorization parameters
var from = senderAddress;
var to = receiverAddress;
var value = new BigInteger(1_000000); // $1.00 USDC
var validAfter = new BigInteger(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
var validBefore = new BigInteger(DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds());
var nonce = GenerateRandomNonce(); // 32-byte random nonce

// Sign the authorization using EIP-712
var domain = new EIP712Domain
{
    Name = "USD Coin",
    Version = "2",
    ChainId = chainId,
    VerifyingContract = usdcAddress
};

var message = new TransferWithAuthorizationMessage
{
    From = from,
    To = to,
    Value = value,
    ValidAfter = validAfter,
    ValidBefore = validBefore,
    Nonce = nonce
};

var signature = await signer.SignTypedDataAsync(domain, message);
var (v, r, s) = ParseSignature(signature);

// Submit the authorization (can be done by anyone)
var receipt = await usdcService.TransferWithAuthorizationRequestAndWaitForReceiptAsync(
    from, to, value, validAfter, validBefore, nonce, v, r, s
);
```

### Checking Authorization State

```csharp
// Check if an authorization has been used
bool isUsed = await usdcService.AuthorizationStateQueryAsync(
    authorizer: senderAddress,
    nonce: authorizationNonce
);

if (isUsed)
{
    Console.WriteLine("Authorization has already been used");
}
```

### Canceling Authorization

```csharp
// Cancel an unused authorization
var cancelReceipt = await usdcService.CancelAuthorizationRequestAndWaitForReceiptAsync(
    authorizer: senderAddress,
    nonce: authorizationNonce,
    v: cancelV,
    r: cancelR,
    s: cancelS
);
```

## Supported Tokens

Any EIP-3009 compliant token can be used with Nethereum.X402:

| Token | Network | Address | Decimals |
|-------|---------|---------|----------|
| USDC | Ethereum Mainnet | `0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48` | 6 |
| USDC | Polygon | `0x3c499c542cEF5E3811e1192ce70d8cC03d5c3359` | 6 |
| USDC | Arbitrum | `0xaf88d065e77c8cC2239327C5EDb3A432268e5831` | 6 |
| USDC | Optimism | `0x0b2C639c533813f4Aa9D7837CAf62653d097Ff85` | 6 |
| USDC | Base | `0x833589fCD6eDb6E08f4c7C32D4f71b54bdA02913` | 6 |
| PYUSD | Ethereum Mainnet | `0x6c3ea9036406852006290770BEdFcAbA0e23A0e8` | 6 |
| EURC | Ethereum Mainnet | `0x1aBaEA1f7C830bD89Acc67eC4af516284b1bC33c` | 6 |

## Security Considerations

### Private Key Management

Never expose private keys in code, configuration files, or version control. Use secure key management:

```csharp
// Use environment variables
var privateKey = Environment.GetEnvironmentVariable("PAYMENT_PRIVATE_KEY");

// Or use Azure Key Vault, AWS Secrets Manager, etc.
var keyVaultClient = new SecretClient(vaultUri, credential);
var secret = await keyVaultClient.GetSecretAsync("payment-private-key");
var privateKey = secret.Value.Value;
```

### Signature Validation

Always verify EIP-3009 signatures before accepting payments:

```csharp
public async Task<bool> VerifySignatureAsync(X402PaymentHeader payment)
{
    // Verify the signature matches the from address
    var recoveredAddress = RecoverSignerAddress(payment);
    if (!recoveredAddress.Equals(payment.From, StringComparison.OrdinalIgnoreCase))
    {
        _logger.LogWarning("Signature verification failed");
        return false;
    }

    // Check authorization hasn't been used
    var isUsed = await _usdcService.AuthorizationStateQueryAsync(
        payment.From, payment.Nonce);
    if (isUsed)
    {
        _logger.LogWarning("Authorization already used");
        return false;
    }

    return true;
}
```

### Nonce Management

Ensure nonces are cryptographically random and never reused:

```csharp
using System.Security.Cryptography;

public byte[] GenerateNonce()
{
    var nonce = new byte[32];
    using (var rng = RandomNumberGenerator.Create())
    {
        rng.GetBytes(nonce);
    }
    return nonce;
}
```

### Time Validity

Enforce validity windows to prevent authorization reuse:

```csharp
public bool IsValidTimeWindow(X402PaymentHeader payment)
{
    var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    if (now < payment.ValidAfter)
    {
        _logger.LogWarning("Payment not yet valid");
        return false;
    }

    if (now > payment.ValidBefore)
    {
        _logger.LogWarning("Payment expired");
        return false;
    }

    // Ensure reasonable validity window (e.g., max 24 hours)
    if (payment.ValidBefore - payment.ValidAfter > 86400)
    {
        _logger.LogWarning("Validity window too large");
        return false;
    }

    return true;
}
```

### Amount Verification

Always verify payment amounts match expectations:

```csharp
public bool VerifyAmount(X402PaymentHeader payment, decimal expectedAmount)
{
    // Allow small tolerance for rounding (0.01 USDC = 10000 units)
    var tolerance = 10000;
    var expectedUnits = (long)(expectedAmount * 1_000000);
    var actualUnits = (long)(payment.Amount * 1_000000);

    if (Math.Abs(actualUnits - expectedUnits) > tolerance)
    {
        _logger.LogWarning(
            "Amount mismatch: expected {Expected}, got {Actual}",
            expectedAmount, payment.Amount);
        return false;
    }

    return true;
}
```

### Rate Limiting

Implement rate limiting to prevent abuse:

```csharp
services.AddRateLimiter(options =>
{
    options.AddPolicy("payment", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString(),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 10
            }));
});

// Apply to endpoints
[HttpGet("content")]
[EnableRateLimiting("payment")]
[X402PaymentRequired(amount: 1_000000)]
public IActionResult GetContent() { /* ... */ }
```

## Troubleshooting

### Payment Verification Fails

**Problem**: Payment header is rejected by server

**Possible Causes**:
- EIP-712 domain signature is incorrect for the chain ID
- Nonce has been previously used
- `validBefore` timestamp has expired
- Sender doesn't have sufficient USDC balance
- USDC contract address doesn't match the chain

**Solutions**:
```csharp
// Verify chain ID matches
if (payment.ChainId != _options.ChainId)
{
    return BadRequest("Incorrect chain ID");
}

// Check authorization state
var isUsed = await _usdcService.AuthorizationStateQueryAsync(
    payment.From, payment.Nonce);
if (isUsed)
{
    return BadRequest("Authorization already used");
}

// Verify time window
var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
if (now > payment.ValidBefore)
{
    return BadRequest("Authorization expired");
}
```

### Transaction Reverts

**Problem**: Blockchain transaction fails when submitting payment

**Possible Causes**:
- Authorization has been used or canceled
- Invalid signature
- Time constraints not met
- Insufficient balance

**Solutions**:
```csharp
try
{
    var receipt = await _usdcService.TransferWithAuthorizationRequestAndWaitForReceiptAsync(
        payment.From, payment.To, payment.Value,
        payment.ValidAfter, payment.ValidBefore, payment.Nonce,
        payment.V, payment.R, payment.S
    );

    if (receipt.Status == 0)
    {
        // Transaction reverted - check error reason
        var errorReason = await _web3.Eth.GetContractTransactionErrorReason
            .SendRequestAsync(receipt.TransactionHash);
        _logger.LogError("Transaction reverted: {Reason}", errorReason);
    }
}
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to submit payment");
    throw;
}
```

### Facilitator Unavailable

**Problem**: Facilitator endpoint returns errors or is unreachable

**Solutions**:

Implement fallback strategies:

```csharp
public class ResilientX402Client
{
    private readonly List<string> _facilitatorUrls;
    private readonly X402Client _client;

    public async Task<HttpResponseMessage> GetWithFallbackAsync(string url)
    {
        foreach (var facilitatorUrl in _facilitatorUrls)
        {
            try
            {
                _client.FacilitatorUrl = facilitatorUrl;
                return await _client.GetAsync(url);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex,
                    "Facilitator {Url} failed, trying next", facilitatorUrl);
            }
        }

        // Fall back to self-facilitated mode
        _client.FacilitatorUrl = null;
        return await _client.GetAsync(url);
    }
}
```

## Additional Resources

- [x402 Protocol Specification](https://github.com/x402/x402)
- [EIP-3009: Transfer With Authorization](https://eips.ethereum.org/EIPS/eip-3009)
- [USDC Developer Documentation](https://developers.circle.com/stablecoins/docs)
- [Nethereum Documentation](https://docs.nethereum.com)
