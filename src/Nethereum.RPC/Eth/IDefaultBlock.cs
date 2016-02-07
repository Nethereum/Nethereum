using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RPC.Eth
{
    public interface IDefaultBlock
    {
        BlockParameter DefaultBlock { get; set; }
    }
}