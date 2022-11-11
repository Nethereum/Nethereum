
using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Parity.RPC.Admin
{
    ///<Summary>
    /// Returns all storage keys of the given address (first parameter) if Fat DB is enabled (--fat-db), null otherwise.
    /// 
    /// Parameters
    /// Address - 20 Bytes - Account for which to retrieve the storage keys.
    /// Quantity - Integer number of addresses to display in a batch.
    /// Hash - 32 Bytes - Offset storage key from which the batch should start in order, or null.
    /// Quantity or Tag - (optional) integer block number, or the string 'latest', 'earliest' or 'pending'.
    /// params: [
    ///   "0x407d73d8a49eeb85d32cf465507dd71d507100c1",
    ///   5,
    ///   null
    /// ]
    /// Returns
    /// Array - Req
    public interface IParityListStorageKeys
    {
        Task<string[]> SendRequestAsync(string address, int quantity, string hash, object id = null);
        Task<string[]> SendRequestAsync(string address, int quantity, string hash, BlockParameter blockNumber, object id = null);
        RpcRequest BuildRequest(string address, int quantity, string hash, object id = null);
        RpcRequest BuildRequest(string address, int quantity, string hash, BlockParameter blockNumber, object id = null);
    }

    ///<Summary>
/// Returns all storage keys of the given address (first parameter) if Fat DB is enabled (--fat-db), null otherwise.
/// 
/// Parameters
/// Address - 20 Bytes - Account for which to retrieve the storage keys.
/// Quantity - Integer number of addresses to display in a batch.
/// Hash - 32 Bytes - Offset storage key from which the batch should start in order, or null.
/// Quantity or Tag - (optional) integer block number, or the string 'latest', 'earliest' or 'pending'.
/// params: [
///   "0x407d73d8a49eeb85d32cf465507dd71d507100c1",
///   5,
///   null
/// ]
/// Returns
/// Array - Requested number of 32 byte long storage keys for the given account or null if Fat DB is not enabled.    
///</Summary>
    public class ParityListStorageKeys : RpcRequestResponseHandler<string[]>, IParityListStorageKeys
    {
        public ParityListStorageKeys(IClient client) : base(client,ApiMethods.parity_listStorageKeys.ToString()) { }

        public Task<string[]> SendRequestAsync(string address, int quantity, string hash, object id = null)
        {
            return base.SendRequestAsync(id, address, quantity, hash);
        }
        public RpcRequest BuildRequest(string address, int quantity, string hash, object id = null)
        {
            return base.BuildRequest(id, address, quantity, hash);
        }

        public Task<string[]> SendRequestAsync(string address, int quantity, string hash, BlockParameter blockNumber, object id = null)
        {
            return base.SendRequestAsync(id, address, quantity, hash, blockNumber.GetRPCParamAsNumber());
        }
        public RpcRequest BuildRequest(string address, int quantity, string hash, BlockParameter blockNumber, object id = null)
        {
            return base.BuildRequest(id, address, quantity, hash, blockNumber.GetRPCParamAsNumber());
        }
    }

}

