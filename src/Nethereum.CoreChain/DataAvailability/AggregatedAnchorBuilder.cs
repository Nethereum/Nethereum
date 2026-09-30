using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Proving;
using Nethereum.CoreChain.Storage;

namespace Nethereum.CoreChain.DataAvailability
{
    public class AggregatedAnchorData
    {
        public ulong AppChainKey { get; set; }
        public byte[] AppChainGenesisHash { get; set; }
        public ulong StartBlock { get; set; }
        public ulong EndBlock { get; set; }
        public byte AnchorVersion { get; set; }
        public byte ProofSystem { get; set; }
        public byte[] EndBlockHash { get; set; }
        public byte[] PreviousAnchorHash { get; set; }
        public byte[] BlockHashesRoot { get; set; }
        public byte[] PostStateRoot { get; set; }
        public byte[] ManifestHash { get; set; }
    }

    public class AnchorBuildResult
    {
        public AggregatedAnchorData Anchor { get; set; }
        public BatchManifest Manifest { get; set; }
        public byte[] ProofBytes { get; set; }
        public List<byte[]> BlockHashes { get; set; }
    }

    public class AggregatedAnchorBuilder
    {
        private readonly ulong _appChainKey;
        private readonly byte[] _genesisHash;
        private readonly byte _anchorVersion;
        private readonly byte _proofSystem;

        private byte[] _previousAnchorHash;
        private byte[] _previousPostStateRoot;

        public AggregatedAnchorBuilder(
            ulong appChainKey,
            byte[] genesisHash,
            byte anchorVersion = 1,
            byte proofSystem = 0,
            byte[] initialPreviousAnchorHash = null,
            byte[] initialPreviousPostStateRoot = null)
        {
            _appChainKey = appChainKey;
            _genesisHash = genesisHash;
            _anchorVersion = anchorVersion;
            _proofSystem = proofSystem;
            _previousAnchorHash = initialPreviousAnchorHash ?? new byte[32];
            _previousPostStateRoot = initialPreviousPostStateRoot ?? new byte[32];
        }

        public async Task<AnchorBuildResult> BuildFullAsync(
            IChainNode chain,
            ulong startBlock,
            ulong endBlock,
            IWitnessStore witnessStore = null)
        {
            var blockHashes = new List<byte[]>();
            for (ulong b = startBlock; b <= endBlock; b++)
                blockHashes.Add(await chain.GetBlockHashByNumberAsync(b));

            var blockHashesRoot = BlockHashesTree.ComputeRoot(blockHashes);

            var endBlockHeader = await chain.GetBlockByNumberAsync(endBlock);
            var endBlockHash = await chain.GetBlockHashByNumberAsync(endBlock);

            var headers = new List<byte[]>();
            for (ulong b = startBlock; b <= endBlock; b++)
            {
                var header = await chain.GetBlockByNumberAsync(b);
                if (header != null)
                    headers.Add(Model.BlockHeaderEncoder.Current.Encode(header));
            }

            byte[] proofBytes = null;
            BlockProofResult proofResult = null;
            if (witnessStore != null)
            {
                proofResult = await witnessStore.GetProofAsync(endBlock);
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

            var manifestHash = manifest.ComputeManifestHash();

            var anchor = new AggregatedAnchorData
            {
                AppChainKey = _appChainKey,
                AppChainGenesisHash = _genesisHash,
                StartBlock = startBlock,
                EndBlock = endBlock,
                AnchorVersion = _anchorVersion,
                ProofSystem = _proofSystem,
                EndBlockHash = endBlockHash,
                PreviousAnchorHash = _previousAnchorHash,
                BlockHashesRoot = blockHashesRoot,
                PostStateRoot = endBlockHeader.StateRoot,
                ManifestHash = manifestHash
            };

            _previousAnchorHash = endBlockHash;
            _previousPostStateRoot = endBlockHeader.StateRoot;

            return new AnchorBuildResult
            {
                Anchor = anchor,
                Manifest = manifest,
                ProofBytes = proofBytes,
                BlockHashes = blockHashes
            };
        }

        public byte[] PreviousAnchorHash => _previousAnchorHash;
        public byte[] PreviousPostStateRoot => _previousPostStateRoot;
    }
}
