using System;
using System.Collections.Generic;
using Nethereum.CoreChain.Freezer.Codecs;
using Nethereum.Documentation;
using Nethereum.Freezer;
using Nethereum.Model;

namespace Nethereum.CoreChain.Freezer.FilterMaps
{
    [NethereumDocExample(DocSection.ChainInfrastructure, "chain-infrastructure", "FreezerChainView — the production IChainView over the freezer")]
    public sealed class FreezerChainView : IChainView
    {
        private readonly IFrozenReadSource _freezer;
        private readonly FreezerCodecSet _codecs;

        public FreezerChainView(IFrozenReadSource freezer, FreezerCodecSet codecs)
        {
            _freezer = freezer ?? throw new ArgumentNullException(nameof(freezer));
            _codecs = codecs ?? throw new ArgumentNullException(nameof(codecs));
        }

        public long HeadNumber => _freezer.Items - 1;

        public byte[] BlockId(long number) => _codecs.Hashes.Decode(_freezer.ReadHash(number));

        public BlockHeader Header(long number) => _codecs.Headers.Decode(_freezer.ReadHeader(number));

        public IReadOnlyList<ReceiptForStorage> Receipts(long number) =>
            _codecs.Receipts.Decode(_freezer.ReadReceipts(number));
    }
}
