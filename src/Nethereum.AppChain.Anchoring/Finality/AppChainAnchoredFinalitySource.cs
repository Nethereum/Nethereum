using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Validation;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;

namespace Nethereum.AppChain.Anchoring.Finality
{
    public sealed class AppChainAnchoredFinalitySource : ICanonicalStateRootSource
    {
        private readonly IAnchorRecordReader _anchors;
        private readonly IBlockStore _blocks;
        private readonly BlockParameter _l1Finality;
        private readonly ILogger? _logger;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

        private CanonicalTip? _finalisedTip;

        public AppChainAnchoredFinalitySource(
            IAnchorRecordReader anchors,
            IBlockStore blocks,
            BlockParameter? l1Finality = null,
            ILogger? logger = null)
        {
            _anchors = anchors ?? throw new ArgumentNullException(nameof(anchors));
            _blocks = blocks ?? throw new ArgumentNullException(nameof(blocks));
            _l1Finality = l1Finality ?? BlockParameter.CreateFinalized();
            _logger = logger;
        }

        public string Name => "AppChainAnchoredFinality";

        public AnchorAcceptance LastAcceptance { get; private set; } = AnchorAcceptance.NoAnchorYet;

        public async Task<CanonicalTip> GetLatestAsync(CancellationToken ct)
        {
            var target = await GetAnchorTargetAsync(ct).ConfigureAwait(false);

            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                _finalisedTip ??= await ReadGenesisTipAsync().ConfigureAwait(false);
                LastAcceptance = await AcceptAsync(_finalisedTip, target).ConfigureAwait(false);
                if (LastAcceptance == AnchorAcceptance.Accepted) _finalisedTip = target;
                LogAcceptance(target);
                return _finalisedTip;
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<(byte[] StateRoot, byte[] BlockHash)> GetCanonicalAsync(
            ulong blockNumber,
            CancellationToken ct)
        {
            var tip = await GetLatestAsync(ct).ConfigureAwait(false);
            return tip.BlockNumber == blockNumber
                ? (tip.StateRoot, tip.BlockHash)
                : (null, null);
        }

        public async Task<CanonicalTip?> GetAnchorTargetAsync(CancellationToken ct)
        {
            var record = await _anchors.GetLatestAnchorAsync(_l1Finality, ct).ConfigureAwait(false);
            if (record == null || record.EndBlock == 0) return null;

            return new CanonicalTip
            {
                BlockNumber = record.EndBlock,
                BlockHash = record.EndBlockHash,
                StateRoot = record.PostStateRoot
            };
        }

        private async Task<AnchorAcceptance> AcceptAsync(CanonicalTip current, CanonicalTip? target)
        {
            if (target == null) return AnchorAcceptance.NoAnchorYet;
            if (target.BlockNumber <= current.BlockNumber) return AnchorAcceptance.NotAdvanced;

            var descendsFromCurrent = await MatchLocalChainAsync(current).ConfigureAwait(false);
            return descendsFromCurrent == AnchorAcceptance.Accepted
                ? await MatchLocalChainAsync(target).ConfigureAwait(false)
                : descendsFromCurrent;
        }

        private async Task<AnchorAcceptance> MatchLocalChainAsync(CanonicalTip tip)
        {
            var localHash = await _blocks.GetHashByNumberAsync(tip.BlockNumber).ConfigureAwait(false);
            if (localHash == null || localHash.Length == 0) return AnchorAcceptance.NotYetLocal;

            return ByteUtil.AreEqual(localHash, tip.BlockHash)
                ? AnchorAcceptance.Accepted
                : AnchorAcceptance.Diverged;
        }

        private async Task<CanonicalTip> ReadGenesisTipAsync()
        {
            var header = await _blocks.GetByNumberAsync(0).ConfigureAwait(false);
            var hash = await _blocks.GetHashByNumberAsync(0).ConfigureAwait(false);

            return new CanonicalTip
            {
                BlockNumber = 0,
                BlockHash = hash ?? Array.Empty<byte>(),
                StateRoot = header?.StateRoot ?? Array.Empty<byte>()
            };
        }

        private void LogAcceptance(CanonicalTip? target)
        {
            if (_logger == null) return;

            switch (LastAcceptance)
            {
                case AnchorAcceptance.Diverged:
                    _logger.LogError(
                        "Anchor at block {AnchorBlock} does not descend from the local chain; finalised tip held at {FinalisedTip}",
                        target?.BlockNumber, _finalisedTip?.BlockNumber);
                    break;
                case AnchorAcceptance.NotYetLocal:
                    _logger.LogWarning(
                        "Anchor at block {AnchorBlock} is not in the local store yet; finalised tip held at {FinalisedTip}",
                        target?.BlockNumber, _finalisedTip?.BlockNumber);
                    break;
                case AnchorAcceptance.Accepted:
                    _logger.LogInformation("Finalised tip advanced to anchored block {FinalisedTip}", _finalisedTip?.BlockNumber);
                    break;
            }
        }
    }
}
