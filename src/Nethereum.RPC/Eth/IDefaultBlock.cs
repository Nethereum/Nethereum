using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;

namespace Nethereum.RPC.Eth
{
    public interface IDefaultBlock
    {
        BlockParameter DefaultBlock { get; set; }

    }
}