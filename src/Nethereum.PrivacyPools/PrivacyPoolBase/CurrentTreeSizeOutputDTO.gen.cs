using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.PrivacyPools.PrivacyPoolBase;

namespace Nethereum.PrivacyPools.PrivacyPoolBase
{
    public partial class CurrentTreeSizeOutputDTO : CurrentTreeSizeOutputDTOBase { }

    [FunctionOutput]
    public class CurrentTreeSizeOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "_size", 1)]
        public virtual BigInteger Size { get; set; }
    }
}
