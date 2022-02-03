
using Nethereum.JsonRpc.Client;
using Nethereum.Quorum.RPC.DTOs;
using System.Threading.Tasks;

namespace Nethereum.Quorum.RPC.IBFT
{

///<Summary>
/// Retrieves the public addresses whose seals are included in the specified block number. This means that they participated in the consensus for this block and attested to its validity.
/// 
/// Parameters
/// blockHash: string - hash of the block to retrieve (required)
/// 
/// Returns
/// result: object - result object with the following fields:
/// 
/// number: number - retrieved block’s number
/// 
/// hash: string - retrieved block’s hash
/// 
/// author: string - address of the block proposer
/// 
/// committers: array of strings - list of all addresses whose seal appears in this block    
///</Summary>
    public class IstanbulGetSignersFromBlockByHash : RpcRequestResponseHandler<IstanbulSignersFromBlock>
    {
        public IstanbulGetSignersFromBlockByHash(IClient client) : base(client,ApiMethods.istanbul_getSignersFromBlockByHash.ToString()) { }

        public Task<IstanbulSignersFromBlock> SendRequestAsync(string blockHash, object id = null)
        {
            return base.SendRequestAsync(id, blockHash);
        }
        public RpcRequest BuildRequest(string blockHash, object id = null)
        {
            return base.BuildRequest(id, blockHash);
        }
    }

}

