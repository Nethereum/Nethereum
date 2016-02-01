using System;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;

namespace Nethereum.RPC.Generic
{
    [JsonConverter(typeof(BlockParameterJsonConverter))]
    public class BlockParameter
    {
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

        private BlockParameter(BlockParameterType type)
        {
            this.ParameterType = type;
        }

        public BlockParameter()
        {
            this.ParameterType = BlockParameterType.latest;
        }
        public enum BlockParameterType
        {
            latest,
            earliest,
            pending,
            blockNumber
        }

        public HexBigInteger BlockNumber { get; private set; }

        public BlockParameterType ParameterType { get; private set; }

       

        public void SetValue(BlockParameterType parameterType)
        {
            if (parameterType == BlockParameterType.blockNumber) throw new ArgumentException("Please provide the blockNumber when setting the type as blockNumber", "parameterType");
            this.ParameterType = parameterType;
            BlockNumber = null;
        }

        public void SetValue(string blockNumberHex) 
        {
            this.ParameterType = BlockParameterType.blockNumber;
            this.BlockNumber = new HexBigInteger(blockNumberHex);
        }

        public void SetValue(HexBigInteger blockNumber)
        {
            this.ParameterType = BlockParameterType.blockNumber;
            this.BlockNumber = blockNumber;
        }

        public void SetValue(BigInteger blockNumber)
        {
            this.ParameterType = BlockParameterType.blockNumber;
            this.BlockNumber = new HexBigInteger(blockNumber);
        }

        public string GetRPCParam()
        {
            if (ParameterType == BlockParameterType.blockNumber)
            {
                return BlockNumber.HexValue;
            }
            return ParameterType.ToString();
        }


    }
}
