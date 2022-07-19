mergeInto(LibraryManager.library, {
    EnableEthereum: async function (gameObjectName, callback, fallback) {
        const parsedObjectName = UTF8ToString(gameObjectName);
        const parsedCallback = UTF8ToString(callback);
        const parsedFallback = UTF8ToString(fallback);
        
        try {
            
            const accounts = await ethereum.request({ method: 'eth_requestAccounts' });
            ethereum.autoRefreshOnNetworkChange = false;

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
    EthereumInit: function(gameObjectName, callBackAccountChange, callBackChainChange){
        const parsedObjectName = UTF8ToString(gameObjectName);
        const parsedCallbackAccountChange = UTF8ToString(callBackAccountChange);
        const parsedCallbackChainChange = UTF8ToString(callBackChainChange);
        console.log("EthereumInit");
            
        ethereum.on("accountsChanged",
                function (accounts) {
                    console.log(accounts[0]);
                    nethereumUnityInstance.SendMessage(parsedObjectName, parsedCallbackAccountChange, accounts[0]);
                });
        ethereum.on("chainChanged",
                function (chainId) {
                    console.log(chainId);
                    nethereumUnityInstance.SendMessage(parsedObjectName, parsedCallbackChainChange, chainId.toString());
                });
    },
    GetChainId: async function(gameObjectName, callback, fallback) {
           const parsedObjectName = UTF8ToString(gameObjectName);
           const parsedCallback = UTF8ToString(callback);
           const parsedFallback = UTF8ToString(fallback);
          try {
           
            const chainId = await ethereum.request({ method: 'eth_chainId' });
            nethereumUnityInstance.SendMessage(parsedObjectName, parsedCallback, chainId.toString());

          } catch (error) {
            nethereumUnityInstance.SendMessage(parsedObjectName, parsedFallback, error.message);
            return null;
         }
    },
    IsMetamaskAvailable: function () {
        if (window.ethereum) return true;
        return false;
    },
    GetSelectedAddress: function () {
        var returnValue = ethereum.selectedAddress;
        if(returnValue !== null) {
            var bufferSize = lengthBytesUTF8(returnValue) + 1;
            var buffer = _malloc(bufferSize);
            stringToUTF8(returnValue, buffer, bufferSize);
            return buffer;
        }
    },
    Request: async function (message, gameObjectName, callback, fallback ) {
        const parsedMessageStr = UTF8ToString(message);
        const parsedObjectName = UTF8ToString(gameObjectName);
        const parsedCallback = UTF8ToString(callback);
        const parsedFallback = UTF8ToString(fallback);

        try {
            
            let parsedMessage = JSON.parse(parsedMessageStr);
            console.log(parsedMessage);
            const response = await ethereum.request(parsedMessage);
            let rpcResponse = {
                jsonrpc: "2.0",
                result: response,
                id: parsedMessage.id,
                error: null
            }
            console.log(rpcResponse);

            var json = JSON.stringify(rpcResponse);
            console.log(json);
            nethereumUnityInstance.SendMessage(parsedObjectName, parsedCallback, json);
            return json;
        } catch (e) {
            let rpcResonseError = {
                jsonrpc: "2.0",
                id: parsedMessage.id,
                error: {
                    message: e,
                }
            }
            return JSON.stringify(rpcResonseError);
        }
    },

    RequestRpcClientCallback: async function (callback, message) {
        const parsedMessageStr = UTF8ToString(message);
        const parsedCallback = UTF8ToString(callback);
        try {
            
            let parsedMessage = JSON.parse(parsedMessageStr);
            console.log(parsedMessage);
            const response = await ethereum.request(parsedMessage);
            let rpcResponse = {
                jsonrpc: "2.0",
                result: response,
                id: parsedMessage.id,
                error: null
            }
            console.log(rpcResponse);

            var json = JSON.stringify(rpcResponse);
            console.log(json);
           
            var len = lengthBytesUTF8(json) + 1;
            var strPtr = _malloc(len);
            stringToUTF8(json, strPtr, len);
            Module.dynCall_vi(callback, strPtr);

            return json;
        } catch (e) {
            let rpcResonseError = {
                jsonrpc: "2.0",
                id: parsedMessage.id,
                error: {
                    message: e,
                }
            }
            var json = JSON.stringify(rpcResonseError);
            var len = lengthBytesUTF8(json) + 1;
            var strPtr = _malloc(len);
            stringToUTF8(json, strPtr, len);
            Module.dynCall_vi(callback, strPtr);
        }
    },

    Send: async function (message) {
        return new Promise(function (resolve, reject) {
            console.log(JSON.parse(message));
            ethereum.send(JSON.parse(message), function (error, result) {
                console.log(result);
                console.log(error);
                resolve(JSON.stringify(result));
            });
        });
    },

    Sign: async function (utf8HexMsg) {
        return new Promise(function (resolve, reject) {
            const from = ethereum.selectedAddress;
            const params = [utf8HexMsg, from];
            const method = 'personal_sign';
            ethereum.send({
                method,
                params,
                from,
            }, function (error, result) {
                if (error) {
                    reject(error);
                } else {
                    console.log(result.result);
                    resolve(JSON.stringify(result.result));
                }

            });
        });
    }

});