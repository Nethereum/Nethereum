using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.Contracts.CQS;
using Nethereum.ABI.FunctionEncoding.Attributes;
using SolidityCallAnotherContract.Contracts.Test.DTOs;
namespace SolidityCallAnotherContract.Contracts.Test.CQS
{
    [Function("callManyContractsSameQuery", "bytes[]")]
    public class CallManyContractsSameQueryFunction:ContractMessage
    {
        [Parameter("address[]", "destination", 1)]
        public List<string> Destination {get; set;}
        [Parameter("bytes", "data", 2)]
        public byte[] Data {get; set;}
    }
}
