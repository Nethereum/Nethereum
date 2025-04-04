using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.AccountAbstraction.EntryPoint.ContractDefinition
{
    public partial class DepositInfo : DepositInfoBase { }

    public class DepositInfoBase 
    {
        [Parameter("uint256", "deposit", 1)]
        public virtual BigInteger Deposit { get; set; }
        [Parameter("bool", "staked", 2)]
        public virtual bool Staked { get; set; }
        [Parameter("uint112", "stake", 3)]
        public virtual BigInteger Stake { get; set; }
        [Parameter("uint32", "unstakeDelaySec", 4)]
        public virtual uint UnstakeDelaySec { get; set; }
        [Parameter("uint48", "withdrawTime", 5)]
        public virtual ulong WithdrawTime { get; set; }
    }
}
