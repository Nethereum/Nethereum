# X402 Facilitator Server Example

This example demonstrates how to build a complete x402 facilitator server using the `Nethereum.X402` library.

## Overview

The facilitator server implements the x402 protocol endpoints for payment verification and settlement. It uses:
- **TransferWithAuthorization** pattern from EIP-3009
- **IAccount** interface for flexible account management
- Reusable validation logic from the library
- ASP.NET Core controllers with dependency injection

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
    "scheme": "exact-evm",
    "payload": {
      "authorization": { /* EIP-3009 authorization */ }
    }
  },
  "paymentRequirements": {
    "network": "sepolia",
    "value": 1000000
  }
}
```

**Response:**
```json
{
  "success": true,
  "transactionHash": null
}
```

### 2. POST /facilitator/settle
Executes a verified payment authorization.

**Request:** Same as /verify

**Response:**
```json
{
  "success": true,
  "transactionHash": "0x..."
}
```

### 3. GET /facilitator/supported
Returns supported payment schemes and networks.

**Response:**
```json
{
  "paymentKinds": [
    {
      "scheme": "exact-evm",
      "networks": ["sepolia", "base-sepolia"]
    }
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
    },
    "TokenAddresses": {
      "Sepolia": "0x1c7D4B196Cb0C7B01d743Fbc6116a902379C7238",
      "BaseSepolia": "0x036CbD53842c5426634e7929541eC2318f3dCF7e"
    }
  }
}
```

### Configuration Keys

- **FacilitatorPrivateKey**: Private key for signing and sending transactions
- **RpcEndpoints**: RPC URLs for each supported network
- **TokenAddresses**: EIP-3009 compatible token contract addresses
- **TokenNames**: Token names for EIP-712 domain (default: "USD Coin")
- **TokenVersions**: Token versions for EIP-712 domain (default: "2")

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

The example shows three ways to configure the facilitator account:

### Option 1: Private Key String (Simple)
```csharp
builder.Services.AddX402TransferProcessor(
    facilitatorPrivateKey,  // String converted to IAccount internally
    rpcEndpoints,
    tokenAddresses,
    chainIds,
    tokenNames,
    tokenVersions);
```

### Option 2: IAccount Instance (Recommended)
```csharp
var facilitatorAccount = new Account(facilitatorPrivateKey);
builder.Services.AddX402TransferProcessor(
    facilitatorAccount,  // IAccount instance
    rpcEndpoints,
    tokenAddresses,
    chainIds,
    tokenNames,
    tokenVersions);
```

### Option 3: Factory Function (Advanced)
```csharp
builder.Services.AddX402TransferProcessor(
    sp => {
        // Resolve account from other services
        var keyManager = sp.GetRequiredService<IKeyManager>();
        return keyManager.GetFacilitatorAccount();
    },
    rpcEndpoints,
    tokenAddresses,
    chainIds,
    tokenNames,
    tokenVersions);
```

## Using Different Account Types

The `IAccount` interface supports various account implementations:

### Regular Account
```csharp
var account = new Account(privateKey);
```

### Managed Account (Web3 Provider)
```csharp
var managedAccount = new ManagedAccount("0xYourAddress", "password");
```

### External Signer (Custom Implementation)
```csharp
public class HardwareWalletAccount : IAccount
{
    public string Address { get; }

    public Task<string> SignAsync(byte[] message)
    {
        // Delegate to hardware wallet
    }

    public Task<string> TransactionManager { get; }
}

var hwAccount = new HardwareWalletAccount(ledgerDevice);
```

## Validation and Error Handling

The facilitator automatically performs these validations:

1. **Signature Verification** - Validates EIP-712 signatures
2. **Authorization Timing** - Checks validAfter/validBefore
3. **Balance Check** - Ensures sufficient token balance
4. **Nonce Verification** - Prevents replay attacks
5. **Network Validation** - Confirms correct chain ID
6. **Recipient Matching** - Validates payment recipient

Error responses follow x402 specification:
```json
{
  "success": false,
  "error": "insufficient_funds",
  "transactionHash": null
}
```

### Error Codes

- `insufficient_funds` - Not enough token balance
- `invalid_exact_evm_payload_signature` - Invalid signature
- `invalid_exact_evm_payload_authorization_valid_after` - Not yet valid
- `invalid_exact_evm_payload_authorization_valid_before` - Expired
- `invalid_exact_evm_payload_authorization_value` - Wrong value
- `invalid_exact_evm_payload_authorization_nonce_used` - Nonce already used
- `invalid_network` - Unsupported or wrong network
- `invalid_payload` - Malformed request

See `X402ErrorCodes.cs` for complete list.

## Customization

### Using ReceiveWithAuthorization Instead

To use the "receive" pattern instead of "transfer":

```csharp
var receiverAccount = new Account(receiverPrivateKey);
builder.Services.AddX402ReceiveProcessor(
    receiverAccount,
    rpcEndpoints,
    tokenAddresses,
    chainIds,
    tokenNames,
    tokenVersions);
```

### Supporting Additional Networks

Add more networks to the configuration dictionaries:

```csharp
var rpcEndpoints = new Dictionary<string, string>
{
    { "sepolia", "https://rpc.sepolia.org" },
    { "base-sepolia", "https://sepolia.base.org" },
    { "mainnet", "https://mainnet.infura.io/v3/YOUR_KEY" },
    { "base", "https://mainnet.base.org" }
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
- Consider Redis for distributed nonce tracking
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
