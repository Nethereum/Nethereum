using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Contracts
{
    public interface IEventLog
    {
        FilterLog Log { get; }
    }
}