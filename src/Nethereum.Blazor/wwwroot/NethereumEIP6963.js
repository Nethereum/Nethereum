logEnabled = true;

function log(message) {
    if (logEnabled && message) {
        console.log(message);
    }
}

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

 
    isAvailable: function () {
        return this.ethereumProviders.length > 0;
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
            log(`Selected wallet: ${provider.info.name}`);
            log(uuid);
            await this.enableEthereum();
        } else {
           log("Error: Wallet not found");
        }
    },

 
    enableEthereum: async function () {
        try {
            if (!this.selectedEthereumProvider) {
                return null;
            };
            
            this.ensureInitialized();
            //const selectedAccount = await this.getSelectedOrRequestAddress();
            //return selectedAccount;
        } catch (error) {
            log("Error enabling Ethereum:", error);
            return null;
        }
    },


    ensureInitialized: function () {
        if (!this.selectedEthereumProvider || this.eventsInitialized) return;

        try {
            this.selectedEthereumProvider.on("accountsChanged", (accounts) => {
                DotNet.invokeMethodAsync('Nethereum.Blazor', 'EIP6963SelectedAccountChanged', accounts[0]);
            });

            this.selectedEthereumProvider.on("chainChanged", (chainId) => {
                DotNet.invokeMethodAsync('Nethereum.Blazor', 'EIP6963SelectedNetworkChanged', chainId.toString());
            });

            this.eventsInitialized = true;
        } catch (error) {
            log("Error initializing wallet:", error);
        }
    },


    getSelectedOrRequestAddress: async function () {
        if (!this.selectedEthereumProvider) {
           log("No wallet selected.");
            return null;
        }
        const accountsResponse = await this.eip6963WalletRequest({ method: 'eth_requestAccounts' });
        log("Accounts response:" + accountsResponse);
        return JSON.stringify(accountsResponse);
    },

    request: async function (message) {
        const parsedMessage = JSON.parse(message);
        const response = await this.eip6963WalletRequest(parsedMessage);
        return JSON.stringify(response);
    },


    eip6963WalletRequest: async function (parsedMessage) {
        try {
            this.ensureInitialized();

            if (!this.selectedEthereumProvider) {
                log("Error: No wallet selected. Call selectWallet() first.");
                throw new Error("No wallet selected. Call selectWallet() first.");
            }

            log(`🔄 Sending request: ${JSON.stringify(parsedMessage)}`);

            const response = await this.selectedEthereumProvider.request(parsedMessage);
            log(`✅ Response received: ${JSON.stringify(response)}`);

            return { jsonrpc: "2.0", result: response, id: parsedMessage.id, error: null };
        } catch (e) {
            log(`❌ Error in Ethereum request: ${e.message}`);

            return {
                jsonrpc: "2.0",
                id: parsedMessage.id,
                error: {
                    code: e.code || -32000,
                    message: e.message || "Unknown error",
                    data: e.data || null
                }
            };
        }
    },


    sign: async function (utf8HexMsg) {
        try {
            const from = await this.getSelectedOrRequestAddress();
            log("Signing from: " + from);
            const params = [utf8HexMsg, from];
            const method = 'personal_sign';
            const rpcResponse = await this.eip6963WalletRequest({ method, params, from });
            return JSON.stringify(rpcResponse);
        } catch (e) {
            log("Error signing: " + e);
            return JSON.stringify({ jsonrpc: "2.0", id: null, error: e });
        }
    },


    getWalletIcon: function (walletUuid) {
        const provider = this.ethereumProviders.find(p => p.info.uuid === walletUuid);
        return provider ? provider.info.icon : null;
    }
};

window.addEventListener("load", function () {
    window.NethereumEIP6963Interop.init();
});

