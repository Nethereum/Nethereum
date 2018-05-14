using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
namespace Nethereum.Generators.Nuget.Test.EIP20v2.DTO
{
    [Event("Created")]
    public class CreatedEventDTO
    {
        [Parameter("bytes32", "cryptletProofKey", 1, true )]
        public byte[] CryptletProofKey {get; set;}
    }
}
