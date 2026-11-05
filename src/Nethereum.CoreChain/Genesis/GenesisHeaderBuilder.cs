using System;
using Nethereum.CoreChain.Forks;
using Nethereum.EVM;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.CoreChain.Genesis
{
    public static class GenesisHeaderBuilder
    {
        public static BlockHeader Build(ChainConfig config, HardforkName fork, byte[] stateRoot)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (stateRoot == null) throw new ArgumentNullException(nameof(stateRoot));

            var header = new BlockHeader
            {
                ParentHash = new byte[32],
                UnclesHash = DefaultValues.EMPTY_UNCLES_HASH,
                Coinbase = config.Coinbase,
                StateRoot = stateRoot,
                TransactionsHash = DefaultValues.EMPTY_TRIE_HASH,
                ReceiptHash = DefaultValues.EMPTY_TRIE_HASH,
                LogsBloom = new byte[256],
                Difficulty = config.GenesisDifficulty ?? 0,
                BlockNumber = 0,
                GasLimit = (long)config.BlockGasLimit,
                GasUsed = 0,
                Timestamp = config.GenesisTimestamp,
                ExtraData = config.GenesisExtraData ?? new byte[0],
                MixHash = config.GenesisMixHash ?? new byte[32],
                Nonce = config.GenesisNonce ?? new byte[8],
                BaseFee = config.BaseFee,
                ExcessBlobGas = config.GenesisExcessBlobGas,
                BlobGasUsed = config.GenesisBlobGasUsed,
                SlotNumber = (ulong?)config.GenesisSlotNumber
            };

            GenesisHeaderFields.Apply(header, fork);
            return header;
        }
    }
}
