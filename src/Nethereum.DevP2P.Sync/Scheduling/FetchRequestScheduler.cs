using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.DevP2P.Sync.FullSync;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;

namespace Nethereum.DevP2P.Sync.Scheduling
{
    public enum TimeoutStreakKind
    {
        Eth,
        Snap
    }

    public sealed class FetchRequestScheduler : IFetchRequestScheduler
    {
        private readonly IPeerPool _pool;
        private readonly IPeerRequestWorker _worker;
        private readonly FetchRequestSchedulerOptions _options;
        private readonly Func<string, PeerScore>? _scoreLookup;
        private readonly ILogger<FetchRequestScheduler> _logger;

        private readonly PeerBoard _board;
        private readonly PeerQuarantine _quarantine;
        private readonly SnapReplyPolicy _replyPolicy;

        public bool RegisterTimeoutForQuarantine(Guid peerId, TimeoutStreakKind kind, bool isTrusted, out int triggeredAtCount)
            => _quarantine.RegisterTimeout(peerId, kind, isTrusted, out triggeredAtCount);

        public void RegisterSuccessForQuarantine(Guid peerId, TimeoutStreakKind kind)
            => _quarantine.RegisterSuccess(peerId, kind);

        public int GetConsecutiveTimeoutsForTest(Guid peerId, TimeoutStreakKind kind)
            => _quarantine.ConsecutiveTimeouts(peerId, kind);

        public int GetSnapQuarantineCyclesForTest(Guid peerId)
            => _quarantine.SnapQuarantineCycles(peerId);

        public TimeSpan GetAdaptiveTimeoutForTest(Guid peerId, bool isTrusted)
            => _board.AdaptiveTimeout(peerId, isTrusted);

        public FetchRequestScheduler(
            IPeerPool pool,
            IPeerRequestWorker worker,
            FetchRequestSchedulerOptions options,
            Func<string, PeerScore>? scoreLookup = null,
            ILogger<FetchRequestScheduler>? logger = null)
        {
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
            _worker = worker ?? throw new ArgumentNullException(nameof(worker));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _scoreLookup = scoreLookup;
            _logger = logger ?? NullLogger<FetchRequestScheduler>.Instance;

            _quarantine = new PeerQuarantine(_pool, _logger, () => HeaderBatchQuarantineDuration);
            _board = new PeerBoard(_pool, _options, _scoreLookup, _quarantine, _logger);
            _replyPolicy = new SnapReplyPolicy(_quarantine, _logger);

            _pool.PeerRemoved += OnPeerRemoved;
        }

        public async Task<List<BlockHeader>> FetchHeadersAsync(
            ulong startBlock, ulong limit, CancellationToken ct, bool reverse = false)
            => (await FetchHeadersWithPeerAsync(startBlock, limit, ct, reverse).ConfigureAwait(false)).Headers;

        public async Task<List<BlockHeader>> FetchAnnouncedHeadersAsync(
            ulong startBlock, ulong limit, CancellationToken ct)
        {
            var claimants = _pool.ActivePeers
                .Where(peer => peer.PeerLatestBlock >= startBlock && IsPreferredHeaderPeer(peer))
                .Select(peer => peer.Id)
                .ToHashSet();
            if (claimants.Count == 0)
                return await FetchHeadersAsync(startBlock, limit, ct).ConfigureAwait(false);

            var (claimed, _) = await FetchHeadersFilteredAsync(
                startBlock, limit, false, null, peer => claimants.Contains(peer.Id), ct).ConfigureAwait(false);
            if (claimed.Count > 0) return claimed;

            if (!_pool.ActivePeers.Any(peer => !claimants.Contains(peer.Id) && _quarantine.HeaderServingFilter(peer)))
                return claimed;

            var (others, _) = await FetchHeadersFilteredAsync(
                startBlock, limit, false, claimants, _quarantine.HeaderServingFilter, ct).ConfigureAwait(false);
            if (others.Count > 0)
                foreach (var claimant in claimants) _quarantine.DemoteClaim(claimant);
            return others;
        }

        private bool IsPreferredHeaderPeer(IEthPeer peer)
            => _quarantine.HeaderServingFilter(peer) && !_quarantine.IsClaimDemoted(peer.Id);

        public Task<(List<BlockHeader> Headers, Guid PeerId)> FetchHeadersWithPeerAsync(
            ulong startBlock, ulong limit, CancellationToken ct, bool reverse = false)
            => FetchHeadersFilteredAsync(startBlock, limit, reverse, null, _quarantine.HeaderServingFilter, ct);

