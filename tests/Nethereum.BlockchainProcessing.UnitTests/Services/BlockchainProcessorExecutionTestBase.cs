using Moq;
using Nethereum.BlockchainProcessing.Orchestrator;
using Nethereum.Contracts;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.ProgressRepositories;

namespace Nethereum.BlockchainProcessing.UnitTests.Services
{
    public class BlockchainProcessorExecutionTestBase: BlockchainProcessorTestBase
    {
        protected List<BlockRange> _orchestratedBlockRanges = new List<BlockRange>();
        protected List<BigInteger> _blocksUpsertedInToProgressRepo = new List<BigInteger>();
        protected CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        protected void SetupProgressRepoUpsertMock()
        {
            _progressRepoMock
                .Setup(p => p.UpsertProgressAsync(It.IsAny<BigInteger>()))
                .Returns<BigInteger>((blk) =>
                {
                    _blocksUpsertedInToProgressRepo.Add(blk);
                    return Task.CompletedTask;
                });
        }

        protected void SetupOrchestratorMock(bool invokeCancellationTokenOnceHandled = false, int? iterationsBeforeCancellation = null)
        {
            
            _orchestratorMock
                .Setup(o => o.ProcessAsync(It.IsAny<BigInteger>(), It.IsAny<BigInteger>(),
                    It.IsAny<CancellationToken>(), _progressRepoMock.Object))
                .Returns<BigInteger, BigInteger, CancellationToken, IBlockProgressRepository>((from, to, ctx, progressRepo) =>
                {
                    _orchestratedBlockRanges.Add(new BlockRange(from, to));

                    if(iterationsBeforeCancellation.HasValue && iterationsBeforeCancellation == _orchestratedBlockRanges.Count)
                    {
                        _cancellationTokenSource.Cancel();
                    }
                    else if (invokeCancellationTokenOnceHandled)
                    {
                        _cancellationTokenSource.Cancel();
                    }

                    return Task.FromResult(new OrchestrationProgress { BlockNumberProcessTo = to });
                });
        }

        protected Exception SetupOrchestratorMockToSimulateError(Exception ex)
        {
            _ = _orchestratorMock
                .Setup(expression: o => o.ProcessAsync(It.IsAny<BigInteger>(), It.IsAny<BigInteger>(),
                    It.IsAny<CancellationToken>(), _progressRepoMock.Object))
                .Returns<BigInteger, BigInteger, CancellationToken, IBlockProgressRepository>((from, to, ctx, progressRepo) =>
                {
                    return Task.FromResult(new OrchestrationProgress { Exception = ex });
                });

            return ex;
        }

        protected void SetupProgressRepoForNoPriorProgress()
        {
            _progressRepoMock
                .Setup(p => p.GetLastBlockNumberProcessedAsync())
                .ReturnsAsync((BigInteger?)null);
        }

        protected void SetupProgressRepoForPreviousProgress(BigInteger lastBlockProcessed)
        {
            _progressRepoMock
                .Setup(p => p.GetLastBlockNumberProcessedAsync())
                .ReturnsAsync(lastBlockProcessed);
        }

        protected void SetupLastConfirmedBlockNumberMock(bool invokeCancellationTokenOnceHandled = false)
        {
            _lastConfirmedBlockNumberMock
                .Setup(s => s.GetLastConfirmedBlockNumberAsync(It.IsAny<BigInteger>(), It.IsAny<CancellationToken>()))
                .Returns<BigInteger, CancellationToken>((blk, ctx) =>
                {
                    if (invokeCancellationTokenOnceHandled)
                    {
                        _cancellationTokenSource.Cancel();
                    }
                    return Task.FromResult(blk);
                });
        }
    }
}
