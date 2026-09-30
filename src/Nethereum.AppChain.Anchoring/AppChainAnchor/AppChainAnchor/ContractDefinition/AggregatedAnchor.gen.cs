using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.AppChain.Anchoring.AppChainAnchor.ContractDefinition
{
    public partial class AggregatedAnchor : AggregatedAnchorBase { }

    public class AggregatedAnchorBase 
    {
        [Parameter("uint64", "chainId", 1)]
        public virtual ulong ChainId { get; set; }
        [Parameter("bytes32", "genesisHash", 2)]
        public virtual byte[] GenesisHash { get; set; }
        [Parameter("uint64", "startBlock", 3)]
        public virtual ulong StartBlock { get; set; }
        [Parameter("uint64", "endBlock", 4)]
        public virtual ulong EndBlock { get; set; }
        [Parameter("uint8", "anchorVersion", 5)]
        public virtual byte AnchorVersion { get; set; }
        [Parameter("uint8", "proofSystem", 6)]
        public virtual byte ProofSystem { get; set; }
        [Parameter("bytes32", "endBlockHash", 7)]
        public virtual byte[] EndBlockHash { get; set; }
        [Parameter("bytes32", "previousAnchorHash", 8)]
        public virtual byte[] PreviousAnchorHash { get; set; }
        [Parameter("bytes32", "blockHashesRoot", 9)]
        public virtual byte[] BlockHashesRoot { get; set; }
        [Parameter("bytes32", "postStateRoot", 10)]
        public virtual byte[] PostStateRoot { get; set; }
        [Parameter("bytes32", "manifestHash", 11)]
        public virtual byte[] ManifestHash { get; set; }
    }
}
