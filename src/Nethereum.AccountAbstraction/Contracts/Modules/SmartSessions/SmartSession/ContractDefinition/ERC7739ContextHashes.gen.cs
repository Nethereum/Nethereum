using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.SmartSession.ContractDefinition
{
    public partial class ERC7739ContextHashes : ERC7739ContextHashesBase { }

    public class ERC7739ContextHashesBase 
    {
        [Parameter("bytes32", "appDomainSeparator", 1)]
        public virtual byte[] AppDomainSeparator { get; set; }
        [Parameter("bytes32[]", "contentNameHashes", 2)]
        public virtual List<byte[]> ContentNameHashes { get; set; }
    }
}
