using System;
using Microsoft.Extensions.Logging;

namespace Nethereum.DevP2P.Sync.Scheduling
{
    internal sealed record SnapReply<TMsg>(
        string Label,
        byte[] Root,
        string Kind,
        Func<TMsg, bool> IsStateless,
        Func<TMsg, bool> Verify,
        string RejectMessage,
        bool StopWhenAllStateless);

    internal sealed class SnapReplyPolicy
    {
        private readonly PeerQuarantine _quarantine;
        private readonly ILogger<FetchRequestScheduler> _logger;

        internal SnapReplyPolicy(PeerQuarantine quarantine, ILogger<FetchRequestScheduler> logger)
        {
            _quarantine = quarantine;
            _logger = logger;
        }

        internal TMsg Accept<TMsg>(IEthPeer peer, TMsg resp, SnapReply<TMsg> reply)
        {
            if (reply.IsStateless(resp)) throw _quarantine.MarkStateless(peer, reply.Root, reply.Label);

            if (reply.Verify != null && !RunVerify(reply.Verify, resp, reply.Kind, peer))
            {
                _quarantine.QuarantineSnapState(peer);
                throw new InvalidOperationException(reply.RejectMessage);
            }
            return resp;
        }

        internal Func<IEthPeer, bool> ServingAt(byte[] stateRoot)
            => p => _quarantine.SnapStateServingFilter(p) && !_quarantine.IsStateless(p, stateRoot);

        private bool RunVerify<TMsg>(Func<TMsg, bool> verify, TMsg resp, string kind, IEthPeer peer)
        {
            try
            {
                return verify(resp);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    "snap.peer.verify.faulted request={Kind} peer={Host} error={ErrorType}: {Error} — treating as verification failure (quarantine+rotate)",
                    kind, SyncPeerSession.ParseHost(peer.Enode), ex.GetType().Name, ex.Message);
                return false;
            }
        }
    }
}
