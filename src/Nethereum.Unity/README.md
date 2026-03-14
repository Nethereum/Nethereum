# Nethereum.Unity

Unity game engine integration for Nethereum. Provides both async/Task and coroutine-based RPC patterns compatible with Unity's execution model, EIP-1559 fee estimation strategies, typed ERC standard helpers, and IPFS utilities.

## Installation

Install via OpenUPM by adding to your `Packages/manifest.json`:

```json
{
  "scopedRegistries": [
    {
      "name": "package.openupm.com",
      "url": "https://package.openupm.com",
      "scopes": ["com.nethereum.unity"]
    }
  ],
  "dependencies": {
    "com.nethereum.unity": "5.0.0",
    "com.unity.nuget.newtonsoft-json": "3.2.1"
  }
}
```

Package source: https://github.com/Nethereum/Nethereum.Unity

For WebGL wallet integration, also add [Nethereum.Unity.EIP6963](../Nethereum.Unity.EIP6963/README.md) (recommended) or [Nethereum.Unity.Metamask](../Nethereum.Unity.Metamask/README.md).

## Key Components

### RPC Clients

| Class | Purpose |
|---|---|
| `UnityWebRequestRpcTaskClient` | **Preferred.** Async Task-based RPC client using `UnityWebRequest` — use with standard `Web3` |
| `UnityWebRequestRpcClient` | Coroutine-based RPC client for `yield return` patterns |
| `UnityWebRequestRpcClientFactory` | Factory for creating coroutine RPC clients with shared configuration |

### Transaction & Transfer (Coroutine)

| Class | Purpose |
|---|---|
| `EthTransferUnityRequest` | Send ETH transfers with automatic EIP-1559 or legacy fee handling |
| `TransactionSignedUnityRequest` | Sign and send any transaction — handles gas estimation, nonce, and fee selection |
| `QueryUnityRequest<TFunc, TResp>` | Read-only smart contract calls with typed function messages |
| `TransactionReceiptPollingRequest` | Poll for transaction receipt with configurable interval |

### EIP-1559 Fee Strategies

| Class | Purpose |
|---|---|
| `SimpleFeeSuggestionUnityRequestStrategy` | `baseFee * 2 + priorityFee` — fast, single RPC call |
| `TimePreferenceFeeSuggestionUnityRequestStrategy` | Time-preference model using fee history — returns array of suggestions by priority |
| `MedianPriorityFeeHistorySuggestionUnityRequestStrategy` | Median of recent priority fees from fee history |
| `SuggestTipUnityRequestStrategy` | Tip calculation from recent blocks |

### ERC Standard Helpers (Coroutine)

| Class | Purpose |
|---|---|
| `ERC20ContractRequestFactory` | Create typed ERC-20 requests: `BalanceOfQueryRequest`, `TransferTransactionRequest`, `ApproveTransactionRequest`, etc. |
| `ERC721ContractRequestFactory` | Create typed ERC-721 requests: `OwnerOfQueryRequest`, `TokenURIQueryRequest`, `SafeTransferFromTransactionRequest`, etc. |
| `ERC1155ContractRequestFactory` | Create typed ERC-1155 requests: `BalanceOfQueryRequest`, `SafeBatchTransferFromTransactionRequest`, `MintTransactionRequest`, etc. |

### Utilities

| Class | Purpose |
|---|---|
| `IpfsUrlService` | Converts `ipfs://` URIs to HTTP gateway URLs for fetching NFT metadata |

## Async/Task Pattern (Preferred)

Use `UnityWebRequestRpcTaskClient` with the standard Nethereum `Web3` API. This is the recommended approach for new projects.

```csharp
using Nethereum.Web3;
using Nethereum.Unity.Rpc;

public class GetBlockNumber : MonoBehaviour
{
    public string Url = "https://ethereum-rpc.publicnode.com";

    async void Start()
    {
        var web3 = new Web3(new UnityWebRequestRpcTaskClient(new Uri(Url)));
        var blockNumber = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
        Debug.Log($"Block: {blockNumber.Value}");
    }
}
```

`UnityWebRequestRpcTaskClient` supports custom request headers for authenticated RPC endpoints:

```csharp
var client = new UnityWebRequestRpcTaskClient(new Uri(url));
client.RequestHeaders["Authorization"] = "Bearer YOUR_API_KEY";
var web3 = new Web3(client);
```

With the async pattern, you have access to the full `Web3` API including `web3.Eth.ERC20`, `web3.Eth.ERC721`, `web3.Eth.GetEtherTransferService()`, and all standard Nethereum services.

## Coroutine Pattern

For Unity's traditional coroutine model, use the dedicated request classes with `yield return`:

### Query Block Number

