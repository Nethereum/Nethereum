using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;

namespace Nethereum.StandardTokenEIP20.DTOs
{
    [FunctionOutput]
    public class DecimalsOutputDTO: IFunctionOutputDTO
    {
        [Parameter("uint8", "", 1)]
        public byte B {get; set;}
    }
}
