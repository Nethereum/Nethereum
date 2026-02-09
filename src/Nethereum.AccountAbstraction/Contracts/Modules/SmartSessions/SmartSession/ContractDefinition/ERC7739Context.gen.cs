using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.SmartSession.ContractDefinition
{
    public partial class ERC7739Context : ERC7739ContextBase { }

    public class ERC7739ContextBase 
    {
        [Parameter("bytes32", "appDomainSeparator", 1)]
        public virtual byte[] AppDomainSeparator { get; set; }
        [Parameter("string[]", "contentNames", 2)]
        public virtual List<string> ContentNames { get; set; }
    }
}
