using System;
using System.Collections.Generic;
using Nethereum.Model.P2P;

namespace Nethereum.DevP2P.Sync.FullSync
{
    public sealed class BodyFetchResult
    {
        public BodyFetchResult(List<BlockBody> bodies, IReadOnlyCollection<Guid> servingPeerIds)
        {
            Bodies = bodies ?? throw new ArgumentNullException(nameof(bodies));
            ServingPeerIds = servingPeerIds ?? throw new ArgumentNullException(nameof(servingPeerIds));
        }

        public List<BlockBody> Bodies { get; }

        public IReadOnlyCollection<Guid> ServingPeerIds { get; }
    }
}
