using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Wallet.Storage;

namespace Nethereum.Wallet.Services.Network
{
    public interface IRpcEndpointService
    {
        Task<RpcSelectionConfiguration?> GetConfigurationAsync(BigInteger chainId);
        Task SaveConfigurationAsync(RpcSelectionConfiguration config);
        Task<bool> CheckHealthAsync(string rpcUrl, BigInteger chainId);
        Task<RpcEndpointHealthCache?> GetHealthCacheAsync(string rpcUrl);
        Task<List<RpcEndpointHealthCache>> GetAllHealthCachesAsync(BigInteger chainId);
        Task<string?> SelectEndpointAsync(BigInteger chainId);
        Task<string?> SelectEndpointAsync(BigInteger chainId, RpcSelectionConfiguration config);
        void ResetRoundRobinState(BigInteger chainId);
        event EventHandler<RpcHealthChangedEventArgs>? HealthChanged;
    }
    public class RpcHealthChangedEventArgs : EventArgs
    {
        public BigInteger ChainId { get; set; }
        public string RpcUrl { get; set; } = string.Empty;
        public bool IsHealthy { get; set; }
        public string? ErrorMessage { get; set; }
        public double? ResponseTimeMs { get; set; }
    }
}