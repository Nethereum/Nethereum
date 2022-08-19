using Nethereum.Web3;

namespace Nethereum.Geth
{
    public interface IWeb3Geth: IWeb3
    {
        IAdminApiService Admin { get; }
        IDebugApiService GethDebug { get; }
        IGethEthApiService GethEth { get; }
        IMinerApiService Miner { get; }
        ITxnPoolApiService TxnPool { get; }
    }
}