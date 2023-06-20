
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.Util;
using System.Threading.Tasks;

namespace Nethereum.RPC.Extensions.DevTools.Hardhat
{

///<Summary>
///  Writes a single position of an account's storage    
///</Summary>
    public class HardhatSetStorageAt : RpcRequestResponseHandler<string>
    {
        public HardhatSetStorageAt(IClient client) : base(client,ApiMethods.hardhat_setStorageAt.ToString()) { }

        public Task SendRequestAsync(string address, HexBigInteger position, byte[] value, object id = null)
        {
            return base.SendRequestAsync(id, address, position, value.PadTo32Bytes());
        }
        public RpcRequest BuildRequest(string address, HexBigInteger position, byte[] value, object id = null)
        {
            return base.BuildRequest(id, address, position, value.PadTo32Bytes());
        }
    }

}

