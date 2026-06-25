using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model.Enr;
using Nethereum.Signer;
using Nethereum.Util;

namespace Nethereum.DevP2P.Dns
{
    /// <summary>
    /// EIP-1459 DNS-based ENR tree resolver. Walks the tree of TXT records
    /// served from a single domain (e.g.
    /// <c>enrtree://AKA3AM6LPBYEUDMVNU3BSVQJ5AD45Y7YPOHJLEF6W26QOE4VTUDPE@all.mainnet.ethdisco.net</c>)
    /// and emits one enode URL per ENR leaf. Geth uses this for cold-start
    /// peer bootstrap; it scales far better than hardcoded bootnodes because
    /// the EF refreshes the tree continuously with new live peers. Verifies
    /// the root signature per EIP-1459 to prevent DNS poisoning, and walks
    /// link-trees so curated lists from Erigon/Nethermind/Lodestar are picked
    /// up too.
    /// </summary>
    public sealed class EnrTreeResolver
    {
        public const string MainnetEnrTree =
            "enrtree://AKA3AM6LPBYEUDMVNU3BSVQJ5AD45Y7YPOHJLEF6W26QOE4VTUDPE@all.mainnet.ethdisco.net";

        /// <summary>
        /// All ENR trees Ethereum Foundation publishes for mainnet. Resolving
        /// all of these gives ~3x more candidate enodes than resolving only
        /// <see cref="MainnetEnrTree"/>. SyncNode's RotatingPeerSession
        /// resolves all of these during DNS seed phase + backoff re-seed.
        /// References: https://github.com/eth-clients/ethereum-mainnet-bootstrap
        /// </summary>
        public static readonly string[] MainnetEnrTrees = new[]
        {
            // The "all" tree — every protocol, every shard
            "enrtree://AKA3AM6LPBYEUDMVNU3BSVQJ5AD45Y7YPOHJLEF6W26QOE4VTUDPE@all.mainnet.ethdisco.net",
            // Snap-protocol-only tree — peers actively serving snap/1
            "enrtree://AKA3AM6LPBYEUDMVNU3BSVQJ5AD45Y7YPOHJLEF6W26QOE4VTUDPE@snap.mainnet.ethdisco.net",
            // Les-protocol tree — light-client-friendly servers (smaller but
            // typically more inbound-receptive)
            "enrtree://AKA3AM6LPBYEUDMVNU3BSVQJ5AD45Y7YPOHJLEF6W26QOE4VTUDPE@les.mainnet.ethdisco.net",
        };

        private static readonly IPEndPoint[] PublicFallbackDnsServers = new[]
        {
            new IPEndPoint(IPAddress.Parse("1.1.1.1"), 53),
            new IPEndPoint(IPAddress.Parse("8.8.8.8"), 53),
            new IPEndPoint(IPAddress.Parse("9.9.9.9"), 53)
        };

        /// <summary>
        /// DNS servers to try in order: OS-configured first (works through
        /// corporate / WSL / Hyper-V virtual switch where outbound UDP to
        /// public resolvers is blocked or NATed asymmetrically), then public
        /// fallback. Cached per-process; refreshed when adapters change is
        /// out of scope — restart the process to pick up new DNS config.
        /// </summary>
        private static readonly Lazy<IPEndPoint[]> DnsServers =
            new Lazy<IPEndPoint[]>(GetDnsServers);

