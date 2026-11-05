using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Net;
using Nethereum.DevP2P.Netutil;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model.Enr;
using Nethereum.Signer.Enr;

namespace Nethereum.DevP2P.Discv5
{
    public sealed class Discv5RequestTracker : IDisposable
    {
        public const int MaxNodesEnrsPerRequest = 64;

        public const int MaxNodesChunksPerRequest = 16;

        private const int MinNodeUdpPort = 1024;

        private abstract class PendingEntry
        {
            public string Key;
            public CancellationTokenSource Cts;
            public CancellationTokenRegistration LinkedRegistration;
            public abstract void Cancel();
            public abstract void Fail(Exception ex);
        }

        private sealed class PingEntry : PendingEntry
        {
            public TaskCompletionSource<Discv5PongMessage> Tcs;
            public override void Cancel() => Tcs.TrySetCanceled();
            public override void Fail(Exception ex) => Tcs.TrySetException(ex);
        }

        private sealed class FindNodeEntry : PendingEntry
        {
            public TaskCompletionSource<List<EnrRecord>> Tcs;
            public ulong ExpectedTotal;
            public int Received;
            public List<EnrRecord> Accum;
            public byte[] RemoteNodeId;
            public HashSet<uint> Distances;
            public IPAddress SenderAddress;
            public HashSet<string> Seen = new HashSet<string>(StringComparer.Ordinal);
            public override void Cancel() => Tcs.TrySetCanceled();
            public override void Fail(Exception ex) => Tcs.TrySetException(ex);
        }

        private sealed class TalkRequestEntry : PendingEntry
        {
            public TaskCompletionSource<byte[]> Tcs;
            public override void Cancel() => Tcs.TrySetCanceled();
            public override void Fail(Exception ex) => Tcs.TrySetException(ex);
        }

        private readonly ConcurrentDictionary<string, PendingEntry> _pending = new();
        private bool _disposed;

        public Task<Discv5PongMessage> RegisterPing(
            byte[] remoteNodeId, byte[] requestId, TimeSpan timeout, CancellationToken ct)
        {
            var key = MakeKey(remoteNodeId, requestId);
            var tcs = new TaskCompletionSource<Discv5PongMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            var entry = new PingEntry { Key = key, Tcs = tcs };
            return RegisterPending(remoteNodeId, requestId, key, entry, tcs, timeout, ct);
        }

        public Task<List<EnrRecord>> RegisterFindNode(
            byte[] remoteNodeId, byte[] requestId, ulong expectedTotalHint,
            TimeSpan timeout, CancellationToken ct,
            IEnumerable<uint> requestedDistances = null, IPAddress senderAddress = null)
        {
            var key = MakeKey(remoteNodeId, requestId);
            var tcs = new TaskCompletionSource<List<EnrRecord>>(TaskCreationOptions.RunContinuationsAsynchronously);
            var entry = new FindNodeEntry
            {
                Key = key,
                Tcs = tcs,
                ExpectedTotal = expectedTotalHint == 0 ? 1 : expectedTotalHint,
                Received = 0,
                Accum = new List<EnrRecord>(),
                RemoteNodeId = remoteNodeId,
                Distances = requestedDistances == null ? null : new HashSet<uint>(requestedDistances),
                SenderAddress = senderAddress
            };
            return RegisterPending(remoteNodeId, requestId, key, entry, tcs, timeout, ct);
        }

        public Task<byte[]> RegisterTalkRequest(
            byte[] remoteNodeId, byte[] requestId, TimeSpan timeout, CancellationToken ct)
        {
            var key = MakeKey(remoteNodeId, requestId);
            var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            var entry = new TalkRequestEntry { Key = key, Tcs = tcs };
            return RegisterPending(remoteNodeId, requestId, key, entry, tcs, timeout, ct);
        }

        private Task<TResult> RegisterPending<TEntry, TResult>(
            byte[] remoteNodeId, byte[] requestId, string key, TEntry entry,
            TaskCompletionSource<TResult> tcs, TimeSpan timeout, CancellationToken ct)
            where TEntry : PendingEntry
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            if (timeout > TimeSpan.Zero) cts.CancelAfter(timeout);
            entry.Cts = cts;
            entry.LinkedRegistration = cts.Token.Register(() =>
            {
                if (_pending.TryRemove(key, out _))
                    tcs.TrySetCanceled();
            });
            if (!_pending.TryAdd(key, entry))
            {
                cts.Dispose();
                throw new InvalidOperationException(
                    $"Duplicate discv5 request id for node {remoteNodeId.ToHex()}: {requestId.ToHex()}");
            }
            return tcs.Task;
        }

        public bool CompletePong(byte[] remoteNodeId, Discv5PongMessage pong)
        {
            if (pong == null) return false;
            return CompleteSingle<PingEntry, Discv5PongMessage>(
                remoteNodeId, pong.RequestId, pong, e => e.Tcs);
        }

