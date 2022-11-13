logEnabled = false;

async function metamaskRequest(parsedMessage) {
    try {
        sanitizeParams(parsedMessage);
        ensureInitilised();
        log(parsedMessage);
        const response = await ethereum.request(parsedMessage);
        let rpcResponse = {
            jsonrpc: "2.0",
            result: response,
            id: parsedMessage.id,
            error: null
        }
        log("request response:");
        log(rpcResponse);
        return rpcResponse;
    } catch (e) {
        log("Error:" + e);
        let rpcResonseError = {
            jsonrpc: "2.0",
            id: parsedMessage.id,
            error: e
        }
        return rpcResonseError;
    }
}

async function getSelectedOrRequestAddress() {
    let accountsResponse = await requestAccounts();
    if (accountsResponse.error !== null) throw accountsResponse.error;
    return accountsResponse.result[0];
}

async function requestAccounts() {
    return await metamaskRequest({ method: 'eth_requestAccounts' });
}

async function getAddresses() {
    return await metamaskRequest({ method: 'eth_accounts' });
}

function log(message) {
    if (logEnabled) {
        console.log(message);
    }
}

function ensureInitilised() {
    if (!initialised) {
        try {
            ethereum.autoRefreshOnNetworkChange = false;
            ethereum.on("accountsChanged",
                function (accounts) {
                    DotNet.invokeMethodAsync('Nethereum.Metamask.Blazor', 'SelectedAccountChanged', accounts[0]);
                });
            ethereum.on("chainChanged",
                function (chainId) {
                    DotNet.invokeMethodAsync('Nethereum.Metamask.Blazor', 'SelectedNetworkChanged', chainId.toString());
                });
            initialised = true;
        } catch (error) {
            return null;
        }
    }
}

function sanitizeParams(parsedMessage) {
    if (!Array.isArray(parsedMessage.params)) return;
    parsedMessage.params.forEach(params => {
        for (const i in params) {
            if (params[i] === null) {
                delete params[i];
            }
        }
    });
}

initialised = false;

window.NethereumMetamaskInterop = {
    EnableEthereum: async () => {
        try {
            const selectedAccount = getSelectedOrRequestAddress();
            ensureInitilised();
            return selectedAccount;
        } catch (error) {
            return null;
        }
    },
    IsMetamaskAvailable: () => {
        if (window.ethereum) return true;
        return false;
    },
    GetAddresses: async () => {
        const rpcResponse = await getAddresses();
        return JSON.stringify(rpcResponse);
    },

    Request: async (message) => {
        const parsedMessage = JSON.parse(message);
        const rpcResponse = await metamaskRequest(parsedMessage);
        return JSON.stringify(rpcResponse);
    },

    Send: async (message) => {
        return new Promise(function (resolve, reject) {
            log(JSON.parse(message))
            ethereum.send(JSON.parse(message), function (error, result) {
                log(result);
                log(error);
                resolve(JSON.stringify(result));
            });
        });
    },

    Sign: async (utf8HexMsg) => {
        try {
            const from = await getSelectedOrRequestAddress();
            log(from);
            const params = [utf8HexMsg, from];
            const method = 'personal_sign';
            const rpcResponse = await metamaskRequest({
                method,
                params,
                from
            });
            return JSON.stringify(rpcResponse);
        } catch (e) {
            log("Error signing:" + e);
            let rpcResponseError = {
                jsonrpc: "2.0",
                id: parsedMessage.id,
                error: e
            }
            return JSON.stringify(rpcResponseError);
        }
    }
}