        private Task<(List<BlockHeader> Headers, Guid PeerId)> FetchHeadersFilteredAsync(
            ulong startBlock, ulong limit, bool reverse,
            IReadOnlyCollection<Guid> excluded, Func<IEthPeer, bool> filter, CancellationToken ct)
            => ExecuteAsync(
                FetchRequest.Eth(
                    $"headers {startBlock}+{limit}{(reverse ? " rev" : string.Empty)}",
                    async (peer, attemptCt) => AcceptHeadersByNumber(
                        peer, startBlock, limit, reverse,
                        await _worker.GetHeadersAsync(peer, startBlock, limit, reverse, attemptCt).ConfigureAwait(false)),
                    excluded,
                    filter,
                    _quarantine.DropOnRepeatedEthTimeout,
                    headers => headers.Count),
                ct);

        private List<BlockHeader> AcceptHeadersByNumber(
            IEthPeer peer, ulong startBlock, ulong limit, bool reverse, List<BlockHeader> headers)
        {
            if (BlockBatchValidator.ValidateHeadersByNumberReply(headers, startBlock, limit, reverse)) return headers;

            throw new PeerMisbehaviourException(
                $"peer {SyncPeerSession.ParseHost(peer.Enode)} answered headers from {startBlock} with headers that are not the requested contiguous numbers");
        }

        public async Task<List<BlockHeader>> FetchHeadersByHashAsync(
            byte[] startHash, ulong limit, CancellationToken ct)
        {
            var (result, _) = await ExecuteAsync(
                FetchRequest.Eth(
                    $"headers-by-hash+{limit}",
                    async (peer, attemptCt) => AcceptHeadersByHash(
                        peer, startHash, await _worker.GetHeadersByHashAsync(peer, startHash, limit, attemptCt).ConfigureAwait(false)),
                    null,
                    _quarantine.HeaderServingFilter,
                    _quarantine.DropOnRepeatedEthTimeout,
                    headers => headers.Count),
                ct).ConfigureAwait(false);
            return result;
        }

        private List<BlockHeader> AcceptHeadersByHash(IEthPeer peer, byte[] startHash, List<BlockHeader> headers)
        {
            if (BlockBatchValidator.ValidateHeadersByHashReply(headers, startHash)) return headers;

            throw new PeerMisbehaviourException(
                $"peer {SyncPeerSession.ParseHost(peer.Enode)} answered headers-by-hash with headers that do not chain from the requested hash");
        }

