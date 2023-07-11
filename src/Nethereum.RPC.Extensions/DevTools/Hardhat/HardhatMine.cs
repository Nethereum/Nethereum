
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.RPC.Extensions.DevTools.Hardhat
{

///<Summary>
/// Mines a specified number of blocks at a given interval
/// 
/// blocks Number of blocks to mine
/// Configures the interval (in seconds) between the timestamps of each mined block. Defaults to 1    
///</Summary>
    public class HardhatMine : RpcRequestResponseHandler<string>
    {
        public HardhatMine(IClient client, ApiMethods apiMethod) : base(client, apiMethod.ToString()) { }
        public HardhatMine(IClient client) : base(client,ApiMethods.hardhat_mine.ToString()) { }

        public Task SendRequestAsync(HexBigInteger blocks, HexBigInteger interval = null, object id = null)
        {
            if(interval == null) interval = new HexBigInteger(1);
            return base.SendRequestAsync(id, blocks, interval);
        }

        public Task SendRequestAsync(int blocks = 1, HexBigInteger interval = null, object id = null)
        {
            if (interval == null) interval = new HexBigInteger(1);
            return base.SendRequestAsync(id, new HexBigInteger(blocks), interval);
        }

        public RpcRequest BuildRequest(HexBigInteger blocks, HexBigInteger interval = null, object id = null)
        {
            if (interval == null) interval = new HexBigInteger(1);
            return base.BuildRequest(id, blocks, interval);
        }

        public RpcRequest BuildRequest(int blocks = 1, HexBigInteger interval = null, object id = null)
        {
            if (interval == null) interval = new HexBigInteger(1);
            return base.BuildRequest(id, new HexBigInteger(blocks), interval);
        }
    }

}

