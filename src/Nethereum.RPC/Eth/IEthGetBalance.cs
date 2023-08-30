using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RPC.Eth
{
    public interface IEthGetBalance
    {
        BlockParameter DefaultBlock { get; set; }

        RpcRequest BuildRequest(string address, BlockParameter block, object id = null);
        RpcRequestResponseBatchItem<EthGetBalance, HexBigInteger> CreateBatchItem(string address, BlockParameter block, object id);
        RpcRequestResponseBatchItem<EthGetBalance, HexBigInteger> CreateBatchItem(string address, object id);
#if !DOTNET35
        Task<List<HexBigInteger>> SendBatchRequestAsync(string[] addresses, BlockParameter block);
        Task<List<HexBigInteger>> SendBatchRequestAsync(params string[] addresses);
#endif
        Task<HexBigInteger> SendRequestAsync(string address, object id = null);
        Task<HexBigInteger> SendRequestAsync(string address, BlockParameter block, object id = null);
    }
}