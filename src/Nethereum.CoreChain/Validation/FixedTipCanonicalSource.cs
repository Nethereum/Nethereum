using System;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.CoreChain.Validation
{
    public sealed class FixedTipCanonicalSource : ICanonicalStateRootSource
    {
        private readonly CanonicalTip _tip;

        public FixedTipCanonicalSource(ulong blockNumber, byte[] blockHash, byte[] stateRoot)
        {
            if (blockHash == null || blockHash.Length != 32)
                throw new ArgumentException("Tip block hash must be 32 bytes.", nameof(blockHash));
            if (stateRoot == null || stateRoot.Length != 32)
                throw new ArgumentException("Tip state root must be 32 bytes.", nameof(stateRoot));

            _tip = new CanonicalTip
            {
                BlockNumber = blockNumber,
                BlockHash = blockHash,
                StateRoot = stateRoot,
            };
        }

        public string Name => $"FixedTip(#{_tip.BlockNumber})";

        public Task<(byte[] StateRoot, byte[] BlockHash)> GetCanonicalAsync(
            ulong blockNumber, CancellationToken ct)
            => Task.FromResult(blockNumber == _tip.BlockNumber
                ? (_tip.StateRoot, _tip.BlockHash)
                : ((byte[])null, (byte[])null));

        public Task<CanonicalTip> GetLatestAsync(CancellationToken ct)
            => Task.FromResult(_tip);
    }
}
