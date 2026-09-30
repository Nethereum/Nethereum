using System.Threading.Tasks;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Infrastructure;

namespace Nethereum.RPC.Eth
{
    public interface IEthConfig : IGenericRpcRequestResponseHandlerNoParam<ChainConfiguration>
    {
    }
}
