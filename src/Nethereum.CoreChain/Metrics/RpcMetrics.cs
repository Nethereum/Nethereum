using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;

namespace Nethereum.CoreChain.Metrics
{
    public class RpcMetrics : IDisposable
    {
        private readonly string _chainId;
        private readonly string _name;
        private readonly Meter _meter;
        private readonly Meter _detailedMeter;
        private readonly Counter<long> _requestsTotal;
        private readonly Counter<long> _errorsTotal;
        private readonly Histogram<double> _requestDuration;

        private long _totalRequests;
        private int _activeConnections;
        private readonly ConcurrentDictionary<string, long> _requestsByMethod = new();

        public long TotalRequests => Interlocked.Read(ref _totalRequests);
        public int ActiveConnections => Volatile.Read(ref _activeConnections);

        public long GetRequestCount(string method)
        {
            return _requestsByMethod.TryGetValue(method, out var count) ? Interlocked.Read(ref count) : 0;
        }

        public RpcMetrics(string chainId, string name = "Nethereum", IMeterFactory? meterFactory = null)
        {
            _chainId = chainId;
            _name = name;
            _meter = meterFactory?.Create($"{name}.CoreChain") ?? new Meter($"{name}.CoreChain");
            _detailedMeter = meterFactory?.Create($"{name}.CoreChain.Detailed") ?? new Meter($"{name}.CoreChain.Detailed");

            _requestsTotal = _meter.CreateCounter<long>(
                "corechain.rpc.requests",
                unit: "{request}",
                description: "Total RPC requests");

            _errorsTotal = _meter.CreateCounter<long>(
                "corechain.rpc.errors",
                unit: "{error}",
                description: "Total RPC errors");

            _requestDuration = _detailedMeter.CreateHistogram<double>(
                "corechain.rpc.request.duration",
                unit: "s",
                description: "RPC request duration"
#if NET9_0_OR_GREATER
                , advice: new InstrumentAdvice<double>
                {
                    HistogramBucketBoundaries = [0.001, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1.0]
                }
#endif
                );

            _meter.CreateObservableGauge(
                "corechain.rpc.connections.active",
                () => new Measurement<int>(Volatile.Read(ref _activeConnections),
                    new KeyValuePair<string, object?>("chain_id", _chainId),
                    new KeyValuePair<string, object?>("chain_name", _name)),
                unit: "{connection}",
                description: "Number of active RPC connections");
        }

        public DurationTimer MeasureRequest(string method)
        {
            var tags = new TagList { { "chain_id", _chainId }, { "chain_name", _name }, { "method", method } };
            _requestsTotal.Add(1, tags);
            Interlocked.Increment(ref _totalRequests);
            _requestsByMethod.AddOrUpdate(method, 1, (_, old) => old + 1);
            return new DurationTimer(_requestDuration, tags);
        }

        public void RecordError(string method, int errorCode)
        {
            _errorsTotal.Add(1, new TagList { { "chain_id", _chainId }, { "chain_name", _name }, { "method", method }, { "code", errorCode.ToString() } });
        }

        public void SetActiveConnections(int count) => Volatile.Write(ref _activeConnections, count);

        public void Dispose()
        {
            _meter.Dispose();
            _detailedMeter.Dispose();
        }
    }
}
