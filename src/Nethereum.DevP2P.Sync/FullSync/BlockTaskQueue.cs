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

namespace Nethereum.DevP2P.Sync.FullSync
{
    public sealed class BlockTaskQueue
    {
        public enum TaskStage
        {
            Pending,
            Reserved,
            Delivered
        }

        public sealed class BlockTask
        {
            public BlockHeader Header { get; init; } = null!;
            public byte[] Hash { get; init; } = null!;
            public ulong BlockNumber { get; init; }

            public TaskStage BodyStage { get; internal set; } = TaskStage.Pending;
            public TaskStage ReceiptStage { get; internal set; } = TaskStage.Pending;

            public BlockBody? Body { get; set; }
            public List<Receipt>? Receipts { get; set; }

            public Guid? ReservedByBodyPeer { get; internal set; }
            public Guid? ReservedByReceiptPeer { get; internal set; }
            public DateTime? BodyReservedAtUtc { get; internal set; }
            public DateTime? ReceiptReservedAtUtc { get; internal set; }

            internal bool IsReady => BodyStage == TaskStage.Delivered && ReceiptStage == TaskStage.Delivered;
        }

        public sealed class BodyReservation
        {
            public Guid PeerId { get; init; }
            public IReadOnlyList<BlockHeader> Headers { get; init; } = Array.Empty<BlockHeader>();
            public IReadOnlyList<byte[]> Hashes { get; init; } = Array.Empty<byte[]>();
            public int Count => Headers.Count;
        }

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

        private readonly object _lock = new();
        private readonly SortedDictionary<ulong, BlockTask> _tasks = new();

        private static readonly TimeSpan LackingMarkTtl = TimeSpan.FromMinutes(2);

        private readonly Dictionary<string, Dictionary<Guid, DateTime>> _lackingPeers = new();

        private readonly Dictionary<Guid, HashSet<ulong>> _bodyReservations = new();
        private readonly Dictionary<Guid, HashSet<ulong>> _receiptReservations = new();
        private readonly Dictionary<Guid, int> _bodyOpenCount = new();
        private readonly Dictionary<Guid, int> _receiptOpenCount = new();

        private readonly int _maxInFlightPerPeer;

        private readonly IBlockRootsProvider _rootsProvider;
        private readonly Func<DateTime> _utcNow;

        private ulong _persistCursor;
        private int _pendingCount;

        public Action<string> DiagnosticLog;

