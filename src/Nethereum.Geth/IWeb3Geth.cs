using Nethereum.Web3;

namespace Nethereum.Geth
{
    public interface IWeb3Geth: IWeb3
    {
        IAdminApiService Admin { get; }
        IDebugApiService Debug { get; }
        IGethEthApiService GethEth { get; }
        IMinerApiService Miner { get; }
    }
}