using System;
using System.Numerics;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage;
using Nethereum.EVM;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.Chain.TestData
{
    public static class ChainGenesisData
    {
        public const string DefaultFork = "prague";
        public const long DefaultChainId = 420420;

        /// <summary>
        /// Allocate the predeploys the fork activates and fund every account with a balance.
        /// A Prague genesis that omits the EIP-7685 request predeploys produces a chain whose
        /// first block is refused, so this is not optional decoration.
        /// </summary>
        public static async Task ApplyAsync(
            IStateStore stateStore,
            string fork = DefaultFork,
            IEnumerable<ChainAccount> accounts = null)
        {
            if (stateStore == null) throw new ArgumentNullException(nameof(stateStore));

            await SystemContractPredeploys.ApplyGenesisAllocationAsync(
                stateStore,
                HardforkNames.Parse(fork),
                SystemContractPredeploys.All).ConfigureAwait(false);

            foreach (var account in accounts ?? ChainAccounts.Funded)
            {
                await FundAsync(stateStore, account).ConfigureAwait(false);
            }
        }

        public static async Task FundAsync(IStateStore stateStore, ChainAccount account)
        {
            var address = account.Address.ToLowerInvariant();
            var existing = await stateStore.GetAccountAsync(address).ConfigureAwait(false);

            await stateStore.SaveAccountAsync(address, new Account
            {
                Balance = (EvmUInt256)account.Balance,
                Nonce = existing?.Nonce ?? EvmUInt256.Zero,
                CodeHash = existing?.CodeHash
            }).ConfigureAwait(false);
        }

        public static readonly BigInteger RosterPrefundBalance = BigInteger.Parse("1000000000000000000000");

        public static string[] PrefundedRoster(int generatedCount = 0, params string[] extra)
        {
            var addresses = new List<string>();
            foreach (var account in ChainRoster.Take(generatedCount)) addresses.Add(account.Address);
            foreach (var address in extra ?? Array.Empty<string>())
                if (!string.IsNullOrEmpty(address)) addresses.Add(address);
            return addresses.ToArray();
        }

        public static string[] PrefundedAddresses(IEnumerable<ChainAccount> accounts = null)
        {
            var result = new List<string>();
            foreach (var account in accounts ?? ChainAccounts.Funded) result.Add(account.Address);
            return result.ToArray();
        }
    }
}
