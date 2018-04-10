using System;
using System.Threading.Tasks;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
namespace StandardToken.MyContractName.DTOs
{
    [FunctionOutput]
    public class AllowedOutputDTO
    {
        [Parameter("uint256", "", 1)]
        public BigInteger B {get; set;}
    }
}
