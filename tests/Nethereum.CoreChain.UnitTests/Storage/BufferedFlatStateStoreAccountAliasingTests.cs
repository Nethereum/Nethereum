using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Storage
{
    public class BufferedFlatStateStoreAccountAliasingTests
    {
        private const string Addr = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        private static byte[] Root(byte b)
        {
            var r = new byte[32];
            r[0] = b;
            return r;
        }

        [Fact]
        public async Task GetAccountAsync_SecondReadAcrossAWindow_UnaffectedByFirstReadsInPlaceMutation()
        {
            var inner = new InMemoryStateStore();
            var buffered = new BufferedFlatStateStore(inner);
            buffered.BeginBuffering();

            await buffered.SaveAccountAsync(Addr, new Account { Nonce = 1, Balance = 100, StateRoot = Root(0x11), CodeHash = Root(0x22) });

            var firstRead = await buffered.GetAccountAsync(Addr);
            Assert.Equal(0x11, firstRead.StateRoot[0]);
            firstRead.StateRoot = Root(0x99);

            var secondRead = await buffered.GetAccountAsync(Addr);
            Assert.Equal(0x11, secondRead.StateRoot[0]);
            Assert.NotEqual(0x99, secondRead.StateRoot[0]);

            var thirdRead = await buffered.GetAccountAsync(Addr);
            Assert.Equal(0x11, thirdRead.StateRoot[0]);
        }

        [Fact]
        public async Task GetAccountAsync_ReturnsDistinctInstancesOnEachCall()
        {
            var inner = new InMemoryStateStore();
            var buffered = new BufferedFlatStateStore(inner);
            buffered.BeginBuffering();

            await buffered.SaveAccountAsync(Addr, new Account { Nonce = 1, Balance = 100, StateRoot = Root(0x11), CodeHash = Root(0x22) });

            var a = await buffered.GetAccountAsync(Addr);
            var b = await buffered.GetAccountAsync(Addr);
            Assert.NotSame(a, b);
        }


        [Fact]
        public async Task StreamAccountsAsync_MutatedAfterEnumeration_DoesNotCorruptOverlaysStoredValue()
        {
            var inner = new InMemoryStateStore();
            var buffered = new BufferedFlatStateStore(inner);
            buffered.BeginBuffering();

            await buffered.SaveAccountAsync(Addr, new Account { Nonce = 1, Balance = 100, StateRoot = Root(0x11), CodeHash = Root(0x22) });

            Account streamed = null;
            await foreach (var kv in buffered.StreamAccountsAsync())
            {
                if (string.Equals(kv.Key, Addr, System.StringComparison.OrdinalIgnoreCase)) streamed = kv.Value;
            }
            Assert.NotNull(streamed);
            Assert.Equal(0x11, streamed.StateRoot[0]);

            streamed.StateRoot = Root(0x99);

            var reread = await buffered.GetAccountAsync(Addr);
            Assert.Equal(0x11, reread.StateRoot[0]);
            Assert.NotEqual(0x99, reread.StateRoot[0]);
        }

        [Fact]
        public async Task GetAllAccountsAsync_MutatedAfterEnumeration_DoesNotCorruptOverlaysStoredValue()
        {
            var inner = new InMemoryStateStore();
            var buffered = new BufferedFlatStateStore(inner);
            buffered.BeginBuffering();

            await buffered.SaveAccountAsync(Addr, new Account { Nonce = 1, Balance = 100, StateRoot = Root(0x11), CodeHash = Root(0x22) });

            var all = await buffered.GetAllAccountsAsync();
            var fetched = Assert.Single(all).Value;
            Assert.Equal(0x11, fetched.StateRoot[0]);

            fetched.StateRoot = Root(0x99);

            var reread = await buffered.GetAccountAsync(Addr);
            Assert.Equal(0x11, reread.StateRoot[0]);
            Assert.NotEqual(0x99, reread.StateRoot[0]);
        }
    }
}
