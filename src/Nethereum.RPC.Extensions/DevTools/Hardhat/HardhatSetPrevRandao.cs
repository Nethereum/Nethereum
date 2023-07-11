
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.Util;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.RPC.Extensions.DevTools.Hardhat
{

///<Summary>
/// Sets the PREVRANDAO value of the next block.    
///</Summary>
    public class HardhatSetPrevRandao : RpcRequestResponseHandler<string>
    {
        public HardhatSetPrevRandao(IClient client, ApiMethods apiMethod) : base(client, apiMethod.ToString()) { }
        public HardhatSetPrevRandao(IClient client) : base(client,ApiMethods.hardhat_setPrevRandao.ToString()) { }

        public Task SendRequestAsync(BigInteger prevRandao, object id = null)
        {
            var ranDaoBytes = prevRandao.ConvertToByteArray(false).PadTo32Bytes();
            return base.SendRequestAsync(id, ranDaoBytes.ToHex());
        }
        public RpcRequest BuildRequest(BigInteger prevRandao, object id = null)
        {
            var ranDaoBytes = prevRandao.ConvertToByteArray(false).PadTo32Bytes();
            return base.BuildRequest(id, ranDaoBytes.ToHex());
        }
    }

}

