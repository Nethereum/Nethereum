
using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;

namespace Nethereum.RPC.Extensions.DevTools.Evm
{

///<Summary>
/// Revert the state of the blockchain to a previous snapshot. Takes a single parameter, which is the snapshot id to revert to. This deletes the given snapshot, as well as any snapshots taken after (e.g.: reverting to id 0x1 will delete snapshots with ids 0x1, 0x2, etc.)    
///</Summary>
    public class EvmSetAccountBalance : RpcRequestResponseHandler<bool>
    {
        public EvmSetAccountBalance(IClient client) : base(client,ApiMethods.evm_setAccountBalance.ToString()) { }

        public Task<bool> SendRequestAsync(string address, HexBigInteger balance, object id = null)
        {
            return base.SendRequestAsync(id, address, balance);
        }
        public RpcRequest BuildRequest(string address, HexBigInteger balance, object id = null)
        {
            return base.BuildRequest(id, address, balance);
        }
    }

}

