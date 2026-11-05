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
using Nethereum.Signer.Enr;
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

        public const int MaxTreeDepth = 32;

        private const int MinHashBytes = 12;
        private const int MinQueryBudget = 64;
        private const int QueriesPerLeaf = 4;
        private const string RootPrefix = "enrtree-root:v1 ";
        private const string BranchPrefix = "enrtree-branch:";

        private readonly Action<string> _log;
        private readonly Func<string, TimeSpan, CancellationToken, Task<List<string>>> _queryTxt;
        private readonly Dictionary<string, ulong> _highestSeqByTree = new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);
        private readonly object _seqLock = new object();

        public EnrTreeResolver(Action<string> log)
            : this(log, new DnsTxtClient().QueryTxtAsync)
        {
        }

        public EnrTreeResolver(Action<string> log, Func<string, TimeSpan, CancellationToken, Task<List<string>>> queryTxt)
        {
            _log = log ?? (_ => { });
            _queryTxt = queryTxt ?? throw new ArgumentNullException(nameof(queryTxt));
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
            await ResolveTreesAsync(enrtreeUrl, OnEnrFound, timeout, maxLeaves, ct);
            return enodes;
        }

        public async Task<List<EnrRecord>> ResolveEnrsAsync(
            string enrtreeUrl, TimeSpan timeout, int maxLeaves, CancellationToken ct)
        {
            var enrs = new List<EnrRecord>();
            void OnEnrFound(EnrRecord enr) => enrs.Add(enr);
            try
            {
                await ResolveTreesAsync(enrtreeUrl, OnEnrFound, timeout, maxLeaves, ct);
            }
            catch (OperationCanceledException)
            {
            }
            return enrs;
        }

        private sealed class ResolveState
        {
            public Action<EnrRecord> OnEnrFound;
            public TimeSpan Timeout;
            public int MaxLeaves;
            public long MaxQueries;
            public int Leaves;
            public long Queries;
            public HashSet<string> VisitedTrees = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public Queue<string> PendingTrees = new Queue<string>();

            public bool Exhausted => Leaves >= MaxLeaves || Queries >= MaxQueries;
        }

        private async Task ResolveTreesAsync(
            string enrtreeUrl, Action<EnrRecord> onEnrFound, TimeSpan timeout, int maxLeaves, CancellationToken ct)
        {
            var state = new ResolveState
            {
                OnEnrFound = onEnrFound,
                Timeout = timeout,
                MaxLeaves = maxLeaves,
                MaxQueries = Math.Max(MinQueryBudget, (long)maxLeaves * QueriesPerLeaf)
            };
            state.PendingTrees.Enqueue(enrtreeUrl);
            var isRequestedTree = true;
            while (state.PendingTrees.Count > 0 && !state.Exhausted)
            {
                var url = state.PendingTrees.Dequeue();
                if (!state.VisitedTrees.Add(url)) continue;
                if (isRequestedTree)
                {
                    await ResolveTreeAsync(url, state, ct);
                    isRequestedTree = false;
                    continue;
                }
                try
                {
                    await ResolveTreeAsync(url, state, ct);
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    _log($"  enrtree link {url} failed: {ex.GetType().Name}");
                }
            }
        }

        private async Task ResolveTreeAsync(string enrtreeUrl, ResolveState state, CancellationToken ct)
        {
            if (!TryParseEnrTreeUrl(enrtreeUrl, out var pubkeyBase32, out var domain))
                throw new ArgumentException("Not an enrtree URL", nameof(enrtreeUrl));

            state.Queries++;
            var rootTxts = await _queryTxt(domain, state.Timeout, ct);
            var root = rootTxts.FirstOrDefault(t => t.StartsWith(RootPrefix, StringComparison.Ordinal));
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

            if (!TryAcceptSequence(enrtreeUrl, root, domain)) return;

            var roots = new List<string>();
            var eHash = ExtractField(root, " e=");
            var lHash = ExtractField(root, " l=");
            if (eHash != null) roots.Add(eHash);
            if (lHash != null) roots.Add(lHash);

            await WalkTreeAsync(roots, domain, state, ct);
        }

        private async Task WalkTreeAsync(List<string> roots, string domain, ResolveState state, CancellationToken ct)
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var stack = new Stack<(string Hash, int Depth)>();
            for (var i = roots.Count - 1; i >= 0; i--) stack.Push((roots[i], 0));

            while (stack.Count > 0 && !state.Exhausted)
            {
                ct.ThrowIfCancellationRequested();
                var (hash, depth) = stack.Pop();
                if (depth > MaxTreeDepth)
                {
                    _log($"  enrtree entry {hash}.{domain} is deeper than {MaxTreeDepth} levels - ignoring");
                    continue;
                }
                if (!visited.Add(hash)) continue;

                state.Queries++;
                List<string> txts;
                try
                {
                    txts = await _queryTxt($"{hash}.{domain}", state.Timeout, ct);
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    _log($"  enrtree DNS error for {hash}.{domain}: {ex.GetType().Name}");
                    continue;
                }

                var record = string.Concat(txts);
                if (!MatchesLabel(hash, record))
                {
                    _log($"  enrtree entry {hash}.{domain} does not hash to its label - ignoring");
                    continue;
                }

                if (record.StartsWith(BranchPrefix, StringComparison.Ordinal))
                {
                    var children = record.Substring(BranchPrefix.Length).Split(',');
                    for (var i = children.Length - 1; i >= 0; i--)
                    {
                        var child = children[i].Trim();
                        if (child.Length > 0) stack.Push((child, depth + 1));
                    }
                }
                else if (record.StartsWith("enr:", StringComparison.Ordinal))
                {
                    AcceptEnrLeaf(record, hash, domain, state);
                }
                else if (record.StartsWith("enrtree://", StringComparison.Ordinal))
                {
                    var link = record.Trim();
                    if (TryParseEnrTreeUrl(link, out _, out _))
                        state.PendingTrees.Enqueue(link);
                    else
                        _log($"  enrtree link {hash}.{domain} is malformed - ignoring");
                }
            }
        }

        private void AcceptEnrLeaf(string record, string hash, string domain, ResolveState state)
        {
            try
            {
                var enr = EnrRecordEncoder.ParseUrl(record);
                if (!EnrRecordSigner.Verify(enr))
                {
                    _log($"  enr signature invalid for {hash}.{domain} - ignoring");
                    return;
                }
                state.OnEnrFound(enr);
                state.Leaves++;
            }
            catch (Exception ex)
            {
                _log($"  enr decode failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private bool TryAcceptSequence(string enrtreeUrl, string root, string domain)
        {
            if (!ulong.TryParse(ExtractField(root, " seq="), out var seq))
            {
                _log($"  enrtree root for {domain} has no valid seq - ignoring");
                return false;
            }
            lock (_seqLock)
            {
                if (_highestSeqByTree.TryGetValue(enrtreeUrl, out var highest) && seq < highest)
                {
                    _log($"  enrtree root for {domain} has seq {seq} below seen {highest} - ignoring");
                    return false;
                }
                _highestSeqByTree[enrtreeUrl] = seq;
                return true;
            }
        }

        private static bool MatchesLabel(string label, string record)
        {
            byte[] want;
            try
            {
                want = Base32DecodeNoPad(label);
            }
            catch (FormatException)
            {
                return false;
            }
            if (want.Length < MinHashBytes) return false;
            var digest = new Sha3Keccack().CalculateHash(Encoding.UTF8.GetBytes(record));
            if (want.Length > digest.Length) return false;
            for (var i = 0; i < want.Length; i++)
            {
                if (digest[i] != want[i]) return false;
            }
            return true;
        }

        private static bool TryParseEnrTreeUrl(string url, out string pubKey, out string domain)
        {
            pubKey = null;
            domain = null;
            const string prefix = "enrtree://";
            if (url == null || !url.StartsWith(prefix, StringComparison.Ordinal)) return false;
            var rest = url.Substring(prefix.Length);
            var at = rest.IndexOf('@');
            if (at <= 0 || at == rest.Length - 1) return false;
            pubKey = rest.Substring(0, at);
            domain = rest.Substring(at + 1);
            return true;
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
                return sig.IsLowS && key.Verify(digest, sig);
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
