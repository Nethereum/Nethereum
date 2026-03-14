# Nethereum.Unity.Metamask

MetaMask integration for Unity WebGL builds. Provides JavaScript interop to connect MetaMask in the browser, sign transactions, and send RPC requests through MetaMask's provider — all within Unity's coroutine execution model.

## Key Components

| Class | Purpose |
|---|---|
| `MetamaskWebglHostProvider` | Unity `IEthereumHostProvider` implementation that delegates to MetaMask via JS interop |
| `MetamaskWebglInterop` | Low-level JavaScript interop calls to `window.ethereum` |
| `MetamaskWebglCoroutineRequestRpcClient` | Unity coroutine-based RPC client routing requests through MetaMask |
| `MetamaskTransactionCoroutineUnityRequest` | Coroutine wrapper for sending transactions via MetaMask |
| `MetamaskRpcRequestMessage` | RPC request message model |
| `MetamaskWebglCoroutineRequestRpcClientFactory` | Factory for creating coroutine RPC clients with custom timeouts |
| `MetamaskWebglTaskRequestInterop` | Task-based async interop — used internally by `MetamaskWebglHostProvider` |

## Usage

```csharp
// In a MonoBehaviour — use singleton pattern
var metamaskProvider = MetamaskWebglHostProvider.CreateOrGetCurrentInstance();
await metamaskProvider.EnableProviderAsync();
var web3 = await metamaskProvider.GetWeb3Async();

// Send a transaction — wallet handles signing
var receipt = await web3.Eth.GetEtherTransferService()
    .TransferEtherAndWaitForReceiptAsync(toAddress, 0.1m);
```

## Relationship to Other Packages

- **[Nethereum.Unity](../Nethereum.Unity/README.md)** — Core Unity integration
- **[Nethereum.Unity.EIP6963](../Nethereum.Unity.EIP6963/README.md)** — Alternative: multi-wallet discovery via EIP-6963 (supports MetaMask and other wallets)
- **[Nethereum.Metamask](../Nethereum.Metamask/README.md)** — MetaMask abstractions (non-Unity, Blazor-focused)
