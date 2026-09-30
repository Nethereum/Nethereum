using System;
using Nethereum.DevP2P.Rlpx;
using Nethereum.Model.P2P;

namespace Nethereum.DevP2P.Sync.Abstractions
{
    public interface IEthPeerRegistry
    {
        Guid Register(RlpxConnection connection, int ethOffset, Eth68StatusMessage remoteStatus);

        void Unregister(Guid id);
    }
}
