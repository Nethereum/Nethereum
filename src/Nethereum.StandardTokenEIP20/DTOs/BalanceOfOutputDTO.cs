using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;

namespace Nethereum.StandardTokenEIP20.DTOs
{
    [FunctionOutput]
    public class BalanceOfOutputDTO: IFunctionOutputDTO
    {
        [Parameter("uint256", "balance", 1)]
        public BigInteger Balance {get; set;}
    }
}
