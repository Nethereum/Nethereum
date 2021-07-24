using System;
using System.Threading.Tasks;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
namespace SolidityCallAnotherContract.Contracts.Test.DTOs
{
    [FunctionOutput]
    public class CallAnotherContractOutputDTO
    {
        [Parameter("bytes", "result", 1)]
        public byte[] Result {get; set;}
    }
}
