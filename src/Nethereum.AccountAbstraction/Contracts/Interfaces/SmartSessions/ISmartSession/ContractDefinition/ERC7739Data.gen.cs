using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Interfaces.SmartSessions.ISmartSession.ContractDefinition
{
    public partial class ERC7739Data : ERC7739DataBase { }

    public class ERC7739DataBase 
    {
        [Parameter("tuple[]", "allowedERC7739Content", 1)]
        public virtual List<ERC7739Context> AllowedERC7739Content { get; set; }
        [Parameter("tuple[]", "erc1271Policies", 2)]
        public virtual List<PolicyData> Erc1271Policies { get; set; }
    }
}
