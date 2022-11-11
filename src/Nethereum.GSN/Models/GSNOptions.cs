using System;

namespace Nethereum.GSN.Models
{
    public class GSNOptions : ICloneable
    {
        public bool UseGSN { get; set; } = false;

        public int HttpTimeout { get; set; } = 1000;

        public int RelayLookupLimitBlocks { get; set; } = 6000;

        public int AllowedRelayNonceGap { get; set; } = 3;

        public string UserAgent { get; set; } = "gsn-neth-agent";

        public object Clone()
        {
            return this.MemberwiseClone();
        }
    }
}
