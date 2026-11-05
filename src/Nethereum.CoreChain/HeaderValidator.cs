using Nethereum.Documentation;
using System;
using System.Numerics;
using System.Text;
using Nethereum.EVM;
using Nethereum.Model;
using Nethereum.Model.Codecs;

namespace Nethereum.CoreChain
{
    [NethereumDocExample(DocSection.ChainInfrastructure, "corechain", "SealRules - which seal a header is judged under")]
    public enum SealRules
    {
        ProofOfStake,
        Ethash,
        Clique
    }

    public static class HeaderValidator
    {
        public const int MaximumExtraDataSize = 32;

        public const long AllowedFutureBlockTimeSeconds = 15;

        public const int DaoForkExtraRange = 10;

        public static readonly byte[] DaoForkBlockExtra = Encoding.ASCII.GetBytes("dao-hard-fork");

        [NethereumDocExample(DocSection.ChainInfrastructure, "corechain", "HeaderValidator.Validate - the header rules for one block")]
        public static BlockValidityCheck? Validate(
            BlockHeader header,
            BlockHeader parent,
            HardforkName fork,
            HardforkConfig config,
            SealRules seal,
            long? unixNow = null,
            bool daoExtraDataRequired = false,
            bool uncle = false,
            bool feeFreeBaseFee = false)
        {
            if (header == null) throw new ArgumentNullException(nameof(header));
            if (parent == null) throw new ArgumentNullException(nameof(parent));

            var codec = BlockHeaderCodecs.ForFork(fork);
            var ethash = seal == SealRules.Ethash;
            var clique = seal == SealRules.Clique;
            var proofOfStake = seal == SealRules.ProofOfStake;

            if (!clique && header.ExtraData != null && header.ExtraData.Length > MaximumExtraDataSize)
                return BlockValidityCheck.ExtraDataSize;

            if (proofOfStake && !IsZero(header.Nonce))
                return BlockValidityCheck.Nonce;

            if (!ethash && header.UnclesHash != null && !AreEqual(header.UnclesHash, DefaultValues.EMPTY_UNCLES_HASH))
                return BlockValidityCheck.UnclesHash;

            var timestamp = unchecked((ulong)header.Timestamp);
            if (ethash && !uncle && unixNow.HasValue
                && timestamp > unchecked((ulong)(unixNow.Value + AllowedFutureBlockTimeSeconds)))
                return BlockValidityCheck.FutureBlock;

            if (!clique && timestamp <= unchecked((ulong)parent.Timestamp))
                return BlockValidityCheck.Timestamp;

            if (proofOfStake && !header.Difficulty.IsZero)
                return BlockValidityCheck.Difficulty;

            if (ethash && (BigInteger)header.Difficulty != EthashDifficulty.Calculate(fork, timestamp, parent))
                return BlockValidityCheck.Difficulty;

            if (header.GasLimit < 0)
                return BlockValidityCheck.GasLimitTooBig;

            if (header.GasUsed < 0 || header.GasUsed > header.GasLimit)
                return BlockValidityCheck.GasCapacity;

            if (!ethash && !IsNextBlockNumber(header, parent))
                return BlockValidityCheck.BlockNumber;

            var feeVerdict = codec.CarriesBaseFee
                ? VerifyBaseFeeAndGasLimit(header, parent, feeFreeBaseFee)
                : VerifyGasLimitWithoutBaseFee(header, parent);
            if (feeVerdict != null) return feeVerdict;

            if (ethash && !IsNextBlockNumber(header, parent))
                return BlockValidityCheck.BlockNumber;

            if ((header.WithdrawalsRoot != null) != codec.CarriesWithdrawalsRoot)
                return BlockValidityCheck.WithdrawalsRootPresence;

            var blobVerdict = VerifyBlobFields(header, parent, codec, config);
            if (blobVerdict != null) return blobVerdict;

            if (codec.CarriesBlockAccessList != (header.BlockAccessListHash != null))
                return BlockValidityCheck.BlockAccessListHashPresence;

            if (codec.CarriesBlockAccessList != header.SlotNumber.HasValue)
                return BlockValidityCheck.SlotNumberPresence;

            if (ethash && daoExtraDataRequired && !AreEqual(header.ExtraData, DaoForkBlockExtra))
                return BlockValidityCheck.DaoExtraData;

            return null;
        }

