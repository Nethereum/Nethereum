# Nethereum.Wallet.RpcRequests

EIP-1193 JSON-RPC method handlers for wallet-dApp interaction. Provides handlers for account management, chain switching, signing requests, transaction approval, and permission management that bridge dApps to Nethereum wallet capabilities.

## Overview

Nethereum.Wallet.RpcRequests implements EIP-1193 wallet RPC methods with user prompting and permission management. Each handler processes JSON-RPC requests from dApps and coordinates with wallet services to:

- Request user approval for account access, transactions, and signatures
- Validate parameters and enforce permission checks
- Switch networks or add custom chains
- Return standard JSON-RPC responses or errors

**Supported RPC Methods:**
- **Account Access** - `eth_accounts`, `eth_requestAccounts`
- **Chain Management** - `eth_chainId`, `wallet_addEthereumChain`, `wallet_switchEthereumChain`
- **Signing** - `personal_sign`, `eth_signTypedData_v4`
- **Transactions** - `eth_sendTransaction`
- **Permissions** - `wallet_requestPermissions`, `wallet_getPermissions`, `wallet_revokePermissions`

## Installation

```bash
dotnet add package Nethereum.Wallet.RpcRequests
```

Or via Package Manager Console:

```powershell
Install-Package Nethereum.Wallet.RpcRequests
```

## Dependencies

**Package References:**
- Nethereum.JsonRpc.Client
- Nethereum.RPC
- Nethereum.Wallet
- Nethereum.Web3

## Handler Base Class

All RPC method handlers extend `RpcMethodHandlerBase` which provides:

```csharp
public abstract class RpcMethodHandlerBase : IRpcMethodHandler
{
    public abstract string MethodName { get; }
    public abstract Task<RpcResponseMessage> HandleAsync(
        RpcRequestMessage request,
        IWalletContext context);

    // Error helpers
    protected RpcResponseMessage MethodNotImplemented(object id);
    protected RpcResponseMessage InvalidParams(object id, string? message = null);
    protected RpcResponseMessage UserRejected(object id);
    protected RpcResponseMessage InternalError(object id, string? message = null);
}
```

**From:** `src/Nethereum.Wallet.RpcRequests/RpcMethodHandlerBase.cs:8`

## Account Access Handlers

### eth_accounts

Returns the selected account address if dApp has permission.

```csharp
// MethodName: "eth_accounts"
// Returns: [address] or [] if no permission
```

**Behavior:**
1. Returns empty array if no account selected
2. Checks if origin has permission for selected account
3. Returns array with account address if permitted
4. Returns empty array if not permitted

**From:** `src/Nethereum.Wallet.RpcRequests/EthAccountsHandler.cs:10`

### eth_requestAccounts

Requests account access permission from user.

```csharp
// MethodName: "eth_requestAccounts"
// Returns: [address] or error 4001 if rejected
```

**Behavior:**
1. Returns empty array if no account selected
2. Checks existing permission for origin and account
3. Prompts user for permission if not already granted
4. Returns error 4001 (User rejected) if denied
5. Returns array with account address if approved

**From:** `src/Nethereum.Wallet.RpcRequests/EthRequestAccountsHandler.cs:10`

## Chain Management Handlers

### eth_chainId

Returns the current active chain ID.

```csharp
// MethodName: "eth_chainId"
// Returns: "0x1" (hex-encoded chain ID)
```

**Behavior:**
- Returns hex-encoded chain ID from wallet context
- Defaults to "0x1" (Ethereum Mainnet) if not set

**From:** `src/Nethereum.Wallet.RpcRequests/EthChainIdHandler.cs:9`

### wallet_addEthereumChain

Adds a custom network or switches to existing network.

```csharp
// MethodName: "wallet_addEthereumChain"
// Params: AddEthereumChainParameter
// Returns: null or error
```

**Behavior:**
1. Enables provider (prompts for account if needed)
2. Validates AddEthereumChainParameter with chainId
3. If chain already exists:
   - Prompts user to switch to existing chain
   - Returns null on success or error on failure