        private readonly Channel<bool> _bodyWake = Channel.CreateBounded<bool>(
            new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });
        private readonly Channel<bool> _receiptWake = Channel.CreateBounded<bool>(
            new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });
        private readonly Channel<bool> _persistWake = Channel.CreateBounded<bool>(
            new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });

        public BlockTaskQueue(
            IBlockRootsProvider rootsProvider,
            ulong initialCursor,
            IChainActivations? activations = null,
            int maxInFlightPerPeer = 1,
            Func<DateTime> utcNow = null)
        {
            _rootsProvider = rootsProvider ?? throw new ArgumentNullException(nameof(rootsProvider));
            _persistCursor = initialCursor;
            _maxInFlightPerPeer = maxInFlightPerPeer < 1 ? 1 : maxInFlightPerPeer;
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        private static void DecrementOpen(Dictionary<Guid, int> counts, Guid peerId)
        {
            if (!counts.TryGetValue(peerId, out var n)) return;
            if (n <= 1) counts.Remove(peerId);
            else counts[peerId] = n - 1;
        }

        private void ReleaseBodyBookLocked(BodyReservation reservation)
        {
            if (_bodyReservations.TryGetValue(reservation.PeerId, out var book))
            {
                if (reservation.Headers != null)
                    foreach (var h in reservation.Headers)
                        book.Remove(NumberOf(h));
                if (book.Count == 0) _bodyReservations.Remove(reservation.PeerId);
            }
            DecrementOpen(_bodyOpenCount, reservation.PeerId);
        }

        private void ReleaseReceiptBookLocked(ReceiptReservation reservation)
        {
            if (_receiptReservations.TryGetValue(reservation.PeerId, out var book))
            {
                if (reservation.Headers != null)
                    foreach (var h in reservation.Headers)
                        book.Remove(NumberOf(h));
                if (book.Count == 0) _receiptReservations.Remove(reservation.PeerId);
            }
            DecrementOpen(_receiptOpenCount, reservation.PeerId);
        }

        public int Pending
        {
            get { lock (_lock) return _pendingCount; }
        }

        public ulong PersistCursor
        {
            get { lock (_lock) return _persistCursor; }
        }

        public ChannelReader<bool> BodyWorkAvailable => _bodyWake.Reader;
        public ChannelReader<bool> ReceiptWorkAvailable => _receiptWake.Reader;
        public ChannelReader<bool> PersistableAvailable => _persistWake.Reader;


        private static ulong NumberOf(BlockHeader header)
        {
            var number = header.BlockNumber;
            if (!number.FitsInULong)
                throw new ArgumentException("header block number does not fit in 64 bits", nameof(header));
            return (ulong)number;
        }

        public void EnqueueHeaderWithBody(BlockHeader header, byte[] hash, BlockBody body)
        {
            if (header is null) throw new ArgumentNullException(nameof(header));
            if (hash is null || hash.Length != 32) throw new ArgumentException("hash must be 32 bytes", nameof(hash));

            var blockNumber = NumberOf(header);
            lock (_lock)
            {
                if (_tasks.ContainsKey(blockNumber)) return;
                _tasks[blockNumber] = new BlockTask
                {
                    Header = header,
                    Hash = hash,
                    BlockNumber = blockNumber,
                    Body = body,
                    BodyStage = TaskStage.Delivered
                };
                _pendingCount++;
            }
            _receiptWake.Writer.TryWrite(true);
        }

        public void EnqueueHeader(BlockHeader header, byte[] hash)
        {
            if (header is null) throw new ArgumentNullException(nameof(header));
            if (hash is null || hash.Length != 32) throw new ArgumentException("hash must be 32 bytes", nameof(hash));

            var blockNumber = NumberOf(header);
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
            _bodyWake.Writer.TryWrite(true);
            _receiptWake.Writer.TryWrite(true);
        }


        public BodyReservation ReserveBodies(Guid peerId, int capacity)
        {
            if (capacity <= 0)
                return new BodyReservation { PeerId = peerId };

            var headers = new List<BlockHeader>(capacity);
            var hashes = new List<byte[]>(capacity);

            lock (_lock)
            {
                if (_bodyOpenCount.GetValueOrDefault(peerId) >= _maxInFlightPerPeer)
                    return new BodyReservation { PeerId = peerId };

                foreach (var (blockNumber, task) in _tasks)
                {
                    if (headers.Count >= capacity) break;
                    if (task.BodyStage != TaskStage.Pending) continue;
                    if (IsLacking(task.Hash, peerId)) continue;

                    task.BodyStage = TaskStage.Reserved;
                    task.ReservedByBodyPeer = peerId;
                    task.BodyReservedAtUtc = _utcNow();
                    headers.Add(task.Header);
                    hashes.Add(task.Hash);

                    if (!_bodyReservations.TryGetValue(peerId, out var set))
                        _bodyReservations[peerId] = set = new HashSet<ulong>();
                    set.Add(blockNumber);
                }

                if (headers.Count > 0)
                    _bodyOpenCount[peerId] = _bodyOpenCount.GetValueOrDefault(peerId) + 1;
            }

            return new BodyReservation
            {
                PeerId = peerId,
                Headers = headers,
                Hashes = hashes
            };
        }

        public ReceiptReservation ReserveReceipts(Guid peerId, int capacity)
        {
            if (capacity <= 0)
                return new ReceiptReservation { PeerId = peerId };

            var headers = new List<BlockHeader>(capacity);
            var hashes = new List<byte[]>(capacity);

            lock (_lock)
            {
                if (_receiptOpenCount.GetValueOrDefault(peerId) >= _maxInFlightPerPeer)
                    return new ReceiptReservation { PeerId = peerId };

                foreach (var (blockNumber, task) in _tasks)
                {
                    if (headers.Count >= capacity) break;
                    if (task.ReceiptStage != TaskStage.Pending) continue;
                    if (IsLacking(task.Hash, peerId)) continue;

                    task.ReceiptStage = TaskStage.Reserved;
                    task.ReservedByReceiptPeer = peerId;
                    task.ReceiptReservedAtUtc = _utcNow();
                    headers.Add(task.Header);
                    hashes.Add(task.Hash);

                    if (!_receiptReservations.TryGetValue(peerId, out var set))
                        _receiptReservations[peerId] = set = new HashSet<ulong>();
                    set.Add(blockNumber);
                }

                if (headers.Count > 0)
                    _receiptOpenCount[peerId] = _receiptOpenCount.GetValueOrDefault(peerId) + 1;
            }

            return new ReceiptReservation
            {
                PeerId = peerId,
                Headers = headers,
                Hashes = hashes
            };
        }


        public BodyDeliveryResult DeliverBodies(BodyReservation reservation, IList<BlockBody> bodies)
        {
            if (reservation is null) throw new ArgumentNullException(nameof(reservation));
            if (bodies is null) bodies = Array.Empty<BlockBody>();

            var bag = BlockBatchValidator.BucketBodiesByContent(bodies, _rootsProvider, includeWithdrawals: true);

            int matched, unmatched, newlyReady;

            lock (_lock)
            {
                (matched, unmatched, newlyReady) = DeliverLocked(
                    reservation.Headers, reservation.PeerId, task => task.ReservedByBodyPeer,
                    (BlockTask task, BlockHeader header, ulong blockNumber, int unmatchedSoFar, out bool ready)
                        => TryAcceptBodyLocked(task, header, blockNumber, bag, out ready));

                ReleaseBodyBookLocked(reservation);
            }

            if (matched > 0) _bodyWake.Writer.TryWrite(true);
            if (unmatched > 0) _bodyWake.Writer.TryWrite(true);
            if (newlyReady > 0) _persistWake.Writer.TryWrite(true);

            return new BodyDeliveryResult { Matched = matched, Unmatched = unmatched };
        }

        private delegate bool TryAcceptLocked(
            BlockTask task, BlockHeader header, ulong blockNumber, int unmatchedSoFar, out bool newlyReady);

        private (int Matched, int Unmatched, int NewlyReady) DeliverLocked(
            IReadOnlyList<BlockHeader> headers, Guid peerId, Func<BlockTask, Guid?> reservedBy, TryAcceptLocked accept)
        {
            int matched = 0, unmatched = 0, newlyReady = 0;
            List<byte[]> unmatchedHashes = null;

            foreach (var header in headers)
            {
                var blockNumber = NumberOf(header);
                if (!_tasks.TryGetValue(blockNumber, out var task)) continue;
                if (reservedBy(task) != peerId) continue;

                if (accept(task, header, blockNumber, unmatched, out var ready))
                {
                    matched++;
                    if (ready) newlyReady++;
                }
                else
                {
                    (unmatchedHashes ??= new List<byte[]>()).Add(task.Hash);
                    unmatched++;
                }
            }

            if (matched == 0 && unmatchedHashes != null)
                foreach (var h in unmatchedHashes)
                    MarkLacking(h, peerId);

            return (matched, unmatched, newlyReady);
        }

        private int ReleaseLocked(
            IReadOnlyList<BlockHeader> headers, Func<BlockTask, bool> isReservedBy, Action<BlockTask> unreserve)
        {
            int released = 0;
            foreach (var header in headers)
            {
                var blockNumber = NumberOf(header);
                if (_tasks.TryGetValue(blockNumber, out var task) && isReservedBy(task))
                {
                    unreserve(task);
                    released++;
                }
            }
            return released;
        }

        private bool TryAcceptBodyLocked(
            BlockTask task,
            BlockHeader header,
            ulong blockNumber,
            Dictionary<string, Queue<BlockBody>> bag,
            out bool newlyReady)
        {
            newlyReady = false;
            var key = BlockBatchValidator.ContentKeyOf(header, includeWithdrawals: true);
            if (bag.TryGetValue(key, out var q) && q.Count > 0)
            {
                task.Body = q.Dequeue();
                task.BodyStage = TaskStage.Delivered;
                task.ReservedByBodyPeer = null;
                newlyReady = task.IsReady && task.BlockNumber == _persistCursor;
                return true;
            }

            if (blockNumber == _persistCursor)
                DiagnosticLog?.Invoke(
                    $"cursor BODY unmatched block {blockNumber}: need txRoot:uncles:withdrawals {key}; " +
                    $"peer served {bag.Count} bodies computing to [{string.Join(" | ", bag.Keys)}]");
            task.BodyStage = TaskStage.Pending;
            task.ReservedByBodyPeer = null;
            return false;
        }

        public ReceiptDeliveryResult DeliverReceipts(
            ReceiptReservation reservation, IList<List<Receipt>> receipts)
        {
            if (reservation is null) throw new ArgumentNullException(nameof(reservation));
            if (receipts is null) receipts = Array.Empty<List<Receipt>>();

            var bag = BlockBatchValidator.BucketReceiptsByRoot(receipts, _rootsProvider);

            int matched, unmatched, newlyReady;

            lock (_lock)
            {
                (matched, unmatched, newlyReady) = DeliverLocked(
                    reservation.Headers, reservation.PeerId, task => task.ReservedByReceiptPeer,
                    (BlockTask task, BlockHeader header, ulong blockNumber, int unmatchedSoFar, out bool ready)
                        => TryAcceptReceiptsLocked(task, header, blockNumber, receipts, bag, unmatchedSoFar, out ready));

                ReleaseReceiptBookLocked(reservation);
            }

            if (matched > 0 || unmatched > 0) _receiptWake.Writer.TryWrite(true);
            if (newlyReady > 0) _persistWake.Writer.TryWrite(true);

            return new ReceiptDeliveryResult { Matched = matched, Unmatched = unmatched };
        }

        private bool TryAcceptReceiptsLocked(
            BlockTask task,
            BlockHeader header,
            ulong blockNumber,
            IList<List<Receipt>> receipts,
            Dictionary<string, Queue<int>> bag,
            int unmatchedSoFar,
            out bool newlyReady)
        {
            newlyReady = false;
            var key = header.ReceiptHash.ToHex();
            if (bag.TryGetValue(key, out var q) && q.Count > 0)
            {
                task.Receipts = receipts[q.Dequeue()] ?? new List<Receipt>();
                task.ReceiptStage = TaskStage.Delivered;
                task.ReservedByReceiptPeer = null;
                newlyReady = task.IsReady && task.BlockNumber == _persistCursor;
                return true;
            }

            if (blockNumber == _persistCursor || unmatchedSoFar < 3)
                DiagnosticLog?.Invoke(
                    $"RECEIPTS unmatched block {blockNumber}: need {key}; expected_txs=" +
                    $"{(task.Body?.Transactions != null ? task.Body.Transactions.Count.ToString() : "?")}; " +
                    $"peer served {receipts.Count} lists counts=[{string.Join(",", System.Linq.Enumerable.Select(receipts, r => r?.Count ?? 0))}] " +
                    $"roots=[{string.Join(" | ", bag.Keys)}]");
            task.ReceiptStage = TaskStage.Pending;
            task.ReservedByReceiptPeer = null;
            return false;
        }


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
                _bodyOpenCount.Remove(peerId);
                _receiptOpenCount.Remove(peerId);

                List<string> emptyLackingKeys = null;
                foreach (var entry in _lackingPeers)
                {
                    if (entry.Value.Remove(peerId) && entry.Value.Count == 0)
                        (emptyLackingKeys ??= new List<string>()).Add(entry.Key);
                }
                if (emptyLackingKeys != null)
                    foreach (var key in emptyLackingKeys)
                        _lackingPeers.Remove(key);
            }

            if (released > 0)
            {
                _bodyWake.Writer.TryWrite(true);
                _receiptWake.Writer.TryWrite(true);
            }
        }

        public void ReleaseBodyReservation(BodyReservation reservation)
        {
            if (reservation?.Headers == null || reservation.Headers.Count == 0) return;
            lock (_lock)
            {
                ReleaseLocked(
                    reservation.Headers,
                    task => task.BodyStage == TaskStage.Reserved && task.ReservedByBodyPeer == reservation.PeerId,
                    task =>
                    {
                        task.BodyStage = TaskStage.Pending;
                        task.ReservedByBodyPeer = null;
                    });
                ReleaseBodyBookLocked(reservation);
            }
        }

        public void ReleaseReceiptReservation(ReceiptReservation reservation)
        {
            if (reservation?.Headers == null || reservation.Headers.Count == 0) return;
            lock (_lock)
            {
                ReleaseLocked(
                    reservation.Headers,
                    task => task.ReceiptStage == TaskStage.Reserved && task.ReservedByReceiptPeer == reservation.PeerId,
                    task =>
                    {
                        task.ReceiptStage = TaskStage.Pending;
                        task.ReservedByReceiptPeer = null;
                    });
                ReleaseReceiptBookLocked(reservation);
            }
        }

        public int ReclaimStaleReservations(TimeSpan ttl)
        {
            int reclaimed = 0;
            var cutoff = _utcNow() - ttl;
            HashSet<Guid> bodyPeers = null, receiptPeers = null;
            lock (_lock)
            {
                foreach (var kv in _tasks)
                {
                    var task = kv.Value;
                    if (task.BodyStage == TaskStage.Reserved
                        && task.BodyReservedAtUtc is DateTime bAt && bAt <= cutoff)
                    {
                        if (task.ReservedByBodyPeer is Guid bp) (bodyPeers ??= new HashSet<Guid>()).Add(bp);
                        task.BodyStage = TaskStage.Pending;
                        task.ReservedByBodyPeer = null;
                        task.BodyReservedAtUtc = null;
                        reclaimed++;
                    }
                    if (task.ReceiptStage == TaskStage.Reserved
                        && task.ReceiptReservedAtUtc is DateTime rAt && rAt <= cutoff)
                    {
                        if (task.ReservedByReceiptPeer is Guid rp) (receiptPeers ??= new HashSet<Guid>()).Add(rp);
                        task.ReceiptStage = TaskStage.Pending;
                        task.ReservedByReceiptPeer = null;
                        task.ReceiptReservedAtUtc = null;
                        reclaimed++;
                    }
                }
                if (bodyPeers != null)
                    foreach (var p in bodyPeers) { _bodyReservations.Remove(p); _bodyOpenCount.Remove(p); }
                if (receiptPeers != null)
                    foreach (var p in receiptPeers) { _receiptReservations.Remove(p); _receiptOpenCount.Remove(p); }
            }
            if (reclaimed > 0)
            {
                _bodyWake.Writer.TryWrite(true);
                _receiptWake.Writer.TryWrite(true);
            }
            return reclaimed;
        }


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


        private bool IsLacking(byte[] hash, Guid peerId)
        {
            var key = hash.ToHex();
            if (!_lackingPeers.TryGetValue(key, out var marks)) return false;
            if (!marks.TryGetValue(peerId, out var markedAt)) return false;

            if (_utcNow() - markedAt < LackingMarkTtl) return true;

            marks.Remove(peerId);
            if (marks.Count == 0) _lackingPeers.Remove(key);
            return false;
        }

        private void MarkLacking(byte[] hash, Guid peerId)
        {
            var key = hash.ToHex();
            if (!_lackingPeers.TryGetValue(key, out var marks))
                _lackingPeers[key] = marks = new Dictionary<Guid, DateTime>();
            marks[peerId] = _utcNow();
        }
    }
}
