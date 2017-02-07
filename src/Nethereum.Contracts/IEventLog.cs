using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Filters;

namespace Nethereum.Contracts
{
    public interface IEventLog
    {
        FilterLog Log { get; }
    }
}