using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Besu.RPC.EEA
{
    /// <Summary>
    ///     Creates a private transaction from a signed transaction, generates the transaction hash and submits it to the
    ///     transaction pool, and returns the transaction hash of the Privacy Marker Transaction.
    ///     The signed transaction passed as an input parameter includes the privateFrom, privateFor, and restriction fields.
    ///     To avoid exposing your private key, create signed transactions offline and send the signed transaction data using
    ///     eea_sendRawTransaction.
    /// </Summary>
    public class EeaSendRawTransaction : RpcRequestResponseHandler<string>, IEeaSendRawTransaction
    {
        public EeaSendRawTransaction(IClient client) : base(client, ApiMethods.eea_sendRawTransaction.ToString())
        {
        }

        public async Task<string> SendRequestAsync(string signedTransaction, object id = null)
        {
            return await base.SendRequestAsync(id, signedTransaction);
        }

        public RpcRequest BuildRequest(string signedTransaction, object id = null)
        {
            return base.BuildRequest(id, signedTransaction);
        }
    }
}