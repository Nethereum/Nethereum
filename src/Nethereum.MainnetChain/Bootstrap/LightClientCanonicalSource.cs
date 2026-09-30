using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.Consensus.LightClient;
using Nethereum.CoreChain.Validation;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.MainnetChain.Bootstrap
{
    public sealed class LightClientCanonicalSource : ICanonicalStateRootSource
    {
        private readonly ITrustedHeaderProvider _provider;
        private readonly bool _useOptimistic;
        private readonly ILogger _logger;
        private long _lastSuccessfulTipUnixTicks;
        private long _lastReportedTipBlockSigned;

        public LightClientCanonicalSource(ITrustedHeaderProvider provider, bool useOptimistic = false, ILogger logger = null)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _useOptimistic = useOptimistic;
            _logger = logger ?? NullLogger.Instance;
        }

        public string Name => _useOptimistic ? "LightClient(optimistic)" : "LightClient(finalized)";

        public DateTimeOffset LastSuccessfulTipAt
        {
            get
            {
                var ticks = System.Threading.Interlocked.Read(ref _lastSuccessfulTipUnixTicks);
                return ticks == 0 ? DateTimeOffset.MinValue : new DateTimeOffset(ticks, TimeSpan.Zero);
            }
        }

        public Task<(byte[] StateRoot, byte[] BlockHash)> GetCanonicalAsync(
            ulong blockNumber,
            CancellationToken ct)
        {
            TrustedExecutionHeader header;
            try
            {
                header = _useOptimistic ? _provider.GetLatestOptimistic() : _provider.GetLatestFinalized();
            }
            catch (InvalidOperationException)
            {
                return Task.FromResult<(byte[] StateRoot, byte[] BlockHash)>((null, null));
            }

            if (header.BlockNumber != blockNumber)
            {
                return Task.FromResult<(byte[] StateRoot, byte[] BlockHash)>((null, null));
            }

            return Task.FromResult<(byte[] StateRoot, byte[] BlockHash)>((header.StateRoot, header.BlockHash));
        }

        public Task<CanonicalTip> GetLatestAsync(CancellationToken ct)
        {
            TrustedExecutionHeader header;
            try
            {
                header = _useOptimistic ? _provider.GetLatestOptimistic() : _provider.GetLatestFinalized();
            }
            catch (InvalidOperationException)
            {
                return Task.FromResult<CanonicalTip>(null);
            }

            System.Threading.Interlocked.Exchange(ref _lastSuccessfulTipUnixTicks, DateTimeOffset.UtcNow.Ticks);

            var prev = Interlocked.Exchange(ref _lastReportedTipBlockSigned, (long)header.BlockNumber);
            if ((ulong)prev != header.BlockNumber)
            {
                _logger.LogInformation(
                    "snap.canonical.forkchoice number={Number} hash={Hash} source={Source}",
                    header.BlockNumber,
                    header.BlockHash != null && header.BlockHash.Length > 0 ? header.BlockHash.ToHex() : "<none>",
                    Name);
            }

            return Task.FromResult(new CanonicalTip
            {
                BlockNumber = header.BlockNumber,
                BlockHash = header.BlockHash,
                StateRoot = header.StateRoot,
            });
        }
    }
}
