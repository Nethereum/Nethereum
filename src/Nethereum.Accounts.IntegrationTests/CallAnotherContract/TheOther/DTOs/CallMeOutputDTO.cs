using System;
using System.Threading.Tasks;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
namespace SolidityCallAnotherContract.Contracts.TheOther.DTOs
{
    [FunctionOutput]
    public class CallMeOutputDTO
    {
        [Parameter("bytes", "test", 1)]
        public byte[] Test {get; set;}
    }
}
