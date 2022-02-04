using Nethereum.Quorum.RPC.Debug;

namespace Nethereum.Quorum.RPC.Services
{
    public interface IDebugQuorumService
    {
        IDebugDumpAddress DebugDumpAddress { get; }
        IDebugPrivateStateRoot DebugPrivateStateRoot { get; }
    }
}