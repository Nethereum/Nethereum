using System;
using System.Collections.Concurrent;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.Wallet.Services.Transaction
{
    public class InMemoryGasConfigurationPersistenceService : IGasConfigurationPersistenceService
    {
        private readonly ConcurrentDictionary<string, CustomGasConfiguration> _configurations = new();
        private readonly ConcurrentDictionary<string, bool> _modePreferences = new();
        private readonly TimeSpan _expirationTime = TimeSpan.FromDays(7);

        public Task SaveCustomGasConfigurationAsync(BigInteger chainId, CustomGasConfiguration config)
        {
            config.LastUsed = DateTime.UtcNow;
            _configurations[chainId.ToString()] = config;
            return Task.CompletedTask;
        }

        public Task<CustomGasConfiguration?> GetCustomGasConfigurationAsync(BigInteger chainId)
        {
            var key = chainId.ToString();
            if (_configurations.TryGetValue(key, out var config))
            {
                if (DateTime.UtcNow - config.LastUsed < _expirationTime)
                {
                    return Task.FromResult<CustomGasConfiguration?>(config);
                }
                _configurations.TryRemove(key, out _);
            }
            return Task.FromResult<CustomGasConfiguration?>(null);
        }

        public Task ClearCustomGasConfigurationAsync(BigInteger chainId)
        {
            _configurations.TryRemove(chainId.ToString(), out _);
            return Task.CompletedTask;
        }

        public Task<bool> GetGasModePreferenceAsync(BigInteger chainId)
        {
            return Task.FromResult(_modePreferences.TryGetValue(chainId.ToString(), out var preference) && preference);
        }

        public Task SaveGasModePreferenceAsync(BigInteger chainId, bool preferEip1559)
        {
            _modePreferences[chainId.ToString()] = preferEip1559;
            return Task.CompletedTask;
        }
    }
}