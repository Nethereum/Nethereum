using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.PrivacyPools.PrivacyPoolBase;

namespace Nethereum.PrivacyPools.PrivacyPoolBase
{
    public partial class NullifierHashesOutputDTO : NullifierHashesOutputDTOBase { }

    [FunctionOutput]
    public class NullifierHashesOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "_spent", 1)]
        public virtual bool Spent { get; set; }
    }
}
