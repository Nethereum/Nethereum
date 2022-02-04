
using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;
using Nethereum.Quorum.RPC.DTOs;

namespace Nethereum.Quorum.RPC.ContractExtensions
{
    ///<Summary>
    /// Starts the process of extending an existing private contract to a new participant by deploying a new extension management contract to the blockchain.
    /// 
    /// Parameters
    /// toExtend: string - address of the private contract to extend
    /// 
    /// newRecipientPtmPublicKey: string - new participant’s Tessera public key
    /// 
    /// recipientAddress: string - new participant’s Ethereum address; the participant must later approve the extension using this address.
    /// 
    /// txArgs: object - arguments for the transaction that deploys the extension management contract; privateFor must contain only the newRecipientPtmPublicKey.
    /// 
    /// Returns
    /// result: data - hash of the creation transaction for the new extension management contract    
    ///</Summary>
    public interface IQuorumExtensionExtendContract
    {
        Task<string> SendRequestAsync(string toExtend, string recipientAddress, PrivateTransactionInput txArgs, object id = null);
        RpcRequest BuildRequest(string toExtend, string recipientAddress, PrivateTransactionInput txArgs, object id = null);
    }

    ///<Summary>
/// Starts the process of extending an existing private contract to a new participant by deploying a new extension management contract to the blockchain.
/// 
/// Parameters
/// toExtend: string - address of the private contract to extend
/// 
/// newRecipientPtmPublicKey: string - new participant’s Tessera public key
/// 
/// recipientAddress: string - new participant’s Ethereum address; the participant must later approve the extension using this address.
/// 
/// txArgs: object - arguments for the transaction that deploys the extension management contract; privateFor must contain only the newRecipientPtmPublicKey.
/// 
/// Returns
/// result: data - hash of the creation transaction for the new extension management contract    
///</Summary>
    public class QuorumExtensionExtendContract : RpcRequestResponseHandler<string>, IQuorumExtensionExtendContract
    {
        public QuorumExtensionExtendContract(IClient client) : base(client,ApiMethods.quorumExtension_extendContract.ToString()) { }

        public Task<string> SendRequestAsync(string toExtend, string recipientAddress, PrivateTransactionInput txArgs, object id = null)
        {
            return base.SendRequestAsync(id, toExtend, recipientAddress, txArgs);
        }
        public RpcRequest BuildRequest(string toExtend, string recipientAddress, PrivateTransactionInput txArgs, object id = null)
        {
            return base.BuildRequest(id, toExtend, recipientAddress, txArgs);
        }
    }

}

