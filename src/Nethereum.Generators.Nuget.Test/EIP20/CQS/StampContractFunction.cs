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
    [Function("stampContract")]
    public class StampContractFunction:ContractMessage
    {
        [Parameter("string", "bindingId", 1)]
        public string BindingId {get; set;}
        [Parameter("string", "registryVersionHash", 2)]
        public string RegistryVersionHash {get; set;}
        [Parameter("string", "runtimeBindingHash", 3)]
        public string RuntimeBindingHash {get; set;}
        [Parameter("bytes32", "cryptletProofKey", 4)]
        public byte[] CryptletProofKey {get; set;}
    }
}
