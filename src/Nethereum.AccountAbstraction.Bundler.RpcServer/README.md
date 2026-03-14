# Nethereum.AccountAbstraction.Bundler.RpcServer

JSON-RPC 2.0 server exposing ERC-4337 bundler methods including `eth_sendUserOperation`, gas estimation, receipt queries, and debug endpoints.

## Overview

Nethereum.AccountAbstraction.Bundler.RpcServer provides JSON-RPC handlers that expose the ERC-4337 bundler as a standard JSON-RPC 2.0 endpoint. It implements all spec-required methods (`eth_sendUserOperation`, `eth_estimateUserOperationGas`, `eth_getUserOperationByHash`, `eth_getUserOperationReceipt`, `eth_supportedEntryPoints`) plus debug methods for mempool inspection and reputation management.

Each handler extends `RpcHandlerBase` from Nethereum.CoreChain.Rpc, allowing seamless integration with AppChain or DevChain server infrastructure.

### Key Features

- **Full ERC-4337 RPC**: All spec-required bundler JSON-RPC methods
- **Debug Endpoints**: Mempool dump, flush, reputation get/set
- **Standard Integration**: Extends CoreChain `RpcHandlerBase` for plug-and-play registration
- **Error Codes**: Proper JSON-RPC error codes per ERC-4337 spec (-32602, -32500, -32603)

## Installation

```bash
dotnet add package Nethereum.AccountAbstraction.Bundler.RpcServer
```

### Dependencies

- **Nethereum.AccountAbstraction.Bundler** - Core bundler service (`IBundlerService`)
- **Nethereum.CoreChain** - RPC handler base classes and registry

## RPC Methods

### Standard Methods

| Method | Description |
|--------|-------------|
| `eth_sendUserOperation` | Submit a UserOperation to the bundler mempool |
| `eth_estimateUserOperationGas` | Estimate gas for a UserOperation |
| `eth_getUserOperationByHash` | Get UserOperation by its hash |
| `eth_getUserOperationReceipt` | Get receipt for a mined UserOperation |
| `eth_supportedEntryPoints` | List supported EntryPoint addresses |
| `eth_chainId` | Get chain ID |

### Debug Methods

| Method | Description |
|--------|-------------|
| `debug_bundler_dumpMempool` | List all pending UserOperations |
| `debug_bundler_sendBundleNow` | Force immediate bundle execution |
| `debug_bundler_setReputation` | Set entity reputation entries |
| `debug_bundler_dumpReputation` | Get entity reputation status |

## Quick Start

```csharp
using Nethereum.AccountAbstraction.Bundler.RpcServer;

// Register handlers in RPC registry
var registry = new RpcHandlerRegistry();
registry.Register(new EthSendUserOperationHandler(bundlerService));
registry.Register(new EthEstimateUserOperationGasHandler(bundlerService));
registry.Register(new EthGetUserOperationByHashHandler(bundlerService));
registry.Register(new EthGetUserOperationReceiptHandler(bundlerService));
registry.Register(new EthSupportedEntryPointsHandler(bundlerService));
registry.Register(new BundlerEthChainIdHandler(bundlerService));
```

## Usage Examples

### Example 1: Register All Bundler Handlers

```csharp
var bundlerService = new BundlerService(web3, bundlerConfig);
var registry = new RpcHandlerRegistry();

// Standard methods
registry.Register(new EthSendUserOperationHandler(bundlerService));
registry.Register(new EthEstimateUserOperationGasHandler(bundlerService));
registry.Register(new EthGetUserOperationByHashHandler(bundlerService));
registry.Register(new EthGetUserOperationReceiptHandler(bundlerService));
registry.Register(new EthSupportedEntryPointsHandler(bundlerService));

// Debug methods
registry.Register(new DebugBundlerDumpMempoolHandler(bundlerService));
registry.Register(new DebugBundlerFlushHandler(bundlerService));

// Recommended: use extension methods for one-line registration
// registry.AddBundlerHandlers(bundlerService);       // all standard handlers
// registry.AddBundlerDebugHandlers(bundlerService);  // all debug handlers
```

## API Reference

### EthSendUserOperationHandler

Handles `eth_sendUserOperation` - submits UserOperation to mempool.

Parameters: `[userOp (JSON object), entryPoint (address)]`
Returns: `userOpHash (hex string)`
Errors: `-32602` (invalid params), `-32500` (validation failed), `-32603` (internal error)

### EthEstimateUserOperationGasHandler

Handles `eth_estimateUserOperationGas` - estimates gas limits.

Parameters: `[userOp (JSON object), entryPoint (address)]`
Returns: `{ preVerificationGas, verificationGasLimit, callGasLimit, maxFeePerGas, maxPriorityFeePerGas }`

### EthGetUserOperationReceiptHandler

Handles `eth_getUserOperationReceipt` - retrieves mined operation receipt.

Parameters: `[userOpHash (hex)]`
Returns: Receipt with `userOpHash`, `entryPoint`, `sender`, `nonce`, `paymaster`, `actualGasUsed`, `actualGasCost`, `success`, `reason`, `logs`, and nested `receipt`

## Related Packages

### Dependencies
- **[Nethereum.AccountAbstraction.Bundler](../Nethereum.AccountAbstraction.Bundler/README.md)** - Core bundler service
- **[Nethereum.CoreChain](../Nethereum.CoreChain/README.md)** - RPC handler infrastructure

### See Also
- **[Nethereum.AccountAbstraction](../Nethereum.AccountAbstraction/README.md)** - Core ERC-4337 types and client-side usage

## Additional Resources

- [ERC-4337: Account Abstraction](https://eips.ethereum.org/EIPS/eip-4337)
- [Nethereum Documentation](https://docs.nethereum.com)
