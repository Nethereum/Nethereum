
using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;

namespace Nethereum.Quorum.RPC.IBFT
{
    ///<Summary>
    /// Returns the signing status of blocks for the specified block range.
    /// 
    /// Parameters
    /// startBlockNumber: number - start block number
    /// 
    /// endBlockNumber: number - end block number
    /// 
    /// If the start block and end block numbers are not provided, the status of the last 64 blocks is returned.
    /// 
    /// Returns
    /// result: object - result object with the following fields:
    /// 
    /// numBlocks: number - number of blocks for which sealer activity is retrieved
    /// 
    /// sealerActivity: map of strings to numbers - key is the validator and value is the number of blocks sealed by the validator    
    ///</Summary>
    public interface IIstanbulStatus
    {
        Task<IstanbulStatus> SendRequestAsync(long startBlockNumber, long endBlockNumber, object id = null);
        RpcRequest BuildRequest(long startBlockNumber, long endBlockNumber, object id = null);
    }

    ///<Summary>
/// Returns the signing status of blocks for the specified block range.
/// 
/// Parameters
/// startBlockNumber: number - start block number
/// 
/// endBlockNumber: number - end block number
/// 
/// If the start block and end block numbers are not provided, the status of the last 64 blocks is returned.
/// 
/// Returns
/// result: object - result object with the following fields:
/// 
/// numBlocks: number - number of blocks for which sealer activity is retrieved
/// 
/// sealerActivity: map of strings to numbers - key is the validator and value is the number of blocks sealed by the validator    
///</Summary>
    public class IstanbulStatus : RpcRequestResponseHandler<IstanbulStatus>, IIstanbulStatus
    {
        public IstanbulStatus(IClient client) : base(client,ApiMethods.istanbul_status.ToString()) { }

        public Task<IstanbulStatus> SendRequestAsync(long startBlockNumber, long endBlockNumber, object id = null)
        {
            return base.SendRequestAsync(id, startBlockNumber, endBlockNumber);
        }
        public RpcRequest BuildRequest(long startBlockNumber, long endBlockNumber, object id = null)
        {
            return base.BuildRequest(id, startBlockNumber, endBlockNumber);
        }
    }

}

