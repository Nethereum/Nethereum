using System.Numerics;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.Blocks;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RPC.Fee1559Calculators
{

#if !DOTNET35
    public class DefaultFeeCalculationStrategy : IFee1559CalculationStrategy
    {
        public IClient Client { get; set; }
        private EthGetBlockWithTransactionsHashesByNumber _ethGetBlockWithTransactionsHashes;
        public static BigInteger DEFAULT_MAX_PRIORITY_FEE_PER_GAS = 2000000000;

        public DefaultFeeCalculationStrategy(IClient client)
        {
            Client = client;
            _ethGetBlockWithTransactionsHashes = new EthGetBlockWithTransactionsHashesByNumber(client);
        }

        public async Task<Fee1559> CalculateFee(BigInteger? maxPriorityFeePerGas = null)
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