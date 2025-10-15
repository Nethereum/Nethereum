using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.Services;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Uniswap.V4.PositionManager.ContractDefinition;
using Nethereum.Uniswap.V4.StateView;

namespace Nethereum.Uniswap.V4.PositionManager
{
    public class V4UserPosition
    {
        public BigInteger TokenId { get; set; }
        public byte[] PoolId { get; set; }
        public PoolKey PoolKey { get; set; }
        public int TickLower { get; set; }
        public int TickUpper { get; set; }
        public BigInteger Liquidity { get; set; }
        public BigInteger Amount0 { get; set; }
        public BigInteger Amount1 { get; set; }
        public BigInteger UnclaimedFees0 { get; set; }
        public BigInteger UnclaimedFees1 { get; set; }
        public bool IsInRange { get; set; }
        public bool HasSubscriber { get; set; }
        public decimal CurrentPrice { get; set; }
        public decimal PriceAtTickLower { get; set; }
        public decimal PriceAtTickUpper { get; set; }
        public BigInteger SqrtPriceX96 { get; set; }
        public int CurrentTick { get; set; }
    }

    public partial class PositionManagerService
    {
        public async Task<List<BigInteger>> GetPositionTokenIdsByEventsAsync(
            string owner,
            BigInteger fromBlockNumber,
            BigInteger toBlockNumber,
            CancellationToken cancellationToken = default,
            int numberOfBlocksPerRequest = 1000)
        {
            var blockchainLogProcessing = new BlockchainLogProcessingService(Web3.Eth);
            var erc721Service = new Nethereum.BlockchainProcessing.Services.SmartContracts.ERC721LogProcessingService(
                blockchainLogProcessing,
                Web3.Eth);

            var ownedTokens = await erc721Service.GetErc721OwnedByAccountUsingAllTransfersForContract(
                ContractHandler.ContractAddress,
                owner,
                fromBlockNumber,
                toBlockNumber,
                cancellationToken,
                numberOfBlocksPerRequest);

            return ownedTokens.Select(x => x.TokenId).ToList();
        }

        

        public async Task<V4UserPosition> GetPositionDetailsAsync(
            BigInteger tokenId,
            string stateViewAddress)
        {
            var stateView = new StateViewService(Web3, stateViewAddress);

            var poolAndPositionInfo = await GetPoolAndPositionInfoQueryAsync(tokenId);
            var poolKey = poolAndPositionInfo.PoolKey;
            var packedInfo = poolAndPositionInfo.Info;

            var decodedInfo = V4PositionInfoDecoder.DecodePositionInfo(packedInfo);

            var liquidity = await GetPositionLiquidityQueryAsync(tokenId);

            var poolId = CalculatePoolId(poolKey);

            var slot0 = await stateView.GetSlot0QueryAsync(poolId);
            var sqrtPriceX96 = slot0.SqrtPriceX96;
            var currentTick = slot0.Tick;

            var amounts = V4LiquidityMath.GetAmountsForLiquidityByTicks(
                sqrtPriceX96,
                decodedInfo.TickLower,
                decodedInfo.TickUpper,
                liquidity);

            var feeGrowthInside = await stateView.GetFeeGrowthInsideQueryAsync(
                poolId,
                decodedInfo.TickLower,
                decodedInfo.TickUpper);

            var positionId = CalculatePositionId(
                ContractHandler.ContractAddress,
                decodedInfo.TickLower,
                decodedInfo.TickUpper,
                new byte[32]);

            var positionInfo = await stateView.GetPositionInfoQueryAsync(poolId, positionId);

            var unclaimedFees = V4FeeCalculator.CalculateUnclaimedFees(
                liquidity,
                positionInfo.FeeGrowthInside0LastX128,
                positionInfo.FeeGrowthInside1LastX128,
                feeGrowthInside.FeeGrowthInside0X128,
                feeGrowthInside.FeeGrowthInside1X128);

            var isInRange = V4PositionInfoHelper.IsPositionInRange(currentTick, decodedInfo.TickLower, decodedInfo.TickUpper);

            return new V4UserPosition
            {
                TokenId = tokenId,
                PoolId = poolId,
                PoolKey = poolKey,
                TickLower = decodedInfo.TickLower,
                TickUpper = decodedInfo.TickUpper,
                Liquidity = liquidity,
                Amount0 = amounts.Amount0,
                Amount1 = amounts.Amount1,
                UnclaimedFees0 = unclaimedFees.Fees0,
                UnclaimedFees1 = unclaimedFees.Fees1,
                IsInRange = isInRange,
                HasSubscriber = decodedInfo.HasSubscriber,
                CurrentPrice = V4PriceCalculator.CalculatePriceFromSqrtPriceX96(sqrtPriceX96),
                PriceAtTickLower = V4TickMath.GetPriceAtTick(decodedInfo.TickLower),
                PriceAtTickUpper = V4TickMath.GetPriceAtTick(decodedInfo.TickUpper),
                SqrtPriceX96 = sqrtPriceX96,
                CurrentTick = currentTick
            };
        }

        public async Task<List<V4UserPosition>> GetAllPositionsAsync(
            string owner,
            BigInteger fromBlockNumber,
            BigInteger toBlockNumber,
            string stateViewAddress,
            CancellationToken cancellationToken = default)
        {
            var tokenIds = await GetPositionTokenIdsByEventsAsync(owner, fromBlockNumber, toBlockNumber, cancellationToken);
            var positions = new List<V4UserPosition>();

            foreach (var tokenId in tokenIds)
            {
                var position = await GetPositionDetailsAsync(tokenId, stateViewAddress);
                positions.Add(position);
            }

            return positions;
        }

        private byte[] CalculatePoolId(PoolKey poolKey)
        {
            var poolKeyEncoded = poolKey.EncodePoolKey();
            var hash = Nethereum.Util.Sha3Keccack.Current.CalculateHash(poolKeyEncoded);

            var poolId = new byte[32];
            System.Array.Copy(hash, 0, poolId, 0, 32);
            return poolId;
        }

        private byte[] CalculatePositionId(string owner, int tickLower, int tickUpper, byte[] salt)
        {
            var ownerBytes = Nethereum.Util.AddressUtil.Current.ConvertToChecksumAddress(owner).HexToByteArray();
            var tickLowerBytes = new Nethereum.ABI.Encoders.IntTypeEncoder().Encode(tickLower);
            var tickUpperBytes = new Nethereum.ABI.Encoders.IntTypeEncoder().Encode(tickUpper);

            var combined = new List<byte>();
            combined.AddRange(ownerBytes);
            combined.AddRange(tickLowerBytes);
            combined.AddRange(tickUpperBytes);
            combined.AddRange(salt);

            var hash = Nethereum.Util.Sha3Keccack.Current.CalculateHash(combined.ToArray());
            return hash;
        }
    }

}
