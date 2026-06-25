using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Channels;
using Nethereum.CoreChain;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Util;

namespace Nethereum.DevP2P.Sync
{
    /// <summary>
    /// Central work queue that decouples block-header production (one
    /// producer) from body and receipt fetching (many per-peer consumers)
    /// while preserving strict cursor-ordered persistence.
    /// <para>
    /// Invariants:
    /// <list type="bullet">
    /// <item>Tasks are inserted in block-number order by the producer and
    /// kept sorted; iterators always walk oldest-first so straggler blocks
    /// can't block forward progress on later blocks that have already
    /// completed.</item>
    /// <item>Each task holds two independent state machines — one for the
    /// block body and one for the receipt list. Either can be reserved,
    /// delivered, or reverted without affecting the other. A block becomes
    /// persist-ready only when both stages reach Delivered.</item>
    /// <item>One in-flight per (peer, stage). A peer cannot hold two body
    /// reservations simultaneously; this keeps backpressure honest and
    /// makes per-peer error handling trivial.</item>
    /// <item>Delivery is content-addressed. Each returned body is matched
    /// to a reserved header by recomputing (TxRoot, UnclesHash); receipts
    /// match by recomputing the receipt-trie root and comparing against
    /// the header's commitment. Unmatched headers revert to Pending and
    /// the responsible peer is added to that block's lacking-peer set so
    /// it won't be chosen again for the same hash.</item>
    /// <item>The persist cursor only advances over a contiguous fully-
    /// ready prefix. Out-of-order delivery is allowed (block N+5 can
    /// finish before block N); the consumer just waits until N is ready
    /// before draining anything.</item>
    /// </list>
    /// </para>
    /// <para>
    /// Concurrency: a single coarse monitor lock covers reserve / deliver
    /// / expire / dequeue. Operations are bounded by per-peer reservation
    /// size (~100 blocks), so the lock is never held long enough to matter.
    /// Three channels (body wake / receipt wake / persist wake) are signaled
    /// outside the lock so workers don't spin.
    /// </para>
    /// </summary>
    public sealed class BlockTaskQueue
    {
        public enum TaskStage
        {
            /// <summary>Header arrived; body/receipt fetch not started.</summary>
            Pending,
            /// <summary>A peer is fetching this task.</summary>
            Reserved,
            /// <summary>Peer returned data that matched the header commitment.</summary>
            Delivered
        }

        public sealed class BlockTask
        {
            public BlockHeader Header { get; init; } = null!;
            public byte[] Hash { get; init; } = null!;
            public ulong BlockNumber { get; init; }

            public TaskStage BodyStage { get; internal set; } = TaskStage.Pending;
            public TaskStage ReceiptStage { get; internal set; } = TaskStage.Pending;

            public BlockBody? Body { get; internal set; }
            public List<Receipt>? Receipts { get; internal set; }

            public Guid? ReservedByBodyPeer { get; internal set; }
            public Guid? ReservedByReceiptPeer { get; internal set; }

            internal bool IsReady => BodyStage == TaskStage.Delivered && ReceiptStage == TaskStage.Delivered;
        }

        /// <summary>Snapshot returned by <see cref="ReserveBodies"/>; carries
        /// the per-peer batch the worker should fetch.</summary>
        public sealed class BodyReservation
        {
            public Guid PeerId { get; init; }
            public IReadOnlyList<BlockHeader> Headers { get; init; } = Array.Empty<BlockHeader>();
            public IReadOnlyList<byte[]> Hashes { get; init; } = Array.Empty<byte[]>();
            public int Count => Headers.Count;
        }

        /// <summary>Snapshot returned by <see cref="ReserveReceipts"/>.</summary>
        public sealed class ReceiptReservation
        {
            public Guid PeerId { get; init; }
            public IReadOnlyList<BlockHeader> Headers { get; init; } = Array.Empty<BlockHeader>();
            public IReadOnlyList<byte[]> Hashes { get; init; } = Array.Empty<byte[]>();
            public int Count => Headers.Count;
        }

        public sealed class BodyDeliveryResult
        {
            public int Matched { get; init; }
            public int Unmatched { get; init; }
        }

        public sealed class ReceiptDeliveryResult
        {
            public int Matched { get; init; }
            public int Unmatched { get; init; }
        }

        private static readonly byte[] EmptyTrieRoot =
            "56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".HexToByteArray();

        private static readonly byte[] EmptyUnclesHash =
            "1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347".HexToByteArray();

