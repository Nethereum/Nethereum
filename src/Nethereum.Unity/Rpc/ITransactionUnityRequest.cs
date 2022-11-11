using Nethereum.RPC.Eth.DTOs;
using System.Collections;

namespace Nethereum.Unity.Rpc
{
    public interface ITransactionUnityRequest : IUnityRequest<string>
    {
        bool UseLegacyAsDefault { get; set; }
        IEnumerator SignAndSendTransaction(TransactionInput transactionInput);
    }
}