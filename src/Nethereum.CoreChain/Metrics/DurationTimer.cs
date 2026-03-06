using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Nethereum.CoreChain.Metrics
{
    public readonly struct DurationTimer : IDisposable
    {
        private readonly Histogram<double> _histogram;
        private readonly TagList _tags;
        private readonly long _startTimestamp;

        internal DurationTimer(Histogram<double> histogram, TagList tags)
        {
            _histogram = histogram;
            _tags = tags;
            _startTimestamp = Stopwatch.GetTimestamp();
        }

        public void Dispose()
        {
            _histogram.Record(Stopwatch.GetElapsedTime(_startTimestamp).TotalSeconds, _tags);
        }
    }
}
