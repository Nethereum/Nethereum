using Nethereum.GSN.Policies;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.GSN
{
    public interface IRelayHubManager
    {
        Task<string> GetHubAddressAsync(string contractAddress);
        Task<BigInteger> GetNonceAsync(string hubAddress, string from);
        Task<RelayCollection> GetRelaysAsync(string hubAddress, IRelayPriorityPolicy policy);
    }
}