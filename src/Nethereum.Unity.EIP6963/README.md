# Nethereum.Unity.EIP6963

Unity WebGL integration for EIP-6963 multi-wallet discovery and connection. Provides JavaScript interop for discovering and connecting to multiple browser wallets using the EIP-6963 standard.

## Overview

Nethereum.Unity.EIP6963 enables Unity WebGL applications to discover and connect to multiple browser wallets (Metamask, Rainbow, Coinbase Wallet, etc.) using the EIP-6963 Multi Injected Provider Discovery standard. Uses JavaScript interop via `[DllImport("__Internal")]` to listen for wallet announcements and manage wallet selection.

**Core Features:**
- **Multi-Wallet Discovery** - Discover all EIP-6963 compliant wallets in browser
- **Wallet Selection** - Select from multiple discovered wallets
- **Wallet Metadata** - Access wallet name, icon, UUID, and RDNS identifier
- **Account Management** - Request account access with `eth_requestAccounts`
- **Transaction Signing** - Sign and send transactions through selected wallet
- **Personal Sign** - Sign messages with `personal_sign`
- **EIP-712 Typed Data** - Sign typed data with `eth_signTypedData_v4`
- **Chain Events** - Listen for account and chain changes
- **Async/Await Support** - Task-based async pattern with polling
- **WebGL Callbacks** - AOT-compatible callbacks using `[MonoPInvokeCallback]`

## Installation

This package is typically installed alongside Nethereum.Unity via OpenUPM or Git URL.

### Via OpenUPM

Add to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.nethereum.unity.eip6963": "4.19.2"
  }
}
```

### Via Git URL

```
https://github.com/Nethereum/Nethereum.Unity.git
```

## Dependencies

**Package References:**
- Nethereum.EIP6963WalletInterop (core EIP-6963 abstraction)
- Nethereum.Unity (Unity coroutine infrastructure)
- Nethereum.JsonRpc.Client
- Nethereum.RPC
- UnityEngine

**JavaScript File:**
- `NethereumEIP6963.jslib` - Browser JavaScript interop library for EIP-6963

## EIP-6963 Standard

EIP-6963 defines a standard for discovering multiple injected Ethereum providers in a browser. Instead of relying on a single `window.ethereum` object, wallets announce themselves via events, allowing dApps to discover and select from all available wallets.

**Key Concepts:**
- **Provider Announcement**: Wallets dispatch `eip6963:announceProvider` events
- **Provider Request**: dApps dispatch `eip6963:requestProvider` to trigger announcements
- **Wallet Metadata**: Each wallet provides name, UUID, icon (data URI), and RDNS
- **Provider Selection**: dApp selects which wallet to use for interactions

**From:** [EIP-6963 Specification](https://eips.ethereum.org/EIPS/eip-6963)

## Architecture

### JavaScript Interop Layer

`EIP6963WebglInterop` declares P/Invoke methods for JavaScript functions:

```csharp
public class EIP6963WebglInterop
{
    [DllImport("__Internal")]
    public static extern string EIP6963_EnableEthereum(string gameObjectName, string callback, string fallback);

    [DllImport("__Internal")]
    public static extern string EIP6963_GetSelectedAddress();

    [DllImport("__Internal")]
    public static extern void EIP6963_GetChainId(string gameObjectName, string callback, string fallback);

    [DllImport("__Internal")]
    public static extern bool EIP6963_IsAvailable();

    [DllImport("__Internal")]
    public static extern void EIP6963_InitEIP6963(); // Unity must call this first on startup

    [DllImport("__Internal")]
    public static extern string EIP6963_GetAvailableWallets();

    [DllImport("__Internal")]
    public static extern void EIP6963_SelectWallet(string walletUuid);

    [DllImport("__Internal")]
    public static extern string EIP6963_GetWalletIcon(string walletUuid);

