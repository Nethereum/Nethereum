using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Optimism.L1CrossDomainMessenger.ContractDefinition
{
    public partial class L2MessageInclusionProof : L2MessageInclusionProofBase { }

    public class L2MessageInclusionProofBase
    {
        [Parameter("bytes32", "stateRoot", 1)]
        public virtual byte[] StateRoot { get; set; }
        [Parameter("tuple", "stateRootBatchHeader", 2)]
        public virtual ChainBatchHeader StateRootBatchHeader { get; set; }
        [Parameter("tuple", "stateRootProof", 3)]
        public virtual ChainInclusionProof StateRootProof { get; set; }
        [Parameter("bytes", "stateTrieWitness", 4)]
        public virtual byte[] StateTrieWitness { get; set; }
        [Parameter("bytes", "storageTrieWitness", 5)]
        public virtual byte[] StorageTrieWitness { get; set; }
    }
}
