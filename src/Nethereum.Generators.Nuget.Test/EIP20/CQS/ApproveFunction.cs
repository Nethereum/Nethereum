using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.Contracts.CQS;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Generators.Nuget.Test.EIP20.DTO;
namespace Nethereum.Generators.Nuget.Test.EIP20.CQS
{
    [Function("approve", "bool")]
    public class ApproveFunction:ContractMessage
    {
        [Parameter("bytes32", "cryptletProofKey", 1)]
        public byte[] CryptletProofKey {get; set;}
        [Parameter("address", "_spender", 2)]
        public string Spender {get; set;}
        [Parameter("uint256", "_value", 3)]
        public BigInteger Value {get; set;}
    }
}