    [DllImport("__Internal")]
    public static extern void EIP6963_EthereumInit(string gameObjectName, string callBackAccountChange, string callBackChainChange);

    [DllImport("__Internal")]
    public static extern void EIP6963_EthereumInitRpcClientCallback(Action<string> callBackAccountChange, Action<string> callBackChainIdChange);

    [DllImport("__Internal")]
    public static extern string EIP6963_Request(string rpcRequestMessage, string gameObjectName, string callback, string fallback);

    [DllImport("__Internal")]
    public static extern void EIP6963_RequestRpcClientCallback(Action<string> rpcResponse, string rpcRequest);
}
```

**From:** `src/Nethereum.Unity.EIP6963/EIP6963WebglInterop.cs:11`

### EIP6963WebglHostProvider

Unity WebGL implementation of `EIP6963WalletHostProvider`:

```csharp
public class EIP6963WebglHostProvider : EIP6963WalletHostProvider
{
    public static EIP6963WalletHostProvider CreateOrGetCurrentInstance()
    {
        if (Current == null) return new EIP6963WebglHostProvider();
        return Current;
    }

    public EIP6963WebglHostProvider() : base(new EIP6963WebglTaskRequestInterop())
    {
        ((EIP6963WebglTaskRequestInterop)this._walletInterop).InitEIP6963();
    }
}
```

**From:** `src/Nethereum.Unity.EIP6963/EIP6963WebglHostProvider.cs:6`

**Initialization:**
- Calls `InitEIP6963()` on construction to set up browser-side event listeners
- Creates singleton instance via `CreateOrGetCurrentInstance()`
- Uses `EIP6963WebglTaskRequestInterop` for browser communication

### EIP6963WebglTaskRequestInterop

Implements `IEIP6963WalletInterop` using async/await with polling:

```csharp
public class EIP6963WebglTaskRequestInterop : IEIP6963WalletInterop
{
    public static Dictionary<string, RpcResponseMessage> RequestResponses = new Dictionary<string, RpcResponseMessage>();

    [MonoPInvokeCallback(typeof(Action<string>))]
    public static void EIP6963_TaskRequestInteropCallBack(string responseMessage)
    {
        var response = JsonConvert.DeserializeObject<RpcResponseMessage>(responseMessage, DefaultJsonSerializerSettingsFactory.BuildDefaultJsonSerializerSettings());
        RequestResponses.Add((string)response.Id, response);
    }

    public async Task<EIP6963WalletInfo[]> GetAvailableWalletsAsync()
    {
        var wallets = EIP6963WebglInterop.EIP6963_GetAvailableWallets();
        return JsonConvert.DeserializeObject<EIP6963WalletInfo[]>(wallets, JsonSerializerSettings);
    }

    public async Task SelectWalletAsync(string walletId)
    {
        EIP6963WebglInterop.EIP6963_SelectWallet(walletId);
    }

    public async Task<string> GetWalletIconAsync(string walletId)
    {
        return EIP6963WebglInterop.EIP6963_GetWalletIcon(walletId);
    }

    public void InitEIP6963()
    {
        EIP6963WebglInterop.EIP6963_InitEIP6963();
    }
}
```

**From:** `src/Nethereum.Unity.EIP6963/EIP6963WebglTaskRequestInterop.cs:19`

**Request Flow:**
1. Generates unique request ID
2. Calls JavaScript via `EIP6963_RequestRpcClientCallback`
3. JavaScript callback populates `RequestResponses` dictionary
4. Polls dictionary every `DelayBetweenResponseCheckMilliseconds` (default 1000ms)
5. Returns response or throws timeout exception

**Configuration:**
- `TimeOutMilliseconds` - Default 3600000ms (1 hour)
- `DelayBetweenResponseCheckMilliseconds` - Default 1000ms

### Event Handlers

Static callbacks for wallet events using `[MonoPInvokeCallback]`:

```csharp
[MonoPInvokeCallback(typeof(Action<string>))]
public static void EIP6963_SelectedAccountChanged(string selectedAccount)
{
    EIP6963WebglHostProvider.Current.ChangeSelectedAccountAsync(selectedAccount).RunSynchronously();
}

