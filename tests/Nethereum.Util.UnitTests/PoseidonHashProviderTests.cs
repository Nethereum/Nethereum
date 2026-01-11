using System.Linq;
using Nethereum.Util.HashProviders;
using Xunit;

namespace Nethereum.Util.UnitTests
{
    public class PoseidonHashProviderTests
    {
        [Fact]
        public void ComputeHashMatchesHasherBytes()
        {
            var provider = new PoseidonHashProvider();
            var hasher = new PoseidonHasher();
            var input = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();

            var fromProvider = provider.ComputeHash(input);
            var fromHasher = hasher.HashBytesToBytes(input);

            Assert.Equal(fromHasher, fromProvider);
        }
    }
}
