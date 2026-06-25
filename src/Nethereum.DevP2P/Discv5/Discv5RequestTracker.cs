using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model.Enr;

namespace Nethereum.DevP2P.Discv5
{
    /// <summary>
    /// Tracks outbound discv5 requests that expect a response. Keyed by
    /// <c>(remoteNodeId, requestId)</c>. A request is completed by the listener
    /// when the matching Pong / Nodes arrives, or cancelled by the linked
    /// timeout / external cancellation token.
    /// <para>
    /// Per discv5-wire.md §"Common Message Types" the request-id is 0-8 bytes;
    /// we key by its hex representation to keep the dictionary lookup cheap.
    /// </para>
    /// </summary>
    public sealed class Discv5RequestTracker : IDisposable
    {
        /// <summary>
        /// Cap on the cumulative ENRs accumulated across NODES chunks for a
        /// single FindNode request. Defends against a malicious peer claiming
        /// a huge <c>total</c> field.
        /// </summary>
        public const int MaxNodesEnrsPerRequest = 64;

        /// <summary>
        /// Cap on the number of NODES chunks accepted for a single FindNode.
        /// </summary>
        public const int MaxNodesChunksPerRequest = 16;

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

        /// <summary>
        /// Register an outbound Ping. Resolves when the matching Pong arrives or
        /// rejects on timeout / cancellation.
        /// </summary>
        public Task<Discv5PongMessage> RegisterPing(
            byte[] remoteNodeId, byte[] requestId, TimeSpan timeout, CancellationToken ct)
        {
            var key = MakeKey(remoteNodeId, requestId);
            var tcs = new TaskCompletionSource<Discv5PongMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            if (timeout > TimeSpan.Zero) cts.CancelAfter(timeout);
            var entry = new PingEntry { Key = key, Tcs = tcs, Cts = cts };
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

        /// <summary>
        /// Register an outbound FindNode. Resolves with the aggregated ENR list
        /// once <paramref name="expectedTotalHint"/> NODES chunks have arrived,
        /// or on timeout / cancellation.
        /// </summary>
        /// <remarks>
        /// The <paramref name="expectedTotalHint"/> is advisory — the peer fills
        /// the authoritative <c>total</c> field in the first NODES reply and the
        /// tracker rebinds to it on the first chunk.
        /// </remarks>
        public Task<List<EnrRecord>> RegisterFindNode(
            byte[] remoteNodeId, byte[] requestId, ulong expectedTotalHint,
            TimeSpan timeout, CancellationToken ct)
        {
            var key = MakeKey(remoteNodeId, requestId);
            var tcs = new TaskCompletionSource<List<EnrRecord>>(TaskCreationOptions.RunContinuationsAsynchronously);
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            if (timeout > TimeSpan.Zero) cts.CancelAfter(timeout);
            var entry = new FindNodeEntry
            {
                Key = key,
                Tcs = tcs,
                Cts = cts,
                ExpectedTotal = expectedTotalHint == 0 ? 1 : expectedTotalHint,
                Received = 0,
                Accum = new List<EnrRecord>()
            };
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

        /// <summary>
        /// Register an outbound TalkReq. Resolves with the peer's TalkResp
        /// response bytes, or rejects on timeout / cancellation.
        /// Per discv5-wire.md §"TALKREQ", the response is a single packet so
        /// the tracker completes on first matching TalkResp.
        /// </summary>
        public Task<byte[]> RegisterTalkRequest(
            byte[] remoteNodeId, byte[] requestId, TimeSpan timeout, CancellationToken ct)
        {
            var key = MakeKey(remoteNodeId, requestId);
            var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            if (timeout > TimeSpan.Zero) cts.CancelAfter(timeout);
            var entry = new TalkRequestEntry { Key = key, Tcs = tcs, Cts = cts };
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

        /// <summary>
        /// Notify the tracker that a Pong arrived. Returns true if a pending
        /// Ping matched; false otherwise (unsolicited / duplicate).
        /// </summary>
        public bool CompletePong(byte[] remoteNodeId, Discv5PongMessage pong)
        {
            if (pong == null) return false;
            var key = MakeKey(remoteNodeId, pong.RequestId);
            if (!_pending.TryRemove(key, out var entry)) return false;
            if (entry is PingEntry ping)
            {
                ping.Tcs.TrySetResult(pong);
                Cleanup(entry);
                return true;
            }
            // Wrong shape for this request id — re-add and return false.
            _pending.TryAdd(key, entry);
            return false;
        }

        /// <summary>
        /// Notify the tracker that a NODES chunk arrived. Appends its ENRs to
        /// the accumulator, rebinds <c>ExpectedTotal</c> to the chunk's total
        /// field, and completes the awaiting task once the full count has been
        /// received. Returns true if a pending FindNode matched.
        /// </summary>
        public bool CompleteNodesChunk(byte[] remoteNodeId, Discv5NodesMessage nodes)
        {
            if (nodes == null) return false;
            var key = MakeKey(remoteNodeId, nodes.RequestId);
            if (!_pending.TryGetValue(key, out var entry)) return false;
            if (entry is not FindNodeEntry find) return false;

            lock (find)
            {
                // First chunk authoritative for total. Clamp to defend against
                // a peer claiming an absurd count.
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
                        EnrRecord enr;
                        try { enr = EnrRecordEncoder.Decode(encoded); }
                        catch (Exception) { continue; }
                        find.Accum.Add(enr);
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

        /// <summary>
        /// Notify the tracker that a TalkResp arrived. Returns true if a
        /// pending TalkReq matched; false otherwise (unsolicited / duplicate).
        /// </summary>
        public bool CompleteTalkResp(byte[] remoteNodeId, Discv5TalkRespMessage resp)
        {
            if (resp == null) return false;
            var key = MakeKey(remoteNodeId, resp.RequestId);
            if (!_pending.TryRemove(key, out var entry)) return false;
            if (entry is TalkRequestEntry talk)
            {
                talk.Tcs.TrySetResult(resp.Response ?? Array.Empty<byte>());
                Cleanup(entry);
                return true;
            }
            _pending.TryAdd(key, entry);
            return false;
        }

        /// <summary>True if a pending request exists for the (nodeId, requestId) pair.</summary>
        public bool IsPending(byte[] remoteNodeId, byte[] requestId)
            => _pending.ContainsKey(MakeKey(remoteNodeId, requestId));

        /// <summary>Number of currently outstanding requests.</summary>
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
            try { entry.LinkedRegistration.Dispose(); } catch { /* registration already gone */ }
            try { entry.Cts.Dispose(); } catch { /* cts already disposed */ }
        }

        private static string MakeKey(byte[] remoteNodeId, byte[] requestId)
        {
            if (remoteNodeId == null) throw new ArgumentNullException(nameof(remoteNodeId));
            if (requestId == null) requestId = Array.Empty<byte>();
            return remoteNodeId.ToHex() + "|" + requestId.ToHex();
        }
    }
}
