using System;
using System.Threading.Tasks;
using EdjCase.JsonRpc.Core;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;

namespace Nethereum.RPC.Eth.Transactions
{
    /// <Summary>
    ///     eth_sendRawTransaction
    ///     Creates new message call transaction or a contract creation for signed transactions.
    ///     Parameters
    ///     DATA, The signed transaction data.
    ///     params: ["0xd46e8dd67c5d32be8d46e8dd67c5d32be8058bb8eb970870f072445675058bb8eb970870f072445675"]
    ///     Returns
    ///     DATA, 32 Bytes - the transaction hash, or the zero hash if the transaction is not yet available.
    ///     Use eth_getTransactionReceipt to get the contract address, after the transaction was mined, when you created a
    ///     contract.
    ///     Example
    ///     Request
    ///     curl -X POST --data '{"jsonrpc":"2.0","method":"eth_sendRawTransaction","params":[{see above}],"id":1}'
    ///     Result
    ///     {
    ///     "id":1,
    ///     "jsonrpc": "2.0",
    ///     "result": "0xe670ec64341771606e55d6b4ca35a1a6b75ee3d5145a99d05921026d1527331"
    ///     }
    /// </Summary>
    public class EthSendRawTransaction : RpcRequestResponseHandler<string>
    {
        public EthSendRawTransaction(IClient client) : base(client, ApiMethods.eth_sendRawTransaction.ToString())
        {
        }

        public Task<string> SendRequestAsync(string signedTransactionData, object id = null)
        {
            if (signedTransactionData == null) throw new ArgumentNullException(nameof(signedTransactionData));
            return base.SendRequestAsync(id, signedTransactionData.EnsureHexPrefix());
        }

        public RpcRequest BuildRequest(string signedTransactionData, object id = null)
        {
            if (signedTransactionData == null) throw new ArgumentNullException(nameof(signedTransactionData));
            return base.BuildRequest(id, signedTransactionData.EnsureHexPrefix());
        }
    }
}