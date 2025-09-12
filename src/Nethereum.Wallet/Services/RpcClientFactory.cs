using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.WebSocketClient;
using Nethereum.RPC.Chain;
using Nethereum.Wallet.UI;
using Nethereum.Wallet.Services.Network;

namespace Nethereum.Wallet.Services
{
    public class RpcClientFactory : IRpcClientFactory
    {
        private readonly IRpcEndpointService _rpcEndpointService;
        private readonly IChainManagementService _chainManagementService;
        private readonly Dictionary<string, IClient> _clientCache = new Dictionary<string, IClient>();

        public IClient DefaultClient { get; } = new RpcClient(new Uri("http://localhost:8545/"));
        public string? LastError { get; private set; }

        public RpcClientFactory(
            IRpcEndpointService rpcEndpointService,
            IChainManagementService chainManagementService)
        {
            _rpcEndpointService = rpcEndpointService ?? throw new ArgumentNullException(nameof(rpcEndpointService));
            _chainManagementService = chainManagementService ?? throw new ArgumentNullException(nameof(chainManagementService));
        }

        public async Task<IClient> CreateClientAsync(ChainFeature? activeChain = null)
        {
            LastError = null;
            
            try
            {
                if (activeChain == null)
                    return DefaultClient;

                // Use the unified RPC endpoint service to select the best endpoint
                var selectedUrl = await _rpcEndpointService.SelectEndpointAsync(activeChain.ChainId);
                
                if (string.IsNullOrEmpty(selectedUrl))
                {
                    // Fallback: try to get chain data and use first available RPC
                    var chain = await _chainManagementService.GetChainAsync(activeChain.ChainId);
                    selectedUrl = chain?.HttpRpcs?.FirstOrDefault();
                    
                    if (string.IsNullOrEmpty(selectedUrl))
                    {
                        LastError = "No RPC endpoints configured for this network";
                        return DefaultClient;
                    }
                }

                if (_clientCache.TryGetValue(selectedUrl, out var cachedClient))
                    return cachedClient;

                var client = CreateClientForUrl(selectedUrl);
                _clientCache[selectedUrl] = client;
                
                return client;
            }
            catch (Exception ex)
            {
                LastError = $"Error creating RPC client: {ex.Message}";
                Console.WriteLine(LastError);
                return DefaultClient;
            }
        }
        private IClient CreateClientForUrl(string rpcUrl)
        {
            if (string.IsNullOrWhiteSpace(rpcUrl))
                throw new ArgumentException("RPC URL cannot be empty", nameof(rpcUrl));

            if (rpcUrl.StartsWith("ws://", StringComparison.OrdinalIgnoreCase) || 
                rpcUrl.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
            {
                return new WebSocketClient(rpcUrl);
            }
            else
            {
                return new RpcClient(new Uri(rpcUrl));
            }
        }
        public void ClearCache()
        {
            _clientCache.Clear();
        }
    }
}