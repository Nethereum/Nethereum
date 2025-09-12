// Nethereum Wallet Provider JavaScript Module
// Handles interaction with browser-based wallet providers (MetaMask, etc.)

let dotNetRef = null;

export function initializeWallet() {
    if (typeof window.ethereum === 'undefined') {
        console.warn('No wallet provider found');
        return false;
    }

    console.log('Wallet provider initialized');
    return true;
}

export function setupEventListeners(dotNetReference) {
    dotNetRef = dotNetReference;
    
    if (typeof window.ethereum !== 'undefined') {
        // Listen for account changes
        window.ethereum.on('accountsChanged', handleAccountsChanged);
        
        // Listen for chain changes
        window.ethereum.on('chainChanged', handleChainChanged);
        
        // Listen for connection events
        window.ethereum.on('connect', handleConnect);
        window.ethereum.on('disconnect', handleDisconnect);
        
        console.log('Wallet event listeners setup complete');
    }
}

export async function requestAccounts() {
    if (typeof window.ethereum === 'undefined') {
        throw new Error('No wallet provider available');
    }

    try {
        const accounts = await window.ethereum.request({
            method: 'eth_requestAccounts'
        });
        
        return accounts || [];
    } catch (error) {
        console.error('Failed to request accounts:', error);
        throw error;
    }
}

export async function getCurrentAccounts() {
    if (typeof window.ethereum === 'undefined') {
        return [];
    }

    try {
        const accounts = await window.ethereum.request({
            method: 'eth_accounts'
        });
        
        return accounts || [];
    } catch (error) {
        console.error('Failed to get current accounts:', error);
        return [];
    }
}

export async function getCurrentChainId() {
    if (typeof window.ethereum === 'undefined') {
        return null;
    }

    try {
        const chainId = await window.ethereum.request({
            method: 'eth_chainId'
        });
        
        return chainId;
    } catch (error) {
        console.error('Failed to get chain ID:', error);
        return null;
    }
}

export async function switchNetwork(chainIdHex) {
    if (typeof window.ethereum === 'undefined') {
        return false;
    }

    try {
        await window.ethereum.request({
            method: 'wallet_switchEthereumChain',
            params: [{ chainId: chainIdHex }]
        });
        
        return true;
    } catch (error) {
        console.error('Failed to switch network:', error);
        
        // If the network doesn't exist, try to add it
        if (error.code === 4902) {
            return await addNetwork(chainIdHex);
        }
        
        return false;
    }
}

export async function addNetwork(chainIdHex) {
    const networks = {
        '0x89': { // Polygon
            chainId: '0x89',
            chainName: 'Polygon Mainnet',
            rpcUrls: ['https://polygon-rpc.com/'],
            nativeCurrency: {
                name: 'MATIC',
                symbol: 'MATIC',
                decimals: 18
            },
            blockExplorerUrls: ['https://polygonscan.com/']
        },
        '0xa': { // Optimism
            chainId: '0xa',
            chainName: 'Optimism',
            rpcUrls: ['https://mainnet.optimism.io'],
            nativeCurrency: {
                name: 'Ethereum',
                symbol: 'ETH',
                decimals: 18
            },
            blockExplorerUrls: ['https://optimistic.etherscan.io/']
        },
        '0xa4b1': { // Arbitrum
            chainId: '0xa4b1',
            chainName: 'Arbitrum One',
            rpcUrls: ['https://arb1.arbitrum.io/rpc'],
            nativeCurrency: {
                name: 'Ethereum',
                symbol: 'ETH',
                decimals: 18
            },
            blockExplorerUrls: ['https://arbiscan.io/']
        }
    };

    const networkConfig = networks[chainIdHex];
    if (!networkConfig) {
        console.error(`Unknown network: ${chainIdHex}`);
        return false;
    }

    try {
        await window.ethereum.request({
            method: 'wallet_addEthereumChain',
            params: [networkConfig]
        });
        
        return true;
    } catch (error) {
        console.error('Failed to add network:', error);
        return false;
    }
}

export function disconnect() {
    // Most providers don't support programmatic disconnect
    // This is mainly for cleanup
    console.log('Wallet disconnected');
}

// Event handlers
function handleAccountsChanged(accounts) {
    console.log('Accounts changed:', accounts);
    
    if (dotNetRef) {
        dotNetRef.invokeMethodAsync('OnAccountsChanged', accounts);
    }
}

function handleChainChanged(chainId) {
    console.log('Chain changed:', chainId);
    
    if (dotNetRef) {
        dotNetRef.invokeMethodAsync('OnChainChanged', chainId);
    }
}

function handleConnect(connectInfo) {
    console.log('Wallet connected:', connectInfo);
    
    if (dotNetRef) {
        dotNetRef.invokeMethodAsync('OnConnect');
    }
}

function handleDisconnect(error) {
    console.log('Wallet disconnected:', error);
    
    if (dotNetRef) {
        dotNetRef.invokeMethodAsync('OnDisconnect', error?.message || 'Unknown error');
    }
}

// Utility functions
export function isWalletAvailable() {
    return typeof window.ethereum !== 'undefined';
}

export function getWalletInfo() {
    if (typeof window.ethereum === 'undefined') {
        return null;
    }

    return {
        isMetaMask: window.ethereum.isMetaMask,
        isCoinbaseWallet: window.ethereum.isCoinbaseWallet,
        isWalletConnect: window.ethereum.isWalletConnect
    };
}