using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Nethereum.DevP2P.Sync.Abstractions;

namespace Nethereum.ChainNode.Hosting
{
    public interface IChainProfile
    {
        ulong NetworkId { get; }

        byte[] GenesisHash { get; }

        (ulong[] BlockHeights, ulong[] Timestamps) ForkThresholds { get; }

        IReadOnlyList<string> Bootnodes { get; }

        IPeerHandshakeWorker CreateHandshakeWorker(ILoggerFactory loggerFactory, bool advertiseSnap2);
    }
}
