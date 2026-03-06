using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;

namespace Nethereum.AppChain.Sequencer.Metrics
{
    public class SequencerMetrics : IDisposable
    {
        private readonly string _chainId;
        private readonly string _name;
        private readonly Meter _meter;
        private readonly Counter<long> _policyRejections;

        private int _isActive;
        private long _epoch;
        private long _lastHeartbeatTimestamp;

        public bool IsActive => Volatile.Read(ref _isActive) == 1;
        public long Epoch => Interlocked.Read(ref _epoch);

        public SequencerMetrics(string chainId, string name = "Nethereum", IMeterFactory? meterFactory = null)
        {
            _chainId = chainId;
            _name = name;
            _meter = meterFactory?.Create($"{name}.Sequencer") ?? new Meter($"{name}.Sequencer");

            _policyRejections = _meter.CreateCounter<long>(
                "sequencer.policy_rejections",
                unit: "{rejection}",
                description: "Total transactions rejected by policy");

            _meter.CreateObservableGauge("sequencer.active",
                () => new Measurement<int>(Volatile.Read(ref _isActive),
                    new KeyValuePair<string, object?>("chain_id", _chainId),
                    new KeyValuePair<string, object?>("chain_name", _name)),
                description: "Whether this node is the active sequencer (1=active, 0=standby)");

            _meter.CreateObservableGauge("sequencer.epoch",
                () => new Measurement<long>(Interlocked.Read(ref _epoch),
                    new KeyValuePair<string, object?>("chain_id", _chainId),
                    new KeyValuePair<string, object?>("chain_name", _name)),
                description: "Current sequencer epoch");

            _meter.CreateObservableGauge("sequencer.last_heartbeat",
                () => new Measurement<long>(Interlocked.Read(ref _lastHeartbeatTimestamp),
                    new KeyValuePair<string, object?>("chain_id", _chainId),
                    new KeyValuePair<string, object?>("chain_name", _name)),
                unit: "s",
                description: "Unix timestamp of last heartbeat");
        }

        public void SetActive(bool active, long epoch)
        {
            Volatile.Write(ref _isActive, active ? 1 : 0);
            Interlocked.Exchange(ref _epoch, epoch);
        }

        public void RecordHeartbeat()
        {
            Interlocked.Exchange(ref _lastHeartbeatTimestamp, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }

        public void RecordPolicyRejection(string policy)
        {
            _policyRejections.Add(1, new TagList { { "chain_id", _chainId }, { "chain_name", _name }, { "policy", policy } });
        }

        public void Dispose() => _meter.Dispose();
    }
}
