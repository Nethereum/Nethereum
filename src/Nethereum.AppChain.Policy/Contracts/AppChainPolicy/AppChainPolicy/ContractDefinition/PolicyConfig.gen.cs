using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.AppChain.Policy.Contracts.AppChainPolicy.AppChainPolicy.ContractDefinition
{
    public partial class PolicyConfig : PolicyConfigBase { }

    public class PolicyConfigBase 
    {
        [Parameter("uint256", "version", 1)]
        public virtual BigInteger Version { get; set; }
        [Parameter("uint256", "maxCalldataBytes", 2)]
        public virtual BigInteger MaxCalldataBytes { get; set; }
        [Parameter("uint256", "maxLogBytes", 3)]
        public virtual BigInteger MaxLogBytes { get; set; }
        [Parameter("uint256", "blockGasLimit", 4)]
        public virtual BigInteger BlockGasLimit { get; set; }
        [Parameter("address", "sequencer", 5)]
        public virtual string Sequencer { get; set; }
    }
}
