using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.Model;

namespace Nethereum.AppChain.Sequencer
{
    public interface ISequencer
    {
        SequencerConfig Config { get; }
        IAppChain AppChain { get; }
        CoreChain.ITxPool TxPool { get; }
        IPolicyEnforcer PolicyEnforcer { get; }

        event EventHandler<BlockProductionResult>? BlockProduced;

        Task StartAsync(CancellationToken cancellationToken = default);
        Task StopAsync();

        Task<byte[]> SubmitTransactionAsync(ISignedTransaction transaction);
        Task<byte[]> ProduceBlockAsync();

        Task<BigInteger> GetBlockNumberAsync();
        Task<BlockHeader?> GetLatestBlockAsync();
    }
}
