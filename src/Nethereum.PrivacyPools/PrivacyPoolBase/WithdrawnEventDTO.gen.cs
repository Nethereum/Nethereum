using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.PrivacyPools.PrivacyPoolBase;

namespace Nethereum.PrivacyPools.PrivacyPoolBase
{
    public partial class WithdrawnEventDTO : WithdrawnEventDTOBase { }

    [Event("Withdrawn")]
    public class WithdrawnEventDTOBase : IEventDTO
    {
        [Parameter("address", "_processooor", 1, true )]
        public virtual string Processooor { get; set; }
        [Parameter("uint256", "_value", 2, false )]
        public virtual BigInteger Value { get; set; }
        [Parameter("uint256", "_spentNullifier", 3, false )]
        public virtual BigInteger SpentNullifier { get; set; }
        [Parameter("uint256", "_newCommitment", 4, false )]
        public virtual BigInteger NewCommitment { get; set; }
    }
}
