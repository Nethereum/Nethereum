using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Interfaces.SmartSessions.ISmartSession.ContractDefinition
{
    public partial class PolicyData : PolicyDataBase { }

    public class PolicyDataBase 
    {
        [Parameter("address", "policy", 1)]
        public virtual string Policy { get; set; }
        [Parameter("bytes", "initData", 2)]
        public virtual byte[] InitData { get; set; }
    }
}
