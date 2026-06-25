using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.EVM;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.CoreChain
{
    /// <summary>
    /// Ethereum mainnet pre-Merge block reward schedule. Credits
    /// <c>header.Coinbase</c> with the per-fork miner reward (5 / 3 / 2 ETH
    /// per <see cref="BlockRewardCalculator.MinerReward"/>), plus the
    /// inclusion bonus and the uncle's own miner reward for every uncle in
    /// the input list. From Paris onwards the reward is zero so this policy
    /// becomes a no-op; mainnet keeps it wired anyway so the same engine
    /// can replay any historical block.
    /// </summary>
    public sealed class EthereumProofOfWorkRewardPolicy : IRewardPolicy
    {
        public static readonly EthereumProofOfWorkRewardPolicy Instance = new();

        public async Task<BigInteger> ApplyAsync(
            BlockHeader header,
            IList<BlockHeader> uncles,
            IStateStore stateStore,
            HardforkName fork,
            CancellationToken ct)
        {
            var minerReward = BlockRewardCalculator.MinerReward(fork);
            if (minerReward.IsZero) return BigInteger.Zero;

            await CreditAsync(stateStore, header.Coinbase, minerReward, ct);

            if (uncles != null)
            {
                var inclusionBonus = BlockRewardCalculator.MinerUncleInclusionReward(minerReward);
                ulong blockNumber = (ulong)header.BlockNumber;
                foreach (var uncle in uncles)
                {
                    // Miner inclusion bonus (per uncle).
                    await CreditAsync(stateStore, header.Coinbase, inclusionBonus, ct);
                    // Uncle's miner reward.
                    var uncleReward = BlockRewardCalculator.UncleReward(
                        minerReward, (ulong)uncle.BlockNumber, blockNumber);
                    await CreditAsync(stateStore, uncle.Coinbase, uncleReward, ct);
                }
            }

            return minerReward;
        }

        internal static async Task CreditAsync(IStateStore stateStore, string address, BigInteger amount, CancellationToken ct)
        {
            if (amount.IsZero) return;
            if (string.IsNullOrEmpty(address)) return;
            // NOTE: the zero address IS a valid coinbase for many test chains
            // (Hive testdata uses 0x000…0 as the miner for every block — the
            // headstate's 0x000…0 balance is the sum of all 500 block rewards).
            // We MUST credit it.
            var acc = await stateStore.GetAccountAsync(address) ?? new Account
            {
                Nonce = EvmUInt256.Zero,
                Balance = EvmUInt256.Zero,
                CodeHash = DefaultValues.EMPTY_DATA_HASH,
                StateRoot = DefaultValues.EMPTY_TRIE_HASH
            };
            var current = new BigInteger(acc.Balance.ToBigEndian(), isUnsigned: true, isBigEndian: true);
            var updated = current + amount;
            acc.Balance = EvmUInt256.FromBigEndian(updated.ToByteArray(isUnsigned: true, isBigEndian: true));
            await stateStore.SaveAccountAsync(address, acc);
        }
    }
}
