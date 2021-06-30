using Nethereum.Besu.RPC.EEA;

namespace Nethereum.Besu
{
    public interface IEeaApiService
    {
        IEeaGetTransactionReceipt GetTransactionReceipt { get; }
        IEeaSendRawTransaction SendRawTransaction { get; }
    }
}