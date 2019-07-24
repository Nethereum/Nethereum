#if !NET35
using Common.Logging;
using Nethereum.Hex.HexTypes;
using Nethereum.Utils;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.RPC.Eth.Blocks
{

    public class LastConfirmedBlockNumberService : ILastConfirmedBlockNumberService
    {
        private readonly IEthBlockNumber _ethBlockNumber;
        private readonly IWaitStrategy _waitStrategy;
        private readonly uint _minimumBlockConfirmations;
        private readonly ILog _log;
        public const uint DEFAULT_BLOCK_CONFIRMATIONS = 12;

        public LastConfirmedBlockNumberService(
            IEthBlockNumber ethBlockNumber,
            uint minimumBlockConfirmations = DEFAULT_BLOCK_CONFIRMATIONS,
            ILog log = null,
            IWaitStrategy waitStrategy = null
            ) : this(
                ethBlockNumber, 
                waitStrategy ?? new WaitStrategy(), 
                minimumBlockConfirmations, 
                log)
        {

        }

        public LastConfirmedBlockNumberService(
            IEthBlockNumber ethBlockNumber,
            IWaitStrategy waitStrategy,
            uint minimumBlockConfirmations = DEFAULT_BLOCK_CONFIRMATIONS,
            ILog log = null
            )
        {
            _ethBlockNumber = ethBlockNumber;
            _waitStrategy = waitStrategy;
            _minimumBlockConfirmations = minimumBlockConfirmations;
            _log = log;
        }



        public async Task<BigInteger> GetLastConfirmedBlockNumberAsync(BigInteger? waitForConfirmedBlockNumber, CancellationToken cancellationToken)
        {
            var currentBlockOnChain = await GetCurrentBlockOnChain();
            uint attemptCount = 0;

            while (!IsBlockNumberConfirmed(waitForConfirmedBlockNumber, currentBlockOnChain.Value, _minimumBlockConfirmations))
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    attemptCount++;
                    LogWaitingForBlockAvailability(currentBlockOnChain, _minimumBlockConfirmations, waitForConfirmedBlockNumber, attemptCount);
                    await _waitStrategy.Apply(attemptCount);
                    currentBlockOnChain = await GetCurrentBlockOnChain();
                }
            }

            return currentBlockOnChain;
        }

        private Task<HexBigInteger> GetCurrentBlockOnChain()
        {
            return _ethBlockNumber.SendRequestAsync();
        }

        private bool IsBlockNumberConfirmed(BigInteger? blockNumber, BigInteger currentBlockNumberOnChain, uint minimumBlockConfirmations)
        {
            if (blockNumber == null ||
                (currentBlockNumberOnChain - minimumBlockConfirmations) >= blockNumber)
            {
                return true;
            }

            return false;
        }

        private void LogWaitingForBlockAvailability(BigInteger currentBlock, uint minimumBlockConfirmations, BigInteger? maxBlockOnChain, uint attempt)
        {
            if (_log != null) _log.Info($"Waiting for current block ({currentBlock}) to be more than {minimumBlockConfirmations} confirmations behind the max block on the chain ({maxBlockOnChain}). Attempt: {attempt}.");
        }

    }

}
#endif