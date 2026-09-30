# X402 Facilitator Server Example

This example demonstrates how to build a complete x402 facilitator server using the `Nethereum.X402` library.

## Overview

The facilitator server implements the x402 protocol endpoints for payment verification and settlement. It uses:
- **Both "exact"-scheme asset-transfer methods** — EIP-3009 `transferWithAuthorization` and **Permit2**
  (`permitWitnessTransferFrom` via the x402ExactPermit2Proxy) — registered together with
  `AddX402ExactProcessor`, which routes each payment by its payload shape
- **IAccount** interface for flexible account management
- Reusable validation logic from the library
- ASP.NET Core controllers with dependency injection

`Program.cs` registers the processor with `builder.Services.AddX402ExactProcessor(account, rpcEndpointsByChainId)`.
Use `AddX402TransferProcessor` instead if you only want the EIP-3009 method.

## Architecture

```
FacilitatorServer
├── Program.cs              # Application setup and configuration
├── appsettings.json        # Production configuration
└── README.md               # This file

Dependencies:
└── Nethereum.X402 (library)
    ├── FacilitatorController    # Reusable controller with 3 endpoints
    ├── FacilitatorModels        # Request/response models
    └── IX402PaymentProcessor    # Shared validation interface
```

## API Endpoints

The facilitator exposes these x402-compliant endpoints:

### 1. POST /facilitator/verify
Verifies a payment authorization without executing it.

**Request:**
```json
{
  "paymentPayload": {
    "x402Version": 2,
    "accepted": { "scheme": "exact", "network": "eip155:84532" },
    "payload": {
      "signature": "0x...",
      "authorization": { "from": "0x...", "to": "0x...", "value": "1000000", "validAfter": "...", "validBefore": "...", "nonce": "0x..." }
    }
  },
  "paymentRequirements": {
    "scheme": "exact",
    "network": "eip155:84532",
    "amount": "1000000",
    "asset": "0x036CbD53842c5426634e7929541eC2318f3dCF7e",
    "payTo": "0xYourReceiverAddress",
    "maxTimeoutSeconds": 60,
    "extra": { "name": "USDC", "version": "2" }
  }
}
```

**Response:**
```json
{
  "isValid": true,
  "invalidReason": null,
  "payer": "0x..."
}
```

### 2. POST /facilitator/settle
Executes a verified payment authorization.

**Request:** Same as /verify

**Response:**
```json
{
  "success": true,
  "transaction": "0x...",
  "network": "eip155:84532",
  "payer": "0x...",
  "amount": "1000000"
}
```

### 3. GET /facilitator/supported
Returns supported payment kinds (one per configured chain id).

**Response:**
```json
{
  "kinds": [
    { "x402Version": 2, "scheme": "exact", "network": "eip155:84532" }
  ]
}
```

## Configuration

### Required Settings

Edit `appsettings.json` to configure your facilitator:

```json
{
  "X402": {
    "FacilitatorPrivateKey": "YOUR_PRIVATE_KEY_HERE",
    "RpcEndpoints": {
      "Sepolia": "https://rpc.sepolia.org",
      "BaseSepolia": "https://sepolia.base.org"
    }
  }
}
```

`Program.cs` maps those RPC endpoints into a `Dictionary<int, string>` keyed by chain id
(`11155111` → Sepolia, `84532` → Base Sepolia) and passes it to `AddX402ExactProcessor`.

### Configuration Keys

- **FacilitatorPrivateKey**: Private key for signing and sending settlement transactions
- **RpcEndpoints**: RPC URL per supported network

That is the entire per-chain configuration. In v2 the token address, chain id and EIP-712 domain
(name/version) all come from each request's `PaymentRequirements` (`asset`, the CAIP-2 `network`, and
`extra.name`/`extra.version`), so the facilitator does not configure token addresses, names or
versions.

## Running the Server

### Prerequisites

1. .NET 8.0 SDK or later
2. Private key with funds on target networks
3. RPC endpoint access (Infura, Alchemy, or local node)

### Steps

1. **Configure settings**
```bash
cd FacilitatorServer
# Edit appsettings.json with your configuration
```

2. **Build the project**
```bash
dotnet build
```

3. **Run the server**
```bash
dotnet run
```

The server will start on `http://localhost:5000`

4. **Test the API**

Visit Swagger UI at: `http://localhost:5000/swagger`

Or use curl:
```bash
# Check supported networks
curl http://localhost:5000/facilitator/supported

# Verify a payment
curl -X POST http://localhost:5000/facilitator/verify \
  -H "Content-Type: application/json" \
  -d @payment-request.json
```

## Account Management Options

Each `AddX402…Processor` overload takes the facilitator account (as a private-key string, an
`IAccount`, or an `IServiceProvider` factory) plus a `Dictionary<int, string>` of RPC endpoints
keyed by chain id. There are no token/name/version parameters — those come from each request's
`PaymentRequirements`.

```csharp
var rpcEndpointsByChainId = new Dictionary<int, string>
{
    { 11155111, "https://rpc.sepolia.org" },  // Sepolia
    { 84532,    "https://sepolia.base.org" }  // Base Sepolia
};
```

### Option 1: Private Key String (Simple)
```csharp
builder.Services.AddX402ExactProcessor(
    facilitatorPrivateKey,     // string, converted to an Account internally
    rpcEndpointsByChainId);
```

