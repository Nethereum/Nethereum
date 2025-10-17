using Nethereum.Uniswap.V4.Pools;
using Nethereum.Uniswap.V4.PositionManager;
using Nethereum.Uniswap.V4.Positions.StateView;
using Nethereum.Uniswap.V4.Pricing;
using Nethereum.Web3;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.Uniswap.V4.Positions
{

    public class PositionValueCalculator
    {
        private readonly IWeb3 _web3;
        private readonly string _positionManagerAddress;
        private readonly string _stateViewAddress;

        public PositionValueCalculator(
            IWeb3 web3,
            string positionManagerAddress,
            string stateViewAddress)
        {
            _web3 = web3;
            _positionManagerAddress = positionManagerAddress;
            _stateViewAddress = stateViewAddress;
        }

        public async Task<PositionValueResult> GetPositionValueAsync(
            BigInteger tokenId,
            int token0Decimals = 18,
            int token1Decimals = 18)
        {
            var positionManager = new PositionManagerService(_web3, _positionManagerAddress);
            var stateView = new StateViewService(_web3, _stateViewAddress);

            var liquidity = await positionManager.GetPositionLiquidityQueryAsync(tokenId);
            var positionInfo = await positionManager.GetPoolAndPositionInfoQueryAsync(tokenId);
            var positionInfoBytes = await positionManager.PositionInfoQueryAsync(tokenId);
            var decodedInfo = PositionInfoDecoder.Current.DecodePositionInfo(positionInfoBytes);

            var poolKeyBytes = positionInfo.PoolKey.EncodePoolKey();
            var poolId = Nethereum.Util.Sha3Keccack.Current.CalculateHash(poolKeyBytes);
            var slot0 = await stateView.GetSlot0QueryAsync(poolId);

            var amounts = LiquidityCalculator.Current.GetAmountsForLiquidityByTicks(
                slot0.SqrtPriceX96,
                decodedInfo.TickLower,
                decodedInfo.TickUpper,
                liquidity);

            var priceToken0InToken1 = PriceCalculator.Current.CalculatePriceFromSqrtPriceX96(
                slot0.SqrtPriceX96,
                token0Decimals,
                token1Decimals);

            var result = new PositionValueResult
            {
                Amount0 = amounts.Amount0,
                Amount1 = amounts.Amount1,
                UnclaimedFees0 = 0,
                UnclaimedFees1 = 0,
                TotalAmount0 = amounts.Amount0,
                TotalAmount1 = amounts.Amount1
            };

            var amount0Decimal = Web3.Web3.Convert.FromWei(amounts.Amount0, token0Decimals);
            var amount1Decimal = Web3.Web3.Convert.FromWei(amounts.Amount1, token1Decimals);

            result.ValueInToken0 = amount0Decimal + (amount1Decimal / priceToken0InToken1);
            result.ValueInToken1 = amount1Decimal + (amount0Decimal * priceToken0InToken1);

            return result;
        }

        public async Task<PositionValueResult> GetPositionValueInUSDAsync(
            BigInteger tokenId,
            decimal token0PriceUSD,
            decimal token1PriceUSD,
            int token0Decimals = 18,
            int token1Decimals = 18)
        {
            var value = await GetPositionValueAsync(tokenId, token0Decimals, token1Decimals);

            var amount0Decimal = Web3.Web3.Convert.FromWei(value.Amount0, token0Decimals);
            var amount1Decimal = Web3.Web3.Convert.FromWei(value.Amount1, token1Decimals);

            var valueUSD = (amount0Decimal * token0PriceUSD) + (amount1Decimal * token1PriceUSD);

            return value;
        }
    }
}



