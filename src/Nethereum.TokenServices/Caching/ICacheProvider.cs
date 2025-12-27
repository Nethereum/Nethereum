using System;
using System.Threading.Tasks;

namespace Nethereum.TokenServices.Caching
{
    public interface ICacheProvider
    {
        Task<T> GetAsync<T>(string key);
        Task SetAsync<T>(string key, T value, TimeSpan? expiry = null);
        Task<bool> ExistsAsync(string key);
        Task RemoveAsync(string key);
    }
}
