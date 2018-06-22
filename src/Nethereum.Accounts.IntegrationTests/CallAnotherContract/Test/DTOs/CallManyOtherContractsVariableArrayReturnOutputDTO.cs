using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
namespace SolidityCallAnotherContract.Contracts.Test.DTOs
{
    [FunctionOutput]
    public class CallManyOtherContractsVariableArrayReturnOutputDTO
    {
        [Parameter("bytes[]", "result", 1)]
        public List<byte[]> Result {get; set;}
    }
}
