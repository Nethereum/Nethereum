using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.PrivacyPools.PrivacyPoolBase;

namespace Nethereum.PrivacyPools.PrivacyPoolBase
{
    public partial class InvalidCommitmentError : InvalidCommitmentErrorBase { }
    [Error("InvalidCommitment")]
    public class InvalidCommitmentErrorBase : IErrorDTO
    {
    }
}
