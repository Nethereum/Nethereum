using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
namespace Nethereum.Generators.Nuget.Test.EIP20v2.DTO
{
    [Event("Approval")]
    public class ApprovalEventDTO
    {
        [Parameter("bytes32", "cryptletProofKey", 1, true )]
        public byte[] CryptletProofKey {get; set;}
        [Parameter("address", "_owner", 2, true )]
        public string Owner {get; set;}
        [Parameter("address", "_spender", 3, true )]
        public string Spender {get; set;}
        [Parameter("uint256", "_value", 4, false )]
        public BigInteger Value {get; set;}
    }
}
