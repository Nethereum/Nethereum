using System;

namespace Ethereum.RPC.Generic
{
    public class BlockParameter
    {
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

        public Int64? BlockNumber { get; private set; }

        public BlockParameterType ParameterType { get; private set; }

        public string GetRPCParam()
        {
            if(ParameterType == BlockParameterType.blockNumber)
            {
                return BlockNumber.ConvertInt64ToHex();
            }
            return ParameterType.ToString();
        }

        public void SetValue(BlockParameterType parameterType)
        {
            if (parameterType == BlockParameterType.blockNumber) throw new ArgumentException("Please provide the blockNumber when setting the type as blockNumber", "parameterType");
            this.ParameterType = parameterType;
            BlockNumber = null;
        }

        public void SetValue(Int64 blockNumber) 
        {
            this.ParameterType = BlockParameterType.blockNumber;
            this.BlockNumber = blockNumber;
        }


    }
}
