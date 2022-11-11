using Nethereum.RPC.Eth.Uncles;

namespace Nethereum.RPC.Eth.Services
{
    public interface IEthApiUncleService
    {
        IEthGetUncleByBlockHashAndIndex GetUncleByBlockHashAndIndex { get; }
        IEthGetUncleByBlockNumberAndIndex GetUncleByBlockNumberAndIndex { get; }
        IEthGetUncleCountByBlockHash GetUncleCountByBlockHash { get; }
        IEthGetUncleCountByBlockNumber GetUncleCountByBlockNumber { get; }
    }
}