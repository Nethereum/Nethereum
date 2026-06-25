using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.Consensus.LightClient;
using Nethereum.CoreChain.Validation;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.MainnetChain.Server.Bootstrap
{
    /// <summary>
    /// <see cref="ICanonicalStateRootSource"/> backed by the consensus light
    /// client (<see cref="ITrustedHeaderProvider"/>). Provides the same
    /// execution-payload-header data the Engine API forkchoiceUpdated pivot
    /// uses, in-process instead of via the Engine API.
    /// Trust = beacon sync-committee BLS quorum.
    ///
    /// <para>Pair ahead of <see cref="ICanonicalStateRootSource"/> impls
    /// in a <c>CompositeCanonicalStateRootSource</c>: prefer the light client
    /// (free + provable) and fall back to a trusted RPC (cheap + any height).</para>
    ///
    /// <para>Use <c>useOptimistic=true</c> for low-latency followers that
    /// accept the small reorg risk at the optimistic head (~1 epoch).
    /// Default is finalized — pivot can be up to 2 epochs behind tip but is
    /// L1-finalised under consensus rules.</para>
    ///
    /// <para><c>GetCanonicalAsync(blockNumber)</c> returns
    /// <c>(null, null)</c> for arbitrary block numbers — the light client
    /// only knows the latest finalized/optimistic execution header, not
    /// historical state roots. For point validation at arbitrary heights
    /// compose with an RPC source.</para>
    /// </summary>
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

        /// <summary>
        /// UTC timestamp of the most recent <see cref="GetLatestAsync"/> call that
        /// returned a non-null tip, or <see cref="DateTimeOffset.MinValue"/> if
        /// the source has never produced one. The reporter heartbeat reads this
        /// to emit <c>snap.canonical.stalled</c> when staleness exceeds
        /// 60 seconds.
        /// </summary>
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
            // Light client only knows the latest finalized/optimistic head,
            // not historical state roots. If the requested block matches the
            // current finalized head, return it; otherwise null.
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
                // Light client has no payload yet — caller treats null as "tip
                // unavailable" and retries with backoff.
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
