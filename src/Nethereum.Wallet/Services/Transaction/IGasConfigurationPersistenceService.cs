using System;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.Wallet.Services.Transaction
{
    public interface IGasConfigurationPersistenceService
    {
        Task SaveCustomGasConfigurationAsync(BigInteger chainId, CustomGasConfiguration config);
        Task<CustomGasConfiguration?> GetCustomGasConfigurationAsync(BigInteger chainId);
        Task ClearCustomGasConfigurationAsync(BigInteger chainId);
        Task<bool> GetGasModePreferenceAsync(BigInteger chainId);
        Task SaveGasModePreferenceAsync(BigInteger chainId, bool preferEip1559);
    }

    public class CustomGasConfiguration
    {
        public string? GasLimit { get; set; }
        public string? GasPrice { get; set; }
        public string? MaxFee { get; set; }
        public string? PriorityFee { get; set; }
        public bool IsEip1559 { get; set; }
        public DateTime LastUsed { get; set; }
    }
}