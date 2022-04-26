using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Optimism.L1CrossDomainMessenger.ContractDefinition
{
    public partial class ChainBatchHeader : ChainBatchHeaderBase { }

    public class ChainBatchHeaderBase
    {
        [Parameter("uint256", "batchIndex", 1)]
        public virtual BigInteger BatchIndex { get; set; }
        [Parameter("bytes32", "batchRoot", 2)]
        public virtual byte[] BatchRoot { get; set; }
        [Parameter("uint256", "batchSize", 3)]
        public virtual BigInteger BatchSize { get; set; }
        [Parameter("uint256", "prevTotalElements", 4)]
        public virtual BigInteger PrevTotalElements { get; set; }
        [Parameter("bytes", "extraData", 5)]
        public virtual byte[] ExtraData { get; set; }
    }
}