        public static bool RequiresDaoExtraData(IChainActivations activations, long blockNumber, ulong timestamp)
        {
            for (var back = 0; back < DaoForkExtraRange; back++)
            {
                var candidate = blockNumber - back;
                if (candidate < 0) return false;
                if (activations.ResolveAt(candidate, timestamp) != HardforkName.DaoFork) continue;
                if (candidate == 0 || activations.ResolveAt(candidate - 1, timestamp) != HardforkName.DaoFork) return true;
            }
            return false;
        }

        private static BlockValidityCheck? VerifyBaseFeeAndGasLimit(BlockHeader header, BlockHeader parent, bool feeFreeBaseFee)
        {
            var parentGasLimit = new BigInteger(unchecked((ulong)parent.GasLimit));
            if (parent.BaseFee == null)
                parentGasLimit *= GasLimitBound.ElasticityMultiplier;

            if (!GasLimitIsWithinBound(parentGasLimit, header.GasLimit))
                return BlockValidityCheck.GasLimitBound;

            if (header.BaseFee is not Nethereum.Util.EvmUInt256 declaredBaseFee)
                return BlockValidityCheck.BaseFee;

            var expectedBaseFee = feeFreeBaseFee ? Nethereum.Util.EvmUInt256.Zero : BaseFeeCalculator.CalculateExpectedBaseFeePerGas(
                parent.BaseFee, parent.GasLimit, parent.GasUsed);
            return declaredBaseFee == expectedBaseFee ? null : BlockValidityCheck.BaseFee;
        }

        private static BlockValidityCheck? VerifyGasLimitWithoutBaseFee(BlockHeader header, BlockHeader parent)
        {
            if (header.BaseFee != null) return BlockValidityCheck.BaseFee;

            return GasLimitIsWithinBound(new BigInteger(unchecked((ulong)parent.GasLimit)), header.GasLimit)
                ? null
                : BlockValidityCheck.GasLimitBound;
        }

        private static bool GasLimitIsWithinBound(BigInteger parentGasLimit, long headerGasLimit)
        {
            var difference = BigInteger.Abs(new BigInteger(unchecked((ulong)headerGasLimit)) - parentGasLimit);
            return difference < parentGasLimit / GasLimitBound.BoundDivisor
                && headerGasLimit >= GasLimitBound.MinimumGasLimit;
        }

        private static BlockValidityCheck? VerifyBlobFields(
            BlockHeader header, BlockHeader parent, IBlockHeaderCodec codec, HardforkConfig config)
        {
            if (!codec.CarriesBlobFieldsAndBeaconRoot)
            {
                if (header.ExcessBlobGas.HasValue || header.BlobGasUsed.HasValue)
                    return BlockValidityCheck.BlobFieldFormat;
                return header.ParentBeaconBlockRoot != null ? BlockValidityCheck.ParentBeaconRootPresence : null;
            }

            if (header.ParentBeaconBlockRoot == null)
                return BlockValidityCheck.ParentBeaconRootPresence;

            if (!header.ExcessBlobGas.HasValue || !header.BlobGasUsed.HasValue)
                return BlockValidityCheck.BlobFieldFormat;

            var blobGasUsed = header.BlobGasUsed.Value;
            if (blobGasUsed > (long)config.MaxBlobsPerBlock * BlobGasCalculator.GAS_PER_BLOB
                || blobGasUsed % BlobGasCalculator.GAS_PER_BLOB != 0)
                return BlockValidityCheck.BlobGasCapacity;

            var expectedExcessBlobGas = BlockExecutor.ExpectedExcessBlobGas(parent, config);
            return unchecked((ulong)header.ExcessBlobGas.Value) == expectedExcessBlobGas
                ? null
                : BlockValidityCheck.ExcessBlobGas;
        }

        private static bool IsNextBlockNumber(BlockHeader header, BlockHeader parent) =>
            (ulong)header.BlockNumber == (ulong)parent.BlockNumber + 1;

        private static bool IsZero(byte[] value)
        {
            if (value == null) return true;
            foreach (var b in value)
                if (b != 0) return false;
            return true;
        }

        private static bool AreEqual(byte[] left, byte[] right) => Nethereum.Util.ByteUtil.AreEqual(left, right);
    }
}
