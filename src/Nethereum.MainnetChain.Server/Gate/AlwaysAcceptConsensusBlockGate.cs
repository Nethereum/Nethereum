using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.MainnetChain.Server.Gate
{
    public sealed class AlwaysAcceptConsensusBlockGate : IConsensusBlockGate
    {
        public Task<ConsensusBlockGateResult> IsBlockCanonicalAsync(
            BlockHeader header,
            byte[] computedBlockHash,
            CancellationToken ct)
            => Task.FromResult(ConsensusBlockGateResult.Accept());
    }
}
