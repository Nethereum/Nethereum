using System;
using System.Collections.Concurrent;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading;

namespace Nethereum.MainnetChain.Server
{
    internal sealed class AllocationProfiler : EventListener
    {
        private static AllocationProfiler _instance;
        public static void Start(int intervalSeconds = 30) => _instance ??= new AllocationProfiler(intervalSeconds);

        private readonly ConcurrentDictionary<string, long> _byType = new();
        private long _total;
        private Timer _timer;
        private readonly int _intervalSeconds;

        private AllocationProfiler(int intervalSeconds)
        {
            _intervalSeconds = intervalSeconds;
            _timer = new Timer(_ => Dump(), null, TimeSpan.FromSeconds(intervalSeconds), TimeSpan.FromSeconds(intervalSeconds));
        }

        protected override void OnEventSourceCreated(EventSource source)
        {
            if (source.Name == "Microsoft-Windows-DotNETRuntime")
                EnableEvents(source, EventLevel.Verbose, (EventKeywords)0x1);
        }

        protected override void OnEventWritten(EventWrittenEventArgs e)
        {
            if (e.EventName == null || !e.EventName.StartsWith("GCAllocationTick")) return;
            string type = null;
            long amount = 0;
            var names = e.PayloadNames;
            for (int i = 0; i < names.Count; i++)
            {
                if (names[i] == "TypeName") type = e.Payload[i] as string;
                else if (names[i] == "AllocationAmount64") amount = Convert.ToInt64(e.Payload[i]);
                else if (names[i] == "AllocationAmount" && amount == 0) amount = Convert.ToInt64(e.Payload[i]);
            }
            if (string.IsNullOrEmpty(type) || amount <= 0) return;
            _byType.AddOrUpdate(type, amount, (_, v) => v + amount);
            Interlocked.Add(ref _total, amount);
        }

        private void Dump()
        {
            var snapshot = _byType.ToArray();
            _byType.Clear();
            long total = Interlocked.Exchange(ref _total, 0);
            if (total == 0) return;
            var sb = new StringBuilder();
            sb.Append($"alloc.profile interval={_intervalSeconds}s total_mb={total / (1024 * 1024)} top:");
            foreach (var kv in snapshot.OrderByDescending(kv => kv.Value).Take(20))
                sb.Append($" [{kv.Key}={kv.Value / (1024 * 1024)}mb]");
            Console.Error.WriteLine(sb.ToString());
        }
    }
}
