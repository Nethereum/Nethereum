using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.Wallet.Services.Transaction
{
    public interface IGasPriceProvider
    {
        Task<GasPriceSuggestion> GetGasPriceAsync();
        Task<IList<GasPriceSuggestion>> GetGasPriceLevelsAsync();
        Task<bool> GetSupportsEIP1559Async();
        
        Task<GasPriceSuggestion> GetEIP1559GasPriceAsync();
        Task<GasPriceSuggestion> GetLegacyGasPriceAsync();
    }

    public class GasPriceSuggestion
    {
        public BigInteger? GasPrice { get; set; }
        
        public BigInteger? BaseFeePerGas { get; set; }
        public BigInteger? MaxPriorityFeePerGas { get; set; }
        public BigInteger? MaxFeePerGas { get; set; }
    }
}