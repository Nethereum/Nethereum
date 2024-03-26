using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Contracts.Standards.ERC2535Diamond.DiamondCutFacet.ContractDefinition
{
    public partial class FacetCut : FacetCutBase { }

    public class FacetCutBase 
    {
        [Parameter("address", "facetAddress", 1)]
        public virtual string FacetAddress { get; set; }
        [Parameter("uint8", "action", 2)]
        public virtual byte Action { get; set; }
        [Parameter("bytes4[]", "functionSelectors", 3)]
        public virtual List<byte[]> FunctionSelectors { get; set; }
    }
}
