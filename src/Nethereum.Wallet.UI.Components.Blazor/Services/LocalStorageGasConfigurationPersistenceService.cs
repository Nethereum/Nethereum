using System;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using Nethereum.Blazor.Storage;
using Nethereum.Wallet.Services.Transaction;

namespace Nethereum.Wallet.UI.Components.Blazor.Services
{
    public class LocalStorageGasConfigurationPersistenceService : IGasConfigurationPersistenceService
    {
        private readonly LocalStorageHelper _localStorage;
        private const string GAS_CONFIG_PREFIX = "nethereum_gas_config_";
        private const string GAS_MODE_PREFIX = "nethereum_gas_mode_";

        public LocalStorageGasConfigurationPersistenceService(IJSRuntime jsRuntime)
        {
            _localStorage = new LocalStorageHelper(jsRuntime);
        }

        public async Task SaveCustomGasConfigurationAsync(BigInteger chainId, CustomGasConfiguration config)
        {
            config.LastUsed = DateTime.UtcNow;
            var key = $"{GAS_CONFIG_PREFIX}{chainId}";
            var json = JsonSerializer.Serialize(config);
            await _localStorage.SetItemAsync(key, json);
        }

        public async Task<CustomGasConfiguration?> GetCustomGasConfigurationAsync(BigInteger chainId)
        {
            var key = $"{GAS_CONFIG_PREFIX}{chainId}";
            var json = await _localStorage.GetItemAsync(key);
            
            if (string.IsNullOrEmpty(json))
                return null;

            try
            {
                var config = JsonSerializer.Deserialize<CustomGasConfiguration>(json);
                
                if (config != null && DateTime.UtcNow - config.LastUsed < TimeSpan.FromDays(30))
                {
                    return config;
                }
                
                await _localStorage.RemoveItemAsync(key);
            }
            catch
            {
                await _localStorage.RemoveItemAsync(key);
            }
            
            return null;
        }

        public async Task ClearCustomGasConfigurationAsync(BigInteger chainId)
        {
            var key = $"{GAS_CONFIG_PREFIX}{chainId}";
            await _localStorage.RemoveItemAsync(key);
        }

        public async Task<bool> GetGasModePreferenceAsync(BigInteger chainId)
        {
            var key = $"{GAS_MODE_PREFIX}{chainId}";
            var value = await _localStorage.GetItemAsync(key);
            
            // Default to true (prefer EIP-1559) if no preference saved
            return value != "legacy";
        }

        public async Task SaveGasModePreferenceAsync(BigInteger chainId, bool preferEip1559)
        {
            var key = $"{GAS_MODE_PREFIX}{chainId}";
            await _localStorage.SetItemAsync(key, preferEip1559 ? "eip1559" : "legacy");
        }
    }
}