[MonoPInvokeCallback(typeof(Action<string>))]
public static void EIP6963_SelectedNetworkChanged(string chainId)
{
    EIP6963WebglHostProvider.Current.ChangeSelectedNetworkAsync((long)new HexBigInteger(chainId).Value).RunSynchronously();
}
```

**From:** `src/Nethereum.Unity.EIP6963/EIP6963WebglTaskRequestInterop.cs:30`

## JavaScript Library (NethereumEIP6963.jslib)

Browser-side JavaScript functions merged into Unity WebGL build:

**EIP-6963 Initialization:**

```javascript
EIP6963_InitEIP6963: function () {
    if (window.NethereumEIP6963Interop) return;

    window.NethereumEIP6963Interop = {
        ethereumProviders: [],
        selectedEthereumProvider: null,
        eventsInitialized: false,
        initialized: false,

        init: function () {
            if (this.initialized) return;
            this.initialized = true;
            this.ethereumProviders = [];

            window.addEventListener("eip6963:announceProvider", (event) => {
                const provider = event.detail;
                if (!this.ethereumProviders.some(p => p.info.uuid === provider.info.uuid)) {
                    this.ethereumProviders.push(provider);
                }
            });

            window.dispatchEvent(new Event("eip6963:requestProvider"));
        },

        getAvailableWallets: function () {
            return this.ethereumProviders.map(provider => ({
                name: provider.info.name,
                uuid: provider.info.uuid,
                icon: provider.info.icon,
                rdns: provider.info.rdns
            }));
        },

        selectWallet: async function (uuid) {
            const provider = this.ethereumProviders.find(p => p.info.uuid === uuid);
            if (provider) {
                this.selectedEthereumProvider = provider.provider;
            }
        },

        getWalletIcon: function (walletUuid) {
            const provider = this.ethereumProviders.find(p => p.info.uuid === walletUuid);
            return provider ? provider.info.icon : null;
        }
    };

    window.NethereumEIP6963Interop.init();
}
```

**From:** `src/Nethereum.Unity.EIP6963/NethereumEIP6963.jslib:2`

**Get Available Wallets:**

```javascript
EIP6963_GetAvailableWallets: function () {
    const wallets = window.NethereumEIP6963Interop.getAvailableWallets();
    const walletsJson = JSON.stringify(wallets);
    if (walletsJson !== null) {
        var bufferSize = lengthBytesUTF8(walletsJson) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(walletsJson, buffer, bufferSize);
        return buffer;
    }
    return null;
}
```

**From:** `src/Nethereum.Unity.EIP6963/NethereumEIP6963.jslib:115`

**Select Wallet:**

```javascript
EIP6963_SelectWallet: function (walletUuid) {
    const decodedWalletUuid = UTF8ToString(walletUuid);
    return window.NethereumEIP6963Interop.selectWallet(decodedWalletUuid);
}
```

**From:** `src/Nethereum.Unity.EIP6963/NethereumEIP6963.jslib:139`

**RPC Request with Selected Wallet:**

```javascript
EIP6963_RequestRpcClientCallback: async function (callback, message) {
    const parsedMessageStr = UTF8ToString(message);

    try {
        if (!window.NethereumEIP6963Interop.selectedEthereumProvider) {
            throw new Error("No wallet selected.");
        }

        let parsedMessage = JSON.parse(parsedMessageStr);
        const response = await window.NethereumEIP6963Interop.selectedEthereumProvider.request(parsedMessage);

        let rpcResponse = {
            jsonrpc: "2.0",
            result: response,
            id: parsedMessage.id,
            error: null
        };

        var json = JSON.stringify(rpcResponse);
        var len = lengthBytesUTF8(json) + 1;
        var strPtr = _malloc(len);
        stringToUTF8(json, strPtr, len);
        Module.dynCall_vi(callback, strPtr);
    } catch (error) {
        let rpcResponseError = {
            jsonrpc: "2.0",
            id: null,
            error: { message: error.message }
        };

        var json = JSON.stringify(rpcResponseError);
        var len = lengthBytesUTF8(json) + 1;
        var strPtr = _malloc(len);
        stringToUTF8(json, strPtr, len);
        Module.dynCall_vi(callback, strPtr);
    }
}
```

**From:** `src/Nethereum.Unity.EIP6963/NethereumEIP6963.jslib:180`

## Wallet Metadata Structure

```csharp
public class EIP6963WalletInfo
{
    public string Name { get; set; }      // e.g., "MetaMask"
    public string Uuid { get; set; }      // Unique identifier
    public string Icon { get; set; }      // Data URI (PNG, SVG, etc.)
    public string Rdns { get; set; }      // Reverse DNS identifier (e.g., "io.metamask")
}
```

## Usage

### Basic Setup

```csharp
using UnityEngine;
using Nethereum.Unity.EIP6963;
using Nethereum.EIP6963WalletInterop;

