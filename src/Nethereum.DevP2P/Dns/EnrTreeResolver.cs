using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Discv5;
using Nethereum.Documentation;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model.Enr;
using Nethereum.Signer;
using Nethereum.Util;

namespace Nethereum.DevP2P.Dns
{
    public sealed class EnrTreeResolver
    {
        public const string MainnetEnrTree =
            "enrtree://AKA3AM6LPBYEUDMVNU3BSVQJ5AD45Y7YPOHJLEF6W26QOE4VTUDPE@all.mainnet.ethdisco.net";

        public static readonly string[] MainnetEnrTrees = new[]
        {
            "enrtree://AKA3AM6LPBYEUDMVNU3BSVQJ5AD45Y7YPOHJLEF6W26QOE4VTUDPE@all.mainnet.ethdisco.net",
            "enrtree://AKA3AM6LPBYEUDMVNU3BSVQJ5AD45Y7YPOHJLEF6W26QOE4VTUDPE@snap.mainnet.ethdisco.net",
            "enrtree://AKA3AM6LPBYEUDMVNU3BSVQJ5AD45Y7YPOHJLEF6W26QOE4VTUDPE@les.mainnet.ethdisco.net",
        };

        private readonly Action<string> _log;
        private readonly DnsTxtClient _dnsTxtClient;

        public EnrTreeResolver(Action<string> log)
        {
            _log = log ?? (_ => { });
            _dnsTxtClient = new DnsTxtClient();
        }

        [NethereumDocExample(DocSection.DevP2P, "devp2p", "EnrTreeResolver.ResolveAsync — DNS ENR-tree discovery")]
        public async Task<List<string>> ResolveAsync(
            string enrtreeUrl, TimeSpan timeout, int maxLeaves, CancellationToken ct)
        {
            var enodes = new List<string>();
            void OnEnrFound(EnrRecord enr)
            {
                var enode = Discv5PeerDiscoveryService.ConvertEnrToEnode(enr);
                if (enode != null) enodes.Add(enode);
            }
            var visitedTrees = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var counter = new LeafCounter();
            await ResolveTreeAsync(enrtreeUrl, OnEnrFound, counter, visitedTrees, timeout, maxLeaves, ct);
            return enodes;
        }

        public async Task<List<EnrRecord>> ResolveEnrsAsync(
            string enrtreeUrl, TimeSpan timeout, int maxLeaves, CancellationToken ct)
        {
            var enrs = new List<EnrRecord>();
            void OnEnrFound(EnrRecord enr) => enrs.Add(enr);
            var visitedTrees = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var counter = new LeafCounter();
            try
            {
                await ResolveTreeAsync(enrtreeUrl, OnEnrFound, counter, visitedTrees, timeout, maxLeaves, ct);
            }
            catch (OperationCanceledException)
            {
            }
            return enrs;
        }

        private sealed class LeafCounter
        {
            public int Count;
        }

        private async Task ResolveTreeAsync(
            string enrtreeUrl, Action<EnrRecord> onEnrFound, LeafCounter counter,
            HashSet<string> visitedTrees, TimeSpan timeout, int maxLeaves, CancellationToken ct)
        {
            if (counter.Count >= maxLeaves) return;
            if (!visitedTrees.Add(enrtreeUrl)) return;

            var (pubkeyBase32, domain) = ParseEnrTreeUrl(enrtreeUrl);
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var rootTxts = await _dnsTxtClient.QueryTxtAsync(domain, timeout, ct);
            var root = rootTxts.FirstOrDefault(t => t.StartsWith("enrtree-root:", StringComparison.Ordinal));
            if (root == null)
            {
                _log($"  enrtree root not found for {domain}");
                return;
            }

            if (!VerifyEnrTreeRoot(pubkeyBase32, root))
            {
                _log($"  enrtree root signature INVALID for {domain} — ignoring (likely DNS poisoning or stale URL)");
                return;
            }

            var eHash = ExtractField(root, " e=");
            var lHash = ExtractField(root, " l=");

            if (eHash != null)
            {
                await WalkSubtreeAsync(eHash, domain, onEnrFound, counter, visited, visitedTrees, timeout, maxLeaves, ct);
            }

            if (lHash != null && counter.Count < maxLeaves)
            {
                await WalkSubtreeAsync(lHash, domain, onEnrFound, counter, visited, visitedTrees, timeout, maxLeaves, ct);
            }
        }

