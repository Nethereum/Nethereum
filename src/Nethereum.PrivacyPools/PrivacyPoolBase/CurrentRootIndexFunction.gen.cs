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
    public partial class CurrentRootIndexFunction : CurrentRootIndexFunctionBase { }

    [Function("currentRootIndex", "uint32")]
    public class CurrentRootIndexFunctionBase : FunctionMessage
    {

    }
}
