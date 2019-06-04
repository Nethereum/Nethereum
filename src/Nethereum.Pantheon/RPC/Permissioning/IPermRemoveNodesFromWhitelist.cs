using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;

public interface IPermRemoveNodesFromWhitelist
{
    Task<string> SendRequestAsync(string[] addresses, object id = null);
    RpcRequest BuildRequest(string[] addresses, object id = null);
}