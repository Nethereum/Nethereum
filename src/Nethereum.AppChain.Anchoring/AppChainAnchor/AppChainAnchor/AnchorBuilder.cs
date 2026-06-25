using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.AppChain.Anchoring.AppChainAnchor.ContractDefinition;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Proving;
using Nethereum.CoreChain.Storage;
using Nethereum.Merkle;
using Nethereum.Util.ByteArrayConvertors;
using Nethereum.Util.HashProviders;

namespace Nethereum.AppChain.Anchoring.AppChainAnchor
{
    public class BlockLeafData
    {
        public ulong BlockNumber { get; set; }
        public byte[] BlockHash { get; set; }
        public byte[] PreStateRoot { get; set; }
        public byte[] PostStateRoot { get; set; }

        public byte[] ToLeafInput()
        {
            var input = new byte[104];
            for (int i = 0; i < 8; i++)
                input[i] = (byte)(BlockNumber >> (56 - i * 8));
            Array.Copy(BlockHash, 0, input, 8, 32);
            Array.Copy(PreStateRoot, 0, input, 40, 32);
            Array.Copy(PostStateRoot, 0, input, 72, 32);
            return input;
        }
    }

    public class AnchorBuildResult
    {
        public AggregatedAnchor Anchor { get; set; }
        public BatchManifest Manifest { get; set; }
        public byte[] ProofBytes { get; set; }
        public List<BlockLeafData> BlockLeaves { get; set; }
        public MerkleTree<byte[]> BlockHashesTree { get; set; }
    }

    public class AnchorBuilder
    {
        private readonly ulong _chainId;
        private readonly byte[] _genesisHash;
        private readonly byte _anchorVersion;
        private readonly byte _proofSystem;

        private byte[] _previousAnchorHash;
        private byte[] _previousPostStateRoot;

        public byte[] PreviousAnchorHash => _previousAnchorHash;
        public byte[] PreviousPostStateRoot => _previousPostStateRoot;

        public AnchorBuilder(
            ulong chainId, byte[] genesisHash,
            byte anchorVersion = 1, byte proofSystem = 0,
            byte[] initialPreviousAnchorHash = null,
            byte[] initialPreviousPostStateRoot = null)
        {
            _chainId = chainId;
            _genesisHash = genesisHash;
            _anchorVersion = anchorVersion;
            _proofSystem = proofSystem;
            _previousAnchorHash = initialPreviousAnchorHash ?? new byte[32];
            _previousPostStateRoot = initialPreviousPostStateRoot ?? new byte[32];
        }

        public async Task<AnchorBuildResult> BuildAsync(
            IChainNode chain, ulong startBlock, ulong endBlock,
            IWitnessStore witnessStore = null)
        {
            var leaves = new List<BlockLeafData>();
            var leafInputs = new List<byte[]>();
            var headers = new List<byte[]>();
            Nethereum.Model.BlockHeader endBlockHeader = null;
            byte[] endBlockHash = null;
            byte[] prevState = _previousPostStateRoot;

            for (ulong b = startBlock; b <= endBlock; b++)
            {
                var hash = await chain.GetBlockHashByNumberAsync(b).ConfigureAwait(false);
                var header = await chain.GetBlockByNumberAsync(b).ConfigureAwait(false);

                var leaf = new BlockLeafData
                {
                    BlockNumber = b,
                    BlockHash = hash,
                    PreStateRoot = prevState,
                    PostStateRoot = header.StateRoot
                };
                leaves.Add(leaf);
                leafInputs.Add(leaf.ToLeafInput());

                if (header != null)
                    headers.Add(Model.BlockHeaderEncoder.Current.Encode(header));

                prevState = header.StateRoot;
                if (b == endBlock)
                {
                    endBlockHeader = header;
                    endBlockHash = hash;
                }
            }

            var tree = new MerkleTree<byte[]>(
                new Sha3KeccackHashProvider(),
                new ByteArrayToByteArrayConvertor());
            tree.BuildTree(leafInputs);
            var blockHashesRoot = tree.Root.Hash;

            byte[] proofBytes = null;
            BlockProofResult proofResult = null;
            if (witnessStore != null)
            {
                proofResult = await witnessStore.GetProofAsync(endBlock).ConfigureAwait(false);
                proofBytes = proofResult?.ProofBytes;
            }

            var core = new ManifestCore
            {
                AppChainGenesisHash = _genesisHash,
                AnchorVersion = _anchorVersion,
                ProofSystem = _proofSystem,
                StartBlock = startBlock,
                EndBlock = endBlock,
                PreStateRoot = _previousPostStateRoot,
                PostStateRoot = endBlockHeader.StateRoot,
                EndBlockHash = endBlockHash,
                BlockHashesRoot = blockHashesRoot,
                TxDataBundleHash = new byte[32]
            };

            var manifest = new BatchManifest
            {
                Core = core,
                Proof = proofResult != null ? new ProofEnvelope
                {
                    ProofBytes = proofResult.ProofBytes,
                    ElfHash = proofResult.ElfHash,
                    ProverMode = proofResult.ProverMode,
                    GasUsed = proofResult.GasUsed
                } : null,
                BlockHeaders = headers
            };

            var anchor = new AggregatedAnchor
            {
                ChainId = _chainId,
                GenesisHash = _genesisHash,
                StartBlock = startBlock,
                EndBlock = endBlock,
                AnchorVersion = _anchorVersion,
                ProofSystem = _proofSystem,
                EndBlockHash = endBlockHash,
                PreviousAnchorHash = _previousAnchorHash,
                BlockHashesRoot = blockHashesRoot,
                PostStateRoot = endBlockHeader.StateRoot,
                ManifestHash = manifest.ComputeManifestHash()
            };

            _previousAnchorHash = endBlockHash;
            _previousPostStateRoot = endBlockHeader.StateRoot;

            return new AnchorBuildResult
            {
                Anchor = anchor,
                Manifest = manifest,
                ProofBytes = proofBytes,
                BlockLeaves = leaves,
                BlockHashesTree = tree
            };
        }
    }
}
