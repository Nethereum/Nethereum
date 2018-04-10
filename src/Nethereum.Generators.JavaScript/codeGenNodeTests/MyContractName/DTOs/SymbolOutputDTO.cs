using System;
using System.Threading.Tasks;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
namespace StandardToken.MyContractName.DTOs
{
    [FunctionOutput]
    public class SymbolOutputDTO
    {
        [Parameter("string", "", 1)]
        public string B {get; set;}
    }
}
