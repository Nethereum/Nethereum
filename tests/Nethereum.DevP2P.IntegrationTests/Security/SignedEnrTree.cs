using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model.Enr;
using Nethereum.Signer;
using Nethereum.Signer.Enr;
using Nethereum.Util;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    internal sealed class SignedEnrTree
    {
        private readonly ConcurrentDictionary<string, string> _records = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private int _queries;

        public EthECKey Key { get; } = EthECKey.GenerateKey();
        public string Domain { get; }
        public int Queries => Volatile.Read(ref _queries);
        public string Url => $"enrtree://{Base32(Key.GetPubKey(true))}@{Domain}";

        public SignedEnrTree(string domain) { Domain = domain; }

        public static string Base32(byte[] data)
        {
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
            var sb = new StringBuilder();
            int buffer = 0, bits = 0;
            foreach (var b in data)
            {
                buffer = (buffer << 8) | b;
                bits += 8;
                while (bits >= 5)
                {
                    sb.Append(alphabet[(buffer >> (bits - 5)) & 31]);
                    bits -= 5;
                }
            }
            if (bits > 0) sb.Append(alphabet[(buffer << (5 - bits)) & 31]);
            return sb.ToString();
        }

        private static string Base64Url(byte[] data) => Convert.ToBase64String(data).Replace('+', '-').Replace('/', '_').TrimEnd('=');

        public string Add(string entry, int hashBytes = 16)
        {
            var digest = new Sha3Keccack().CalculateHash(Encoding.UTF8.GetBytes(entry));
            var label = Base32(digest.Take(hashBytes).ToArray());
            _records[label] = entry;
            return label;
        }

        public string AddBranch(params string[] childLabels) => Add("enrtree-branch:" + string.Join(",", childLabels));

        public string AddEnr(EthECKey key, string ip = "8.8.4.4", int port = 30303)
        {
            var enr = new EnrRecord { Sequence = 1 };
            enr.Pairs["ip"] = System.Net.IPAddress.Parse(ip).GetAddressBytes();
            enr.Pairs["tcp"] = new[] { (byte)(port >> 8), (byte)port };
            enr.Pairs["udp"] = new[] { (byte)(port >> 8), (byte)port };
            EnrRecordSigner.Sign(enr, key);
            return Add(EnrRecordEncoder.ToUrl(enr));
        }

        public void SetRoot(string eLabel, string lLabel, ulong seq = 1, string version = "v1")
        {
            var content = $"enrtree-root:{version} e={eLabel} l={lLabel} seq={seq}";
            var digest = new Sha3Keccack().CalculateHash(Encoding.ASCII.GetBytes(content));
            var sig = Key.SignAndCalculateV(digest);
            var sigBytes = new byte[65];
            Buffer.BlockCopy(sig.R.PadBytes(32), 0, sigBytes, 0, 32);
            Buffer.BlockCopy(sig.S.PadBytes(32), 0, sigBytes, 32, 32);
            sigBytes[64] = (byte)(sig.V[0] - 27);
            _records["@root"] = content + " sig=" + Base64Url(sigBytes);
        }

        public string Lookup(string name)
        {
            Interlocked.Increment(ref _queries);
            if (name.Equals(Domain, StringComparison.OrdinalIgnoreCase))
                return _records.TryGetValue("@root", out var root) ? root : null;
            var suffix = "." + Domain;
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                var label = name.Substring(0, name.Length - suffix.Length);
                if (_records.TryGetValue(label, out var record)) return record;
            }
            return null;
        }

        public static Func<string, TimeSpan, CancellationToken, Task<List<string>>> Resolver(params SignedEnrTree[] trees)
            => async (name, timeout, ct) =>
            {
                await Task.Yield();
                foreach (var tree in trees)
                {
                    if (!name.EndsWith(tree.Domain, StringComparison.OrdinalIgnoreCase)) continue;
                    var text = tree.Lookup(name);
                    if (text != null) return new List<string> { text };
                }
                throw new InvalidOperationException("NXDOMAIN " + name);
            };
    }
}
