using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.DevP2P.Sync.Scheduling;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Proofs;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    internal enum SnapKind { AccountRange, StorageRange, ByteCodes, TrieNodes }

    internal sealed class ServedState
    {
        public static readonly byte[] ContractHash = Sha3Keccack.Current.CalculateHash(new byte[] { 0x03, 0xCC });
        public static readonly byte[] Code = new byte[] { 0x60, 0x01, 0x60, 0x02, 0x01, 0x60, 0x00, 0x55, 0x00 };
        public static readonly byte[] CodeHash = Sha3Keccack.Current.CalculateHash(Code);

        public InMemoryContentNodeStore Store { get; } = new();
        public CodeBook Bytecodes { get; } = new();
        public byte[] Root { get; }
        public byte[] OtherRoot { get; }
        public byte[] StorageRoot { get; }

        public ServedState()
        {
            Bytecodes.Put(CodeHash, Code);
            var storage = new PatriciaTrie(Store, ContractHash);
            for (byte i = 1; i <= 3; i++)
                storage.Put(Sha3Keccack.Current.CalculateHash(new[] { i }), RLP.RLP.EncodeElement(new[] { (byte)(0xB0 + i) }));
            storage.SaveDirtyNodesToStorage();
            StorageRoot = storage.Root.GetHash();
            Root = BuildAccounts(1000);
            OtherRoot = BuildAccounts(2000);
        }

        private byte[] BuildAccounts(ulong eoaBalance)
        {
            var trie = new PatriciaTrie(Store);
            trie.Put(ContractHash, new AccountEncoder().Encode(new Account
            {
                Nonce = (EvmUInt256)1,
                Balance = (EvmUInt256)1UL,
                StateRoot = StorageRoot,
                CodeHash = CodeHash,
            }));
            for (byte i = 1; i <= 2; i++)
                trie.Put(Sha3Keccack.Current.CalculateHash(new[] { (byte)0xE0, i }), new AccountEncoder().Encode(new Account
                {
                    Nonce = (EvmUInt256)0,
                    Balance = (EvmUInt256)eoaBalance,
                    StateRoot = DefaultValues.EMPTY_TRIE_HASH,
                    CodeHash = DefaultValues.EMPTY_DATA_HASH,
                }));
            trie.SaveDirtyNodesToStorage();
            return trie.Root.GetHash();
        }

        public PatriciaSnapRequestHandler Handler() => new(Store, Bytecodes);

        public static List<List<byte[]>> RootPath()
            => new() { new List<byte[]> { PatriciaPathWalker.NibblesToCompact(Array.Empty<byte>()) } };

        internal sealed class CodeBook : IBytecodeStore
        {
            private readonly ConcurrentDictionary<string, byte[]> _codes = new();
            public void Put(byte[] codeHash, byte[] code) => _codes[codeHash.ToHex()] = code;
            public byte[] Get(byte[] codeHash) => _codes.TryGetValue(codeHash.ToHex(), out var c) ? c : null;
        }
    }

    internal sealed class ScriptedSnapHandler : ISnapRequestHandler
    {
        private readonly ISnapRequestHandler _serving;
        private readonly Func<SnapKind, byte[], bool> _answersEmpty;
        private readonly ConcurrentQueue<(SnapKind Kind, byte[] Root)> _calls = new();

        public ScriptedSnapHandler(ISnapRequestHandler serving, Func<SnapKind, byte[], bool> answersEmpty)
        {
            _serving = serving;
            _answersEmpty = answersEmpty;
        }

        public static ScriptedSnapHandler Serving(ServedState state) => new(state.Handler(), (_, _) => false);

        public static ScriptedSnapHandler EmptyFor(ServedState state, params SnapKind[] kinds)
            => new(state.Handler(), (kind, _) => kinds.Contains(kind));

        public static ScriptedSnapHandler EmptyAt(ServedState state, byte[] root)
            => new(state.Handler(), (kind, r) => r == null || ByteUtil.AreEqual(r, root));

        public Func<SnapKind, CancellationToken, Task> BeforeServing { get; set; }

        public int Calls(SnapKind kind) => _calls.Count(c => c.Kind == kind);

        public int Calls(SnapKind kind, byte[] root) => _calls.Count(c => c.Kind == kind && c.Root != null && ByteUtil.AreEqual(c.Root, root));

        public IReadOnlyList<SnapKind> Sequence => _calls.Select(c => c.Kind).ToList();

        public async Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage request, CancellationToken ct = default)
        {
            if (await EmptyAsync(SnapKind.AccountRange, request.RootHash, ct))
                return new AccountRangeMessage { RequestId = request.RequestId, Accounts = new List<AccountRangeMessage.AccountEntry>(), Proof = new List<byte[]>() };
            return await _serving.GetAccountRangeAsync(request, ct);
        }

        public async Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage request, CancellationToken ct = default)
        {
            if (await EmptyAsync(SnapKind.StorageRange, request.RootHash, ct))
                return new StorageRangesMessage { RequestId = request.RequestId, Slots = new List<List<StorageRangesMessage.SlotEntry>>(), Proof = new List<byte[]>() };
            return await _serving.GetStorageRangesAsync(request, ct);
        }

        public async Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage request, CancellationToken ct = default)
        {
            if (await EmptyAsync(SnapKind.ByteCodes, null, ct))
                return new ByteCodesMessage { RequestId = request.RequestId, Codes = new List<byte[]>() };
            return await _serving.GetByteCodesAsync(request, ct);
        }

        public async Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage request, CancellationToken ct = default)
        {
            if (await EmptyAsync(SnapKind.TrieNodes, request.RootHash, ct))
                return new TrieNodesMessage { RequestId = request.RequestId, Nodes = new List<byte[]>() };
            return await _serving.GetTrieNodesAsync(request, ct);
        }

        public Task<Nethereum.Model.P2P.Snap.BlockAccessListsMessage> GetBlockAccessListsAsync(Nethereum.Model.P2P.Snap.GetBlockAccessListsMessage request, CancellationToken ct = default)
            => _serving.GetBlockAccessListsAsync(request, ct);

        private async Task<bool> EmptyAsync(SnapKind kind, byte[] root, CancellationToken ct)
        {
            _calls.Enqueue((kind, root));
            if (BeforeServing != null) await BeforeServing(kind, ct);
            return _answersEmpty(kind, root);
        }
    }

    internal sealed class RecordingLogger<T> : ILogger<T>
    {
        private readonly ConcurrentQueue<(LogLevel Level, string Message)> _entries = new();
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            => _entries.Enqueue((logLevel, formatter(state, exception)));
        public int Count(LogLevel level, string fragment) => _entries.Count(e => e.Level == level && e.Message.Contains(fragment, StringComparison.Ordinal));
        public int Count(string fragment) => _entries.Count(e => e.Message.Contains(fragment, StringComparison.Ordinal));
    }

    internal sealed class SnapLoopback : IAsyncDisposable
    {
        private static readonly byte[] GenesisHash = System.Array.ConvertAll(new byte[32], _ => (byte)1);
        private const ulong NetworkId = 1;
        private readonly ConcurrentDictionary<string, PeerListener> _listeners = new();

        public PeerPoolManager Pool { get; private set; }
        public FetchRequestScheduler Scheduler { get; private set; }
        public RecordingLogger<FetchRequestScheduler> SchedulerLog { get; } = new();

        public async Task<string> ListenAsync(ISnapRequestHandler handler, ulong headBlock = 0, bool serveSnap = true)
        {
            var bundle = InMemoryChainStoreBundle.Open();
            for (ulong n = 0; n <= headBlock; n++)
            {
                var hash = new byte[32];
                hash[0] = 1;
                hash[31] = (byte)n;
                await bundle.Blocks.SaveAsync(new BlockHeader { BlockNumber = (long)n }, hash);
            }
            var options = new PeerListenerOptions
            {
                ListenPort = 0,
                BindAddress = IPAddress.Loopback,
                MaxInboundPeers = 5,
                MaxInboundPerIP = 5,
                ServeSnap = serveSnap,
                MirrorRemoteStatus = false,
            };
            var status = new Eth68StatusMessage
            {
                ProtocolVersion = 68,
                NetworkId = NetworkId,
                TotalDifficulty = BigInteger.One,
                BestHash = GenesisHash,
                GenesisHash = GenesisHash,
                ForkHash = 0,
                ForkNext = 0,
            };
            var key = EthECKey.GenerateKey();
            var listener = new PeerListener(key, bundle, options, status, snapHandler: handler);
            await listener.StartAsync();
            var enode = $"enode://{key.GetPubKeyNoPrefix().ToHex()}@127.0.0.1:{listener.Port}";
            _listeners[enode] = listener;
            return enode;
        }

        public async Task StartPoolAsync(int targetPeers, FetchRequestSchedulerOptions options = null, params string[] trustedEnodes)
        {
            Pool = new PeerPoolManager(new DirectHandshakeWorker(), new PeerPoolOptions(TargetPeerCount: targetPeers, MinPeerLatestBlock: 0),
                trustedDialKeys: trustedEnodes.Length == 0 ? null : trustedEnodes);
            await Pool.StartAsync(CancellationToken.None);
            Scheduler = new FetchRequestScheduler(Pool, new PeerRequestWorker(), options ?? new FetchRequestSchedulerOptions(), logger: SchedulerLog);
        }

        public async Task<SyncPeerSession> JoinAsync(string enode, bool snap = true)
        {
            for (var dial = 0; dial < JoinDials && Session(enode, snap) == null; dial++)
            {
                Pool.EnqueueCandidate(enode);
                await WaitUntilAsync(() => Session(enode, snap) != null, JoinDialWindow);
            }
            Assert.True(Session(enode, snap) != null, $"{enode} never joined");
            return Session(enode, snap);
        }

        private const int JoinDials = 6;
        private static readonly TimeSpan JoinDialWindow = TimeSpan.FromSeconds(10);

        private SyncPeerSession Session(string enode, bool snap)
            => Pool.ActivePeers.OfType<SyncPeerSession>().FirstOrDefault(p => ((IEthPeer)p).Enode == enode && (!snap || p.SupportsSnap));

        public async Task StopListenerAsync(string enode)
        {
            if (_listeners.TryRemove(enode, out var listener)) await listener.DisposeAsync();
        }

        public async ValueTask DisposeAsync()
        {
            if (Pool != null) await Pool.DisposeAsync();
            foreach (var listener in _listeners.Values) await listener.DisposeAsync();
            _listeners.Clear();
        }

        public static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (condition()) return true;
                await Task.Delay(50);
            }
            return condition();
        }

        private sealed class DirectHandshakeWorker : IPeerHandshakeWorker
        {
            public async Task<IEthPeer> HandshakeAsync(string enode, TimeSpan timeout, ulong minPeerLatestBlock, CancellationToken ct)
                => await SyncPeerSession.ConnectAsync(enode, timeout, ct, GenesisHash, NetworkId, minPeerLatestBlock);
        }
    }
}
