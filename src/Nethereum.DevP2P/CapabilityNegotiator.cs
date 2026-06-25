using System.Collections.Generic;
using System.Linq;
using Nethereum.Model.P2P;

namespace Nethereum.DevP2P
{
    public static class CapabilityNegotiator
    {
        private const int BaseProtocolOffset = 0x10;

        // The slot count for the eth sub-protocol is version-dependent —
        // the eth protocol message-id space is
        //     protocolLengths = { ETH68: 17, ETH69: 18 }.
        // Any later sub-protocol (snap, les, ...) starts at
        // BaseProtocolOffset + length(eth). If we hand a fixed length here
        // regardless of the version we negotiated, the snap base offset
        // drifts one slot from what the peer expects. Concretely: a peer
        // that negotiates eth/68 + snap/1 places snap at 0x21, so its
        // AccountRange response slot is 0x22. If we send GetAccountRange
        // at 0x22 (because we computed snap = 0x10 + 18), the peer's
        // dispatcher tries to RLP-decode our request as an AccountRange
        // response, fails immediately, and drops the connection without
        // ever replying. Hence the version-aware lookup.
        private static int GetMessageCount(string name, int version) => name switch
        {
            // eth/68: IDs 0x00-0x10 (17 slots; 0x0b-0x0e are unused).
            // eth/69+: adds BlockRangeUpdate at 0x11, so 18 slots.
            "eth" => version >= 69 ? 18 : 17,
            // snap/1 has 8 IDs (0x00-0x07).
            "snap" => 8,
            // les/4: IDs 0x00-0x17 (24 slots).
            "les" => 24,
            _ => 0
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
