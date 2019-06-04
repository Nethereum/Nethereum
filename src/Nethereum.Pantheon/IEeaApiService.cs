namespace Nethereum.Pantheon
{
    public interface IEeaApiService
    {
        IEeaGetTransactionReceipt GetTransactionReceipt { get; }
        IEeaSendRawTransaction SendRawTransaction { get; }
    }
}