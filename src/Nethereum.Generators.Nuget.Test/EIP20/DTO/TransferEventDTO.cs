using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
namespace Nethereum.Generators.Nuget.Test.EIP20.DTO
{
    [Event("Transfer")]
    public class TransferEventDTO
    {
        [Parameter("bytes32", "cryptletProofKey", 1, true )]
        public byte[] CryptletProofKey {get; set;}
        [Parameter("address", "_from", 2, true )]
        public string From {get; set;}
        [Parameter("address", "_to", 3, true )]
        public string To {get; set;}
        [Parameter("uint256", "_value", 4, false )]
        public BigInteger Value {get; set;}
    }
}
