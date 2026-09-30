using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Contracts.Standards.ENS.UniversalResolver.ContractDefinition
{
    public partial class ResolverInfo : ResolverInfoBase { }

    public class ResolverInfoBase 
    {
        [Parameter("bytes", "name", 1)]
        public virtual byte[] Name { get; set; }
        [Parameter("uint256", "offset", 2)]
        public virtual BigInteger Offset { get; set; }
        [Parameter("bytes32", "node", 3)]
        public virtual byte[] Node { get; set; }
        [Parameter("address", "resolver", 4)]
        public virtual string Resolver { get; set; }
        [Parameter("bool", "extended", 5)]
        public virtual bool Extended { get; set; }
    }
}
