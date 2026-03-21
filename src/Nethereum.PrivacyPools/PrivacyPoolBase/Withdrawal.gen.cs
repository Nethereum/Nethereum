using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.PrivacyPools.PrivacyPoolBase
{
    public partial class Withdrawal : WithdrawalBase { }

    public class WithdrawalBase 
    {
        [Parameter("address", "processooor", 1)]
        public virtual string Processooor { get; set; }
        [Parameter("bytes", "data", 2)]
        public virtual byte[] Data { get; set; }
    }
}