```csharp
using Nethereum.Unity.Rpc;

IEnumerator GetBlockNumber()
{
    var blockNumberRequest = new EthBlockNumberUnityRequest(url);
    yield return blockNumberRequest.SendRequest();

    if (blockNumberRequest.Exception == null)
        Debug.Log($"Block: {blockNumberRequest.Result.Value}");
    else
        Debug.LogError(blockNumberRequest.Exception.Message);
}
```

### Transfer ETH

```csharp
var ethTransfer = new EthTransferUnityRequest(url, privateKey, chainId);

// EIP-1559 (default) — fees estimated automatically
yield return ethTransfer.TransferEther(toAddress, 0.1m);

// Or with explicit EIP-1559 fees
yield return ethTransfer.TransferEther(toAddress, 0.1m,
    maxPriorityFeePerGas, maxFeePerGas);

// Or legacy gas price
ethTransfer.UseLegacyAsDefault = true;
yield return ethTransfer.TransferEther(toAddress, 0.1m, gasPriceGwei: 20);
```

### Query Smart Contract

```csharp
var queryRequest = new QueryUnityRequest<BalanceOfFunction, BalanceOfOutputDTO>(
    url, defaultAccount);
yield return queryRequest.Query(
    new BalanceOfFunction { Owner = ownerAddress },
    contractAddress);

if (queryRequest.Exception == null)
    Debug.Log($"Balance: {queryRequest.Result.Balance}");
```

### Send Contract Transaction

```csharp
var transactionRequest = new TransactionSignedUnityRequest(url, privateKey, chainId);

var transferFunction = new TransferFunction
{
    To = recipientAddress,
    Value = Web3.Convert.ToWei(100)
};

yield return transactionRequest.SignAndSendTransaction(transferFunction, contractAddress);
string txHash = transactionRequest.Result;
```

### Wait for Receipt

```csharp
var receiptPolling = new TransactionReceiptPollingRequest(url);
yield return receiptPolling.PollForReceipt(txHash, 2); // poll every 2 seconds

if (receiptPolling.Exception == null)
    Debug.Log($"Status: {receiptPolling.Result.Status.Value}");
```

## Fee Estimation Strategies

`TransactionSignedUnityRequest` uses `SimpleFeeSuggestionUnityRequestStrategy` by default. You can swap it:

```csharp
var txRequest = new TransactionSignedUnityRequest(url, privateKey, chainId);

// Use time-preference strategy (returns multiple suggestions by priority)
var timePreference = new TimePreferenceFeeSuggestionUnityRequestStrategy(url);
yield return timePreference.SuggestFees();
var fee = timePreference.Result[0]; // highest priority
yield return ethTransfer.TransferEther(toAddress, 0.1m,
    fee.MaxPriorityFeePerGas.Value, fee.MaxFeePerGas.Value);

// Or use median fee history strategy
var median = new MedianPriorityFeeHistorySuggestionUnityRequestStrategy(url);
yield return median.SuggestFee();
var medianFee = median.Result;
```

## IPFS URL Resolution

Convert `ipfs://` URIs to HTTP gateway URLs for NFT metadata:

```csharp
var httpUrl = IpfsUrlService.ResolveIpfsUrlGateway("ipfs://QmYwAPJzv5CZsnA625s3Xf2nemtYgPpHdWEz79ojWnPbdG");
// Returns: ipfs.infura.io/ipfs/QmYwAPJzv5CZsnA625s3Xf2nemtYgPpHdWEz79ojWnPbdG

// Change the default gateway
IpfsUrlService.DefaultIpfsGateway = "https://gateway.pinata.cloud/ipfs/";
```

## Platform Notes

- **WebGL + async/await**: Requires [WebGLThreadingPatcher](https://github.com/nicloay/WebGLThreadingPatcher) for Task support in WebGL builds
- **IL2CPP/AOT**: Set **IL2CPP Code Generation** to **Faster (smaller) builds** in Player Settings (Edit > Project Settings > Player > Other Settings)
- **Desktop HTTPS**: Newer Unity versions require `https://` for desktop builds — `http://` may be rejected
- **AOT builds**: The OpenUPM package uses AOT-compatible compiled libraries with Unity's custom Newtonsoft Json.NET

## Relationship to Other Packages

- **[Nethereum.Unity.EIP6963](../Nethereum.Unity.EIP6963/README.md)** — EIP-6963 multi-wallet discovery for Unity WebGL (recommended)
- **[Nethereum.Unity.Metamask](../Nethereum.Unity.Metamask/README.md)** — MetaMask integration for Unity WebGL
- **Nethereum.Web3** — Core Ethereum interaction (used with `UnityWebRequestRpcTaskClient`)

## Additional Resources

- **Unity3d Sample Template**: https://github.com/Nethereum/Unity3dSampleTemplate
- **Nethereum.Unity Package**: https://github.com/Nethereum/Nethereum.Unity