public class EIP6963Example : MonoBehaviour
{
    private EIP6963WalletHostProvider walletProvider;

    async void Start()
    {
        // Create or get singleton instance (initializes EIP-6963)
        walletProvider = EIP6963WebglHostProvider.CreateOrGetCurrentInstance();

        // Check if EIP-6963 is available
        var available = await walletProvider.CheckProviderAvailableAsync();
        if (!available)
        {
            Debug.LogError("No EIP-6963 wallets available");
            return;
        }

        // Get list of available wallets
        var wallets = await walletProvider.GetAvailableWalletsAsync();
        Debug.Log($"Found {wallets.Length} wallets");

        foreach (var wallet in wallets)
        {
            Debug.Log($"Wallet: {wallet.Name} ({wallet.Uuid})");
            Debug.Log($"RDNS: {wallet.Rdns}");
            Debug.Log($"Icon: {wallet.Icon.Substring(0, 50)}..."); // Data URI
        }

        // Select a wallet by UUID
        await walletProvider.SelectWalletAsync(wallets[0].Uuid);

        // Connect and get account
        var selectedAccount = await walletProvider.ConnectAndGetSelectedAccountAsync();
        if (selectedAccount != null)
        {
            Debug.Log($"Connected to: {selectedAccount}");
        }
    }
}
```

### Display Wallet Selection UI

```csharp
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class WalletSelectionUI : MonoBehaviour
{
    public GameObject walletButtonPrefab;
    public Transform walletListContainer;

    private EIP6963WalletHostProvider walletProvider;
    private Dictionary<string, Texture2D> walletIcons = new Dictionary<string, Texture2D>();

    async void Start()
    {
        walletProvider = EIP6963WebglHostProvider.CreateOrGetCurrentInstance();

        // Get available wallets
        var wallets = await walletProvider.GetAvailableWalletsAsync();

        // Create UI button for each wallet
        foreach (var wallet in wallets)
        {
            var button = Instantiate(walletButtonPrefab, walletListContainer);
            var buttonText = button.GetComponentInChildren<Text>();
            buttonText.text = wallet.Name;

            // Load wallet icon (data URI)
            var icon = await LoadWalletIconAsync(wallet.Uuid);
            if (icon != null)
            {
                var buttonImage = button.GetComponent<Image>();
                buttonImage.sprite = Sprite.Create(icon, new Rect(0, 0, icon.width, icon.height), Vector2.zero);
            }

            // Add click listener
            button.GetComponent<Button>().onClick.AddListener(async () => {
                await SelectWalletAndConnect(wallet.Uuid, wallet.Name);
            });
        }
    }

    async System.Threading.Tasks.Task<Texture2D> LoadWalletIconAsync(string walletUuid)
    {
        // Icon is returned as data URI (e.g., "data:image/png;base64,...")
        var iconDataUri = await walletProvider.GetWalletIconAsync(walletUuid);

        // Parse data URI and convert to Texture2D
        // (Implementation depends on data URI format - PNG, SVG, etc.)
        return ConvertDataUriToTexture(iconDataUri);
    }

    async void SelectWalletAndConnect(string walletUuid, string walletName)
    {
        try
        {
            await walletProvider.SelectWalletAsync(walletUuid);
            var account = await walletProvider.ConnectAndGetSelectedAccountAsync();
            Debug.Log($"Connected to {walletName}: {account}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Connection failed: {ex.Message}");
        }
    }
}
```

### Send Transaction

```csharp
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Hex.HexTypes;

