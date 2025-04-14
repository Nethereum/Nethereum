using Newtonsoft.Json;

namespace Nethereum.Geth.RPC.Debug.DTOs
{
    public class TracingOptions
    {
        
        public TracerInfo TracerInfo { get; set; }

        public string Timeout { get; set; }
        
        public long? Reexec { get; set; }

        public TraceConfigDto ToDto()
        {
            return new TraceConfigDto
            {
                Timeout = string.IsNullOrWhiteSpace(Timeout) ? null : Timeout,
                Reexec = Reexec ?? null,
                Tracer = TracerInfo?.Tracer ?? null,
                TracerConfig = TracerInfo?.TracerConfig ?? null
            };
        }

        
    }

    
}