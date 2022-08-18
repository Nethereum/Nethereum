using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;

namespace Nethereum.RPC.AccountSigning
{
    public class EthPersonalSign : RpcRequestResponseHandler<string>, IEthPersonalSign
    {
        public EthPersonalSign() : this(null)
        {
        }

        public EthPersonalSign(IClient client) : base(client, ApiMethods.personal_sign.ToString())
        {

        }

        public Task<string> SendRequestAsync(byte[] value, object id = null)
        {
            return SendRequestAsync(id, value.ToHex());
        }

        public Task<string> SendRequestAsync(HexUTF8String utf8Hex, object id = null)
        {
            return SendRequestAsync(id, utf8Hex.HexValue);
        }

        public RpcRequest BuildRequest(HexUTF8String utf8Hex, object id = null)
        {
            return BuildRequest(id, utf8Hex.HexValue);
        }

        public RpcRequest BuildRequest(byte[] value, object id = null)
        {
            return BuildRequest(id, value.ToHex());
        }
    }
}
