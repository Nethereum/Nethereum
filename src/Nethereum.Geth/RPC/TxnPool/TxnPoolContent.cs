using System;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Nethereum.Geth.RPC.TxnPool
{
    /// <summary>
    /// The content inspection property can be queried to list the exact details of all the transactions currently pending for inclusion in the next block(s), as well as the ones that are being scheduled for future execution only.
    ///
    ///The result is an object with two fields pending and queued.Each of these fields are associative arrays, in which each entry maps an origin-address to a batch of scheduled transactions.These batches themselves are maps associating nonces with actual transactions.
    ///
    ///Please note, there may be multiple transactions associated with the same account and nonce. This can happen if the user broadcast mutliple ones with varying gas allowances (or even complerely different transactions).
    /// </summary>
    public class TxnPoolContent : GenericRpcRequestResponseHandlerNoParam<JObject>, ITxnPoolContent
    {
        public TxnPoolContent(IClient client) : base(client, ApiMethods.txpool_content.ToString())
        {
        } 
    }
}