### Option 2: IAccount Instance (Recommended)
```csharp
var facilitatorAccount = new Account(facilitatorPrivateKey);
builder.Services.AddX402ExactProcessor(
    facilitatorAccount,        // IAccount — also supports external signers/KMS
    rpcEndpointsByChainId);
```

### Option 3: Factory Function (Advanced)
```csharp
builder.Services.AddX402ExactProcessor(
    sp =>
    {
        var keyManager = sp.GetRequiredService<IKeyManager>();
        return keyManager.GetFacilitatorAccount();  // returns an IAccount
    },
    rpcEndpointsByChainId);
```

`AddX402TransferProcessor` (EIP-3009 only) and `AddX402ReceiveProcessor` expose the same three
overloads.

## Using Different Account Types

The facilitator account is any Nethereum `IAccount`, so an external signer (hardware wallet, KMS)
is supported by supplying an `IAccount` that wraps it — e.g. an `ExternalAccount` backed by an
`IEthExternalSigner`. A simple in-process account is just:

```csharp
var account = new Account(privateKey);
```

## Validation and Error Handling

The facilitator automatically performs these validations:

1. **Signature Verification** - Validates EIP-712 signatures
2. **Authorization Timing** - Checks validAfter/validBefore
3. **Balance Check** - Ensures sufficient token balance
4. **Nonce Verification** - Prevents replay attacks
5. **Network Validation** - Confirms correct chain ID
6. **Recipient Matching** - Validates payment recipient

A failed verify carries the reason in `invalidReason`; a failed settle in `errorReason`:
```json
{
  "success": false,
  "errorReason": "invalid_exact_evm_insufficient_balance",
  "transaction": null
}
```

### Error Codes

- `invalid_exact_evm_insufficient_balance` - Not enough token balance
- `invalid_exact_evm_signature` - Invalid signature
- `invalid_exact_evm_payload_authorization_valid_after` - Not yet valid
- `invalid_exact_evm_payload_authorization_valid_before` - Expired
- `invalid_exact_evm_authorization_value` - Wrong value
- `invalid_exact_evm_nonce_already_used` - Nonce already used (also a rejected replay)
- `invalid_exact_evm_transaction_simulation_failed` - Settlement dry-run reverted
- `invalid_network` - Unsupported or wrong network
- `invalid_payload` - Malformed request

See `X402ErrorCodes.cs` for the complete list.

## Customization

### Using ReceiveWithAuthorization Instead

To use the "receive" pattern instead of "transfer":

```csharp
var receiverAccount = new Account(receiverPrivateKey);
builder.Services.AddX402ReceiveProcessor(
    receiverAccount,
    rpcEndpointsByChainId);
```

### Supporting Additional Networks

Add more chain ids to the RPC dictionary:

```csharp
var rpcEndpointsByChainId = new Dictionary<int, string>
{
    { 11155111, "https://rpc.sepolia.org" },              // Sepolia
    { 84532,    "https://sepolia.base.org" },             // Base Sepolia
    { 1,        "https://mainnet.infura.io/v3/YOUR_KEY" },// Ethereum mainnet
    { 8453,     "https://mainnet.base.org" }              // Base
};
```

### Custom Processor Implementation

Implement `IX402PaymentProcessor` for custom logic:

```csharp
public class CustomProcessor : IX402PaymentProcessor
{
    public async Task<VerificationResponse> VerifyPaymentAsync(
        PaymentPayload payload,
        PaymentRequirements requirements,
        CancellationToken cancellationToken)
    {
        // Custom verification logic
    }

    // ... other methods
}

builder.Services.AddSingleton<IX402PaymentProcessor, CustomProcessor>();
```

## Production Considerations

### Security

1. **Never commit private keys** - Use environment variables or key vaults
2. **Use HTTPS** - Configure SSL certificates for production
3. **Rate limiting** - Protect endpoints from abuse
4. **Authentication** - Add API key or OAuth validation
5. **Monitoring** - Log transactions and errors

### Configuration Management

```csharp
// Read from environment variable
var privateKey = Environment.GetEnvironmentVariable("X402_PRIVATE_KEY")
    ?? builder.Configuration["X402:FacilitatorPrivateKey"];

// Or use Azure Key Vault, AWS Secrets Manager, etc.
```

### Scaling

- The processor is stateless and can be scaled horizontally
- If you also run the `X402Middleware` on scaled resource servers, register a distributed
  `IPaymentReplayStore` (e.g. Redis-backed) so replay reservations are shared across replicas — the
  default store is in-process
- Use read replicas for RPC endpoints

## Testing

Test your facilitator with the x402 test suite:

```bash
# From the repository root
cd dotnet/tests/Nethereum.X402.IntegrationTests
dotnet test --filter FacilitatorTests
```

## Resources

- [x402 Protocol Specification](https://github.com/x402/spec)
- [EIP-3009: Transfer With Authorization](https://eips.ethereum.org/EIPS/eip-3009)
- [Nethereum Documentation](https://docs.nethereum.com/)

## Support

For issues or questions:
- GitHub Issues: [x402/dotnet](https://github.com/x402/dotnet/issues)
- x402 Discord: [discord.gg/x402](https://discord.gg/x402)
