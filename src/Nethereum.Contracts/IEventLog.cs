using Nethereum.RPC.Eth.Filters;

namespace Nethereum.Web3
{
    public interface IEventLog
    {
        FilterLog Log { get; }
    }
}