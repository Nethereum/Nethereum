using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Discv5;
using Nethereum.DevP2P.Dns;
using Nethereum.Model.Enr;
using Nethereum.Signer;
using Nethereum.Signer.Enr;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.SpecTests.Dns
{
    public class EnrTreeResolverTests
    {
        [Fact]
        public async Task ResolveEnrsAsync_NonExistentDomain_ReturnsEmptyList()
        {
            var resolver = new EnrTreeResolver(_ => { });
            const string url =
                "enrtree://AKA3AM6LPBYEUDMVNU3BSVQJ5AD45Y7YPOHJLEF6W26QOE4VTUDPE@invalid.test.example.invalid";

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var result = await resolver.ResolveEnrsAsync(
                url, TimeSpan.FromMilliseconds(500), maxLeaves: 10, cts.Token);

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task ResolveEnrsAsync_Cancelled_ReturnsEmptyListWithoutThrowing()
        {
            var resolver = new EnrTreeResolver(_ => { });
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var result = await resolver.ResolveEnrsAsync(
                EnrTreeResolver.MainnetEnrTree, TimeSpan.FromMilliseconds(100),
                maxLeaves: 10, cts.Token);

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

        private static string Base32(byte[] data)
        {
            var sb = new StringBuilder();
            var buffer = 0;
            var bits = 0;
            foreach (var b in data)
            {
                buffer = (buffer << 8) | b;
                bits += 8;
                while (bits >= 5)
                {
                    bits -= 5;
                    sb.Append(Alphabet[(buffer >> bits) & 31]);
                }
                buffer &= (1 << bits) - 1;
            }
            if (bits > 0) sb.Append(Alphabet[(buffer << (5 - bits)) & 31]);
            return sb.ToString();
        }

        private static string LabelOf(string record)
        {
            var digest = new Sha3Keccack().CalculateHash(Encoding.UTF8.GetBytes(record));
            return Base32(digest.Take(16).ToArray());
        }

        private static string NewEnr(EthECKey nodeKey, string ip, int tcp, bool corruptSignature = false)
        {
            var enr = new EnrRecord { Sequence = 1 };
            enr.Pairs["id"] = Encoding.ASCII.GetBytes("v4");
            enr.Pairs["secp256k1"] = nodeKey.GetPubKey(true);
            enr.Pairs["ip"] = System.Net.IPAddress.Parse(ip).GetAddressBytes();
            enr.Pairs["tcp"] = new[] { (byte)(tcp >> 8), (byte)tcp };
            EnrRecordSigner.Sign(enr, nodeKey);
            if (corruptSignature) enr.Signature[5] ^= 0x01;
            return EnrRecordEncoder.ToUrl(enr);
        }

        private static string SignedRoot(EthECKey treeKey, string e, string l, ulong seq)
        {
            var payload = "enrtree-root:v1 e=" + e + (l == null ? "" : " l=" + l) + " seq=" + seq;
            var digest = new Sha3Keccack().CalculateHash(Encoding.ASCII.GetBytes(payload));
            var sig = treeKey.SignAndCalculateV(digest);
            var bytes = new byte[65];
            Buffer.BlockCopy(sig.R.PadBytes(32), 0, bytes, 0, 32);
            Buffer.BlockCopy(sig.S.PadBytes(32), 0, bytes, 32, 32);
            bytes[64] = (byte)(sig.V[0] >= 27 ? sig.V[0] - 27 : sig.V[0]);
            return payload + " sig=" + Base64UrlConvert.Encode(bytes);
        }

        private sealed class Publisher
        {
            public Dictionary<string, string> Zone { get; } = new Dictionary<string, string>();

            public Func<string, TimeSpan, CancellationToken, Task<List<string>>> Resolver =>
                (name, _, __) => Task.FromResult(
                    Zone.TryGetValue(name, out var txt) ? new List<string> { txt } : new List<string>());

            public string Add(string domain, string record)
            {
                var label = LabelOf(record);
                Zone[label + "." + domain] = record;
                return label;
            }

            public string Publish(string domain, EthECKey treeKey, ulong seq, string[] enrs, string[] links = null,
                bool nestedBranch = false)
            {
                var leafLabels = enrs.Select(e => Add(domain, e)).ToArray();
                var branch = Add(domain, "enrtree-branch:" + string.Join(",", leafLabels));
                var eRoot = nestedBranch ? Add(domain, "enrtree-branch:" + branch) : branch;
                string lRoot = null;
                if (links != null)
                {
                    var linkLabels = links.Select(l => Add(domain, l)).ToArray();
                    lRoot = Add(domain, "enrtree-branch:" + string.Join(",", linkLabels));
                }
                Zone[domain] = SignedRoot(treeKey, eRoot, lRoot, seq);
                return Url(treeKey, domain);
            }

            public static string Url(EthECKey treeKey, string domain) =>
                "enrtree://" + Base32(treeKey.GetPubKey(true)) + "@" + domain;
        }

        private static EthECKey NewKey() => EthECKey.GenerateKey();

        private static Task<List<EnrRecord>> Resolve(EnrTreeResolver resolver, string url) =>
            resolver.ResolveEnrsAsync(url, TimeSpan.FromSeconds(1), 100, CancellationToken.None);

        private static string EnodeOf(string enrUrl) =>
            Discv5PeerDiscoveryService.ConvertEnrToEnode(EnrRecordEncoder.ParseUrl(enrUrl));

        private static string[] Enodes(List<EnrRecord> records) =>
            records.Select(r => Discv5PeerDiscoveryService.ConvertEnrToEnode(r)).ToArray();

        [Fact]
        public async Task Given_an_honest_signed_tree_with_a_link_When_resolved_Then_exactly_its_enodes_are_returned()
        {
            var publisher = new Publisher();
            var otherKey = NewKey();
            var honest = NewEnr(NewKey(), "1.2.3.4", 30303);
            var linked = NewEnr(NewKey(), "5.6.7.8", 30304);
            publisher.Publish("other.test", otherKey, 1, new[] { linked });
            var url = publisher.Publish("main.test", NewKey(), 1, new[] { honest },
                new[] { Publisher.Url(otherKey, "other.test") });

            var resolver = new EnrTreeResolver(_ => { }, publisher.Resolver);
            var enodes = await resolver.ResolveAsync(url, TimeSpan.FromSeconds(1), 100, CancellationToken.None);

            Assert.Equal(new[] { EnodeOf(honest), EnodeOf(linked) }, enodes);
        }

        [Fact]
        public async Task Given_a_different_validly_signed_enr_under_the_same_label_When_resolved_Then_it_is_not_returned()
        {
            var publisher = new Publisher();
            var url = publisher.Publish("main.test", NewKey(), 1, new[] { NewEnr(NewKey(), "1.2.3.4", 30303) });
            var leafName = publisher.Zone.Keys.Single(k => publisher.Zone[k].StartsWith("enr:"));
            publisher.Zone[leafName] = NewEnr(NewKey(), "9.9.9.9", 30303);

            var result = await Resolve(new EnrTreeResolver(_ => { }, publisher.Resolver), url);

            Assert.Empty(result);
        }

        [Fact]
        public async Task Given_a_substituted_branch_record_When_resolved_Then_its_subtree_is_not_walked()
        {
            var publisher = new Publisher();
            var url = publisher.Publish("main.test", NewKey(), 1, new[] { NewEnr(NewKey(), "1.2.3.4", 30303) },
                nestedBranch: true);
            var evilLeaf = publisher.Add("main.test", NewEnr(NewKey(), "9.9.9.9", 30303));
            var innerBranchName = publisher.Zone.Keys.Single(k =>
                publisher.Zone[k].StartsWith("enrtree-branch:") &&
                publisher.Zone.TryGetValue(publisher.Zone[k].Substring("enrtree-branch:".Length) + ".main.test", out var child) &&
                child.StartsWith("enr:") &&
                !publisher.Zone[k].EndsWith(evilLeaf));
            publisher.Zone[innerBranchName] = "enrtree-branch:" + evilLeaf;

            var result = await Resolve(new EnrTreeResolver(_ => { }, publisher.Resolver), url);

            Assert.Empty(result);
        }

        [Fact]
        public async Task Given_a_substituted_enrtree_link_When_resolved_Then_it_is_not_followed()
        {
            var publisher = new Publisher();
            var otherKey = NewKey();
            var evilKey = NewKey();
            var honest = NewEnr(NewKey(), "1.2.3.4", 30303);
            publisher.Publish("other.test", otherKey, 1, new[] { NewEnr(NewKey(), "5.6.7.8", 30304) });
            publisher.Publish("evil.test", evilKey, 1, new[] { NewEnr(NewKey(), "9.9.9.9", 30305) });
            var url = publisher.Publish("main.test", NewKey(), 1, new[] { honest },
                new[] { Publisher.Url(otherKey, "other.test") });
            var linkName = publisher.Zone.Keys.Single(k => publisher.Zone[k].StartsWith("enrtree://"));
            publisher.Zone[linkName] = Publisher.Url(evilKey, "evil.test");

            var result = await Resolve(new EnrTreeResolver(_ => { }, publisher.Resolver), url);

            Assert.Equal(new[] { EnodeOf(honest) }, Enodes(result));
        }

        [Fact]
        public async Task Given_an_enr_matching_its_label_with_a_bad_node_signature_When_resolved_Then_it_is_not_returned()
        {
            var publisher = new Publisher();
            var url = publisher.Publish("main.test", NewKey(), 1,
                new[] { NewEnr(NewKey(), "1.2.3.4", 30303, corruptSignature: true) });

            var result = await Resolve(new EnrTreeResolver(_ => { }, publisher.Resolver), url);

            Assert.Empty(result);
        }

        [Fact]
        public async Task Given_a_root_signed_with_the_wrong_key_When_resolved_Then_nothing_is_returned()
        {
            var publisher = new Publisher();
            var url = publisher.Publish("main.test", NewKey(), 1, new[] { NewEnr(NewKey(), "1.2.3.4", 30303) });
            var wrongUrl = Publisher.Url(NewKey(), "main.test");

            var honest = await Resolve(new EnrTreeResolver(_ => { }, publisher.Resolver), url);
            var wrong = await Resolve(new EnrTreeResolver(_ => { }, publisher.Resolver), wrongUrl);

            Assert.Single(honest);
            Assert.Empty(wrong);
        }

        [Fact]
        public async Task Given_a_resolver_that_saw_seq_40_When_a_root_with_seq_39_is_served_Then_it_is_ignored()
        {
            var publisher = new Publisher();
            var treeKey = NewKey();
            var current = NewEnr(NewKey(), "1.2.3.4", 30303);
            var stale = NewEnr(NewKey(), "5.6.7.8", 30304);
            var resolver = new EnrTreeResolver(_ => { }, publisher.Resolver);

            var url = publisher.Publish("main.test", treeKey, 40, new[] { current });
            var first = await Resolve(resolver, url);
            publisher.Publish("main.test", treeKey, 39, new[] { stale });
            var second = await Resolve(resolver, url);

            Assert.Equal(new[] { EnodeOf(current) }, Enodes(first));
            Assert.Empty(second);
        }

        [Fact]
        public async Task Given_a_resolver_that_saw_seq_40_When_a_root_with_seq_40_is_served_Then_it_is_accepted()
        {
            var publisher = new Publisher();
            var treeKey = NewKey();
            var first = NewEnr(NewKey(), "1.2.3.4", 30303);
            var second = NewEnr(NewKey(), "5.6.7.8", 30304);
            var resolver = new EnrTreeResolver(_ => { }, publisher.Resolver);

            var url = publisher.Publish("main.test", treeKey, 40, new[] { first });
            await Resolve(resolver, url);
            publisher.Publish("main.test", treeKey, 40, new[] { second });
            var result = await Resolve(resolver, url);

            Assert.Equal(new[] { EnodeOf(second) }, Enodes(result));
        }
    }
}
