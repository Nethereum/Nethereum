using System;
using System.Numerics;
using Ethereum.RPC.Util;

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

        public string BlockNumber { get; private set; }

        public BlockParameterType ParameterType { get; private set; }

        public string GetRPCParam()
        {
            if(ParameterType == BlockParameterType.blockNumber)
            {
                return BlockNumber;
            }
            return ParameterType.ToString();
        }

        public void SetValue(BlockParameterType parameterType)
        {
            if (parameterType == BlockParameterType.blockNumber) throw new ArgumentException("Please provide the blockNumber when setting the type as blockNumber", "parameterType");
            this.ParameterType = parameterType;
            BlockNumber = null;
        }

        public void SetValue(string blockNumber) 
        {
            this.ParameterType = BlockParameterType.blockNumber;
            this.BlockNumber = blockNumber;
        }


    }
}
