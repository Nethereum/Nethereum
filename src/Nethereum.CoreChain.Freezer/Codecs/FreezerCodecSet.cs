using System.Collections.Generic;
using Nethereum.Documentation;
using Nethereum.Freezer;
using Nethereum.Model;

namespace Nethereum.CoreChain.Freezer.Codecs
{
    [NethereumDocExample(DocSection.ChainInfrastructure, "chain-infrastructure", "FreezerCodecSet — the five typed per-stream item codecs")]
    public sealed class FreezerCodecSet
    {
        public IItemCodec<BlockHeader> Headers { get; }
        public IItemCodec<BlockBodyCluster> Bodies { get; }
        public IItemCodec<IReadOnlyList<ReceiptForStorage>> Receipts { get; }
        public IItemCodec<byte[]> Hashes { get; }
        public IItemCodec<byte[]> Bals { get; }

        public FreezerCodecSet()
        {
            Headers = new HeaderItemCodec();
            Bodies = new BodyClusterItemCodec();
            Receipts = new ReceiptsItemCodec();
            Hashes = new HashesItemCodec();
            Bals = new BalItemCodec();
        }
    }
}
