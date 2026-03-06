using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.CoreChain.Consensus
{
    public interface IConsensusEngine
    {
        string Name { get; }

        bool CanProduceBlock(long blockNumber);

        Task<TimeSpan> GetSigningDelayAsync(long blockNumber, CancellationToken cancellationToken = default);

        BigInteger GetDifficulty(long blockNumber, string signerAddress);

        byte[] PrepareExtraData(long blockNumber, object? vote = null);

        byte[] SignBlock(BlockHeader header);

        void ApplyBlock(BlockHeader header, string signer, byte[] blockHash);

        bool ValidateBlock(BlockHeader header, BlockHeader? parent);

        string? RecoverSigner(BlockHeader header);
    }
}
