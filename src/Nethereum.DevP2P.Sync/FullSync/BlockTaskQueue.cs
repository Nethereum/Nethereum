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

            public BlockBody? Body { get; internal set; }
            public List<Receipt>? Receipts { get; internal set; }

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

        private static readonly byte[] EmptyTrieRoot =
            "56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".HexToByteArray();

        private static readonly byte[] EmptyUnclesHash =
            "1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347".HexToByteArray();

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
        private readonly Sha3Keccack _keccak = new();
        private readonly IChainActivations? _activations;
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
            _activations = activations;
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
                        book.Remove((ulong)(long)h.BlockNumber.ToBigInteger());
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
                        book.Remove((ulong)(long)h.BlockNumber.ToBigInteger());
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
            List<byte[]> unmatchedHashes = null;

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
                        if (blockNumber == _persistCursor)
                            DiagnosticLog?.Invoke(
                                $"cursor BODY unmatched block {blockNumber}: need txRoot:uncles {key}; " +
                                $"peer served {bag.Count} bodies computing to [{string.Join(" | ", bag.Keys)}]");
                        task.BodyStage = TaskStage.Pending;
                        task.ReservedByBodyPeer = null;
                        (unmatchedHashes ??= new List<byte[]>()).Add(task.Hash);
                        unmatched++;
                    }
                }

                if (matched == 0 && unmatchedHashes != null)
                    foreach (var h in unmatchedHashes)
                        MarkLacking(h, reservation.PeerId);

                ReleaseBodyBookLocked(reservation);
            }

            if (matched > 0) _bodyWake.Writer.TryWrite(true);
            if (unmatched > 0) _bodyWake.Writer.TryWrite(true);
            if (newlyReady > 0) _persistWake.Writer.TryWrite(true);

            return new BodyDeliveryResult { Matched = matched, Unmatched = unmatched };
        }

        public ReceiptDeliveryResult DeliverReceipts(
            ReceiptReservation reservation, IList<List<Receipt>> receipts)
        {
            if (reservation is null) throw new ArgumentNullException(nameof(reservation));
            if (receipts is null) receipts = Array.Empty<List<Receipt>>();

            var bag = BucketByComputedReceiptsRoot(receipts);

            int matched = 0, unmatched = 0, newlyReady = 0;
            List<byte[]> unmatchedHashes = null;

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
                        if (blockNumber == _persistCursor || unmatched < 3)
                            DiagnosticLog?.Invoke(
                                $"RECEIPTS unmatched block {blockNumber}: need {key}; expected_txs=" +
                                $"{(task.Body?.Transactions != null ? task.Body.Transactions.Count.ToString() : "?")}; " +
                                $"peer served {receipts.Count} lists counts=[{string.Join(",", System.Linq.Enumerable.Select(receipts, r => r?.Count ?? 0))}] " +
                                $"roots=[{string.Join(" | ", bag.Keys)}]");
                        task.ReceiptStage = TaskStage.Pending;
                        task.ReservedByReceiptPeer = null;
                        (unmatchedHashes ??= new List<byte[]>()).Add(task.Hash);
                        unmatched++;
                        continue;
                    }

                    task.Receipts = receipts[matchedIdx] ?? new List<Receipt>();
                    task.ReceiptStage = TaskStage.Delivered;
                    task.ReservedByReceiptPeer = null;
                    matched++;
                    if (task.IsReady && task.BlockNumber == _persistCursor) newlyReady++;
                }

                if (matched == 0 && unmatchedHashes != null)
                    foreach (var h in unmatchedHashes)
                        MarkLacking(h, reservation.PeerId);

                ReleaseReceiptBookLocked(reservation);
            }

            if (matched > 0 || unmatched > 0) _receiptWake.Writer.TryWrite(true);
            if (newlyReady > 0) _persistWake.Writer.TryWrite(true);

            return new ReceiptDeliveryResult
            {
                Matched = matched,
                Unmatched = unmatched,
            };
        }

        private Dictionary<string, Queue<int>> BucketByComputedReceiptsRoot(IList<List<Receipt>> receipts)
        {
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

            return bag;
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
            int released = 0;
            lock (_lock)
            {
                foreach (var header in reservation.Headers)
                {
                    var blockNumber = (ulong)(long)header.BlockNumber.ToBigInteger();
                    if (_tasks.TryGetValue(blockNumber, out var task)
                        && task.BodyStage == TaskStage.Reserved
                        && task.ReservedByBodyPeer == reservation.PeerId)
                    {
                        task.BodyStage = TaskStage.Pending;
                        task.ReservedByBodyPeer = null;
                        released++;
                    }
                }
                ReleaseBodyBookLocked(reservation);
            }
            if (released > 0) _bodyWake.Writer.TryWrite(true);
        }

        public void ReleaseReceiptReservation(ReceiptReservation reservation)
        {
            if (reservation?.Headers == null || reservation.Headers.Count == 0) return;
            int released = 0;
            lock (_lock)
            {
                foreach (var header in reservation.Headers)
                {
                    var blockNumber = (ulong)(long)header.BlockNumber.ToBigInteger();
                    if (_tasks.TryGetValue(blockNumber, out var task)
                        && task.ReceiptStage == TaskStage.Reserved
                        && task.ReservedByReceiptPeer == reservation.PeerId)
                    {
                        task.ReceiptStage = TaskStage.Pending;
                        task.ReservedByReceiptPeer = null;
                        released++;
                    }
                }
                ReleaseReceiptBookLocked(reservation);
            }
            if (released > 0) _receiptWake.Writer.TryWrite(true);
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

        private byte[] ComputeUnclesHash(IList<BlockHeader> uncles)
        {
            var encoded = new byte[uncles.Count][];
            for (int i = 0; i < uncles.Count; i++)
                encoded[i] = BlockHeaderEncoder.Current.Encode(uncles[i]);
            return _keccak.CalculateHash(Nethereum.RLP.RLP.EncodeList(encoded));
        }
    }
}
