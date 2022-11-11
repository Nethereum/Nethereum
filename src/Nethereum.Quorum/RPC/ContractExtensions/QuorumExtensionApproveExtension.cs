
using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;
using Nethereum.Quorum.RPC.DTOs;

namespace Nethereum.Quorum.RPC.ContractExtensions
{
    ///<Summary>
    /// Submits an approval/denial vote to the specified extension management contract.
    /// 
    /// Parameters
    /// addressToVoteOn: string - address of the contract extension’s management contract (this can be found using quorumExtension_activeExtensionContracts)
    /// 
    /// vote: boolean - true approves the extension process, false cancels the extension process
    /// 
    /// txArgs: object - arguments for the vote submission transaction; privateFor must contain the public key of the node that initiated the contract extension.
    /// 
    /// Returns
    /// result: data - hash of the vote submission transaction    
    ///</Summary>
    public interface IQuorumExtensionApproveExtension
    {
        Task<string> SendRequestAsync(string addressToVoteOn, bool vote, PrivateTransactionInput privateTrasnsactionINput, object id = null);
        RpcRequest BuildRequest(string addressToVoteOn, bool vote, PrivateTransactionInput privateTrasnsactionINput, object id = null);
    }

    ///<Summary>
/// Submits an approval/denial vote to the specified extension management contract.
/// 
/// Parameters
/// addressToVoteOn: string - address of the contract extension’s management contract (this can be found using quorumExtension_activeExtensionContracts)
/// 
/// vote: boolean - true approves the extension process, false cancels the extension process
/// 
/// txArgs: object - arguments for the vote submission transaction; privateFor must contain the public key of the node that initiated the contract extension.
/// 
/// Returns
/// result: data - hash of the vote submission transaction    
///</Summary>
    public class QuorumExtensionApproveExtension : RpcRequestResponseHandler<string>, IQuorumExtensionApproveExtension
    {
        public QuorumExtensionApproveExtension(IClient client) : base(client,ApiMethods.quorumExtension_approveExtension.ToString()) { }

        public Task<string> SendRequestAsync(string addressToVoteOn, bool vote, PrivateTransactionInput privateTrasnsactionINput, object id = null)
        {
            return base.SendRequestAsync(id, addressToVoteOn, vote, privateTrasnsactionINput);
        }
        public RpcRequest BuildRequest(string addressToVoteOn, bool vote, PrivateTransactionInput privateTrasnsactionINput, object id = null)
        {
            return base.BuildRequest(id, addressToVoteOn, vote, privateTrasnsactionINput);
        }
    }

}