4. If chain does not exist:
   - Prompts user to add new chain
   - Switches to new chain if approved
5. Returns error 4001 if user rejects

**From:** `src/Nethereum.Wallet.RpcRequests/WalletAddEthereumChainHandler.cs:13`

**AddEthereumChainParameter Structure:**
- `chainId` (required) - Hex-encoded chain ID
- `chainName` - Network name
- `rpcUrls` - Array of RPC URLs
- `nativeCurrency` - { name, symbol, decimals }
- `blockExplorerUrls` - Array of explorer URLs

### wallet_switchEthereumChain

Switches to a different network.

```csharp
// MethodName: "wallet_switchEthereumChain"
// Params: SwitchEthereumChainParameter { chainId }
// Returns: null or error
```

**Behavior:**
1. Enables provider
2. Validates chainId parameter
3. Prompts user to switch network
4. Returns null on success
5. Returns error 4001 if user rejects
6. Returns error -32603 if switch fails

**From:** `src/Nethereum.Wallet.RpcRequests/WalletSwitchEthereumChainHandler.cs:10`

## Signing Handlers

### personal_sign

Signs a personal message using eth_sign prefix.

```csharp
// MethodName: "personal_sign"
// Params: [message, address] or [address, message]
// Returns: signature (hex) or error 4001 if rejected
```

**Behavior:**
1. Enables provider if no account selected
2. Extracts message and address from parameters (order-agnostic)
3. Validates address matches selected account
4. Detects hex messages and attempts UTF-8 decoding
5. Creates SignaturePromptContext with:
   - Original message
   - Decoded message (if hex)
   - dApp origin and metadata
6. Prompts user for signature approval
7. Returns hex signature or error 4001 if rejected

**From:** `src/Nethereum.Wallet.RpcRequests/PersonalSignHandler.cs:13`

**Message Detection:**
- Checks if message is hex-encoded
- Attempts UTF-8 decoding for display
- Provides both raw and decoded message to user

**From:** `src/Nethereum.Wallet.RpcRequests/PersonalSignHandler.cs:102`

### eth_signTypedData_v4

Signs EIP-712 typed structured data.

```csharp
// MethodName: "eth_signTypedData_v4"
// Params: [address, typedDataJson]
// Returns: signature (hex) or error 4001 if rejected
```

**Behavior:**
1. Enables provider if no account selected
2. Validates parameters (address, typedDataJson)
3. Deserializes and validates EIP-712 structure
4. Validates domain chainId matches active chain
5. Extracts domain metadata:
   - Domain name
   - Domain version
   - Verifying contract address
   - Chain ID
6. Creates TypedDataSignPromptContext with domain info
7. Prompts user for signature approval
8. Validates address matches selected account
9. Returns hex signature or error 4001 if rejected

**From:** `src/Nethereum.Wallet.RpcRequests/EthSignTypedDataV4Handler.cs:14`

**Chain ID Validation:**
```csharp
var domainChainId = typedDataRaw.GetChainIdFromDomain();
var contextChainId = context.ChainId?.Value;

if (domainChainId != null && contextChainId != null && domainChainId != contextChainId)
{
    return InvalidParams(id, $"Domain chainId {domainChainId} does not match active context chainId {contextChainId}");
}
```

**From:** `src/Nethereum.Wallet.RpcRequests/EthSignTypedDataV4Handler.cs:51`

## Transaction Handler

### eth_sendTransaction

Sends a transaction with user approval.

```csharp
// MethodName: "eth_sendTransaction"
// Params: TransactionInput
// Returns: transaction hash (hex) or error 4001 if rejected
```

**Behavior:**
1. Validates TransactionInput parameter
2. Sets `from` field to selected account if not provided
3. Returns InvalidParams if `from` address missing
4. Prompts user to approve transaction
5. Returns transaction hash on approval
6. Returns error 4001 if user rejects

**From:** `src/Nethereum.Wallet.RpcRequests/EthSendTransactionHandler.cs:15`

## Permission Handlers

### wallet_requestPermissions

Requests permissions for dApp.

