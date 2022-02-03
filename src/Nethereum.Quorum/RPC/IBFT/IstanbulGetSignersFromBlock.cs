
using Nethereum.JsonRpc.Client;
using Nethereum.Quorum.RPC.DTOs;
using System.Threading.Tasks;

namespace Nethereum.Quorum.RPC.IBFT
{

///<Summary>
/// Retrieves the public addresses whose seals are included in the specified block number. This means that they participated in the consensus for this block and attested to its validity.
/// 
/// Parameters
/// blockNumber: number - (optional) block number to retrieve; defaults to current block
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
    public class IstanbulGetSignersFromBlock : RpcRequestResponseHandler<IstanbulSignersFromBlock>
    {
        public IstanbulGetSignersFromBlock(IClient client) : base(client,ApiMethods.istanbul_getSignersFromBlock.ToString()) { }

        public Task<IstanbulSignersFromBlock> SendRequestAsync(long number, object id = null)
        {
            return base.SendRequestAsync(id, number);
        }
        public RpcRequest BuildRequest(long number, object id = null)
        {
            return base.BuildRequest(id, number);
        }
    }

}

