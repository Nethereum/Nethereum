using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.CoreChain.Freezer.Codecs;
using Nethereum.Documentation;
using Nethereum.Freezer;
using Nethereum.Model;

namespace Nethereum.CoreChain.Freezer
{
    [NethereumDocExample(DocSection.ChainInfrastructure, "chain-infrastructure", "FrozenBlockHashProjector — parallel block-hash + tx-hash projection over frozen blocks")]
    public static class FrozenBlockHashProjector
    {
        public static IReadOnlyList<(byte[] BlockHash, IReadOnlyList<byte[]> TxHashes)> ProjectChunk(
            IReadOnlyList<(long BlockNumber, byte[] Decoded)> bodiesChunk,
            IReadOnlyList<(long BlockNumber, byte[] Decoded)> hashesChunk,
            FreezerCodecSet codecs, int? maxDegreeOfParallelism = null)
        {
            var len = Math.Min(bodiesChunk.Count, hashesChunk.Count);
            var result = new (byte[], IReadOnlyList<byte[]>)[len];
            var options = new ParallelOptions { MaxDegreeOfParallelism = FrozenParallelism.Resolve(maxDegreeOfParallelism) };

            Parallel.For(0, len, options, i =>
                result[i] = Project(codecs.Hashes.Decode(hashesChunk[i].Decoded),
                    codecs.Bodies.Decode(bodiesChunk[i].Decoded)));

            return result;
        }

        private static (byte[] BlockHash, IReadOnlyList<byte[]> TxHashes) Project(byte[] blockHash, BlockBodyCluster body)
            => (blockHash, body.Txs.Select(tx => tx?.Hash).ToList());
    }
}
