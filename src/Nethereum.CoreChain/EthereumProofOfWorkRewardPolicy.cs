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
                    await CreditAsync(stateStore, header.Coinbase, inclusionBonus, ct);
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
