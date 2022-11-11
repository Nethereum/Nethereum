using System.Threading.Tasks;
 
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RPC.Personal
{
    /// <Summary>
    ///     Validate the given passphrase and submit transaction.
    ///     The transaction is the same argument as for eth_sendTransaction and contains the from address. If the passphrase
    ///     can be used to decrypt the private key belogging to tx.from the transaction is verified, signed and send onto the
    ///     network. The account is not unlocked globally in the node and cannot be used in other RPC calls.
    /// </Summary>
    public class PersonalSignAndSendTransaction : RpcRequestResponseHandler<string>, IPersonalSignAndSendTransaction
    {
        public PersonalSignAndSendTransaction(IClient client)
            : base(client, ApiMethods.personal_sendTransaction.ToString())
        {
        }

        public Task<string> SendRequestAsync(TransactionInput txn, string password, object id = null)
        {
            return base.SendRequestAsync(id, txn, password);
        }

        public RpcRequest BuildRequest(TransactionInput txn, string password, object id = null)
        {
            return base.BuildRequest(id, txn, password);
        }
    }
}