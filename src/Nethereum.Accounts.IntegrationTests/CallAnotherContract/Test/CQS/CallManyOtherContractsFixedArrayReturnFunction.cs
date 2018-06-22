using System;
using System.Threading.Tasks;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.Contracts.CQS;
using Nethereum.ABI.FunctionEncoding.Attributes;
using SolidityCallAnotherContract.Contracts.Test.DTOs;
namespace SolidityCallAnotherContract.Contracts.Test.CQS
{
    [Function("callManyOtherContractsFixedArrayReturn", "bytes[10]")]
    public class CallManyOtherContractsFixedArrayReturnFunction:ContractMessage
    {
        [Parameter("address", "theOther", 1)]
        public string TheOther {get; set;}
    }
}
