using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.CoreChain.Freezer.Codecs;
using Nethereum.Documentation;
using Nethereum.Freezer;
using Nethereum.Model;

namespace Nethereum.CoreChain.Freezer.FilterMaps
{
    [NethereumDocExample(DocSection.ChainInfrastructure, "chain-infrastructure", "SegmentChainView — windowed parallel-decode IChainView over the freezer")]
    public sealed class SegmentChainView : IChainView
    {
        public const int DefaultWindowSize = 1024;

        private readonly IFrozenReadSource _freezer;
        private readonly FreezerCodecSet _codecs;
        private readonly int _windowSize;
        private readonly int? _dop;

        private long _windowStart = -1;
        private IReadOnlyList<ReceiptForStorage>[] _window;

        public SegmentChainView(IFrozenReadSource freezer, FreezerCodecSet codecs,
            int windowSize = DefaultWindowSize, int? maxDegreeOfParallelism = null)
        {
            _freezer = freezer ?? throw new ArgumentNullException(nameof(freezer));
            _codecs = codecs ?? throw new ArgumentNullException(nameof(codecs));
            if (windowSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(windowSize), windowSize, "window size must be positive");
            _windowSize = windowSize;
            _dop = maxDegreeOfParallelism;
        }

        public long HeadNumber => _freezer.Items - 1;

        public byte[] BlockId(long number) => _codecs.Hashes.Decode(_freezer.ReadHash(number));

        public BlockHeader Header(long number) => _codecs.Headers.Decode(_freezer.ReadHeader(number));

        public IReadOnlyList<ReceiptForStorage> Receipts(long number)
        {
            if (_window != null && number >= _windowStart && number < _windowStart + _window.Length)
                return _window[number - _windowStart];
            return LoadWindowAndGet(number);
        }

        private IReadOnlyList<ReceiptForStorage> LoadWindowAndGet(long number)
        {
            var committedHead = _freezer.CommittedHead("receipts");
            var layer1 = _freezer.DecodeSealedRange("receipts", number, _windowSize, committedHead, _dop);
            if (layer1.Count == 0)
                return _codecs.Receipts.Decode(_freezer.ReadReceipts(number));

            var decoded = new IReadOnlyList<ReceiptForStorage>[layer1.Count];
            var options = new ParallelOptions { MaxDegreeOfParallelism = FrozenParallelism.Resolve(_dop) };

            Parallel.For(0, layer1.Count, options, i => decoded[i] = _codecs.Receipts.Decode(layer1[i].Decoded));

            _windowStart = number;
            _window = decoded;
            return _window[0];
        }
    }
}
