using Nethereum.BlockchainProcessing.Services;
using Nethereum.Contracts;
using Nethereum.Uniswap.V4.Pools;
using Nethereum.Uniswap.V4.Pools.PoolManager.ContractDefinition;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.Uniswap.V4.Contracts.PoolManager
{
    public partial class PoolManagerService
    {
        public async Task<List<EventLog<InitializeEventDTO>>> GetInitializeEventDTOAsync(string tokenAddress, BigInteger? fromBlockNumber, BigInteger? toBlockNumber, CancellationToken cancellationToken, int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
            int retryWeight = BlockchainLogProcessingService.RetryWeight)
        {
            var blockchainLogProcessing = Web3.Processing.Logs;

                 var filterInputTo = new FilterInputBuilder<InitializeEventDTO>().AddTopic(x => x.Currency0, tokenAddress)
                .Build(this.ContractAddress);
            var allEvents = await blockchainLogProcessing.GetAllEvents<InitializeEventDTO>(filterInputTo, fromBlockNumber, toBlockNumber,
                cancellationToken, numberOfBlocksPerRequest, retryWeight).ConfigureAwait(false);

            var filterInputFrom = new FilterInputBuilder<InitializeEventDTO>().AddTopic(x => x.Currency1, tokenAddress)
                .Build(this.ContractAddress);
            var eventsFrom = await blockchainLogProcessing.GetAllEvents<InitializeEventDTO>(filterInputFrom, fromBlockNumber, toBlockNumber,
                cancellationToken, numberOfBlocksPerRequest, retryWeight).ConfigureAwait(false);
            allEvents.AddRange(eventsFrom);
            return allEvents;
        }


        public async Task<List<EventLog<InitializeEventDTO>>> GetInitializeEventDTOAsync(
                                                                string tokenA,
                                                                string tokenB,
                                                                BigInteger? fromBlockNumber,
                                                                BigInteger? toBlockNumber,
                                                                CancellationToken cancellationToken,
                                                                int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
                                                                int retryWeight = BlockchainLogProcessingService.RetryWeight)
        {
            var blockchainLogProcessing = Web3.Processing.Logs;

            // Sort tokens so they match currency0/currency1 ordering in v4
            string currency0;
            string currency1;
            if (string.CompareOrdinal(tokenA, tokenB) < 0)
            {
                currency0 = tokenA;
                currency1 = tokenB;
            }
            else
            {
                currency0 = tokenB;
                currency1 = tokenA;
            }

            var filterInput = new FilterInputBuilder<InitializeEventDTO>()
                .AddTopic(x => x.Currency0, currency0)
                .AddTopic(x => x.Currency1, currency1)
                .Build(ContractAddress);

            var events = await blockchainLogProcessing.GetAllEvents<InitializeEventDTO>(
                filterInput,
                fromBlockNumber,
                toBlockNumber,
                cancellationToken,
                numberOfBlocksPerRequest,
                retryWeight).ConfigureAwait(false);

            return events;
        }

        public async Task<List<PoolInfo>> GetAllPoolsAsync(
            BigInteger fromBlockNumber,
            BigInteger toBlockNumber,
            CancellationToken cancellationToken = default,
            int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
            int retryWeight = BlockchainLogProcessingService.RetryWeight)
        {
            var blockchainLogProcessing = Web3.Processing.Logs;

            var filterInput = new FilterInputBuilder<InitializeEventDTO>()
                .Build(ContractAddress);

            var events = await blockchainLogProcessing.GetAllEvents<InitializeEventDTO>(
                filterInput,
                fromBlockNumber,
                toBlockNumber,
                cancellationToken,
                numberOfBlocksPerRequest,
                retryWeight).ConfigureAwait(false);

            return events.Select(e => new PoolInfo
            {
                PoolId = e.Event.Id,
                Currency0 = e.Event.Currency0,
                Currency1 = e.Event.Currency1,
                Fee = e.Event.Fee,
                TickSpacing = e.Event.TickSpacing,
                Hooks = e.Event.Hooks,
                SqrtPriceX96 = e.Event.SqrtPriceX96,
                Tick = e.Event.Tick,
                BlockNumber = (ulong)e.Log.BlockNumber.Value
            }).ToList();
        }

        public async Task<PoolInfo> FindPoolAsync(
            string currency0,
            string currency1,
            uint fee,
            int tickSpacing,
            string hooks,
            BigInteger fromBlockNumber,
            BigInteger toBlockNumber,
            CancellationToken cancellationToken = default,
            int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
            int retryWeight = BlockchainLogProcessingService.RetryWeight)
        {
            var pools = await GetAllPoolsAsync(
                fromBlockNumber,
                toBlockNumber,
                cancellationToken,
                numberOfBlocksPerRequest,
                retryWeight).ConfigureAwait(false);

            return pools.FirstOrDefault(p =>
                string.Equals(p.Currency0, currency0, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(p.Currency1, currency1, StringComparison.OrdinalIgnoreCase) &&
                p.Fee == fee &&
                p.TickSpacing == tickSpacing &&
                string.Equals(p.Hooks, hooks, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<List<PoolInfo>> GetPoolsForPairAsync(
            string currency0,
            string currency1,
            BigInteger fromBlockNumber,
            BigInteger toBlockNumber,
            CancellationToken cancellationToken = default,
            int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
            int retryWeight = BlockchainLogProcessingService.RetryWeight)
        {
            var pools = await GetAllPoolsAsync(
                fromBlockNumber,
                toBlockNumber,
                cancellationToken,
                numberOfBlocksPerRequest,
                retryWeight).ConfigureAwait(false);

            return pools.Where(p =>
                (string.Equals(p.Currency0, currency0, StringComparison.OrdinalIgnoreCase) &&
                 string.Equals(p.Currency1, currency1, StringComparison.OrdinalIgnoreCase)) ||
                (string.Equals(p.Currency0, currency1, StringComparison.OrdinalIgnoreCase) &&
                 string.Equals(p.Currency1, currency0, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }
    }

}
