# Unity & MAUI Hardware — Nethereum 7.0

Nethereum's Unity/game-engine integration and the MAUI Android USB transport carry into 7.0 unchanged in behaviour: the coroutine-based RPC and contract request infrastructure for Unity (`Nethereum.Unity`), the EIP-6963 and MetaMask WebGL browser-wallet bridges (`Nethereum.Unity.EIP6963`, `Nethereum.Unity.Metamask`), and the Android USB `IDeviceFactory` that carries Trezor hardware-wallet traffic on MAUI (`Nethereum.Maui.AndroidUsb`). All four packages pre-date 6.1.0; the `6.1.0..HEAD` delta is a README-accuracy pass that rewrote two READMEs to match the shipped source, plus one packaging correction: `Nethereum.Unity.Metamask` now publishes under its intended NuGet id (see below). No runtime code changed. The Unity packages continue to build for `UnityFrameworks` (`net461;net472;netstandard2.1`); `Nethereum.Maui.AndroidUsb` targets `net10.0;net10.0-android`.

## Nethereum.Unity — Coroutine RPC & Contract Requests

Unchanged in 7.0 (zero source delta since 6.1.0). Coroutine-friendly RPC, transaction, and contract-call infrastructure for Unity, built on `UnityWebRequest` so calls run inside Unity's game loop with no blocking I/O.

* `UnityRequest<TResult>` / `UnityRpcRequest<TResult>` — the coroutine request base carrying `Result`, `Exception` and completion state; every RPC and contract request derives from it
* `UnityWebRequestRpcClient` (`: IUnityRpcRequestClient`, `IClientRequestHeaderSupport`), `UnityWebRequestRpcClientFactory` (`: IUnityRpcRequestClientFactory`), and the `Task`-based `UnityWebRequestRpcTaskClient` — the transport bridging Nethereum's RPC layer onto `UnityWebRequest`
* `QueryUnityRequest<TFunctionMessage, TResponse>`, `EthCallUnityRequest`, `EthTransferUnityRequest`, `TransactionSignedUnityRequest` (`: IContractTransactionUnityRequest`), `TransactionReceiptPollingRequest`, and the wallet/RPC request set in `UnityRPCRequests.cs` (`EthRequestAccountsUnityRequest`, `WalletAddEthereumChainUnityRequest`, `WalletWatchAssetUnityRequest`, …)
* Contract request factories — `ContractQueryUnityRequestFactory` (`: IContractQueryUnityRequestFactory`), `ContractTransactionUnityRequestFactory` (`: IContractTransactionUnityRequestFactory`), and `EstimateContractTransactionUnityRequest`, over the generic `ContractFunctionQueryRequest<TFunctionMessage, TResponse>` / `ContractFunctionTransactionRequest<TFunctionMessage>`
* Token standards — `ERC20ContractRequestFactory`, `ERC721ContractRequestFactory`, `ERC1155ContractRequestFactory` and their generated query/transaction request types, plus `NFTsOfUserUnityRequest` and `NftMetadataUnityRequest<TNFTMetadata>`
* EIP-1559 fee strategies — `IFee1559SuggestionUnityRequestStrategy` with `SimpleFeeSuggestionUnityRequestStrategy`, `MedianPriorityFeeHistorySuggestionUnityRequestStrategy`, `TimePreferenceFeeSuggestionUnityRequestStrategy` and `SuggestTipUnityRequestStrategy`
* `IpfsUrlService` — IPFS URI resolution for NFT metadata

## Nethereum.Unity.EIP6963 — EIP-6963 WebGL Wallet Bridge

The EIP-6963 multi-wallet-discovery bridge for Unity WebGL builds. 7.0 delta is README-only. Lets a WebGL build enumerate injected browser wallets and drive the user-selected one.

* `EIP6963WebglHostProvider : EIP6963WalletHostProvider` (base in `Nethereum.EIP6963WalletInterop`) — the Unity host provider that exposes wallet selection, the selected account, and `SelectedNetworkChainId`, and hands back a wallet-backed `Web3` via `GetWeb3Async()`
* `EIP6963WebglTaskRequestInterop : IEIP6963WalletInterop` — the `Task`-based interop the provider talks to
* `EIP6963WebglInterop` — the JS interop surface: `[DllImport("__Internal")]` `extern` methods (`EIP6963_EnableEthereum`, `EIP6963_GetSelectedAddress`, `EIP6963_GetChainId`, `EIP6963_IsAvailable`, …) bound to the `NethereumEIP6963.jslib` browser library
* `EIP6963RpcRequestMessage : RpcRequestMessage` — the RPC request shape marshalled to the browser wallet

## Nethereum.Unity.Metamask — MetaMask WebGL Wallet Bridge

No code change in 7.0. Packaging fix: the NuGet package id is now `Nethereum.Unity.Metamask`; 6.1.0 published it under the misspelt id `Nethereum.Unity.Metamasky`, so update any `PackageReference`. The MetaMask-specific WebGL bridge for Unity, for builds targeting MetaMask directly rather than through EIP-6963 discovery.

* `MetamaskWebglHostProvider : MetamaskHostProvider` (base in `Nethereum.Metamask`) — the Unity host provider for MetaMask connect / account- and chain-change handling
* `MetamaskWebglTaskRequestInterop : IMetamaskInterop` — the `Task`-based interop bridge
* `MetamaskWebglCoroutineRequestRpcClient : UnityRequest<RpcResponseMessage>, IUnityRpcRequestClient` and `MetamaskWebglCoroutineRequestRpcClientFactory : IUnityRpcRequestClientFactory` — coroutine RPC client that routes calls through MetaMask
* `MetamaskTransactionCoroutineUnityRequest : UnityRequest<string>, IContractTransactionUnityRequest` — a coroutine transaction request signed by the browser wallet
* `MetamaskWebglInterop` — the JS interop surface: `[DllImport("__Internal")]` `extern` methods (`EnableEthereum`, `EthereumInit`, `GetChainId`, `IsMetamaskAvailable`, …) bound to the `NethereumMetamask.jslib` browser library
* `MetamaskRpcRequestMessage : RpcRequestMessage` — the RPC request shape marshalled to MetaMask

## Nethereum.Maui.AndroidUsb — Android USB Device Factory

The Android USB transport for .NET MAUI. 7.0 delta is README-only (the README was corrected to describe the real `Device.Net` factory shape and the Trezor integration path). Provides a `Device.Net` `IDeviceFactory` over the Android USB Host API; the verified integration wires it into Trezor hardware-wallet signing.

* `MauiAndroidUsbDeviceFactory : IDeviceFactory` — constructed with the Android `UsbManager`, a `Context`, an `ILoggerFactory`, an activity provider, and the `FilterDeviceDefinition` set to expose; `GetDeviceAsync` returns a `MauiAndroidUsbDevice`
* `MauiAndroidUsbDevice : IDevice` — wraps an Android `UsbDeviceConnection` for HID read/write
* `UsbPermissionHelper` — `static` helper whose `EnsurePermissionAsync` requests Android USB permission
* `UsbAttachReceiver` — `internal sealed` `BroadcastReceiver` for attach/detach events (implementation detail, not a public consumable type)
* All types are compiled only under `net10.0-android`, guarded by `#if ANDROID || __ANDROID__`; they are absent from the plain `net10.0` target
