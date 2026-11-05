using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Chain.TestData.UnitTests
{
    internal static class FollowerStateAtPivot
    {
        public static async Task<Account> ReadAsync(IChainStoreBundle bundle, byte[] pivotStateRoot, string address)
        {
            var flat = await bundle.State.GetAccountAsync(address);
            var leaf = PatriciaTrie.LoadFromStorage(pivotStateRoot, bundle.StateTrieNodes)
                .Get(Sha3Keccack.Current.CalculateHash(address.HexToByteArray()));

            if (leaf == null)
            {
                Assert.Null(flat);
                return null;
            }

            var trie = new AccountEncoder().Decode(leaf);
            Assert.NotNull(flat);
            Assert.Equal(trie.Balance, flat.Balance);
            Assert.Equal(trie.Nonce, flat.Nonce);
            return flat;
        }
    }
}
