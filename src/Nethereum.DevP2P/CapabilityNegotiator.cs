using System.Collections.Generic;
using System.Linq;
using Nethereum.Documentation;
using Nethereum.Model.P2P;

namespace Nethereum.DevP2P
{
    public static class CapabilityNegotiator
    {
        private const int BaseProtocolOffset = 0x10;

        private static int GetMessageCount(string name, int version) => name switch
        {
            "eth" => version >= 71 ? 20 : version >= 69 ? 18 : 17,
            "snap" => version >= 2 ? 10 : 8,
            "les" => 24,
            _ => throw new System.NotSupportedException(
                $"No message-id count known for capability '{name}/{version}'; " +
                "advertising a capability the negotiator cannot size would desync the message-id space.")
        };

        [NethereumDocExample(DocSection.DevP2P, "devp2p", "CapabilityNegotiator.Negotiate — shared capability set")]
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
                var length = GetMessageCount(kv.Key, kv.Value);
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