        private static IPEndPoint[] GetDnsServers()
        {
            var os = new List<IPEndPoint>();
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up) continue;
                    var dns = nic.GetIPProperties().DnsAddresses;
                    foreach (var addr in dns)
                    {
                        if (addr.AddressFamily != AddressFamily.InterNetwork) continue;
                        if (IPAddress.IsLoopback(addr)) continue;
                        var ep = new IPEndPoint(addr, 53);
                        if (!os.Any(e => e.Address.Equals(ep.Address))) os.Add(ep);
                    }
                }
            }
            catch
            {
                // Adapter enumeration can fail on restricted hosts; just fall back.
            }
            // OS first, then public fallback (de-duplicated).
            foreach (var fb in PublicFallbackDnsServers)
            {
                if (!os.Any(e => e.Address.Equals(fb.Address))) os.Add(fb);
            }
            return os.ToArray();
        }

        private readonly Action<string> _log;

        public EnrTreeResolver(Action<string> log)
        {
            _log = log ?? (_ => { });
        }

        public async Task<List<string>> ResolveAsync(
            string enrtreeUrl, TimeSpan timeout, int maxLeaves, CancellationToken ct)
        {
            var enodes = new List<string>();
            void OnEnrFound(EnrRecord enr)
            {
                var enode = ConvertToEnode(enr);
                if (enode != null) enodes.Add(enode);
            }
            // Cycle-safe link traversal: stop revisiting the same enrtree:// URL
            // (linked trees can form arbitrary graphs).
            var visitedTrees = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var counter = new LeafCounter();
            await ResolveTreeAsync(enrtreeUrl, OnEnrFound, counter, visitedTrees, timeout, maxLeaves, ct);
            return enodes;
        }

        /// <summary>
        /// Resolve an EIP-1459 enrtree URL and return the parsed ENR records (rather
        /// than enode URLs). Use this when the caller needs the full ENR — for
        /// example to seed a discv5 routing table, which keys entries by the
        /// RLP-encoded ENR plus the UDP endpoint.
        /// </summary>
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
            if (!visitedTrees.Add(enrtreeUrl)) return; // cycle

            var (pubkeyBase32, domain) = ParseEnrTreeUrl(enrtreeUrl);
            // Per-tree visited set for branch hashes; trees may legitimately
            // share hash spans across each other so this is scoped per tree.
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Root TXT: "enrtree-root:v1 e=<eHash> l=<lHash> seq=N sig=...".
            var rootTxts = await DnsTxtQueryAsync(domain, timeout, ct);
            var root = rootTxts.FirstOrDefault(t => t.StartsWith("enrtree-root:", StringComparison.Ordinal));
            if (root == null)
            {
                _log($"  enrtree root not found for {domain}");
                return;
            }

            // EIP-1459 signature verification — the root TXT is signed by the
            // key whose hash is in the enrtree:// URL. A poisoned DNS reply
            // would substitute a different tree; verification blocks it.
            if (!VerifyEnrTreeRoot(pubkeyBase32, root))
            {
                _log($"  enrtree root signature INVALID for {domain} — ignoring (likely DNS poisoning or stale URL)");
                return;
            }

            var eHash = ExtractField(root, " e=");
            var lHash = ExtractField(root, " l=");

            // Walk e= (the ENR tree — leaves are enr: records).
            if (eHash != null)
            {
                await WalkSubtreeAsync(eHash, domain, onEnrFound, counter, visited, visitedTrees, timeout, maxLeaves, ct);
            }

            // Walk l= (the link tree — leaves are enrtree:// URLs to other
            // trees, e.g. Erigon / Nethermind / Lodestar curated lists). Each
            // linked tree is resolved into the same collector.
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
                txts = await DnsTxtQueryAsync($"{hash}.{domain}", timeout, ct);
            }
            catch (Exception ex)
            {
                _log($"  enrtree DNS error for {hash}.{domain}: {ex.GetType().Name}");
                return;
            }

            // TXT for the subtree node is one logical string. Concatenate the
            // chunks the resolver may have split it into.
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
                // Link tree leaf: a reference to ANOTHER enrtree:// URL.
                // Recurse with cycle protection via visitedTrees. Lets us pick
                // up Erigon / Nethermind / Lodestar curated peer lists.
                await ResolveTreeAsync(record.Trim(), onEnrFound, counter, visitedTrees, timeout, maxLeaves, ct);
            }
        }

        private static string ConvertToEnode(EnrRecord enr)
        {
            if (enr.Id != "v4") return null;
            var compressed = enr.Secp256k1;
            if (compressed == null || compressed.Length != 33) return null;
            var ip = enr.IP4 ?? enr.IP6;
            if (ip == null) return null;
            var tcp = enr.TcpPort;
            if (tcp == null || tcp == 0) return null;

            byte[] uncompressed;
            try
            {
                // EthECKey accepts a 33-byte compressed pubkey via the
                // (byte[], isPrivate:false) ctor.
                var key = new EthECKey(compressed, false);
                var full = key.GetPubKey(false); // 65 bytes, leading 0x04
                uncompressed = new byte[64];
                Buffer.BlockCopy(full, 1, uncompressed, 0, 64);
            }
            catch
            {
                return null;
            }

            return $"enode://{uncompressed.ToHex()}@{ip}:{tcp}";
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

        // ---- Minimal DNS-over-UDP TXT query (RFC 1035) -----------------------

        private static readonly Random _rng = new Random();

        private async Task<List<string>> DnsTxtQueryAsync(
            string domain, TimeSpan timeout, CancellationToken ct)
        {
            Exception last = null;
            foreach (var server in DnsServers.Value)
            {
                try
                {
                    return await DnsTxtQueryOnceAsync(domain, server, timeout, ct);
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    last = ex;
                }
            }
            throw last ?? new InvalidOperationException("No DNS server reachable");
        }

        private static async Task<List<string>> DnsTxtQueryOnceAsync(
            string domain, IPEndPoint server, TimeSpan timeout, CancellationToken ct)
        {
            var (query, queryId) = BuildTxtQuery(domain);
            using var udp = new UdpClient();
            await udp.SendAsync(query, query.Length, server);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);
            var result = await udp.ReceiveAsync().WaitAsync(cts.Token);
            return ParseTxtResponse(result.Buffer, queryId);
        }

        private static (byte[] Query, ushort Id) BuildTxtQuery(string domain)
        {
            var ms = new System.IO.MemoryStream();
            ushort id;
            lock (_rng) { id = (ushort)_rng.Next(0, 65536); }
            ms.WriteByte((byte)(id >> 8));
            ms.WriteByte((byte)id);
            // Flags: standard query, recursion desired (0x0100).
            ms.WriteByte(0x01); ms.WriteByte(0x00);
            // QDCOUNT=1, ANCOUNT=NSCOUNT=ARCOUNT=0.
            ms.WriteByte(0x00); ms.WriteByte(0x01);
            ms.WriteByte(0x00); ms.WriteByte(0x00);
            ms.WriteByte(0x00); ms.WriteByte(0x00);
            ms.WriteByte(0x00); ms.WriteByte(0x00);
            foreach (var label in domain.Split('.'))
            {
                var bytes = Encoding.ASCII.GetBytes(label);
                ms.WriteByte((byte)bytes.Length);
                ms.Write(bytes, 0, bytes.Length);
            }
            ms.WriteByte(0x00); // null terminator
            ms.WriteByte(0x00); ms.WriteByte(0x10); // QTYPE = 16 (TXT)
            ms.WriteByte(0x00); ms.WriteByte(0x01); // QCLASS = 1 (IN)
            return (ms.ToArray(), id);
        }

        // Off-path UDP attackers can spoof DNS replies if we accept any datagram
        // arriving on our ephemeral port. Validating transaction-id + response
        // length defends against the EIP-1459 root-TXT poisoning vector that would
        // otherwise let an attacker steer dialers at attacker-controlled enodes.
        private static List<string> ParseTxtResponse(byte[] response, ushort expectedId)
        {
            if (response == null || response.Length < 12)
                throw new InvalidOperationException(
                    $"DNS response too short ({response?.Length ?? 0} bytes, header needs 12)");

            int responseId = (response[0] << 8) | response[1];
            if (responseId != expectedId)
                throw new InvalidOperationException(
                    $"DNS transaction-id mismatch (expected 0x{expectedId:X4}, got 0x{responseId:X4}) - possible spoofed reply");

            int offset = 12;
            offset = SkipName(response, offset);
            if (offset + 4 > response.Length)
                throw new InvalidOperationException("DNS response truncated after question");
            offset += 4; // QTYPE + QCLASS

            int ancount = (response[6] << 8) | response[7];
            var results = new List<string>();
            for (int i = 0; i < ancount; i++)
            {
                offset = SkipName(response, offset);
                if (offset + 10 > response.Length)
                    throw new InvalidOperationException("DNS response truncated in answer header");
                int type = (response[offset] << 8) | response[offset + 1];
                offset += 8; // TYPE(2) + CLASS(2) + TTL(4)
                int rdlength = (response[offset] << 8) | response[offset + 1];
                offset += 2;
                if (rdlength < 0 || offset + rdlength > response.Length)
                    throw new InvalidOperationException("DNS response truncated in rdata");

                if (type == 16) // TXT
                {
                    int end = offset + rdlength;
                    var sb = new StringBuilder();
                    while (offset < end)
                    {
                        int len = response[offset++];
                        if (offset + len > end)
                            throw new InvalidOperationException("DNS TXT character-string overruns rdata");
                        sb.Append(Encoding.ASCII.GetString(response, offset, len));
                        offset += len;
                    }
                    results.Add(sb.ToString());
                }
                else
                {
                    offset += rdlength;
                }
            }
            return results;
        }

        private static int SkipName(byte[] data, int offset)
        {
            while (true)
            {
                if (offset >= data.Length)
                    throw new InvalidOperationException("DNS name overruns response");
                byte b = data[offset];
                if (b == 0) return offset + 1;
                if ((b & 0xC0) == 0xC0)
                {
                    if (offset + 1 >= data.Length)
                        throw new InvalidOperationException("DNS compression pointer truncated");
                    return offset + 2;
                }
                offset += b + 1;
            }
        }

        // ---- EIP-1459 signature verification ---------------------------------

        private static bool VerifyEnrTreeRoot(string pubkeyBase32, string rootRecord)
        {
            try
            {
                var sigIdx = rootRecord.IndexOf(" sig=", StringComparison.Ordinal);
                if (sigIdx < 0) return false;
                var signedPayload = rootRecord.Substring(0, sigIdx);
                var sigField = rootRecord.Substring(sigIdx + " sig=".Length).Trim();

                var sigBytes = Base64UrlDecodeNoPad(sigField);
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

        private static byte[] Base64UrlDecodeNoPad(string s)
        {
            var standard = s.Replace('-', '+').Replace('_', '/');
            var padded = standard + new string('=', (4 - standard.Length % 4) % 4);
            return Convert.FromBase64String(padded);
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