async void SendTransaction()
{
    var transactionInput = new TransactionInput
    {
        To = "0x...",
        Value = new HexBigInteger(1000000000000000000), // 1 ETH in wei
        Gas = new HexBigInteger(21000)
    };

    try
    {
        string txHash = await walletProvider.SendTransactionAsync(transactionInput);
        Debug.Log($"Transaction hash: {txHash}");
    }
    catch (System.Exception ex)
    {
        Debug.LogError($"Transaction failed: {ex.Message}");
    }
}
```

### Listen to Account/Chain Changes

```csharp
void OnEnable()
{
    walletProvider.SelectedAccountChanged += OnAccountChanged;
    walletProvider.ChainIdChanged += OnChainChanged;
}

void OnDisable()
{
    walletProvider.SelectedAccountChanged -= OnAccountChanged;
    walletProvider.ChainIdChanged -= OnChainChanged;
}

private void OnAccountChanged(string newAccount)
{
    Debug.Log($"Account changed to: {newAccount}");
}

private void OnChainChanged(long newChainId)
{
    Debug.Log($"Chain changed to: {newChainId}");
}
```

## Complete Example: Multi-Wallet DApp

```csharp
using UnityEngine;
using Nethereum.Unity.EIP6963;
using Nethereum.EIP6963WalletInterop;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Hex.HexTypes;
using System.Threading.Tasks;

public class MultiWalletDApp : MonoBehaviour
{
    private EIP6963WalletHostProvider walletProvider;
    private string connectedAccount;

    async void Start()
    {
        // Initialize EIP-6963
        walletProvider = EIP6963WebglHostProvider.CreateOrGetCurrentInstance();

        // Listen for wallet events
        walletProvider.SelectedAccountChanged += OnAccountChanged;
        walletProvider.ChainIdChanged += OnChainChanged;

        // Display wallet selection
        await ShowWalletSelectionAsync();
    }

    async Task ShowWalletSelectionAsync()
    {
        var available = await walletProvider.CheckProviderAvailableAsync();
        if (!available)
        {
            Debug.LogError("No EIP-6963 wallets detected");
            return;
        }

        var wallets = await walletProvider.GetAvailableWalletsAsync();
        Debug.Log("=== Available Wallets ===");

        foreach (var wallet in wallets)
        {
            Debug.Log($"- {wallet.Name}");
            Debug.Log($"  UUID: {wallet.Uuid}");
            Debug.Log($"  RDNS: {wallet.Rdns}");
        }

        // Auto-select first wallet for this example
        if (wallets.Length > 0)
        {
            await ConnectToWalletAsync(wallets[0].Uuid, wallets[0].Name);
        }
    }

    async Task ConnectToWalletAsync(string walletUuid, string walletName)
    {
        try
        {
            Debug.Log($"Selecting {walletName}...");
            await walletProvider.SelectWalletAsync(walletUuid);

            Debug.Log("Requesting account access...");
            connectedAccount = await walletProvider.ConnectAndGetSelectedAccountAsync();

            Debug.Log($"✓ Connected to {walletName}");
            Debug.Log($"  Account: {connectedAccount}");

            // Get current chain
            var chainId = await walletProvider.GetProviderChainIdAsync();
            Debug.Log($"  Chain ID: {chainId}");

        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Connection failed: {ex.Message}");
        }
    }

