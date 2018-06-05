using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.StandardTokenEIP20.DTOs
{
    [FunctionOutput]
    public class BalancesOutputDTO
    {
        [Parameter("uint256", "", 1)]
        public BigInteger B {get; set;}
    }
}
