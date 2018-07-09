using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;

namespace Nethereum.StandardTokenEIP20.DTOs
{
    [FunctionOutput]
    public class SymbolOutputDTO: IFunctionOutputDTO
    {
        [Parameter("string", "", 1)]
        public string B {get; set;}
    }
}
