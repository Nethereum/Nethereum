using System;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;

#if NET6_0_OR_GREATER
using System.Text.Json.Serialization;
#endif

namespace Nethereum.RPC.Eth.DTOs
{
    /// <summary>
    /// "`earliest`: The lowest numbered block the client has available; `finalized`: The most recent crypto-economically secure block, cannot be re-orged outside of manual intervention driven by community coordination; `safe`: The most recent block that is safe from re-orgs under honest majority and certain synchronicity assumptions; `latest`: The most recent block in the canonical chain observed by the client, this block may be re-orged out of the canonical chain even under healthy/normal conditions; `pending`: A sample next block built by the client on top of `latest` and containing the set of transactions usually taken from local mempool.
    /// Before the merge transition is finalized, any call querying for `finalized` or `safe` block MUST be responded to with `-39001: Unknown block` error"
    /// </summary>
    [Newtonsoft.Json.JsonConverter(typeof (BlockParameterJsonConverter))]

#if NET6_0_OR_GREATER
    [System.Text.Json.Serialization.JsonConverter(typeof(BlockParameterSystemTextJsonConverter))] // STJ
#endif
    public class BlockParameter
    {
        public enum BlockParameterType
        {
            latest,
            earliest,
            pending,
            finalized,
            safe,
            blockNumber
        }

        private BlockParameter(BlockParameterType type)
        {
            ParameterType = type;
        }

        public BlockParameter()
        {
            ParameterType = BlockParameterType.latest;
        }

        public BlockParameter(HexBigInteger blockNumber)
        {
            SetValue(blockNumber);
        }

        public BlockParameter(ulong blockNumber) : this(new HexBigInteger(blockNumber))
        {
        }

        public HexBigInteger BlockNumber { get; private set; }

        public BlockParameterType ParameterType { get; private set; }

        public static BlockParameter CreateLatest()
        {
            return new BlockParameter(BlockParameterType.latest);
        }

        public static BlockParameter CreateEarliest()
        {
            return new BlockParameter(BlockParameterType.earliest);
        }

        public static BlockParameter CreatePending()
        {
            return new BlockParameter(BlockParameterType.pending);
        }


        public void SetValue(BlockParameterType parameterType)
        {
            if (parameterType == BlockParameterType.blockNumber)
                throw new ArgumentException("Please provide the blockNumber when setting the type as blockNumber",
                    "parameterType");
            ParameterType = parameterType;
            BlockNumber = null;
        }

        public void SetValue(string blockNumberHex)
        {
            ParameterType = BlockParameterType.blockNumber;
            BlockNumber = new HexBigInteger(blockNumberHex);
        }

        public void SetValue(HexBigInteger blockNumber)
        {
            ParameterType = BlockParameterType.blockNumber;
            BlockNumber = blockNumber;
        }

        public void SetValue(BigInteger blockNumber)
        {
            ParameterType = BlockParameterType.blockNumber;
            BlockNumber = new HexBigInteger(blockNumber);
        }

        public string GetRPCParam()
        {
            if (ParameterType == BlockParameterType.blockNumber)
            {
                return BlockNumber.HexValue;
            }
            return ParameterType.ToString();
        }

        public object GetRPCParamAsNumber()
        {
            if (ParameterType == BlockParameterType.blockNumber)
            {
                return BlockNumber.Value;
            }
            return ParameterType.ToString();
        }
    }
}