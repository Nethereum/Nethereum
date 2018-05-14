using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.Contracts.CQS;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Generators.Nuget.Test.EIP20v2.DTO;
namespace Nethereum.Generators.Nuget.Test.EIP20v2.CQS
{
    [Function("transferFrom", "bool")]
    public class TransferFromFunction:ContractMessage
    {
        [Parameter("bytes32", "cryptletProofKey", 1)]
        public byte[] CryptletProofKey {get; set;}
        [Parameter("address", "_from", 2)]
        public string From {get; set;}
        [Parameter("address", "_to", 3)]
        public string To {get; set;}
        [Parameter("uint256", "_value", 4)]
        public BigInteger Value {get; set;}
    }
}
