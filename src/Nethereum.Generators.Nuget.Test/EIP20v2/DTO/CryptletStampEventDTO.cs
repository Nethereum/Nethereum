using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
namespace Nethereum.Generators.Nuget.Test.EIP20v2.DTO
{
    [Event("CryptletStamp")]
    public class CryptletStampEventDTO
    {
        [Parameter("bytes32", "cryptletProofKey", 1, true )]
        public byte[] CryptletProofKey {get; set;}
        [Parameter("string", "bindingId", 2, false )]
        public string BindingId {get; set;}
        [Parameter("string", "registryVersionHash", 3, false )]
        public string RegistryVersionHash {get; set;}
        [Parameter("string", "runtimeBindingHash", 4, false )]
        public string RuntimeBindingHash {get; set;}
    }
}
