using System;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage.InMemory;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Storage
{
    public class InMemoryStoreNullHashTests
    {
        public static TheoryData<string, string> Stores => new TheoryData<string, string>
        {
            { "block", nameof(ArgumentNullException) },
            { "log", nameof(ArgumentNullException) },
            { "receipt", nameof(ArgumentNullException) },
            { "transaction", nameof(ArgumentNullException) },
            { "state", nameof(ArgumentNullException) },
            { "uncle", "none" },
            { "withdrawal", "none" },
            { "blockaccesslist", "none" }
        };

        private static Task QueryWithNullHash(string store)
        {
            switch (store)
            {
                case "block": return new InMemoryBlockStore().GetByHashAsync(null);
                case "log": return new InMemoryLogStore().GetLogsByTxHashAsync(null);
                case "receipt": return new InMemoryReceiptStore().GetByTxHashAsync(null);
                case "transaction": return new InMemoryTransactionStore().GetByHashAsync(null);
                case "state": return new InMemoryStateStore().GetCodeAsync(null);
                case "uncle": return new InMemoryUncleStore().GetByBlockHashAsync(null);
                case "withdrawal": return new InMemoryWithdrawalStore().GetByBlockHashAsync(null);
                case "blockaccesslist": return new InMemoryBlockAccessListStore().GetByBlockHashAsync(null);
                default: throw new ArgumentOutOfRangeException(nameof(store));
            }
        }

        [Theory]
        [MemberData(nameof(Stores))]
        public async Task Given_ANullHash_When_AnInMemoryStoreIsQueried_Then_TheSameExceptionTypeIsThrownBeforeAndAfter(
            string store, string expected)
        {
            string observed = "none";
            try
            {
                await QueryWithNullHash(store);
            }
            catch (Exception ex)
            {
                observed = ex.GetType().Name;
            }

            Assert.Equal(expected, observed);
        }
    }
}