```csharp
// MethodName: "wallet_requestPermissions"
// Returns: [{ parentCapability: "eth_accounts", caveats: [] }] or error 4001
```

**Behavior:**
1. Returns empty array if no account selected
2. Checks existing permission for origin and account
3. Prompts user if permission not granted
4. Returns error 4001 if user rejects
5. Returns permissions array with eth_accounts capability

**From:** `src/Nethereum.Wallet.RpcRequests/WalletRequestPermissionsHandler.cs:12`

**Response Format:**
```json
[
  {
    "parentCapability": "eth_accounts",
    "caveats": []
  }
]
```

### wallet_getPermissions

Gets current permissions for dApp (implementation varies by wallet).

```csharp
// MethodName: "wallet_getPermissions"
// Returns: permissions array
```

### wallet_revokePermissions

Revokes permissions for dApp (implementation varies by wallet).

```csharp
// MethodName: "wallet_revokePermissions"
// Returns: null or error
```

## RPC Errors

Standard JSON-RPC error codes used by handlers:

```csharp
public static class RpcErrors
{
    // -32601: Method not found
    public static RpcResponseMessage MethodNotFound(object id);

    // -32602: Invalid parameters
    public static RpcResponseMessage InvalidParams(object id, string? message = null);

    // 4001: User rejected the request (EIP-1193)
    public static RpcResponseMessage UserRejected(object id);

    // -32603: Internal error
    public static RpcResponseMessage InternalError(object id, string? message = null);
}
```

**From:** `src/Nethereum.Wallet.RpcRequests/RpcErrors.cs:6`

**Error Code Reference:**
- **-32601** - Method not implemented by wallet
- **-32602** - Invalid or missing parameters
- **4001** - User rejected request (EIP-1193 standard)
- **-32603** - Internal wallet error

## Handler Registration

Register all handlers with `RpcHandlerRegistry`:

```csharp
using Nethereum.Wallet.RpcRequests;
using Nethereum.Wallet.Hosting;

var registry = new RpcHandlerRegistry();

// Register all wallet RPC handlers
WalletRpcHandlerRegistration.RegisterAll(registry);
```

**From:** `src/Nethereum.Wallet.RpcRequests/WalletRpcHandlerRegistration.cs:8`

**Registered Handlers:**
```csharp
public static void RegisterAll(RpcHandlerRegistry registry)
{
    registry.Register(new WalletAddEthereumChainHandler());
    registry.Register(new WalletSwitchEthereumChainHandler());
    registry.Register(new WalletGetPermissionsHandler());
    registry.Register(new WalletRequestPermissionsHandler());
    registry.Register(new WalletRevokePermissionsHandler());
    registry.Register(new PersonalSignHandler());
    registry.Register(new EthSignTypedDataV4Handler());
    registry.Register(new EthRequestAccountsHandler());
    registry.Register(new EthAccountsHandler());
    registry.Register(new EthSendTransactionHandler());
    registry.Register(new EthChainIdHandler());
    // ... additional handlers
}
```

**From:** `src/Nethereum.Wallet.RpcRequests/WalletRpcHandlerRegistration.cs:8`

## Usage with Wallet Context

Handlers require `IWalletContext` which provides:

```csharp
public interface IWalletContext
{
    // Current state
    IWalletAccount? SelectedWalletAccount { get; }
    DappConnectionContext? SelectedDapp { get; }
    HexBigInteger? ChainId { get; }

    // Permission service
    IDappPermissionService DappPermissions { get; }

    // Configuration
    IWalletConfigurationService Configuration { get; }

    // User prompt methods
    Task<string?> EnableProviderAsync();
    Task<bool> RequestDappPermissionAsync(DappConnectionContext dapp, string account);
    Task<string?> ShowTransactionDialogAsync(TransactionInput transaction);
    Task<string?> RequestPersonalSignAsync(SignaturePromptContext context);
    Task<string?> RequestTypedDataSignAsync(TypedDataSignPromptContext context);
    Task<ChainAdditionPromptResult> RequestChainAdditionAsync(ChainAdditionPromptRequest request);
    Task<ChainSwitchPromptResult> RequestChainSwitchAsync(ChainSwitchPromptRequest request);
}
```

