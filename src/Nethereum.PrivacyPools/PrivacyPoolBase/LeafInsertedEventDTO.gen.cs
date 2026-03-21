using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.PrivacyPools.PrivacyPoolBase;

namespace Nethereum.PrivacyPools.PrivacyPoolBase
{
    public partial class LeafInsertedEventDTO : LeafInsertedEventDTOBase { }

    [Event("LeafInserted")]
    public class LeafInsertedEventDTOBase : IEventDTO
    {
        [Parameter("uint256", "_index", 1, false )]
        public virtual BigInteger Index { get; set; }
        [Parameter("uint256", "_leaf", 2, false )]
        public virtual BigInteger Leaf { get; set; }
        [Parameter("uint256", "_root", 3, false )]
        public virtual BigInteger Root { get; set; }
    }
}
