
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;
using Nethereum.Hex.HexTypes;

namespace Nethereum.RPC.Extensions.DevTools.Evm
{

///<Summary>
/// Snapshot the state of the blockchain at the current block. Takes no parameters. Returns the id of the snapshot that was created. A snapshot can only be reverted once. After a successful evm_revert, the same snapshot id cannot be used again. Consider creating a new snapshot after each evm_revert if you need to revert to the same point multiple times.    
///</Summary>
    public class EvmSnapshot : GenericRpcRequestResponseHandlerNoParam<string>
    {
        public EvmSnapshot(IClient client) : base(client, ApiMethods.evm_snapshot.ToString()) { }
    }

}
