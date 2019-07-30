using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json.Linq;


namespace Nethereum.Pantheon.RPC.Debug
{
    public interface IDebugStorageRangeAt
    {
        Task<JObject> SendRequestAsync(string blockHash, int txIndex, string contractAddress, string startKeyHash,
            int limitStorageEntries, object id = null);

        RpcRequest BuildRequest(string blockHash, int txIndex, string contractAddress, string startKeyHash,
            int limitStorageEntries, object id = null);
    }
}