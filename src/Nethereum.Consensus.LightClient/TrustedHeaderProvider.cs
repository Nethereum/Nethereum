using System;
using Nethereum.Consensus.Ssz;

namespace Nethereum.Consensus.LightClient
{
    public class TrustedHeaderProvider : ITrustedHeaderProvider
    {
        private readonly LightClientService _lightClient;

        public TimeSpan FinalizedStalenessThreshold { get; set; } = TimeSpan.FromMinutes(30);
        public TimeSpan OptimisticStalenessThreshold { get; set; } = TimeSpan.FromMinutes(5);
        public bool ThrowOnStaleHeader { get; set; } = false;
        public event EventHandler<StaleHeaderEventArgs> StaleHeaderDetected;

        public TrustedHeaderProvider(LightClientService lightClient)
        {
            _lightClient = lightClient ?? throw new ArgumentNullException(nameof(lightClient));
        }

        public TrustedExecutionHeader GetLatestFinalized()
        {
            var state = _lightClient.GetState();
            if (state.FinalizedExecutionPayload == null || state.FinalizedHeader == null)
            {
                throw new InvalidOperationException("Light client state does not include a finalized execution payload yet.");
            }

            var header = MapToHeader(state.FinalizedExecutionPayload);
            ValidateStaleness(header, state.LastUpdated, FinalizedStalenessThreshold, "Finalized");
            return header;
        }

        public TrustedExecutionHeader GetLatestOptimistic()
        {
            var state = _lightClient.GetState();

            var execution = state.OptimisticExecutionPayload ?? state.FinalizedExecutionPayload;
            if (execution == null)
            {
                throw new InvalidOperationException("Light client state does not include any execution payload yet.");
            }

            var header = MapToHeader(execution);
            var lastUpdated = state.OptimisticExecutionPayload != null
                ? state.OptimisticLastUpdated
                : state.LastUpdated;
            ValidateStaleness(header, lastUpdated, OptimisticStalenessThreshold, "Optimistic");
            return header;
        }

        public byte[] GetBlockHash(ulong blockNumber)
        {
            var state = _lightClient.GetState();
            return state.GetBlockHash(blockNumber);
        }

        private void ValidateStaleness(TrustedExecutionHeader header, DateTimeOffset lastUpdated, TimeSpan threshold, string headerType)
        {
            var age = DateTimeOffset.UtcNow - lastUpdated;

            if (age > threshold)
            {
                var args = new StaleHeaderEventArgs(headerType, age, threshold, header);
                StaleHeaderDetected?.Invoke(this, args);

                if (ThrowOnStaleHeader)
                {
                    throw new StaleHeaderException(
                        $"{headerType} header is stale. Age: {age.TotalMinutes:F1} minutes, Threshold: {threshold.TotalMinutes:F1} minutes. Call UpdateAsync() or UpdateFinalityAsync() to refresh.",
                        age, threshold);
                }
            }
        }

        private static TrustedExecutionHeader MapToHeader(ExecutionPayloadHeader execution)
        {
            return new TrustedExecutionHeader
            {
                BlockHash = execution.BlockHash,
                BlockNumber = execution.BlockNumber,
                StateRoot = execution.StateRoot,
                ReceiptsRoot = execution.ReceiptsRoot,
                Timestamp = DateTimeOffset.FromUnixTimeSeconds((long)execution.Timestamp)
            };
        }
    }

    public class StaleHeaderEventArgs : EventArgs
    {
        public string HeaderType { get; }
        public TimeSpan Age { get; }
        public TimeSpan Threshold { get; }
        public TrustedExecutionHeader Header { get; }

        public StaleHeaderEventArgs(string headerType, TimeSpan age, TimeSpan threshold, TrustedExecutionHeader header)
        {
            HeaderType = headerType;
            Age = age;
            Threshold = threshold;
            Header = header;
        }
    }
}
