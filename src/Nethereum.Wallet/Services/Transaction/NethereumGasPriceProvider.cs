using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Fee1559Suggestions;
using Nethereum.Wallet.Hosting;
using Nethereum.Wallet.Services.Network;

namespace Nethereum.Wallet.Services.Transaction
{
    public class NethereumGasPriceProvider : IGasPriceProvider
    {
        private readonly NethereumWalletHostProvider _hostProvider;
        private readonly IChainManagementService _chainManagementService;

        public NethereumGasPriceProvider(NethereumWalletHostProvider hostProvider,  IChainManagementService chainManagementService)
        {
            _hostProvider = hostProvider;
            _chainManagementService = chainManagementService;
        }

        public async Task<bool> GetSupportsEIP1559Async()
        {
            try
            {
                var chainId = new BigInteger(_hostProvider.SelectedNetworkChainId);
                var chain = await _chainManagementService.GetChainAsync(chainId).ConfigureAwait(false);
                return chain?.SupportEIP1559 == true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<GasPriceSuggestion> GetGasPriceAsync()
        {
            var web3 = await _hostProvider.GetWeb3Async().ConfigureAwait(false);
            
            if (await GetSupportsEIP1559Async().ConfigureAwait(false))
            {
                var strategy = new TimePreferenceFeeSuggestionStrategy(web3.Client);
                var fee = await strategy.SuggestFeeAsync().ConfigureAwait(false);
                return new GasPriceSuggestion
                {
                    MaxFeePerGas = fee.MaxFeePerGas,
                    MaxPriorityFeePerGas = fee.MaxPriorityFeePerGas
                };
            }
            else
            {
                var gasPrice = await web3.Eth.GasPrice.SendRequestAsync().ConfigureAwait(false);
                return new GasPriceSuggestion
                {
                    GasPrice = gasPrice.Value
                };
            }
        }

        public async Task<IList<GasPriceSuggestion>> GetGasPriceLevelsAsync()
        {
            var web3 = await _hostProvider.GetWeb3Async().ConfigureAwait(false);
            
            if (await GetSupportsEIP1559Async().ConfigureAwait(false))
            {
                var strategy = new TimePreferenceFeeSuggestionStrategy(web3.Client);
                var fees = await strategy.SuggestFeesAsync().ConfigureAwait(false);
                return fees.Select(f => new GasPriceSuggestion
                {
                    MaxFeePerGas = f.MaxFeePerGas,
                    MaxPriorityFeePerGas = f.MaxPriorityFeePerGas
                }).ToList();
            }
            else
            {
                var gasPrice = await web3.Eth.GasPrice.SendRequestAsync().ConfigureAwait(false);
                return new List<GasPriceSuggestion>
                {
                    new GasPriceSuggestion { GasPrice = gasPrice.Value }
                };
            }
        }
        
        public async Task<GasPriceSuggestion> GetLegacyGasPriceAsync()
        {
            try
            {
                var web3 = await _hostProvider.GetWeb3Async().ConfigureAwait(false);
                var gasPrice = await web3.Eth.GasPrice.SendRequestAsync().ConfigureAwait(false);

                return new GasPriceSuggestion
                {
                    GasPrice = gasPrice.Value
                };
            }
            catch
            {
                return new GasPriceSuggestion();
            }
        }
        
        public async Task<GasPriceSuggestion> GetEIP1559GasPriceAsync()
        {
            var web3 = await _hostProvider.GetWeb3Async().ConfigureAwait(false);
            
            try
            {
                var strategy = new TimePreferenceFeeSuggestionStrategy(web3.Client);
                var fee = await strategy.SuggestFeeAsync().ConfigureAwait(false);
                
                return new GasPriceSuggestion
                {
                    MaxFeePerGas = fee.MaxFeePerGas,
                    MaxPriorityFeePerGas = fee.MaxPriorityFeePerGas
                };
            }
            catch
            {
                return new GasPriceSuggestion();
            }
        }
    }
}
