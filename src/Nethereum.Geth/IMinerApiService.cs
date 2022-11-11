using Nethereum.Geth.RPC.Miner;

namespace Nethereum.Geth
{
    public interface IMinerApiService
    {
        IMinerSetGasPrice SetGasPrice { get; }
        IMinerStart Start { get; }
        IMinerStop Stop { get; }
    }
}