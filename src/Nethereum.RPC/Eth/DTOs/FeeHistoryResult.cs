using System.Runtime.Serialization;
using Nethereum.Hex.HexTypes;

namespace Nethereum.RPC.Eth.DTOs
{
    /// <summary>
    /// Fee history for the returned block range. This can be a subsection of the requested range if not all blocks are available.
    /// </summary>
    [DataContract]
    public class FeeHistoryResult
    {
        /// <summary>
        /// Lowest number block of the returned range.   
        /// </summary>
        [DataMember(Name = "oldestBlock")]
        public HexBigInteger OldestBlock { get; set; }

        /// <summary>
        ///   An array of block base fees per gas. This includes the next block after the newest of the returned range, because this value can be derived from the newest block. Zeroes are returned for pre-EIP-1559 blocks
        /// </summary>
        [DataMember(Name = "baseFeePerGas")]
        public HexBigInteger[] BaseFeePerGas { get; set; }

        /// <summary>
        ///  An array of block gas used ratios. These are calculated as the ratio of gasUsed and gasLimit. Floating point value between 0 and 1.
        /// </summary>
        [DataMember(Name = "gasUsedRatio")]
        public decimal[] GasUsedRatio { get; set; }

        /// <summary>
        ///  A two-dimensional array of effective priority fees per gas at the requested block percentiles.
        ///  A given percentile sample of effective priority fees per gas from a single block in ascending order, weighted by gas used. Zeroes are returned if the block is empty.
        /// </summary>
        [DataMember(Name = "rewardArray")]
        public HexBigInteger[][] RewardArray { get; set; }

    }
}