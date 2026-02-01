using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Modules.SessionKeyModule.ContractDefinition
{
    public partial class PermissionFlags : PermissionFlagsBase { }

    public class PermissionFlagsBase 
    {
        [Parameter("bool", "canTransferNative", 1)]
        public virtual bool CanTransferNative { get; set; }
        [Parameter("bool", "canTransferERC20", 2)]
        public virtual bool CanTransferERC20 { get; set; }
        [Parameter("bool", "canTransferNFT", 3)]
        public virtual bool CanTransferNFT { get; set; }
        [Parameter("bool", "canApprove", 4)]
        public virtual bool CanApprove { get; set; }
        [Parameter("bool", "canCallAny", 5)]
        public virtual bool CanCallAny { get; set; }
    }
}
