using System.Numerics;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.Blocks;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RPC.Fee1559Suggestions
{

#if !DOTNET35

    public class SimpleFeeSuggestionStrategy : IFee1559SugesstionStrategy
    {
        public IClient Client { get; set; }
        private EthGetBlockWithTransactionsHashesByNumber _ethGetBlockWithTransactionsHashes;
        public static BigInteger DEFAULT_MAX_PRIORITY_FEE_PER_GAS = 2000000000;

        public SimpleFeeSuggestionStrategy(IClient client)
        {
            Client = client;
            _ethGetBlockWithTransactionsHashes = new EthGetBlockWithTransactionsHashesByNumber(client);
        }

        public async Task<Fee1559> SuggestFeeAsync(BigInteger? maxPriorityFeePerGas = null)
        {
            if (maxPriorityFeePerGas == null) maxPriorityFeePerGas = DEFAULT_MAX_PRIORITY_FEE_PER_GAS;
            var lastBlock = await _ethGetBlockWithTransactionsHashes.SendRequestAsync(BlockParameter.CreateLatest());
                
            var baseFee = lastBlock.BaseFeePerGas;
            var maxFeePerGas = baseFee.Value * 2 + maxPriorityFeePerGas;
            return new Fee1559()
            {
                BaseFee = baseFee,
                MaxPriorityFeePerGas = maxPriorityFeePerGas,
                MaxFeePerGas = maxFeePerGas
            };
        }
    }
#endif
}