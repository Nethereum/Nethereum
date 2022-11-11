using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.Util.UnitTests
{
    public class TransactionUtilTests
    {
        [Fact]
        public void ShouldCalculateTransactionHashFromRawSignedTransaction()
        {
            var signedTransaction = "0xf86c820cb6844d7c6d0082520894555f28cb24651c3e7dd740219300ae0e67055ad48705543df729c000801ba03908716fb056408ed2dfe94fe9d7d6ddaa68ec23565e12871f45537df8ad9fe0a06074347d7b5dcf58e55649f291cedc645d204778c94b0cfebda48aa469b5e2de";
            var expectedhash = "0xd5fd95eb09f0a3a00e4b9ce9cccb09b2323f44b7acb3e60738b716cfe9eb23f8";
            var contractAddress = TransactionUtils.CalculateTransactionHash(signedTransaction);
            Assert.True(expectedhash.IsTheSameHex(contractAddress));
        }
    }
}