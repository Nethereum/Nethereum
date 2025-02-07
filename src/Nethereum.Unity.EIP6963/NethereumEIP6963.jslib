mergeInto(LibraryManager.library, {
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
                } else {
                    console.log("Wallet not found.");
                }
            },

            getWalletIcon: function (walletUuid) {
                const provider = this.ethereumProviders.find(p => p.info.uuid === walletUuid);
                return provider ? provider.info.icon : null;
            }
        };
    },

    EIP6963_EnableEthereum: async function (gameObjectName, callback, fallback) {
        const parsedObjectName = UTF8ToString(gameObjectName);
        const parsedCallback = UTF8ToString(callback);
        const parsedFallback = UTF8ToString(fallback);

        try {
            if (!window.NethereumEIP6963Interop.selectedEthereumProvider) {
                throw new Error("No wallet selected. Call SelectWallet() first.");
            }

            const accounts = await window.NethereumEIP6963Interop.selectedEthereumProvider.request({
                method: 'eth_requestAccounts'
            });

            var bufferSize = lengthBytesUTF8(accounts[0]) + 1;
            var buffer = _malloc(bufferSize);
            stringToUTF8(accounts[0], buffer, bufferSize);
            nethereumUnityInstance.SendMessage(parsedObjectName, parsedCallback, accounts[0]);
            return buffer;
        } catch (error) {
            nethereumUnityInstance.SendMessage(parsedObjectName, parsedFallback, error.message);
            return null;
        }
    },

    EIP6963_GetSelectedAddress: function () {
        if (!window.NethereumEIP6963Interop.selectedEthereumProvider) return null;

        var returnValue = window.NethereumEIP6963Interop.selectedEthereumProvider.selectedAddress;
        if (returnValue) {
            var bufferSize = lengthBytesUTF8(returnValue) + 1;
            var buffer = _malloc(bufferSize);
            stringToUTF8(returnValue, buffer, bufferSize);
            return buffer;
        }
        return null;
    },

    EIP6963_GetChainId: async function (gameObjectName, callback, fallback) {
        const parsedObjectName = UTF8ToString(gameObjectName);
        const parsedCallback = UTF8ToString(callback);
        const parsedFallback = UTF8ToString(fallback);

        try {
            if (!window.NethereumEIP6963Interop.selectedEthereumProvider) {
                throw new Error("No wallet selected.");
            }

            const chainId = await window.NethereumEIP6963Interop.selectedEthereumProvider.request({
                method: 'eth_chainId'
            });

            nethereumUnityInstance.SendMessage(parsedObjectName, parsedCallback, chainId.toString());
        } catch (error) {
            nethereumUnityInstance.SendMessage(parsedObjectName, parsedFallback, error.message);
        }
    },

    EIP6963_IsAvailable: function () {
        return window.NethereumEIP6963Interop.isAvailable();
    },

    EIP6963_GetAvailableWallets: function () {
        const walletsJson = window.NethereumEIP6963Interop.getAvailableWallets();
        if (walletsJson !== null) {
            var bufferSize = lengthBytesUTF8(walletsJson) + 1;
            var buffer = _malloc(bufferSize);
            stringToUTF8(walletsJson, buffer, bufferSize);
            return buffer;
        }
        return null;
    },

    EIP6963_GetWalletIcon: function (walletUuid) {
        const decodedWalletUuid = UTF8ToString(walletUuid);
        const iconUrl = JSON.stringify(window.NethereumEIP6963Interop.getWalletIcon(decodedWalletUuid));
        if (iconUrl !== null) {
            var bufferSize = lengthBytesUTF8(iconUrl) + 1;
            var buffer = _malloc(bufferSize);
            stringToUTF8(iconUrl, buffer, bufferSize);
            return buffer;
        }
        return null;
    },

    EIP6963_SelectWallet: function (walletUuid) {
        const decodedWalletUuid = UTF8ToString(walletUuid);
        return window.NethereumEIP6963Interop.selectWallet(decodedWalletUuid);
    },

    EIP6963_EthereumInit: function (gameObjectName, callBackAccountChange, callBackChainChange) {
        const parsedObjectName = UTF8ToString(gameObjectName);
        const parsedCallbackAccountChange = UTF8ToString(callBackAccountChange);
        const parsedCallbackChainChange = UTF8ToString(callBackChainChange);

        if (!window.NethereumEIP6963Interop.selectedEthereumProvider) return;

        window.NethereumEIP6963Interop.selectedEthereumProvider.on("accountsChanged", function (accounts) {
            let account = accounts.length > 0 ? accounts[0] : "";
            nethereumUnityInstance.SendMessage(parsedObjectName, parsedCallbackAccountChange, account);
        });

        window.NethereumEIP6963Interop.selectedEthereumProvider.on("chainChanged", function (chainId) {
            nethereumUnityInstance.SendMessage(parsedObjectName, parsedCallbackChainChange, chainId.toString());
        });
    },

    EIP6963_EthereumInitRpcClientCallback: function (callBackAccountChange, callBackChainChange) {
        if (!window.NethereumEIP6963Interop.selectedEthereumProvider) return;

        window.NethereumEIP6963Interop.selectedEthereumProvider.on("accountsChanged", function (accounts) {
            let account = accounts.length > 0 ? accounts[0] : "";
            var len = lengthBytesUTF8(account) + 1;
            var strPtr = _malloc(len);
            stringToUTF8(account, strPtr, len);
            Module.dynCall_vi(callBackAccountChange, strPtr);
        });

        window.NethereumEIP6963Interop.selectedEthereumProvider.on("chainChanged", function (chainId) {
            var len = lengthBytesUTF8(chainId.toString()) + 1;
            var strPtr = _malloc(len);
            stringToUTF8(chainId.toString(), strPtr, len);
            Module.dynCall_vi(callBackChainChange, strPtr);
        });
    },

     EIP6963_RequestRpcClientCallback: async function (callback, message) {
        const parsedMessageStr = UTF8ToString(message);
        const parsedCallback = UTF8ToString(callback);

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
    },

    EIP6963_Request: async function (message, gameObjectName, callback, fallback) {
        const parsedMessageStr = UTF8ToString(message);
        const parsedObjectName = UTF8ToString(gameObjectName);
        const parsedCallback = UTF8ToString(callback);
        const parsedFallback = UTF8ToString(fallback);

        try {
            const parsedMessage = JSON.parse(parsedMessageStr);
            const response = await window.NethereumEIP6963Interop.selectedEthereumProvider.request(parsedMessage);
            const json = JSON.stringify({ jsonrpc: "2.0", result: response, id: parsedMessage.id, error: null });
            nethereumUnityInstance.SendMessage(parsedObjectName, parsedCallback, json);
            return json;
        } catch (error) {
            const json = JSON.stringify({ jsonrpc: "2.0", id: null, error: { message: error.message } });
            nethereumUnityInstance.SendMessage(parsedObjectName, parsedFallback, json);
            return json;
        }
    }
});
