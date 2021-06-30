using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Besu.RPC.EEA
{
    public interface IEeaSendRawTransaction
    {
        Task<string> SendRequestAsync(string signedTransaction, object id = null);
        RpcRequest BuildRequest(string signedTransaction, object id = null);
    }
}