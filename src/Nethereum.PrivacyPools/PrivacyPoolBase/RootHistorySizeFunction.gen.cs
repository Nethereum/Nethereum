using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.Contracts;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.PrivacyPools.PrivacyPoolBase;

namespace Nethereum.PrivacyPools.PrivacyPoolBase
{
    public partial class RootHistorySizeFunction : RootHistorySizeFunctionBase { }

    [Function("ROOT_HISTORY_SIZE", "uint32")]
    public class RootHistorySizeFunctionBase : FunctionMessage
    {

    }
}
