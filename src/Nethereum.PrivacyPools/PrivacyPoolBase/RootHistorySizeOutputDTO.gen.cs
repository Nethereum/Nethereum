using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.PrivacyPools.PrivacyPoolBase;

namespace Nethereum.PrivacyPools.PrivacyPoolBase
{
    public partial class RootHistorySizeOutputDTO : RootHistorySizeOutputDTOBase { }

    [FunctionOutput]
    public class RootHistorySizeOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint32", "", 1)]
        public virtual uint ReturnValue1 { get; set; }
    }
}
