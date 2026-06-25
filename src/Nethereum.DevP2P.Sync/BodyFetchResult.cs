using System;
using System.Collections.Generic;
using Nethereum.Model.P2P;

namespace Nethereum.DevP2P.Sync
{
    /// <summary>
    /// Outcome of a body fetch that exposes which peer(s) actually served the response.
    /// Required for DevP2PBlockSource to blame the peer that returned bodies failing
    /// header-tx_root validation, so the next retry rotates to a different peer instead
    /// of asking the same lying peer again.
    /// </summary>
    public sealed class BodyFetchResult
    {
        public BodyFetchResult(List<BlockBody> bodies, IReadOnlyCollection<Guid> servingPeerIds)
        {
            Bodies = bodies ?? throw new ArgumentNullException(nameof(bodies));
            ServingPeerIds = servingPeerIds ?? throw new ArgumentNullException(nameof(servingPeerIds));
        }

        public List<BlockBody> Bodies { get; }

        /// <summary>
        /// Set of peer IDs that contributed to <see cref="Bodies"/>. Single-chunk
        /// fast path returns one entry; parallel-chunk path returns one entry per
        /// peer that served a chunk.
        /// </summary>
        public IReadOnlyCollection<Guid> ServingPeerIds { get; }
    }
}
