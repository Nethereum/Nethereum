using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.PrivacyPools.PrivacyPoolBase;

namespace Nethereum.PrivacyPools.PrivacyPoolBase
{
    public partial class CurrentRootOutputDTO : CurrentRootOutputDTOBase { }

    [FunctionOutput]
    public class CurrentRootOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "_root", 1)]
        public virtual BigInteger Root { get; set; }
    }
}
