using System;
using Nethereum.Hex.HexConvertors.Extensions;
using PositionPoolKey = Nethereum.Uniswap.V4.Positions.PositionManager.ContractDefinition.PoolKey;
using QuoterPoolKey = Nethereum.Uniswap.V4.Pricing.V4Quoter.ContractDefinition.PoolKey;
using Nethereum.Util;

namespace Nethereum.Uniswap.V4.Pools
{
    public class PoolKeyUtils
    {
        public static PoolKeyUtils Current { get; } = new PoolKeyUtils();

        private readonly AddressUtil _addressUtil = Nethereum.Util.AddressUtil.Current;

        public PositionPoolKey CreateNormalized(string currencyA, string currencyB, int fee, int tickSpacing, string hooks = null)
        {
            if (string.IsNullOrWhiteSpace(currencyA)) throw new ArgumentNullException(nameof(currencyA));
            if (string.IsNullOrWhiteSpace(currencyB)) throw new ArgumentNullException(nameof(currencyB));

            var checksumA = _addressUtil.ConvertToChecksumAddress(currencyA);
            var checksumB = _addressUtil.ConvertToChecksumAddress(currencyB);

            string currency0;
            string currency1;
            OrderPair(checksumA, checksumB, out currency0, out currency1);

            var normalizedHooks = string.IsNullOrEmpty(hooks)
                ? AddressUtil.ZERO_ADDRESS
                : _addressUtil.ConvertToChecksumAddress(hooks);

            return new PositionPoolKey
            {
                Currency0 = currency0,
                Currency1 = currency1,
                Fee = (uint)fee,
                TickSpacing = tickSpacing,
                Hooks = normalizedHooks
            };
        }

        public QuoterPoolKey CreateNormalizedForQuoter(string currencyA, string currencyB, int fee, int tickSpacing, string hooks = null)
        {
            var normalized = CreateNormalized(currencyA, currencyB, fee, tickSpacing, hooks);
            return ToQuoterPoolKey(normalized);
        }

        public PositionPoolKey Normalize(PositionPoolKey poolKey)
        {
            if (poolKey == null) throw new ArgumentNullException(nameof(poolKey));
            return CreateNormalized(poolKey.Currency0, poolKey.Currency1, (int)poolKey.Fee, poolKey.TickSpacing, poolKey.Hooks);
        }

        public PositionPoolKey Normalize(QuoterPoolKey poolKey)
        {
            if (poolKey == null) throw new ArgumentNullException(nameof(poolKey));
            return CreateNormalized(poolKey.Currency0, poolKey.Currency1, (int)poolKey.Fee, poolKey.TickSpacing, poolKey.Hooks);
        }

        public QuoterPoolKey ToQuoterPoolKey(PositionPoolKey poolKey)
        {
            var normalized = Normalize(poolKey);
            return new QuoterPoolKey
            {
                Currency0 = normalized.Currency0,
                Currency1 = normalized.Currency1,
                Fee = normalized.Fee,
                TickSpacing = normalized.TickSpacing,
                Hooks = normalized.Hooks
            };
        }

        public PositionPoolKey FromQuoterPoolKey(QuoterPoolKey poolKey)
        {
            return Normalize(poolKey);
        }

        public string CalculatePoolId(PositionPoolKey poolKey)
        {
            return CalculatePoolIdBytes(poolKey).ToHex(false);
        }

        public string CalculatePoolId(QuoterPoolKey poolKey)
        {
            return CalculatePoolId(FromQuoterPoolKey(poolKey));
        }

        public byte[] CalculatePoolIdBytes(PositionPoolKey poolKey)
        {
            var normalized = Normalize(poolKey);
            var encoded = normalized.EncodePoolKey();
            return Sha3Keccack.Current.CalculateHash(encoded);
        }

        public byte[] CalculatePoolIdBytes(QuoterPoolKey poolKey)
        {
            return CalculatePoolIdBytes(FromQuoterPoolKey(poolKey));
        }

        private void OrderPair(string currencyA, string currencyB, out string currency0, out string currency1)
        {
            var bytesA = currencyA.HexToByteArray();
            var bytesB = currencyB.HexToByteArray();

            var length = Math.Min(bytesA.Length, bytesB.Length);
            for (var i = 0; i < length; i++)
            {
                if (bytesA[i] == bytesB[i])
                {
                    continue;
                }

                if (bytesA[i] > bytesB[i])
                {
                    currency0 = currencyB;
                    currency1 = currencyA;
                }
                else
                {
                    currency0 = currencyA;
                    currency1 = currencyB;
                }
                return;
            }

            if (string.CompareOrdinal(currencyA, currencyB) <= 0)
            {
                currency0 = currencyA;
                currency1 = currencyB;
            }
            else
            {
                currency0 = currencyB;
                currency1 = currencyA;
            }
        }
    }
}