        private async Task WalkSubtreeAsync(
            string hash, string domain, Action<EnrRecord> onEnrFound, LeafCounter counter,
            HashSet<string> visited, HashSet<string> visitedTrees,
            TimeSpan timeout, int maxLeaves, CancellationToken ct)
        {
            if (counter.Count >= maxLeaves) return;
            if (!visited.Add(hash)) return;

            List<string> txts;
            try
            {
                txts = await _dnsTxtClient.QueryTxtAsync($"{hash}.{domain}", timeout, ct);
            }
            catch (Exception ex)
            {
                _log($"  enrtree DNS error for {hash}.{domain}: {ex.GetType().Name}");
                return;
            }

            var record = string.Concat(txts);
            if (record.StartsWith("enrtree-branch:", StringComparison.Ordinal))
            {
                var inner = record.Substring("enrtree-branch:".Length);
                foreach (var h in inner.Split(','))
                {
                    if (counter.Count >= maxLeaves) break;
                    ct.ThrowIfCancellationRequested();
                    await WalkSubtreeAsync(h.Trim(), domain, onEnrFound, counter, visited, visitedTrees, timeout, maxLeaves, ct);
                }
            }
            else if (record.StartsWith("enr:", StringComparison.Ordinal))
            {
                try
                {
                    var enr = EnrRecordEncoder.ParseUrl(record);
                    onEnrFound(enr);
                    counter.Count++;
                }
                catch (Exception ex)
                {
                    _log($"  enr decode failed: {ex.GetType().Name}: {ex.Message}");
                }
            }
            else if (record.StartsWith("enrtree://", StringComparison.Ordinal))
            {
                await ResolveTreeAsync(record.Trim(), onEnrFound, counter, visitedTrees, timeout, maxLeaves, ct);
            }
        }

        private static (string PubKey, string Domain) ParseEnrTreeUrl(string url)
        {
            const string prefix = "enrtree://";
            if (!url.StartsWith(prefix, StringComparison.Ordinal))
                throw new ArgumentException("Not an enrtree URL", nameof(url));
            var rest = url.Substring(prefix.Length);
            var at = rest.IndexOf('@');
            return (rest.Substring(0, at), rest.Substring(at + 1));
        }

        private static string ExtractField(string record, string marker)
        {
            var idx = record.IndexOf(marker, StringComparison.Ordinal);
            if (idx < 0) return null;
            idx += marker.Length;
            var end = record.IndexOf(' ', idx);
            return end < 0 ? record.Substring(idx) : record.Substring(idx, end - idx);
        }


        private static bool VerifyEnrTreeRoot(string pubkeyBase32, string rootRecord)
        {
            try
            {
                var sigIdx = rootRecord.IndexOf(" sig=", StringComparison.Ordinal);
                if (sigIdx < 0) return false;
                var signedPayload = rootRecord.Substring(0, sigIdx);
                var sigField = rootRecord.Substring(sigIdx + " sig=".Length).Trim();

                var sigBytes = Base64UrlConvert.Decode(sigField);
                var pubCompressed = Base32DecodeNoPad(pubkeyBase32);
                if (sigBytes.Length != 65 || pubCompressed.Length != 33) return false;

                var digest = new Sha3Keccack().CalculateHash(Encoding.ASCII.GetBytes(signedPayload));
                var r = new byte[32];
                var s = new byte[32];
                Buffer.BlockCopy(sigBytes, 0, r, 0, 32);
                Buffer.BlockCopy(sigBytes, 32, s, 0, 32);

                var key = new EthECKey(pubCompressed, false);
                var sig = EthECDSASignatureFactory.FromComponents(r, s);
                return key.Verify(digest, sig);
            }
            catch
            {
                return false;
            }
        }

        private static byte[] Base32DecodeNoPad(string s)
        {
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
            s = s.ToUpperInvariant();
            var output = new byte[s.Length * 5 / 8];
            int outIdx = 0;
            int buffer = 0;
            int bitsInBuffer = 0;
            foreach (var c in s)
            {
                var idx = alphabet.IndexOf(c);
                if (idx < 0) throw new FormatException($"Invalid base32 char '{c}'");
                buffer = (buffer << 5) | idx;
                bitsInBuffer += 5;
                if (bitsInBuffer >= 8)
                {
                    bitsInBuffer -= 8;
                    output[outIdx++] = (byte)((buffer >> bitsInBuffer) & 0xFF);
                }
            }
            return output;
        }
    }
}
