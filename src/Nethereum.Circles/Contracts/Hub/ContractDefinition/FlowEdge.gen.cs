using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Circles.Contracts.Hub.ContractDefinition
{
    public partial class FlowEdge : FlowEdgeBase { }

    public class FlowEdgeBase 
    {
        [Parameter("uint16", "streamSinkId", 1)]
        public virtual ushort StreamSinkId { get; set; }
        [Parameter("uint192", "amount", 2)]
        public virtual BigInteger Amount { get; set; }
    }
}
