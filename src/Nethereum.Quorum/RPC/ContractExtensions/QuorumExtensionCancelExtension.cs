
using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;
using Nethereum.Quorum.RPC.DTOs;

namespace Nethereum.Quorum.RPC.ContractExtensions
{
    ///<Summary>
    /// Cancels the specified active contract extension. This can only be invoked by the initiator of the extension process (the caller of quorumExtension_extendContract).
    /// 
    /// Parameters
    /// extensionContract: string - address of the contract extension’s management contract
    /// 
    /// txArgs: object - arguments for the cancellation transaction
    /// 
    /// Returns
    /// result: data - hash of the cancellation transaction    
    ///</Summary>
    public interface IQuorumExtensionCancelExtension
    {
        Task<string> SendRequestAsync(string extensionContract, PrivateTransactionInput txnArgs, object id = null);
        RpcRequest BuildRequest(string extensionContract, PrivateTransactionInput txnArgs, object id = null);
    }

    ///<Summary>
/// Cancels the specified active contract extension. This can only be invoked by the initiator of the extension process (the caller of quorumExtension_extendContract).
/// 
/// Parameters
/// extensionContract: string - address of the contract extension’s management contract
/// 
/// txArgs: object - arguments for the cancellation transaction
/// 
/// Returns
/// result: data - hash of the cancellation transaction    
///</Summary>
    public class QuorumExtensionCancelExtension : RpcRequestResponseHandler<string>, IQuorumExtensionCancelExtension
    {
        public QuorumExtensionCancelExtension(IClient client) : base(client,ApiMethods.quorumExtension_cancelExtension.ToString()) { }

        public Task<string> SendRequestAsync(string extensionContract, PrivateTransactionInput txnArgs, object id = null)
        {
            return base.SendRequestAsync(id, extensionContract, txnArgs);
        }
        public RpcRequest BuildRequest(string extensionContract, PrivateTransactionInput txnArgs, object id = null)
        {
            return base.BuildRequest(id, extensionContract, txnArgs);
        }
    }

}

