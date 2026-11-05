using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Nethereum.DevP2P.Sync.Scheduling
{
    internal enum ClaimOutcome
    {
        Claimed,
        Retry,
        GiveUp
    }

    internal sealed class RetryingRequest<T>
    {
        private readonly FetchRequest<T> _request;
        private readonly PeerBoard _board;
        private readonly PeerQuarantine _quarantine;
        private readonly IPeerPool _pool;
        private readonly FetchRequestSchedulerOptions _options;
        private readonly ILogger<FetchRequestScheduler> _logger;
        private readonly HashSet<Guid> _triedPeers;
        private int _attempts;
        private Exception _lastError;
        private IEthPeer _claimed;
        private (T Result, Guid PeerId)? _emptyReply;
        private readonly List<Guid> _emptyRepliers = new List<Guid>();

        internal RetryingRequest(
            FetchRequest<T> request,
            PeerBoard board,
            PeerQuarantine quarantine,
            IPeerPool pool,
            FetchRequestSchedulerOptions options,
            ILogger<FetchRequestScheduler> logger)
        {
            _request = request;
            _board = board;
            _quarantine = quarantine;
            _pool = pool;
            _options = options;
            _logger = logger;
            _triedPeers = request.InitialExcluded is null
                ? new HashSet<Guid>()
                : new HashSet<Guid>(request.InitialExcluded);
        }

        internal async Task<(T Result, Guid PeerId)> RunAsync(CancellationToken ct)
        {
            while (_attempts < _options.MaxRetriesPerRequest)
            {
                ct.ThrowIfCancellationRequested();
                if (NobodyElseCanServe()) return _emptyReply.Value;
                var outcome = await ClaimNextAsync(ct).ConfigureAwait(false);
                if (outcome == ClaimOutcome.Retry) continue;
                if (outcome == ClaimOutcome.GiveUp) break;

                var peer = _claimed;
                _triedPeers.Add(peer.Id);
                _attempts++;
                try
                {
                    var reply = await SendOnceAsync(peer, ct).ConfigureAwait(false);
                    if (reply.Served) return Served(reply.Result, peer);
                    OnEmptyReply(peer, reply.Result);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    OnTimeout(peer);
                }
                catch (TimeoutException tex)
                {
                    OnHardTimeout(peer, tex);
                }
                catch (StatelessReplyException ex)
                {
                    OnStateless(ex);
                }
                catch (SnapPeerCapabilityMismatchException ex)
                {
                    OnCapabilityMismatch(peer, ex);
                    throw;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    OnError(peer, ex);
                }
                finally
                {
                    ReleaseSlot(peer);
                }
            }

            if (_emptyReply.HasValue) return _emptyReply.Value;
            throw Fail();
        }

        private bool NobodyElseCanServe()
            => _emptyReply.HasValue && !_board.HasCandidate(_triedPeers, _request.PeerFilter);

        private void OnEmptyReply(IEthPeer peer, T reply)
        {
            _emptyReply = (reply, peer.Id);
            _emptyRepliers.Add(peer.Id);
            _logger.LogDebug(
                "fetch: {Request} peer {Host} delivered nothing; asking the next peer",
                _request.Label, SyncPeerSession.ParseHost(peer.Enode));
        }

        private (T Result, Guid PeerId) Served(T result, IEthPeer peer)
        {
            foreach (var emptyReplier in _emptyRepliers) _quarantine.DemoteClaim(emptyReplier);
            return (result, peer.Id);
        }

        private async Task<ClaimOutcome> ClaimNextAsync(CancellationToken ct)
        {
            var label = _request.Label;
            var noPeerCanServe = _request.NoPeerCanServe;
            _claimed = await _board.ClaimAsync(_triedPeers, ct, _request.PeerFilter, noPeerCanServe, _request.StatelessRoot).ConfigureAwait(false);
            if (_claimed is not null) return ClaimOutcome.Claimed;

            if (noPeerCanServe != null && noPeerCanServe())
            {
                _lastError = new InvalidOperationException(
                    $"No peer serves {label}: every snap peer is stateless for this root.");
                return ClaimOutcome.GiveUp;
            }
            if (_triedPeers.Count > 0)
            {
                _logger.LogInformation("fetch: {Request} — all {Tried} tried peers exhausted, clearing exclusion and retrying after pool refresh", label, _triedPeers.Count);
                _triedPeers.Clear();
                await Task.Delay(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
                return ClaimOutcome.Retry;
            }

            _lastError = new InvalidOperationException(
                $"No peer available for {label} after {_attempts} attempts.");
            return ClaimOutcome.GiveUp;
        }

        private async Task<(T Result, bool Served)> SendOnceAsync(IEthPeer peer, CancellationToken ct)
        {
            using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            attemptCts.CancelAfter(_board.AdaptiveTimeout(peer.Id, peer.IsTrusted));
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = await _request.Send(peer, attemptCts.Token).ConfigureAwait(false);
            if (_request.Delivered != null && _request.Delivered(result) == 0) return (result, false);
            _board.RecordLatency(peer.Id, sw.Elapsed.TotalMilliseconds);
            if (_request.OnRepeatedTimeout != null) _quarantine.RegisterSuccess(peer.Id, _request.Kind);
            _pool.ReportSuccess(peer.Id);
            return (result, true);
        }

        private void OnTimeout(IEthPeer peer)
        {
            var label = _request.Label;
            _lastError = new TimeoutException(
                $"{label}: peer {SyncPeerSession.ParseHost(peer.Enode)} timed out");
            _logger.LogWarning(
                "snap.peer.timeout request={Request} peer={Host} attempt={Attempt}; reassigning",
                label, SyncPeerSession.ParseHost(peer.Enode), _attempts);

            QuarantineIfRepeated(peer);
        }

        private void OnHardTimeout(IEthPeer peer, TimeoutException tex)
        {
            _lastError = tex;
            _logger.LogWarning(
                "snap.peer.timeout.hard request={Request} peer={Host} attempt={Attempt}; reassigning",
                _request.Label, SyncPeerSession.ParseHost(peer.Enode), _attempts);

            QuarantineIfRepeated(peer);
        }

        private void QuarantineIfRepeated(IEthPeer peer)
        {
            var onRepeatedTimeout = _request.OnRepeatedTimeout;
            if (onRepeatedTimeout != null && _quarantine.RegisterTimeout(peer.Id, _request.Kind, peer.IsTrusted, out var timeoutCount))
            {
                _logger.LogWarning(
                    "snap.peer.timeout.quarantine request={Request} peer={Host} consecutive={Count} — peer never responds, quarantining",
                    _request.Label, SyncPeerSession.ParseHost(peer.Enode), timeoutCount);
                onRepeatedTimeout(peer);
            }
        }

        private void OnStateless(StatelessReplyException ex)
        {
            _lastError = ex;
        }

        private void OnCapabilityMismatch(IEthPeer peer, SnapPeerCapabilityMismatchException ex)
        {
            _logger.LogError(
                "snap.peer.capability_mismatch request={Request} peer={Host} error={Error}; permanent — failing fast without reassigning",
                _request.Label, SyncPeerSession.ParseHost(peer.Enode), ex.Message);
        }

        private void OnError(IEthPeer peer, Exception ex)
        {
            _lastError = ex;
            if (ex is PeerMisbehaviourException) _quarantine.QuarantineHeaderPeer(peer.Id);
            _logger.LogWarning(
                "snap.peer.error request={Request} peer={Host} error={ErrorType}: {Error}; reassigning",
                _request.Label, SyncPeerSession.ParseHost(peer.Enode), ex.GetType().Name, ex.Message);
        }

        private void ReleaseSlot(IEthPeer peer)
        {
            _board.Release(peer.Id);
        }

        private FetchRequestFailedException Fail()
        {
            return new FetchRequestFailedException(
                $"{_request.Label} failed after {_attempts} attempts across {_triedPeers.Count} peers.",
                _lastError);
        }
    }
}
