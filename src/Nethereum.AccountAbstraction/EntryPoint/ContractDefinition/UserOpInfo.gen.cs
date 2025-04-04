using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.AccountAbstraction.EntryPoint.ContractDefinition
{
    public partial class UserOpInfo : UserOpInfoBase { }

    public class UserOpInfoBase 
    {
        [Parameter("tuple", "mUserOp", 1)]
        public virtual MemoryUserOp MUserOp { get; set; }
        [Parameter("bytes32", "userOpHash", 2)]
        public virtual byte[] UserOpHash { get; set; }
        [Parameter("uint256", "prefund", 3)]
        public virtual BigInteger Prefund { get; set; }
        [Parameter("uint256", "contextOffset", 4)]
        public virtual BigInteger ContextOffset { get; set; }
        [Parameter("uint256", "preOpGas", 5)]
        public virtual BigInteger PreOpGas { get; set; }
    }
}
