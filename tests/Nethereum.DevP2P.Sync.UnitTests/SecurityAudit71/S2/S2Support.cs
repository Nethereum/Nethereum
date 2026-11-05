using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;

namespace Nethereum.DevP2P.Sync.UnitTests.SecurityAudit71.S2
{
    internal static class S2Chain
    {
        public static BlockHeader HeaderAt(long number, byte[] parentHash) => new BlockHeader
        {
            BlockNumber = number,
            ParentHash = parentHash,
            Timestamp = 1000 + number,
            UnclesHash = DefaultValues.EMPTY_UNCLES_HASH,
            TransactionsHash = DefaultValues.EMPTY_TRIE_HASH,
            ReceiptHash = DefaultValues.EMPTY_TRIE_HASH
        };

        public static byte[] HashOf(BlockHeader header) => BlockHashCalculator.ForHeader(header);

        public static BlockBody EmptyBody() => new BlockBody
        {
            Transactions = new List<ISignedTransaction>(),
            Uncles = new List<BlockHeader>()
        };
    }

    internal sealed class S2Peer : IEthPeer
    {
        private static int _next;

        public S2Peer(ulong announcedHead, bool trusted = false)
        {
            PeerLatestBlock = announcedHead;
            IsTrusted = trusted;
            Enode = $"enode://{new string('c', 128)}@127.0.0.1:{32000 + Interlocked.Increment(ref _next)}";
        }

        public Guid Id { get; } = Guid.NewGuid();
        public string Enode { get; }
        public string Host => "127.0.0.1";
        public bool IsTrusted { get; }
        public int EthVersion => 71;
        public ulong PeerLatestBlock { get; }
        public uint PeerForkHash => 0;
        public RlpxConnection Connection => null!;
        public event EventHandler<IEthPeer>? Disconnected;
    }

    internal sealed class S2Pool : IPeerPool
    {
        private readonly List<IEthPeer> _peers;
        public int Drops;

        public S2Pool(params IEthPeer[] peers) { _peers = peers.ToList(); }

        public IReadOnlyCollection<IEthPeer> ActivePeers => _peers;
        public int TargetPeerCount => _peers.Count;
        public event EventHandler<IEthPeer>? PeerAdded;
        public event EventHandler<IEthPeer>? PeerRemoved;
        public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
        public Task BanAndDropAsync(string enode, string reason, CancellationToken ct) => Task.CompletedTask;

        public Task DropAsync(Guid peerId, string reason, CancellationToken ct)
        {
            Interlocked.Increment(ref Drops);
            _peers.RemoveAll(p => p.Id == peerId);
            return Task.CompletedTask;
        }

        public void ReportSuccess(Guid peerId) { }
        public Task ClearAllBansAsync() => Task.CompletedTask;
        public ValueTask DisposeAsync() => default;
    }

    internal sealed class S2Worker : IPeerRequestWorker
    {
        private readonly Dictionary<(Guid, string), int> _calls = new();

        public Func<IEthPeer, ulong, ulong, bool, CancellationToken, Task<List<BlockHeader>>> Headers =
            (_, _, _, _, _) => Task.FromResult(new List<BlockHeader>());

        public Func<IEthPeer, byte[], ulong, CancellationToken, Task<List<BlockHeader>>> HeadersByHash =
            (_, _, _, _) => Task.FromResult(new List<BlockHeader>());

        public Func<IEthPeer, IReadOnlyList<byte[]>, CancellationToken, Task<List<BlockBody>>> Bodies =
            (_, _, _) => Task.FromResult(new List<BlockBody>());

        public Func<IEthPeer, IReadOnlyList<byte[]>, CancellationToken, Task<List<List<Receipt>>>> Receipts =
            (_, _, _) => Task.FromResult(new List<List<Receipt>>());

        public int CallsOf(Guid peerId, string kind)
        {
            lock (_calls) return _calls.GetValueOrDefault((peerId, kind));
        }

        private void Note(IEthPeer peer, string kind)
        {
            lock (_calls) _calls[(peer.Id, kind)] = _calls.GetValueOrDefault((peer.Id, kind)) + 1;
        }

        public Task<List<BlockHeader>> GetHeadersAsync(IEthPeer peer, ulong startBlock, ulong limit, bool reverse, CancellationToken ct)
        {
            Note(peer, "headers");
            return Headers(peer, startBlock, limit, reverse, ct);
        }

        public Task<List<BlockHeader>> GetHeadersByHashAsync(IEthPeer peer, byte[] startHash, ulong limit, CancellationToken ct)
        {
            Note(peer, "headersByHash");
            return HeadersByHash(peer, startHash, limit, ct);
        }

        public Task<List<BlockBody>> GetBodiesAsync(IEthPeer peer, IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
        {
            Note(peer, "bodies");
            return Bodies(peer, blockHashes, ct);
        }

        public Task<List<List<Receipt>>> GetReceiptsAsync(IEthPeer peer, IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
        {
            Note(peer, "receipts");
            return Receipts(peer, blockHashes, ct);
        }

        public Task<AccountRangeMessage> GetAccountRangeAsync(IEthPeer peer, byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
        public Task<StorageRangesMessage> GetStorageRangesAsync(IEthPeer peer, byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
        public Task<ByteCodesMessage> GetByteCodesAsync(IEthPeer peer, List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
        public Task<TrieNodesMessage> GetTrieNodesAsync(IEthPeer peer, byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
    }
}
