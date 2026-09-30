using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AppChain.Genesis;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.EVM;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.AppChain.UnitTests
{
    public class AppChainGenesisPrefundingTests
    {
        private const string Fork = "prague";

        private static SystemContractPredeploy APredeployWithCode() =>
            SystemContractPredeploys.For(HardforkName.Prague)
                .First(p => p.RuntimeCode != null && p.RuntimeCode.Length > 0);

        private static AppChainGenesisBuilder BuilderOver(InMemoryStateStore store)
        {
            var config = AppChainConfig.Default;
            config.Hardfork = Fork;
            return new AppChainGenesisBuilder(config, store);
        }

        [Fact]
        public async Task Given_APredeployAddressIsAlsoPrefunded_When_GenesisIsBuilt_Then_ItKeepsItsCodeAndNonce()
        {
            var predeploy = APredeployWithCode();
            var store = new InMemoryStateStore();

            var builder = BuilderOver(store);
            builder.AddPrefundedAccount(predeploy.Address, new BigInteger(1000));
            await builder.ApplyGenesisStateAsync();

            var account = await store.GetAccountAsync(predeploy.Address);
            Assert.NotNull(account);
            Assert.NotNull(account.CodeHash);

            var code = await store.GetCodeAsync(account.CodeHash);
            Assert.Equal(predeploy.RuntimeCode, code);
            Assert.Equal(SystemContractPredeploy.Nonce, account.Nonce);
            Assert.Equal((EvmUInt256)1000L, account.Balance);
        }

        [Fact]
        public async Task Given_AnOrdinaryAddressIsPrefunded_When_GenesisIsBuilt_Then_ItHasTheBalanceAndNoCode()
        {
            const string address = "0x00000000000000000000000000000000000ae4e4";
            var store = new InMemoryStateStore();

            var builder = BuilderOver(store);
            builder.AddPrefundedAccount(address, new BigInteger(7777));
            await builder.ApplyGenesisStateAsync();

            var account = await store.GetAccountAsync(address);
            Assert.NotNull(account);
            Assert.Equal((EvmUInt256)7777L, account.Balance);

            Assert.Equal(DefaultValues.EMPTY_DATA_HASH, account.CodeHash);
            Assert.Equal((ulong)0, account.Nonce);
        }
    }
}
