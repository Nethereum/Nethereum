using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.StandardTokenEIP20.DTOs
{
    [FunctionOutput]
    public class AllowanceOutputDTO
    {
        [Parameter("uint256", "remaining", 1)]
        public BigInteger Remaining {get; set;}
    }
}
