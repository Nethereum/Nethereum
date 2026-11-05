using System;
using Nethereum.Documentation;
using System.Collections.Generic;
using System.Linq;
using Nethereum.DevP2P;

namespace Nethereum.ChainNode.Hosting.Configuration
{
    public static class ChainNodeEnodeValidator
    {
        [NethereumDocExample(DocSection.ChainInfrastructure, "chain-node-config", "Refuse a malformed configured enode before the node starts")]
        public static void RefuseMalformed(ChainNodeConfig node, string configPath)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            RefuseMalformed(ConfiguredEnodesByKey(node, configPath));
        }

        public static void RefuseMalformed(IEnumerable<KeyValuePair<string, string>> enodesByConfigKey)
        {
            var malformed = (enodesByConfigKey ?? Enumerable.Empty<KeyValuePair<string, string>>())
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Value) && !EnodeUrl.TryParse(entry.Value, out _))
                .Select(entry => $"{entry.Key} = '{entry.Value}'")
                .ToArray();
            if (malformed.Length == 0) return;

            throw new InvalidOperationException(
                "Refusing to start: malformed enode in configuration: " + string.Join("; ", malformed)
                + ". Each entry must be one enode://<128-hex-node-id>@<host>:<port>.");
        }

        private static IEnumerable<KeyValuePair<string, string>> ConfiguredEnodesByKey(ChainNodeConfig node, string configPath)
        {
            yield return new KeyValuePair<string, string>($"{configPath}:Sync:FollowPeerEnode", node.Sync.FollowPeerEnode ?? string.Empty);
            foreach (var entry in Indexed(node.Network.TrustedPeers, $"{configPath}:Network:TrustedPeers"))
                yield return entry;
            foreach (var entry in Indexed(node.Network.TrustedBootnodes, $"{configPath}:Network:TrustedBootnodes"))
                yield return entry;
        }

        private static IEnumerable<KeyValuePair<string, string>> Indexed(string[] enodes, string key) =>
            (enodes ?? Array.Empty<string>()).Select((enode, i) => new KeyValuePair<string, string>($"{key}:{i}", enode));
    }
}
