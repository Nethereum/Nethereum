using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.AppChain.Anchoring.AppChainAnchor.ContractDefinition
{
    public partial class VersionSchema : VersionSchemaBase { }

    public class VersionSchemaBase 
    {
        [Parameter("bytes32", "hashFunction", 1)]
        public virtual byte[] HashFunction { get; set; }
        [Parameter("uint8", "trieType", 2)]
        public virtual byte TrieType { get; set; }
        [Parameter("uint8", "stateModel", 3)]
        public virtual byte StateModel { get; set; }
        [Parameter("uint8", "manifestFormat", 4)]
        public virtual byte ManifestFormat { get; set; }
        [Parameter("bool", "exists", 5)]
        public virtual bool Exists { get; set; }
    }
}
