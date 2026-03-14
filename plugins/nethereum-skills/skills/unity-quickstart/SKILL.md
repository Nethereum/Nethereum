---
name: unity-quickstart
description: Integrate Ethereum into Unity games using Nethereum with async Web3 API, WebGL wallet support via EIP-6963 multi-wallet discovery and MetaMask (.NET/C#). Use this skill when the user asks about Unity blockchain integration, Unity WebGL wallets, Unity MetaMask, Unity Ethereum, Nethereum.Unity, game blockchain, NFT games, or Unity ERC-20 tokens.
user-invocable: true
---

# Unity Integration

Integrate Ethereum into Unity games. Nethereum provides Unity-compatible libraries with the async `Web3` API (preferred) and coroutine wrappers, WebGL wallet connectivity (EIP-6963 multi-wallet, MetaMask), and typed contract services.

## When to Use This Skill

- Building a Unity game that reads or writes blockchain data
- Connecting browser wallets in Unity WebGL builds
- Deploying or interacting with smart contracts from Unity
- Querying ERC-20/ERC-721 token balances in a game

## Installation

Add to `Packages/manifest.json`:

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

**Important notes:**
- WebGL + async/await requires [WebGLThreadingPatcher](https://github.com/nicloay/WebGLThreadingPatcher) package
- For IL2CPP builds: set **IL2CPP Code Generation** to **Faster (smaller) builds** in Player Settings

## Query Block Number (Async — Preferred)

```csharp
using UnityEngine;
using Nethereum.Web3;
using Nethereum.Unity.Rpc;
using System;

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

`UnityWebRequestRpcTaskClient` uses Unity's `UnityWebRequest` for HTTP — required for WebGL builds.

## Send ETH (Desktop/Mobile)

```csharp
using Nethereum.Web3;
using Nethereum.Web3.Accounts;

var account = new Account("0xYOUR_PRIVATE_KEY", chainId: 31337);
var web3 = new Web3(account, "http://localhost:8545");

var receipt = await web3.Eth.GetEtherTransferService()
    .TransferEtherAndWaitForReceiptAsync("0xRecipient", 0.1m);
Debug.Log($"TX: {receipt.TransactionHash}");
```

## WebGL: EIP-6963 Multi-Wallet Discovery (Recommended)

EIP-6963 discovers all installed browser wallets — MetaMask, Rainbow, Coinbase, etc.:

```csharp
using Nethereum.Unity.EIP6963;
using Nethereum.EIP6963WalletInterop;

var walletProvider = EIP6963WebglHostProvider.CreateOrGetCurrentInstance();

// Discover all installed wallets
var wallets = await walletProvider.GetAvailableWalletsAsync();
foreach (var wallet in wallets)
    Debug.Log($"Found: {wallet.Name} ({wallet.Rdns})");

// Select and connect
if (wallets.Length > 0)
{
    await walletProvider.SelectWalletAsync(wallets[0].Uuid);
    var account = await walletProvider.EnableProviderAsync();
    Debug.Log($"Connected: {account}");

    // Get Web3 — wallet handles signing
    var web3 = await walletProvider.GetWeb3Async();
    var balance = await web3.Eth.GetBalance.SendRequestAsync(walletProvider.SelectedAccount);
    Debug.Log($"Balance: {Web3.Convert.FromWei(balance.Value)} ETH");
}
```

### Listen to Account/Network Changes

Events use `Func<T, Task>` delegates — handlers must return `Task`:

```csharp
walletProvider.SelectedAccountChanged += async (newAccount) =>
{
    Debug.Log($"Account switched: {newAccount}");
    return;  // implicit Task.CompletedTask
};

walletProvider.NetworkChanged += async (newChainId) =>
{
    Debug.Log($"Network switched: {newChainId}");
    return;
};
```

## WebGL: MetaMask Direct (Fallback)

```csharp
using Nethereum.Unity.Metamask;

var metamask = MetamaskWebglHostProvider.CreateOrGetCurrentInstance();
await metamask.EnableProviderAsync();
var web3 = await metamask.GetWeb3Async();

var receipt = await web3.Eth.GetEtherTransferService()
    .TransferEtherAndWaitForReceiptAsync("0xRecipient", 0.1m);
```

## ERC-20 Token Interaction

Use built-in typed services — no ABI or code generation needed:

```csharp
var erc20 = web3.Eth.ERC20.GetContractService(tokenAddress);

var name = await erc20.NameQueryAsync();
var symbol = await erc20.SymbolQueryAsync();
var decimals = await erc20.DecimalsQueryAsync();
var balance = await erc20.BalanceOfQueryAsync(ownerAddress);

Debug.Log($"{name} ({symbol}): {Web3.Convert.FromWei(balance, decimals)}");
```

Similarly, `web3.Eth.ERC721` and `web3.Eth.ERC1155` provide typed access to NFT and multi-token contracts.

## Cross-Platform Architecture

Use conditional compilation to handle WebGL vs desktop:

```csharp
private async Task<IWeb3> GetWeb3Async()
{
#if UNITY_WEBGL
    var walletProvider = EIP6963WebglHostProvider.CreateOrGetCurrentInstance();
    await walletProvider.EnableProviderAsync();
    return await walletProvider.GetWeb3Async();
#else
    var account = new Account(privateKey, chainId);
    return new Web3(account, url);
#endif
}
```

Once you have `IWeb3`, all `web3.Eth.*` calls work identically regardless of platform.

## Code Generation (Shared Projects)

Generate typed C# contract services from Solidity ABI:

```bash
dotnet tool install -g Nethereum.Generator.Console

Nethereum.Generator.Console generate from-abi \
    -abi ./out/MyToken.abi \
    -bin ./out/MyToken.bin \
    -o ./SharedContracts \
    -ns MyGame.Contracts \
    -cn MyToken
```

Share the generated code between Unity and .NET test projects using a `netstandard2.0` project with `package.json` + `.asmdef` files. Reference from Unity via `file:` dependency in `manifest.json`.

## Platform Support

| Platform | Signing | Wallet Connection |
|----------|---------|-------------------|
| WebGL | Browser wallet (EIP-6963, MetaMask) | EIP-6963 or MetaMask interop |
| Windows/macOS/Linux | Private key | RPC endpoint |
| iOS/Android | Private key | RPC endpoint |

## Resources

- [Unity3d Sample Template](https://github.com/Nethereum/Unity3dSampleTemplate) — complete starter project
- [WebGLThreadingPatcher](https://github.com/nicloay/WebGLThreadingPatcher) — async/await for WebGL

For full documentation, see: https://docs.nethereum.com/docs/unity/overview
