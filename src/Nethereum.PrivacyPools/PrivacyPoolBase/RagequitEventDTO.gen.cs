using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.PrivacyPools.PrivacyPoolBase;

namespace Nethereum.PrivacyPools.PrivacyPoolBase
{
    public partial class RagequitEventDTO : RagequitEventDTOBase { }

    [Event("Ragequit")]
    public class RagequitEventDTOBase : IEventDTO
    {
        [Parameter("address", "_ragequitter", 1, true )]
        public virtual string Ragequitter { get; set; }
        [Parameter("uint256", "_commitment", 2, false )]
        public virtual BigInteger Commitment { get; set; }
        [Parameter("uint256", "_label", 3, false )]
        public virtual BigInteger Label { get; set; }
        [Parameter("uint256", "_value", 4, false )]
        public virtual BigInteger Value { get; set; }
    }
}
