using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model.P2P.Snap;

namespace Nethereum.DevP2P.Sync.Snap.Client
{
    internal sealed class SnapPeerRequests
    {
        internal const int RetryDelayMs = 1_000;

        private readonly ISnapPeer _peer;
        private readonly ulong _responseBytesBudget;

        public SnapPeerRequests(ISnapPeer peer, ulong responseBytesBudget)
        {
            _peer = peer;
            _responseBytesBudget = responseBytesBudget;
        }

        public Task<StorageRangesMessage> GetStorageRangeAsync(
            byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, ulong requestId,
            Func<StorageRangesMessage, bool> verifyResponse, CancellationToken ct)
            => _peer.GetStorageRangesAsync(new GetStorageRangesMessage
            {
                RequestId = requestId,
                RootHash = stateRoot,
                AccountHashes = accountHashes,
                StartingHash = startingHash,
                LimitHash = SnapHashRanges.MaxHash(),
                ResponseBytes = _responseBytesBudget
            },
            verifyResponse: verifyResponse,
            ct);

        public Task<AccountRangeMessage> GetOwnerAccountRangeAsync(
            byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
            => _peer.GetAccountRangeAsync(new GetAccountRangeMessage
            {
                RequestId = 0,
                RootHash = stateRoot,
                StartingHash = startingHash,
                LimitHash = limitHash,
                ResponseBytes = responseBytes
            }, ct);
    }
}
