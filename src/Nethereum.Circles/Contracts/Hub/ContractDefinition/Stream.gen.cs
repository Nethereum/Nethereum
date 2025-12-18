using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Circles.Contracts.Hub.ContractDefinition
{
    public partial class Stream : StreamBase { }

    public class StreamBase 
    {
        [Parameter("uint16", "sourceCoordinate", 1)]
        public virtual ushort SourceCoordinate { get; set; }
        [Parameter("uint16[]", "flowEdgeIds", 2)]
        public virtual List<ushort> FlowEdgeIds { get; set; }
        [Parameter("bytes", "data", 3)]
        public virtual byte[] Data { get; set; }
    }
}
