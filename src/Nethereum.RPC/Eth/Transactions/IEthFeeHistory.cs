using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RPC.Eth.Transactions
{
    public interface IEthFeeHistory
    {
        /// <summary>
        /// Returns base fee per gas and transaction effective priority fee per gas history for the requested block range if available. The range between headBlock-4 and headBlock is guaranteed to be available while retrieving data from the pending block and older history are optional to support. For pre-EIP-1559 blocks the gas prices are returned as rewards and zeroes are returned for the base fee per gas.
        /// </summary>
        /// <param name="blockCount">Number of blocks in the requested range. Between 1 and 1024 blocks can be requested in a single query. Less than requested may be returned if not all blocks are available.</param>
        /// <param name="highestBlockNumber">Highest number block of the requested range.</param>
        /// <param name="rewardPercentiles">A monotonically increasing list of percentile values to sample from each block's effective priority fees per gas in ascending order, weighted by gas used.
        /// Floating point value between 0 and 100.</param>
        /// <returns></returns>
        Task<FeeHistoryResult> SendRequestAsync(HexBigInteger blockCount, BlockParameter highestBlockNumber, double[] rewardPercentiles = null, object id = null);

        /// <summary>
        /// Builds the Request, to return base fee per gas and transaction effective priority fee per gas history for the requested block range if available. The range between headBlock-4 and headBlock is guaranteed to be available while retrieving data from the pending block and older history are optional to support. For pre-EIP-1559 blocks the gas prices are returned as rewards and zeroes are returned for the base fee per gas.
        /// </summary>
        /// <param name="blockCount">Number of blocks in the requested range. Between 1 and 1024 blocks can be requested in a single query. Less than requested may be returned if not all blocks are available.</param>
        /// <param name="highestBlockNumber">Highest number block of the requested range.</param>
        /// <param name="rewardPercentiles">A monotonically increasing list of percentile values to sample from each block's effective priority fees per gas in ascending order, weighted by gas used.
        /// Floating point value between 0 and 100.</param>
        /// <returns></returns>
        RpcRequest BuildRequest(HexBigInteger blockCount, BlockParameter highestBlockNumber, double[] rewardPercentiles = null, object id = null);
    }
}