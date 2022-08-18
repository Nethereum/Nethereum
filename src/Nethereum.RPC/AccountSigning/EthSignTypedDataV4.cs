using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;

namespace Nethereum.RPC.AccountSigning
{
    public class EthSignTypedDataV4 : RpcRequestResponseHandler<string>, IEthSignTypedDataV4
    {
        public EthSignTypedDataV4() : this(null)
        {
        }

        public EthSignTypedDataV4(IClient client) : base(client, ApiMethods.eth_signTypedData_v4.ToString())
        {

        }

        public Task<string> SendRequestAsync(string jsonMessage, object id = null)
        {
            return SendRequestAsync(id, jsonMessage);
        }

        public RpcRequest BuildRequest(string message, object id = null)
        {
            return BuildRequest(id, message);
        }
    }
}
