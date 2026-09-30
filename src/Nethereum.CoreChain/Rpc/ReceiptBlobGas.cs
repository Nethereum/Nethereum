using System.Numerics;
using Nethereum.EVM;
using Nethereum.EVM.Precompiles;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.CoreChain.Rpc
{
    public static class ReceiptBlobGas
    {
        public static (BigInteger? BlobGasUsed, BigInteger? BlobGasPrice) Resolve(
            ISignedTransaction tx, BlockHeader header, BigInteger chainId)
        {
            if (!(tx is Transaction4844 blob)) return (null, null);

            BigInteger? blobGasPrice = null;
            if (header?.ExcessBlobGas != null &&
                ChainActivationsRegistry.Instance.TryGet((long)chainId, out var activations))
            {
                var fork = activations.ResolveAt((long)header.BlockNumber, (ulong)header.Timestamp);
                blobGasPrice = BlobBaseFee(
                    DefaultMainnetHardforkRegistry.Instance.Get(fork), header.ExcessBlobGas.Value);
            }

            return (BlobGasUsed(blob), blobGasPrice);
        }

        public static (BigInteger? BlobGasUsed, BigInteger? BlobGasPrice) Resolve(
            ISignedTransaction tx, BlockHeader header, ChainConfig config)
        {
            if (!(tx is Transaction4844 blob)) return (null, null);

            BigInteger? blobGasPrice = null;
            if (header?.ExcessBlobGas != null && config != null)
            {
                var hardforkConfig = config.GetHardforkConfigAt((long)header.BlockNumber, (ulong)header.Timestamp);
                blobGasPrice = BlobBaseFee(hardforkConfig, header.ExcessBlobGas.Value);
            }

            return (BlobGasUsed(blob), blobGasPrice);
        }

        private static BigInteger BlobGasUsed(Transaction4844 blob)
            => (BigInteger)(blob.BlobVersionedHashes?.Count ?? 0) * BlobGasCalculator.GAS_PER_BLOB;

        private static BigInteger? BlobBaseFee(HardforkConfig hardforkConfig, long excessBlobGas)
        {
            var blobRule = hardforkConfig.IntrinsicGasRules.Blob;
            return blobRule != null
                ? (BigInteger)blobRule.CalculateBlobBaseFee(new EvmUInt256((ulong)excessBlobGas))
                : (BigInteger?)null;
        }
    }
}