        public async Task<List<BlockBody>> FetchBodiesAsync(
            IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
        {
            var result = await FetchBodiesAsync(blockHashes, excludePeers: null, ct).ConfigureAwait(false);
            return result.Bodies;
        }

        public async Task<BodyFetchResult> FetchBodiesAsync(
            IReadOnlyList<byte[]> blockHashes,
            IReadOnlyCollection<Guid>? excludePeers,
            CancellationToken ct)
        {
            if (blockHashes is null) throw new ArgumentNullException(nameof(blockHashes));
            if (blockHashes.Count == 0)
                return new BodyFetchResult(new List<BlockBody>(), Array.Empty<Guid>());

            var (bodies, servingPeerIds) = await ChunkedFetch.RunAsync(
                blockHashes,
                _options.EffectiveBodyFetchChunkSize,
                _options.EffectiveMaxParallelBodyFetches,
                () => _pool.ActivePeers.Count,
                chunk => ExecuteAsync(
                    FetchRequest.Eth(
                        $"bodies x{chunk.Count}",
                        (peer, attemptCt) => _worker.GetBodiesAsync(peer, chunk, attemptCt),
                        excludePeers,
                        _quarantine.HeaderServingFilter,
                        _quarantine.DropOnRepeatedEthTimeout,
                        reply => reply.Count),
                    ct)).ConfigureAwait(false);
            return new BodyFetchResult(bodies, servingPeerIds);
        }

        public async Task<List<List<Receipt>>> FetchReceiptsAsync(
            IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
        {
            if (blockHashes is null) throw new ArgumentNullException(nameof(blockHashes));
            if (blockHashes.Count == 0) return new List<List<Receipt>>();

            var (receipts, _) = await ChunkedFetch.RunAsync(
                blockHashes,
                _options.EffectiveReceiptFetchChunkSize,
                _options.EffectiveMaxParallelReceiptFetches,
                () => _pool.ActivePeers.Count,
                chunk => ExecuteAsync(
                    FetchRequest.Eth(
                        $"receipts x{chunk.Count}",
                        (peer, attemptCt) => _worker.GetReceiptsAsync(peer, chunk, attemptCt),
                        null,
                        _quarantine.HeaderServingFilter,
                        _quarantine.DropOnRepeatedEthTimeout,
                        reply => reply.Count),
                    ct)).ConfigureAwait(false);
            return receipts;
        }

        private Task<(T Result, Guid PeerId)> ExecuteAsync<T>(FetchRequest<T> request, CancellationToken ct)
            => new RetryingRequest<T>(request, _board, _quarantine, _pool, _options, _logger).RunAsync(ct);

        private async Task<TMsg> FetchSnapAsync<TMsg>(
            SnapReply<TMsg> reply,
            Func<IEthPeer, CancellationToken, Task<TMsg>> send,
            CancellationToken ct)
        {
            var (result, _) = await ExecuteAsync(
                FetchRequest.Snap(
                    reply.Label,
                    async (peer, attemptCt) => _replyPolicy.Accept(
                        peer, await send(peer, attemptCt).ConfigureAwait(false), reply),
                    _replyPolicy.ServingAt(reply.Root),
                    _quarantine.QuarantineSnapState,
                    reply.Root,
                    reply.StopWhenAllStateless ? () => _quarantine.EverySnapPeerIsStateless(reply.Root) : null),
                ct).ConfigureAwait(false);
            return result;
        }

        private void OnPeerRemoved(object? sender, IEthPeer peer)
        {
            _board.Forget(peer.Id);
            _quarantine.Forget(peer.Id);
        }

        public int GetInFlightCountForTest(Guid peerId)
            => _board.InFlightOf(peerId);

        public double GetLatencyEmaForTest(Guid peerId)
            => _board.LatencyEma(peerId);

        public TimeSpan GetAdaptiveTimeoutForTest(Guid peerId) => _board.AdaptiveTimeout(peerId);

        public static TimeSpan ChooseSnapStateCooldown(ulong peerLatestBlock, ulong networkHeadBlock, bool isTrusted)
            => PeerQuarantine.ChooseSnapStateCooldown(peerLatestBlock, networkHeadBlock, isTrusted);

        public TimeSpan HeaderBatchQuarantineDuration = TimeSpan.FromMinutes(2);

        public void QuarantineHeaderPeer(Guid peerId) => _quarantine.QuarantineHeaderPeer(peerId);

        public bool IsHeaderPeerQuarantined(Guid peerId) => _quarantine.IsHeaderPeerQuarantined(peerId);

        public void QuarantineSnapState(IEthPeer peer) => _quarantine.QuarantineSnapState(peer);

        public bool IsSnapStateQuarantined(Guid peerId) => _quarantine.IsSnapStateQuarantined(peerId);

        public void OnTargetRootChanged()
        {
            _quarantine.ClearStateless();
            _quarantine.ClearSnapStateBenches();
        }

        public void ClearStatelessMarks() => _quarantine.ClearStateless();

        public bool IsSnapStateServing(IEthPeer peer) => _quarantine.SnapStateServingFilter(peer);

        public Task<AccountRangeMessage> FetchAccountRangeAsync(
            byte[] stateRoot, byte[] startingHash, byte[] limitHash,
            ulong responseBytes, CancellationToken ct)
            => FetchAccountRangeAsync(stateRoot, startingHash, limitHash, responseBytes, verifyResponse: null, ct);

        public async Task<AccountRangeMessage> FetchAccountRangeAsync(
            byte[] stateRoot, byte[] startingHash, byte[] limitHash,
            ulong responseBytes, Func<AccountRangeMessage, bool> verifyResponse, CancellationToken ct)
        {
            _quarantine.NoteRequestRoot(stateRoot);
            var label = $"snap account-range root=0x{HexShort(stateRoot)} start=0x{HexShort(startingHash)}";
            var reply = new SnapReply<AccountRangeMessage>(
                label, stateRoot, "account-range",
                resp => HasNoEntriesAndNoProof(resp?.Accounts, resp?.Proof),
                verifyResponse,
                $"snap account-range root=0x{HexShort(stateRoot)} start=0x{HexShort(startingHash)}: " +
                ProofInvalidQuarantined,
                StopWhenAllStateless: false);
            return await FetchSnapAsync(
                reply,
                (peer, attemptCt) => _worker.GetAccountRangeAsync(
                    peer, stateRoot, startingHash, limitHash, responseBytes, attemptCt),
                ct).ConfigureAwait(false);
        }

        public Task<StorageRangesMessage> FetchStorageRangesAsync(
            byte[] stateRoot, List<byte[]> accountHashes,
            byte[] startingHash, byte[] limitHash,
            ulong responseBytes, CancellationToken ct)
            => FetchStorageRangesAsync(stateRoot, accountHashes, startingHash, limitHash, responseBytes, verifyResponse: null, ct);

        public async Task<StorageRangesMessage> FetchStorageRangesAsync(
            byte[] stateRoot, List<byte[]> accountHashes,
            byte[] startingHash, byte[] limitHash,
            ulong responseBytes, Func<StorageRangesMessage, bool> verifyResponse, CancellationToken ct)
        {
            _quarantine.NoteRequestRoot(stateRoot);
            var label = $"snap storage-range root=0x{HexShort(stateRoot)} acct=0x{(accountHashes.Count > 0 ? HexShort(accountHashes[0]) : "")} accounts={accountHashes.Count} start=0x{HexShort(startingHash)}";
            var reply = new SnapReply<StorageRangesMessage>(
                label, stateRoot, "storage-range",
                resp => HasNoEntriesAndNoProof(resp?.Slots, resp?.Proof),
                verifyResponse,
                $"snap storage-range root=0x{HexShort(stateRoot)} accounts={accountHashes.Count}: " +
                ProofInvalidQuarantined,
                StopWhenAllStateless: false);
            return await FetchSnapAsync(
                reply,
                (peer, attemptCt) => _worker.GetStorageRangesAsync(
                    peer, stateRoot, accountHashes, startingHash, limitHash, responseBytes, attemptCt),
                ct).ConfigureAwait(false);
        }

        public Task<ByteCodesMessage> FetchByteCodesAsync(
            List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct)
            => FetchByteCodesAsync(codeHashes, responseBytes, verifyResponse: null, ct);

        public async Task<ByteCodesMessage> FetchByteCodesAsync(
            List<byte[]> codeHashes, ulong responseBytes, Func<ByteCodesMessage, bool> verifyResponse, CancellationToken ct)
        {
            var root = _quarantine.RootOfLastRootRequest;
            var label = $"snap bytecodes x{codeHashes.Count}";
            var reply = new SnapReply<ByteCodesMessage>(
                label, root, "bytecodes",
                resp => codeHashes.Count > 0 && (resp?.Codes == null || resp.Codes.Count == 0),
                verifyResponse,
                $"snap bytecodes x{codeHashes.Count}: peer returned a code " + UnrequestedQuarantined,
                StopWhenAllStateless: true);
            return await FetchSnapAsync(
                reply,
                (peer, attemptCt) => _worker.GetByteCodesAsync(peer, codeHashes, responseBytes, attemptCt),
                ct).ConfigureAwait(false);
        }

        public Task<TrieNodesMessage> FetchTrieNodesAsync(
            byte[] stateRoot, List<List<byte[]>> paths,
            ulong responseBytes, CancellationToken ct)
            => FetchTrieNodesAsync(stateRoot, paths, responseBytes, verifyResponse: null, ct);

        public async Task<TrieNodesMessage> FetchTrieNodesAsync(
            byte[] stateRoot, List<List<byte[]>> paths,
            ulong responseBytes, Func<TrieNodesMessage, bool> verifyResponse, CancellationToken ct)
        {
            _quarantine.NoteRequestRoot(stateRoot);
            var label = $"snap trie-nodes root=0x{HexShort(stateRoot)} paths={paths.Count}";
            var reply = new SnapReply<TrieNodesMessage>(
                label, stateRoot, "trie-nodes",
                resp => paths.Count > 0 && (resp?.Nodes == null || resp.Nodes.Count == 0),
                verifyResponse,
                $"snap trie-nodes paths={paths.Count}: peer returned a node " + UnrequestedQuarantined,
                StopWhenAllStateless: true);
            return await FetchSnapAsync(
                reply,
                (peer, attemptCt) => _worker.GetTrieNodesAsync(peer, stateRoot, paths, responseBytes, attemptCt),
                ct).ConfigureAwait(false);
        }

        private const string ProofInvalidQuarantined = "peer returned a proof-INVALID response (tampered/malformed — quarantined)";

        private const string UnrequestedQuarantined = "whose keccak matches no requested hash (unrequested/over-length — quarantined)";

        private static bool HasNoEntriesAndNoProof(System.Collections.ICollection? entries, System.Collections.ICollection? proof)
            => (entries == null || entries.Count == 0) && (proof == null || proof.Count == 0);

        internal static string HexShort(byte[] hash)
        {
            if (hash == null || hash.Length == 0) return "(empty)";
            var n = System.Math.Min(8, hash.Length);
            var chars = new char[n * 2];
            const string hex = "0123456789abcdef";
            for (int i = 0; i < n; i++)
            {
                chars[i * 2] = hex[hash[i] >> 4];
                chars[i * 2 + 1] = hex[hash[i] & 0x0f];
            }
            return new string(chars);
        }
    }

    public sealed class FetchRequestFailedException : Exception
    {
        public FetchRequestFailedException(string message, Exception? inner)
            : base(message, inner) { }
    }
}
