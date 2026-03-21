using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.PrivacyPools.PrivacyPoolBase;

namespace Nethereum.PrivacyPools.PrivacyPoolBase
{
    public partial class CurrentTreeDepthOutputDTO : CurrentTreeDepthOutputDTOBase { }

    [FunctionOutput]
    public class CurrentTreeDepthOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "_depth", 1)]
        public virtual BigInteger Depth { get; set; }
    }
}