        public bool CompleteNodesChunk(byte[] remoteNodeId, Discv5NodesMessage nodes)
            => CompleteNodesChunk(remoteNodeId, nodes, out _);

        public bool CompleteNodesChunk(byte[] remoteNodeId, Discv5NodesMessage nodes, out List<EnrRecord> accepted)
        {
            accepted = new List<EnrRecord>();
            if (nodes == null) return false;
            var key = MakeKey(remoteNodeId, nodes.RequestId);
            if (!_pending.TryGetValue(key, out var entry)) return false;
            if (entry is not FindNodeEntry find) return false;

            lock (find)
            {
                if (find.Received == 0)
                {
                    ulong claimed = nodes.Total == 0 ? 1UL : (ulong)nodes.Total;
                    find.ExpectedTotal = claimed > (ulong)MaxNodesChunksPerRequest
                        ? (ulong)MaxNodesChunksPerRequest
                        : claimed;
                }

                if (nodes.Records != null)
                {
                    foreach (var encoded in nodes.Records)
                    {
                        if (find.Accum.Count >= MaxNodesEnrsPerRequest) break;
                        if (encoded == null || encoded.Length == 0) continue;
                        if (!TryAcceptNodeRecord(find, encoded, out var enr)) continue;
                        find.Accum.Add(enr);
                        accepted.Add(enr);
                    }
                }

                find.Received++;

                if ((ulong)find.Received >= find.ExpectedTotal ||
                    find.Accum.Count >= MaxNodesEnrsPerRequest)
                {
                    if (_pending.TryRemove(key, out _))
                    {
                        find.Tcs.TrySetResult(find.Accum);
                        Cleanup(find);
                    }
                }
            }
            return true;
        }

        private static bool TryAcceptNodeRecord(FindNodeEntry find, byte[] encoded, out EnrRecord enr)
        {
            enr = null;
            try { enr = EnrRecordEncoder.Decode(encoded); }
            catch (Exception) { return false; }
            if (!EnrRecordSigner.Verify(enr)) return false;

            if (find.SenderAddress != null && !HasRelayableEndpoint(enr, find.SenderAddress)) return false;

            var nodeId = Discv5Crypto.ComputeNodeId(enr.Secp256k1);
            if (find.Distances != null
                && !find.Distances.Contains(Discv5RoutingTable.LogDistance(find.RemoteNodeId, nodeId)))
                return false;
            return find.Seen.Add(nodeId.ToHex());
        }

        private static bool HasRelayableEndpoint(EnrRecord enr, IPAddress sender)
        {
            var ip = enr.IP4 ?? enr.IP6;
            var udpPort = enr.IP4 != null ? enr.UdpPort : enr.UdpPort6 ?? enr.UdpPort;
            if (ip == null || udpPort == null || udpPort.Value <= MinNodeUdpPort) return false;
            return ip.IsValidRelayTarget(sender);
        }

        public bool CompleteTalkResp(byte[] remoteNodeId, Discv5TalkRespMessage resp)
        {
            if (resp == null) return false;
            return CompleteSingle<TalkRequestEntry, byte[]>(
                remoteNodeId, resp.RequestId, resp.Response ?? Array.Empty<byte>(), e => e.Tcs);
        }

        private bool CompleteSingle<TEntry, TResult>(
            byte[] remoteNodeId, byte[] requestId, TResult result,
            Func<TEntry, TaskCompletionSource<TResult>> tcsSelector)
            where TEntry : PendingEntry
        {
            var key = MakeKey(remoteNodeId, requestId);
            if (!_pending.TryRemove(key, out var entry)) return false;
            if (entry is TEntry typed)
            {
                tcsSelector(typed).TrySetResult(result);
                Cleanup(entry);
                return true;
            }
            _pending.TryAdd(key, entry);
            return false;
        }

        public bool IsPending(byte[] remoteNodeId, byte[] requestId)
            => _pending.ContainsKey(MakeKey(remoteNodeId, requestId));

        public int PendingCount => _pending.Count;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var kvp in _pending)
            {
                kvp.Value.Cancel();
                Cleanup(kvp.Value);
            }
            _pending.Clear();
        }

        private static void Cleanup(PendingEntry entry)
        {
            try { entry.LinkedRegistration.Dispose(); } catch { }
            try { entry.Cts.Dispose(); } catch { }
        }

        private static string MakeKey(byte[] remoteNodeId, byte[] requestId)
        {
            if (remoteNodeId == null) throw new ArgumentNullException(nameof(remoteNodeId));
            if (requestId == null) requestId = Array.Empty<byte>();
            return remoteNodeId.ToHex() + "|" + requestId.ToHex();
        }
    }
}
