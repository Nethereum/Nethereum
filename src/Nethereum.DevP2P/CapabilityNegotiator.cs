using System.Collections.Generic;
using System.Linq;
using Nethereum.Model.P2P;

namespace Nethereum.DevP2P
{
    public static class CapabilityNegotiator
    {
        private const int BaseProtocolOffset = 0x10;

        private static readonly Dictionary<string, int> MessageCounts = new()
        {
            // eth message slots = max ID + 1.
            // eth/68: IDs 0x00-0x10 (17 slots; 0x0b-0x0e are unused).
            // eth/69+: adds BlockRangeUpdate at 0x11, so 18 slots.
            ["eth"] = 18,
            ["snap"] = 8,
            // les/4: IDs 0x00-0x17 (24 slots).
            ["les"] = 24
        };

        public static List<P2PCapability> Negotiate(
            List<P2PCapability> local, List<P2PCapability> remote)
        {
            var shared = new Dictionary<string, int>();

            foreach (var lc in local)
            {
                foreach (var rc in remote)
                {
                    if (lc.Name == rc.Name)
                    {
                        var version = System.Math.Min(lc.Version, rc.Version);
                        if (!shared.ContainsKey(lc.Name) || shared[lc.Name] < version)
                            shared[lc.Name] = version;
                    }
                }
            }

            var sorted = shared.OrderBy(kv => kv.Key, System.StringComparer.Ordinal).ToList();

            var result = new List<P2PCapability>();
            var offset = BaseProtocolOffset;
            foreach (var kv in sorted)
            {
                var length = MessageCounts.GetValueOrDefault(kv.Key, 0);
                result.Add(new P2PCapability
                {
                    Name = kv.Key,
                    Version = kv.Value,
                    Offset = offset,
                    Length = length
                });
                offset += length;
            }

            return result;
        }
    }
}
