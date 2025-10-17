using Nethereum.Contracts;
using Nethereum.Contracts.Constants;
using Nethereum.Contracts.QueryHandlers.MultiCall;
using Nethereum.Contracts.Services;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Uniswap.V4.Pricing.V4Quoter.ContractDefinition;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nethereum.Uniswap.V4.V4Quoter
{
    public class QuoteResult
    {
        public QuoteExactParams Params { get; set; }
        public QuoteExactInputOutputDTO Output { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
    }

    public partial class V4QuoterService
    {
        /// <summary>
        /// Gets multiple quotes using multicall for improved performance.
        /// Batches multiple quote requests into a single RPC call.
        /// </summary>
        public async Task<List<QuoteResult>> GetQuotesUsingMultiCallAsync(
            IEnumerable<QuoteExactParams> quoteParams,
            BlockParameter block = null,
            int numberOfCallsPerRequest = MultiQueryHandler.DEFAULT_CALLS_PER_REQUEST,
            string multiCallAddress = CommonAddresses.MULTICALL_ADDRESS)
        {
            var quoteCalls = new List<MulticallInputOutput<QuoteExactInputFunction, QuoteExactInputOutputDTO>>();

            foreach (var param in quoteParams)
            {
                var quoteCall = new QuoteExactInputFunction { Params = param };
                quoteCalls.Add(new MulticallInputOutput<QuoteExactInputFunction, QuoteExactInputOutputDTO>(
                    quoteCall,
                    ContractAddress));
            }

            var ethApiContractService = Web3.Eth;
            var multiqueryHandler = ethApiContractService.GetMultiQueryHandler(multiCallAddress);

            await multiqueryHandler.MultiCallAsync(numberOfCallsPerRequest, quoteCalls.ToArray()).ConfigureAwait(false);

            return quoteCalls.Select(x => new QuoteResult
            {
                Params = x.Input.Params,
                Output = x.Output,
                Success = x.Output != null,
                ErrorMessage = x.Output == null ? "Quote failed" : null
            }).ToList();
        }

        /// <summary>
        /// Gets multiple quotes using multicall for improved performance (uses latest block).
        /// </summary>
        public Task<List<QuoteResult>> GetQuotesUsingMultiCallAsync(
            IEnumerable<QuoteExactParams> quoteParams,
            int numberOfCallsPerRequest = MultiQueryHandler.DEFAULT_CALLS_PER_REQUEST,
            string multiCallAddress = CommonAddresses.MULTICALL_ADDRESS)
        {
            return GetQuotesUsingMultiCallAsync(
                quoteParams,
                BlockParameter.CreateLatest(),
                numberOfCallsPerRequest,
                multiCallAddress);
        }

        /// <summary>
        /// Gets multiple quotes using RPC batch for improved performance.
        /// Uses direct RPC batching instead of multicall contract.
        /// </summary>
        public async Task<List<QuoteResult>> GetQuotesUsingRpcBatchAsync(
            IEnumerable<QuoteExactParams> quoteParams,
            BlockParameter block = null,
            int numberOfCallsPerRequest = MultiQueryHandler.DEFAULT_CALLS_PER_REQUEST)
        {
            var quoteCalls = new List<MulticallInputOutput<QuoteExactInputFunction, QuoteExactInputOutputDTO>>();

            foreach (var param in quoteParams)
            {
                var quoteCall = new QuoteExactInputFunction { Params = param };
                quoteCalls.Add(new MulticallInputOutput<QuoteExactInputFunction, QuoteExactInputOutputDTO>(
                    quoteCall,
                    ContractAddress));
            }

            var ethApiContractService = Web3.Eth;
            var multiqueryBatchHandler = ethApiContractService.GetMultiQueryBatchRpcHandler();

            await multiqueryBatchHandler.MultiCallAsync(numberOfCallsPerRequest, quoteCalls.ToArray()).ConfigureAwait(false);

            return quoteCalls.Select(x => new QuoteResult
            {
                Params = x.Input.Params,
                Output = x.Output,
                Success = x.Output != null,
                ErrorMessage = x.Output == null ? "Quote failed" : null
            }).ToList();
        }

        /// <summary>
        /// Gets multiple quotes using RPC batch for improved performance (uses latest block).
        /// </summary>
        public Task<List<QuoteResult>> GetQuotesUsingRpcBatchAsync(
            IEnumerable<QuoteExactParams> quoteParams,
            int numberOfCallsPerRequest = MultiQueryHandler.DEFAULT_CALLS_PER_REQUEST)
        {
            return GetQuotesUsingRpcBatchAsync(
                quoteParams,
                BlockParameter.CreateLatest(),
                numberOfCallsPerRequest);
        }
    }
}
