using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.DebugNode.Dtos;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.RPC.DebugNode
{
    public interface IDebugStorageRangeAt
    {
        RpcRequest BuildRequest(string blockHash, int transactionIndex, string address, string startKeyHex, int limit, object id = null);
        Task<DebugStorageAtResult> SendRequestAsync(string blockHash, int transactionIndex, string address, string startKeyHex, int limit, object id = null);
        Task<DebugStorageAtResult> SendRequestAsync(string blockHash, int transactionIndex, string address, BigInteger startKey, int limit, object id = null);
    }
}