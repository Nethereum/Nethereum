using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;

namespace Nethereum.RPC.AccountSigning
{
    public interface IEthSignTypedDataV4 : ISignTypedDataV4
    {
        RpcRequest BuildRequest(string message, object id = null);
    }

    public interface ISignTypedDataV4
    {
        Task<string> SendRequestAsync(string jsonMessage, object id = null);
    }
}