using System;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;

namespace Nethereum.RPC.Eth.DTOs
{
    [JsonConverter(typeof (BlockParameterJsonConverter))]
    public class BlockParameter
    {
        public enum BlockParameterType
        {
            latest,
            earliest,
            pending,
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