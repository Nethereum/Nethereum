using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.Consensus.LightClient;
using Nethereum.CoreChain.Storage;

namespace Nethereum.MainnetChain.Bootstrap
{
    public sealed class DurableLightClientStore : ILightClientStore
    {
        private readonly IChainMetadataStore _metadata;
        private readonly TimeSpan _maxTrustedAge;
        private readonly ILogger _logger;
        private readonly Func<DateTimeOffset> _now;

        public DurableLightClientStore(
            IChainMetadataStore metadata,
            TimeSpan maxTrustedAge,
            ILogger logger = null,
            Func<DateTimeOffset> now = null)
        {
            _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            _maxTrustedAge = maxTrustedAge > TimeSpan.Zero ? maxTrustedAge : TimeSpan.FromDays(14);
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
            _now = now ?? (() => DateTimeOffset.UtcNow);
        }

        public Task<LightClientState> LoadAsync()
        {
            var blob = _metadata.GetLightClientStateBlob();
            if (blob == null) return Task.FromResult<LightClientState>(null);

            if (!LightClientStateCodec.TryDecode(blob, out var state))
            {
                _logger.LogWarning("lc.persist.load corrupt state blob — discarding, will re-bootstrap");
                return Task.FromResult<LightClientState>(null);
            }

            var age = _now() - Reference(state);
            if (age > _maxTrustedAge)
            {
                _logger.LogWarning(
                    "lc.persist.load persisted state is {AgeH:F1}h old (> {MaxH:F1}h trust window) — re-bootstrapping instead of resuming a stale head",
                    age.TotalHours, _maxTrustedAge.TotalHours);
                return Task.FromResult<LightClientState>(null);
            }

            _logger.LogInformation(
                "lc.persist.load resumed verified LC state: finalized_slot={Fin} period={Period} age={AgeMin:F1}m — no re-bootstrap",
                state.FinalizedSlot, state.CurrentPeriod, age.TotalMinutes);
            return Task.FromResult(state);
        }

        public Task SaveAsync(LightClientState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            try
            {
                _metadata.SaveLightClientStateBlob(LightClientStateCodec.Encode(state));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "lc.persist.save failed — LC state not persisted this update (in-memory unaffected)");
            }
            return Task.CompletedTask;
        }

        private static DateTimeOffset Reference(LightClientState state)
        {
            var a = state.LastUpdated;
            var b = state.OptimisticLastUpdated;
            return a > b ? a : b;
        }
    }
}
