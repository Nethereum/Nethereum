using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.AppChain.Contracts.Interfaces.IAppChainAccountRegistry.ContractDefinition
{
    public partial class AccountInfo : AccountInfoBase { }

    public class AccountInfoBase 
    {
        [Parameter("uint8", "status", 1)]
        public virtual byte Status { get; set; }
        [Parameter("address", "invitedBy", 2)]
        public virtual string InvitedBy { get; set; }
        [Parameter("uint64", "invitedAt", 3)]
        public virtual ulong InvitedAt { get; set; }
        [Parameter("uint64", "activatedAt", 4)]
        public virtual ulong ActivatedAt { get; set; }
        [Parameter("uint64", "bannedAt", 5)]
        public virtual ulong BannedAt { get; set; }
        [Parameter("uint64", "suspendedUntil", 6)]
        public virtual ulong SuspendedUntil { get; set; }
        [Parameter("uint256", "dailyGasQuota", 7)]
        public virtual BigInteger DailyGasQuota { get; set; }
        [Parameter("uint32", "dailyOpQuota", 8)]
        public virtual uint DailyOpQuota { get; set; }
        [Parameter("uint256", "dailyValueQuota", 9)]
        public virtual BigInteger DailyValueQuota { get; set; }
        [Parameter("uint256", "gasUsedToday", 10)]
        public virtual BigInteger GasUsedToday { get; set; }
        [Parameter("uint32", "opsUsedToday", 11)]
        public virtual uint OpsUsedToday { get; set; }
        [Parameter("uint256", "valueUsedToday", 12)]
        public virtual BigInteger ValueUsedToday { get; set; }
        [Parameter("uint64", "lastResetDay", 13)]
        public virtual ulong LastResetDay { get; set; }
        [Parameter("uint256", "totalGasUsed", 14)]
        public virtual BigInteger TotalGasUsed { get; set; }
        [Parameter("uint256", "totalOps", 15)]
        public virtual BigInteger TotalOps { get; set; }
        [Parameter("uint256", "totalValue", 16)]
        public virtual BigInteger TotalValue { get; set; }
    }
}
