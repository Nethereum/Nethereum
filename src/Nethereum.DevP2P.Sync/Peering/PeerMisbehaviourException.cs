using System;

namespace Nethereum.DevP2P.Sync
{
    [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.DevP2P, "devp2p-sync", "PeerMisbehaviourException — a peer reply that broke a wire rule")]
    public sealed class PeerMisbehaviourException : Exception
    {
        public PeerMisbehaviourException(string message) : base(message)
        {
        }
    }
}
