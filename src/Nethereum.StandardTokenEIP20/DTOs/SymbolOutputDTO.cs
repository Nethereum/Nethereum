using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.StandardTokenEIP20.DTOs
{
    [FunctionOutput]
    public class SymbolOutputDTO
    {
        [Parameter("string", "", 1)]
        public string B {get; set;}
    }
}
