using System;
using System.Threading.Tasks;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
namespace StandardToken.MyContractName.DTOs
{
    [FunctionOutput]
    public class BalanceOfOutputDTO
    {
        [Parameter("uint256", "balance", 1)]
        public BigInteger Balance {get; set;}
    }
}
