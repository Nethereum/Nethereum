using System.Diagnostics.Tracing;

namespace Nethereum.EVM.Microbench
{
    public sealed class AllocationSampler : EventListener
    {
        private readonly Dictionary<string, long> _bytesByType = new Dictionary<string, long>();

        protected override void OnEventSourceCreated(EventSource source)
        {
            if (source.Name == "Microsoft-Windows-DotNETRuntime")
                EnableEvents(source, EventLevel.Verbose, (EventKeywords)0x1);
        }

        protected override void OnEventWritten(EventWrittenEventArgs data)
        {
            if (data.EventName == null || !data.EventName.StartsWith("GCAllocationTick")) return;
            var typeName = (string?)data.Payload![data.PayloadNames!.IndexOf("TypeName")] ?? "?";
            var amount = Convert.ToInt64(data.Payload[data.PayloadNames.IndexOf("AllocationAmount64")]);
            lock (_bytesByType) _bytesByType[typeName] = _bytesByType.GetValueOrDefault(typeName) + amount;
        }

        public void Report()
        {
            lock (_bytesByType)
                foreach (var entry in _bytesByType.OrderByDescending(e => e.Value).Take(8))
                    Console.WriteLine($"    {entry.Value / 1024,10} KB sampled  {entry.Key}");
        }

        public void Reset()
        {
            lock (_bytesByType) _bytesByType.Clear();
        }
    }
}