## Example: Handling eth_requestAccounts

```csharp
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Wallet.UI;
using Nethereum.Wallet.RpcRequests;

var handler = new EthRequestAccountsHandler();

// Simulated request from dApp
var request = new RpcRequestMessage
{
    Id = 1,
    Method = "eth_requestAccounts",
    RawParameters = new object[] { }
};

// Handle request with wallet context
var response = await handler.HandleAsync(request, walletContext);

// Response contains:
// - Success: { id: 1, result: ["0x..."] }
// - Rejected: { id: 1, error: { code: 4001, message: "User rejected the request" } }
```

## Example: Handling personal_sign

```csharp
var handler = new PersonalSignHandler();

var request = new RpcRequestMessage
{
    Id = 2,
    Method = "personal_sign",
    RawParameters = new object[]
    {
        "0x48656c6c6f", // "Hello" in hex
        "0x742d35F3d3A4ab6b07a6d1e5c5f29fCB6b5e76e9"
    }
};

var response = await handler.HandleAsync(request, walletContext);

// Handler:
// 1. Detects hex message
// 2. Decodes to "Hello"
// 3. Shows user both hex and decoded message
// 4. Prompts for signature
// 5. Returns signature or error 4001
```

## Example: Adding Custom Chain

```csharp
var handler = new WalletAddEthereumChainHandler();

var addChainParam = new AddEthereumChainParameter
{
    ChainId = "0x89", // Polygon
    ChainName = "Polygon Mainnet",
    RpcUrls = new[] { "https://polygon-rpc.com" },
    NativeCurrency = new NativeCurrency
    {
        Name = "MATIC",
        Symbol = "MATIC",
        Decimals = 18
    },
    BlockExplorerUrls = new[] { "https://polygonscan.com" }
};

var request = new RpcRequestMessage
{
    Id = 3,
    Method = "wallet_addEthereumChain",
    RawParameters = new object[] { addChainParam }
};

var response = await handler.HandleAsync(request, walletContext);

// Handler:
// 1. Checks if chain exists
// 2. If exists: prompts to switch
// 3. If new: prompts to add and switch
// 4. Returns null on success or error on rejection
```

## Integration with NethereumWalletHostProvider

```csharp
using Nethereum.Wallet.Hosting;
using Nethereum.Wallet.RpcRequests;

var registry = new RpcHandlerRegistry();
WalletRpcHandlerRegistration.RegisterAll(registry);

var walletProvider = new NethereumWalletHostProvider(
    vaultService,
    rpcClientFactory,
    storageService,
    chainManagementService,
    registry, // Handlers registered here
    transactionPromptService,
    signaturePromptService,
    configurationService,
    loginPromptService,
    dappPermissionService,
    dappPermissionPromptService,
    chainAdditionPromptService,
    chainSwitchPromptService);

// Wallet provider uses registry to handle incoming RPC requests
var web3 = await walletProvider.GetWeb3Async();
// web3.Client.OverridingRequestInterceptor intercepts and routes to handlers
```

## Related Packages

- **Nethereum.Wallet** - Core wallet infrastructure and IWalletContext
- **Nethereum.JsonRpc.Client** - JSON-RPC message types
- **Nethereum.RPC** - RPC DTOs and parameters
- **Nethereum.Web3** - Web3 integration

## Additional Resources

- [EIP-1193: Ethereum Provider JavaScript API](https://eips.ethereum.org/EIPS/eip-1193)
- [EIP-712: Typed Structured Data Hashing and Signing](https://eips.ethereum.org/EIPS/eip-712)
- [EIP-1102: Opt-in Account Exposure](https://eips.ethereum.org/EIPS/eip-1102)
- [EIP-3085: wallet_addEthereumChain RPC Method](https://eips.ethereum.org/EIPS/eip-3085)
- [EIP-3326: wallet_switchEthereumChain RPC Method](https://eips.ethereum.org/EIPS/eip-3326)
- [Nethereum Documentation](http://docs.nethereum.com)
