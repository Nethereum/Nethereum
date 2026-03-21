using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.PrivacyPools.PrivacyPoolBase;

namespace Nethereum.PrivacyPools.PrivacyPoolBase
{
    public partial class DepositedEventDTO : DepositedEventDTOBase { }

    [Event("Deposited")]
    public class DepositedEventDTOBase : IEventDTO
    {
        [Parameter("address", "_depositor", 1, true )]
        public virtual string Depositor { get; set; }
        [Parameter("uint256", "_commitment", 2, false )]
        public virtual BigInteger Commitment { get; set; }
        [Parameter("uint256", "_label", 3, false )]
        public virtual BigInteger Label { get; set; }
        [Parameter("uint256", "_value", 4, false )]
        public virtual BigInteger Value { get; set; }
        [Parameter("uint256", "_precommitmentHash", 5, false )]
        public virtual BigInteger PrecommitmentHash { get; set; }
    }
}