        private readonly object _lock = new();
        private readonly SortedDictionary<ulong, BlockTask> _tasks = new();

        // (peer, hash-hex) → known absent. Skipped on future reservations from the same peer.
        private readonly Dictionary<string, HashSet<Guid>> _lackingPeers = new();

        // Per-peer reservation books (block-number sets).
        private readonly Dictionary<Guid, HashSet<ulong>> _bodyReservations = new();
        private readonly Dictionary<Guid, HashSet<ulong>> _receiptReservations = new();

        private readonly IBlockRootsProvider _rootsProvider;
        private readonly Sha3Keccack _keccak = new();
        private readonly IChainActivations? _activations;

        private ulong _persistCursor;
        private int _pendingCount;

        // Wake channels. Writers signal availability (non-blocking, bounded=1 with DropOldest);
        // workers await on Reader.ReadAsync to block until work is available.
        // Bounded=1 collapses many writes into one wake-up — exactly what we want.
        private readonly Channel<bool> _bodyWake = Channel.CreateBounded<bool>(
            new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });
        private readonly Channel<bool> _receiptWake = Channel.CreateBounded<bool>(
            new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });
        private readonly Channel<bool> _persistWake = Channel.CreateBounded<bool>(
            new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });

        /// <param name="rootsProvider">Patricia trie root calculator. Used to
        /// recompute (TxRoot, UnclesHash) per body and the receipt-trie root per
        /// receipt list so we can match each peer response back to a header by
        /// content rather than by request-position. Position-based matching breaks
        /// when peers reply with a subset of what was asked for.</param>
        /// <param name="initialCursor">First block number we want to persist —
        /// typically <c>LastFetchedBody + 1</c>. The persist drain releases blocks
        /// strictly in order starting from this value, so resumes after a kill
        /// remain consistent.</param>
        /// <param name="activations">Optional fork resolver. When provided,
        /// pre-Byzantium receipts use positional pairing instead of root match —
        /// pre-EIP-658 receipts carry a 32-byte post-state-root that most modern
        /// clients have dropped from storage, so the wire root can never match the
        /// canonical commitment. Phase 2 re-execution recomputes those receipts
        /// from the transactions, so accepting the wire bytes positionally here is
        /// safe.</param>
        public BlockTaskQueue(
            IBlockRootsProvider rootsProvider,
            ulong initialCursor,
            IChainActivations? activations = null)
        {
            _rootsProvider = rootsProvider ?? throw new ArgumentNullException(nameof(rootsProvider));
            _persistCursor = initialCursor;
            _activations = activations;
        }

        /// <summary>Total tasks waiting (pending + reserved + delivered-but-not-persisted).</summary>
        public int Pending
        {
            get { lock (_lock) return _pendingCount; }
        }

        /// <summary>Next block number we expect to persist. Drives ordering.</summary>
        public ulong PersistCursor
        {
            get { lock (_lock) return _persistCursor; }
        }

        public ChannelReader<bool> BodyWorkAvailable => _bodyWake.Reader;
        public ChannelReader<bool> ReceiptWorkAvailable => _receiptWake.Reader;
        public ChannelReader<bool> PersistableAvailable => _persistWake.Reader;

        // ---------------------------------------------------------------
        // Producer side — header fetcher calls this as headers arrive
        // ---------------------------------------------------------------

        /// <summary>
        /// Enqueue a fetched header for body + receipt fetch. Caller is responsible
        /// for parent-chain validation BEFORE enqueueing (the queue trusts the
        /// header is canonical). Idempotent on (blockNumber); duplicate enqueue
        /// is a no-op.
        /// </summary>
        public void EnqueueHeader(BlockHeader header, byte[] hash)
        {
            if (header is null) throw new ArgumentNullException(nameof(header));
            if (hash is null || hash.Length != 32) throw new ArgumentException("hash must be 32 bytes", nameof(hash));

            var blockNumber = (ulong)(long)header.BlockNumber.ToBigInteger();
            lock (_lock)
            {
                if (_tasks.ContainsKey(blockNumber)) return;
                _tasks[blockNumber] = new BlockTask
                {
                    Header = header,
                    Hash = hash,
                    BlockNumber = blockNumber
                };
                _pendingCount++;
            }
            // Two wake-ups: body and receipt fetchers both want to know.
            _bodyWake.Writer.TryWrite(true);
            _receiptWake.Writer.TryWrite(true);
        }

        // ---------------------------------------------------------------
        // Reservation side — fetcher workers call ReserveBodies / ReserveReceipts
        // ---------------------------------------------------------------

        /// <summary>
        /// Reserve up to <paramref name="capacity"/> body tasks for this peer,
        /// returning the matched headers + hashes the peer should now fetch.
        /// Walks the queue oldest-first, skipping tasks that are already
        /// reserved or delivered and tasks where this peer has previously
        /// failed to deliver (the lacking set). Refuses entirely if the peer
        /// already holds an open body reservation — keeps the one-in-flight
        /// invariant.
        /// </summary>
        public BodyReservation ReserveBodies(Guid peerId, int capacity)
        {
            if (capacity <= 0)
                return new BodyReservation { PeerId = peerId };

            var headers = new List<BlockHeader>(capacity);
            var hashes = new List<byte[]>(capacity);

            lock (_lock)
            {
                // One-in-flight invariant: a peer holding an open reservation
                // must finish (or be expired) before getting another.
                if (_bodyReservations.TryGetValue(peerId, out var existing) && existing.Count > 0)
                    return new BodyReservation { PeerId = peerId };

                foreach (var (blockNumber, task) in _tasks)
                {
                    if (headers.Count >= capacity) break;
                    if (task.BodyStage != TaskStage.Pending) continue;
                    // Skip blocks this peer has already failed on — saves a
                    // wasted round-trip and keeps the partial-requeue progress.
                    if (IsLacking(task.Hash, peerId)) continue;

                    task.BodyStage = TaskStage.Reserved;
                    task.ReservedByBodyPeer = peerId;
                    headers.Add(task.Header);
                    hashes.Add(task.Hash);

                    if (!_bodyReservations.TryGetValue(peerId, out var set))
                        _bodyReservations[peerId] = set = new HashSet<ulong>();
                    set.Add(blockNumber);
                }
            }

            return new BodyReservation
            {
                PeerId = peerId,
                Headers = headers,
                Hashes = hashes
            };
        }

        /// <summary>Receipt-task analog of <see cref="ReserveBodies"/>.</summary>
        public ReceiptReservation ReserveReceipts(Guid peerId, int capacity)
        {
            if (capacity <= 0)
                return new ReceiptReservation { PeerId = peerId };

            var headers = new List<BlockHeader>(capacity);
            var hashes = new List<byte[]>(capacity);

            lock (_lock)
            {
                if (_receiptReservations.TryGetValue(peerId, out var existing) && existing.Count > 0)
                    return new ReceiptReservation { PeerId = peerId };

                foreach (var (blockNumber, task) in _tasks)
                {
                    if (headers.Count >= capacity) break;
                    if (task.ReceiptStage != TaskStage.Pending) continue;
                    if (IsLacking(task.Hash, peerId)) continue;

                    task.ReceiptStage = TaskStage.Reserved;
                    task.ReservedByReceiptPeer = peerId;
                    headers.Add(task.Header);
                    hashes.Add(task.Hash);

                    if (!_receiptReservations.TryGetValue(peerId, out var set))
                        _receiptReservations[peerId] = set = new HashSet<ulong>();
                    set.Add(blockNumber);
                }
            }

            return new ReceiptReservation
            {
                PeerId = peerId,
                Headers = headers,
                Hashes = hashes
            };
        }

        // ---------------------------------------------------------------
        // Delivery side — fetcher workers deliver responses back
        // ---------------------------------------------------------------

        /// <summary>
        /// Match each delivered body to one of the headers this peer reserved
        /// and advance that block's body stage to Delivered. Matching is
        /// content-addressed: bodies are bucketed by their computed
        /// (TxRoot, UnclesHash) and headers look themselves up in that bucket.
        /// Any reserved header that didn't find a match reverts to Pending and
        /// the peer joins that block's lacking set so the next reservation
        /// skips it. Cleaning up the peer's reservation book is unconditional —
        /// after delivery the peer is free to reserve again.
        /// </summary>
        public BodyDeliveryResult DeliverBodies(BodyReservation reservation, IList<BlockBody> bodies)
        {
            if (reservation is null) throw new ArgumentNullException(nameof(reservation));
            if (bodies is null) bodies = Array.Empty<BlockBody>();

            // Bucket bodies by their computed (TxRoot, UnclesHash). This is
            // the content-addressed signature each header commits to. Two
            // bodies that happen to share a signature (e.g. all empty bodies)
            // end up in the same bucket and are pulled out in arrival order.
            var bag = new Dictionary<string, Queue<BlockBody>>(bodies.Count);
            foreach (var body in bodies)
            {
                var txs = body?.Transactions ?? new List<ISignedTransaction>();
                var uncles = body?.Uncles ?? new List<BlockHeader>();
                var txRoot = _rootsProvider.CalculateTransactionsRoot(txs);
                var unclesHash = uncles.Count == 0 ? EmptyUnclesHash : ComputeUnclesHash(uncles);
                var key = txRoot.ToHex() + ":" + unclesHash.ToHex();
                if (!bag.TryGetValue(key, out var q))
                    bag[key] = q = new Queue<BlockBody>();
                q.Enqueue(body!);
            }

            int matched = 0, unmatched = 0;
            int newlyReady = 0;

            lock (_lock)
            {
                foreach (var header in reservation.Headers)
                {
                    var blockNumber = (ulong)(long)header.BlockNumber.ToBigInteger();
                    if (!_tasks.TryGetValue(blockNumber, out var task)) continue;
                    if (task.ReservedByBodyPeer != reservation.PeerId) continue;

                    var key = header.TransactionsHash.ToHex() + ":" + header.UnclesHash.ToHex();
                    if (bag.TryGetValue(key, out var q) && q.Count > 0)
                    {
                        task.Body = q.Dequeue();
                        task.BodyStage = TaskStage.Delivered;
                        task.ReservedByBodyPeer = null;
                        matched++;
                        if (task.IsReady && task.BlockNumber == _persistCursor) newlyReady++;
                    }
                    else
                    {
                        // Peer didn't return a body for this hash. Revert so
                        // another peer can pick it up, and remember that this
                        // peer is lacking — pestering them again would waste
                        // a round-trip with the same outcome.
                        task.BodyStage = TaskStage.Pending;
                        task.ReservedByBodyPeer = null;
                        MarkLacking(task.Hash, reservation.PeerId);
                        unmatched++;
                    }
                }

                // Peer is now free to reserve again, regardless of how this
                // delivery went.
                _bodyReservations.Remove(reservation.PeerId);
            }

            if (matched > 0) _bodyWake.Writer.TryWrite(true);
            if (unmatched > 0) _bodyWake.Writer.TryWrite(true);
            if (newlyReady > 0) _persistWake.Writer.TryWrite(true);

            return new BodyDeliveryResult { Matched = matched, Unmatched = unmatched };
        }

        /// <summary>
        /// Receipt-stage delivery. Every block — pre- and post-Byzantium —
        /// is matched by computed receipts-trie root against the header's
        /// <c>ReceiptHash</c> commitment. If the
        /// computed root does not match, the receipts for that block are
        /// rejected and the task is reverted to <see cref="TaskStage.Pending"/>;
        /// another peer may supply the canonical bytes on the next retry.
        ///
        /// <para>Note for pre-Byzantium blocks: the wire format committed at
        /// block time hashed over a 32-byte post-state root in the receipt's
        /// first RLP slot. Clients that retain the post-state root
        /// round-trip cleanly through this validator; clients
        /// that compress the slot to a 1-byte status will miss the merkle
        /// check, which is the correct outcome — those bytes are not
        /// canonical and cannot reconstruct the header commitment. Such
        /// blocks must either be served by a canonical-byte peer or
        /// regenerated by re-executing the transactions.</para>
        /// </summary>
        public ReceiptDeliveryResult DeliverReceipts(
            ReceiptReservation reservation, IList<List<Receipt>> receipts)
        {
            if (reservation is null) throw new ArgumentNullException(nameof(reservation));
            if (receipts is null) receipts = Array.Empty<List<Receipt>>();

            // Bucket by computed receipts-root. Per-block lookup keyed on
            // header.ReceiptHash — no positional fallback at any fork.
            var bag = new Dictionary<string, Queue<int>>(receipts.Count);
            for (int j = 0; j < receipts.Count; j++)
            {
                var list = receipts[j] ?? new List<Receipt>();
                var root = list.Count == 0 ? EmptyTrieRoot : _rootsProvider.CalculateReceiptsRoot(list);
                var key = root.ToHex();
                if (!bag.TryGetValue(key, out var q))
                    bag[key] = q = new Queue<int>();
                q.Enqueue(j);
            }

            int matched = 0, unmatched = 0, newlyReady = 0;

            lock (_lock)
            {
                foreach (var header in reservation.Headers)
                {
                    var blockNumber = (ulong)(long)header.BlockNumber.ToBigInteger();
                    if (!_tasks.TryGetValue(blockNumber, out var task)) continue;
                    if (task.ReservedByReceiptPeer != reservation.PeerId) continue;

                    var key = header.ReceiptHash.ToHex();
                    int matchedIdx = -1;
                    if (bag.TryGetValue(key, out var q) && q.Count > 0)
                        matchedIdx = q.Dequeue();

                    if (matchedIdx < 0)
                    {
                        task.ReceiptStage = TaskStage.Pending;
                        task.ReservedByReceiptPeer = null;
                        MarkLacking(task.Hash, reservation.PeerId);
                        unmatched++;
                        continue;
                    }

                    task.Receipts = receipts[matchedIdx] ?? new List<Receipt>();
                    task.ReceiptStage = TaskStage.Delivered;
                    task.ReservedByReceiptPeer = null;
                    matched++;
                    if (task.IsReady && task.BlockNumber == _persistCursor) newlyReady++;
                }

                _receiptReservations.Remove(reservation.PeerId);
            }

            if (matched > 0 || unmatched > 0) _receiptWake.Writer.TryWrite(true);
            if (newlyReady > 0) _persistWake.Writer.TryWrite(true);

            return new ReceiptDeliveryResult
            {
                Matched = matched,
                Unmatched = unmatched,
            };
        }

        // ---------------------------------------------------------------
        // Peer lifecycle — disconnect handling
        // ---------------------------------------------------------------

        /// <summary>
        /// Release all reservations held by this peer (typically on disconnect or
        /// per-request timeout). Reverted tasks return to <see cref="TaskStage.Pending"/>
        /// and become available for other peers to reserve.
        /// </summary>
        public void ReleasePeer(Guid peerId)
        {
            int released = 0;
            lock (_lock)
            {
                if (_bodyReservations.Remove(peerId, out var bodies))
                {
                    foreach (var blockNumber in bodies)
                    {
                        if (_tasks.TryGetValue(blockNumber, out var task)
                            && task.BodyStage == TaskStage.Reserved
                            && task.ReservedByBodyPeer == peerId)
                        {
                            task.BodyStage = TaskStage.Pending;
                            task.ReservedByBodyPeer = null;
                            released++;
                        }
                    }
                }
                if (_receiptReservations.Remove(peerId, out var rcs))
                {
                    foreach (var blockNumber in rcs)
                    {
                        if (_tasks.TryGetValue(blockNumber, out var task)
                            && task.ReceiptStage == TaskStage.Reserved
                            && task.ReservedByReceiptPeer == peerId)
                        {
                            task.ReceiptStage = TaskStage.Pending;
                            task.ReservedByReceiptPeer = null;
                            released++;
                        }
                    }
                }
            }

            if (released > 0)
            {
                _bodyWake.Writer.TryWrite(true);
                _receiptWake.Writer.TryWrite(true);
            }
        }

        // ---------------------------------------------------------------
        // Persistence drain — caller pulls contiguous ready blocks
        // ---------------------------------------------------------------

        /// <summary>
        /// Dequeue up to <paramref name="maxCount"/> ready-to-persist blocks in
        /// strict cursor order. A block is ready only when both body and receipt
        /// are Delivered. Stops at the first gap (out-of-order completion is fine;
        /// we just don't drain past it).
        /// </summary>
        public IList<BlockTask> DequeuePersistable(int maxCount)
        {
            var result = new List<BlockTask>(Math.Min(maxCount, 64));
            lock (_lock)
            {
                while (result.Count < maxCount
                       && _tasks.TryGetValue(_persistCursor, out var task)
                       && task.IsReady)
                {
                    _tasks.Remove(_persistCursor);
                    result.Add(task);
                    _persistCursor++;
                    _pendingCount--;
                }
            }
            return result;
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        private bool IsLacking(byte[] hash, Guid peerId)
        {
            var key = hash.ToHex();
            return _lackingPeers.TryGetValue(key, out var set) && set.Contains(peerId);
        }

        private void MarkLacking(byte[] hash, Guid peerId)
        {
            var key = hash.ToHex();
            if (!_lackingPeers.TryGetValue(key, out var set))
                _lackingPeers[key] = set = new HashSet<Guid>();
            set.Add(peerId);
        }

        private byte[] ComputeUnclesHash(IList<BlockHeader> uncles)
        {
            var encoded = new byte[uncles.Count][];
            for (int i = 0; i < uncles.Count; i++)
                encoded[i] = BlockHeaderEncoder.Current.Encode(uncles[i]);
            return _keccak.CalculateHash(Nethereum.RLP.RLP.EncodeList(encoded));
        }
    }
}