    async void SendETH()
    {
        var transactionInput = new TransactionInput
        {
            To = "0x742d35Cc6634C0532925a3b844Bc9e7595f0bEb",
            Value = new HexBigInteger(10000000000000000), // 0.01 ETH
            Gas = new HexBigInteger(21000)
        };

        try
        {
            var txHash = await walletProvider.SendTransactionAsync(transactionInput);
            Debug.Log($"Transaction sent: {txHash}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Transaction failed: {ex.Message}");
        }
    }

    private void OnAccountChanged(string newAccount)
    {
        Debug.Log($"Account switched: {newAccount}");
        connectedAccount = newAccount;
    }

    private void OnChainChanged(long newChainId)
    {
        Debug.Log($"Chain switched: {newChainId}");
    }

    void OnDestroy()
    {
        if (walletProvider != null)
        {
            walletProvider.SelectedAccountChanged -= OnAccountChanged;
            walletProvider.ChainIdChanged -= OnChainChanged;
        }
    }
}
```

## Important Notes

**WebGL Only:**
- This package only works in Unity WebGL builds
- Requires EIP-6963 compliant browser wallets
- Non-WebGL platforms should use `Nethereum.Unity` with private key signing

**Wallet Detection:**
- Wallets must support EIP-6963 to be discovered
- Not all browser wallets implement EIP-6963 (as of 2025)
- Fallback to `Nethereum.Unity.Metamask` for Metamask-only support

**AOT Compilation:**
- Uses `[MonoPInvokeCallback]` attribute for JavaScript callbacks
- Callbacks must be static methods
- Required for IL2CPP/WebGL builds

**Initialization:**
- `EIP6963_InitEIP6963()` must be called before wallet discovery
- Called automatically in `EIP6963WebglHostProvider` constructor
- Sets up browser event listeners for wallet announcements

**Async/Await Pattern:**
- Uses `async/await` with polling
- Polls response dictionary every 1 second by default
- Default timeout: 1 hour

**Icon Data URIs:**
- Wallet icons are provided as data URIs
- Format: `data:image/png;base64,...` or `data:image/svg+xml,...`
- Requires parsing and conversion to Unity Texture2D

## Comparison with Metamask Package

**Nethereum.Unity.EIP6963:**
- ✅ Discovers multiple wallets
- ✅ User can choose wallet
- ✅ Future-proof (new wallets auto-discovered)
- ✅ Wallet metadata (name, icon, RDNS)
- ❌ Requires EIP-6963 support

**Nethereum.Unity.Metamask:**
- ✅ Direct Metamask connection
- ✅ Broader wallet compatibility (via `window.ethereum`)
- ❌ Single wallet only
- ❌ No multi-wallet discovery

**Recommendation:** Use EIP-6963 for modern multi-wallet support. Fall back to Metamask package if EIP-6963 wallets not detected.

## Related Packages

- **Nethereum.EIP6963WalletInterop** - Core EIP-6963 abstraction and interfaces
- **Nethereum.Unity** - Unity coroutine RPC infrastructure
- **Nethereum.Unity.Metamask** - Metamask-only integration for Unity WebGL
- **Nethereum.JsonRpc.Client** - JSON-RPC client infrastructure

## Additional Resources

- **EIP-6963 Specification**: https://eips.ethereum.org/EIPS/eip-6963
- **Unity WebGL JavaScript Interop**: https://docs.unity3d.com/Manual/webgl-interactingwithbrowserscripting.html
- **Nethereum Documentation**: http://docs.nethereum.com
- **Unity3d Sample Template**: https://github.com/Nethereum/Unity3dSampleTemplate
