using System;
using System.Threading.Tasks;
using Nethereum.GSN.Models;

namespace Nethereum.GSN
{
    public interface IRelayClient
    {
        Task<GetAddrResponse> GetAddrAsync(Uri relayUrl);
        Task<RelayResponse> RelayAsync(Uri relayUrl, RelayRequest request);
    }
}