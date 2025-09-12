using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nethereum.Wallet.Services
{
    public interface IEnsService
    {
        Task<string?> ResolveAddressToNameAsync(string address);
        Task<string?> ResolveNameToAddressAsync(string ensName);
        Task<Dictionary<string, string?>> BatchResolveAddressesToNamesAsync(IEnumerable<string> addresses);
        void ClearCache();
        string? GetCachedName(string address);
    }
}