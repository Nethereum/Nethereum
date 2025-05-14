using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.EntryPoint.ContractDefinition
{
    public partial class UserOpsPerAggregator : UserOpsPerAggregatorBase { }

    public class UserOpsPerAggregatorBase 
    {
        [Parameter("tuple[]", "userOps", 1)]
        public virtual List<PackedUserOperation> UserOps { get; set; }
        [Parameter("address", "aggregator", 2)]
        public virtual string Aggregator { get; set; }
        [Parameter("bytes", "signature", 3)]
        public virtual byte[] Signature { get; set; }
    }
}
