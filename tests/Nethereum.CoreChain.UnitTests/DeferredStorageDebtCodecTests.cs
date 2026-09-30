using Nethereum.CoreChain.Storage;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class DeferredStorageDebtCodecTests
    {
        [Fact]
        public void Given_DebtWithFinalRoots_When_EncodedAndDecoded_Then_RoundTrips()
        {
            var debt = Debt(
                StorageCompleteness.FinalRootReResolved,
                finalStateRoot: Hash(0x44),
                finalStorageRoot: Hash(0x55));

            var decoded = DeferredStorageDebtCodec.Decode(DeferredStorageDebtCodec.Encode(debt));

            Assert.Equal(debt.AccountHash, decoded.AccountHash);
            Assert.Equal(debt.DiscoveredStorageRoot, decoded.DiscoveredStorageRoot);
            Assert.Equal(debt.FetchStateRoot, decoded.FetchStateRoot);
            Assert.Equal(debt.FetchPivotBlock, decoded.FetchPivotBlock);
            Assert.Equal(debt.Reason, decoded.Reason);
            Assert.Equal(debt.Status, decoded.Status);
            Assert.Equal(debt.FinalStateRoot, decoded.FinalStateRoot);
            Assert.Equal(debt.FinalStorageRoot, decoded.FinalStorageRoot);
        }

        [Fact]
        public void Given_DebtWithoutOptionalFields_When_EncodedAndDecoded_Then_NullsRoundTrip()
        {
            var debt = Debt(StorageCompleteness.DeferredBigAccount);
            debt.FetchPivotBlock = null;

            var decoded = DeferredStorageDebtCodec.Decode(DeferredStorageDebtCodec.Encode(debt));

            Assert.Null(decoded.FetchPivotBlock);
            Assert.Null(decoded.FinalStateRoot);
            Assert.Null(decoded.FinalStorageRoot);
            Assert.Equal(StorageCompleteness.DeferredBigAccount, decoded.Status);
            Assert.True(decoded.IsOpen);
        }

        [Theory]
        [InlineData(StorageCompleteness.FullRangeFetched)]
        [InlineData(StorageCompleteness.DeepComplete)]
        [InlineData(StorageCompleteness.ProofDropped)]
        public void Given_TerminalStatus_When_Checked_Then_NotOpen(StorageCompleteness status)
        {
            Assert.False(DeferredStorageDebt.IsOpenStatus(status));
        }

        private static DeferredStorageDebt Debt(
            StorageCompleteness status,
            byte[] finalStateRoot = null,
            byte[] finalStorageRoot = null)
            => new DeferredStorageDebt
            {
                AccountHash = Hash(0x11),
                DiscoveredStorageRoot = Hash(0x22),
                FetchStateRoot = Hash(0x33),
                FetchPivotBlock = 25_000_000,
                Reason = DeferredStorageReason.BigAccountSubrangeFailed,
                Status = status,
                FinalStateRoot = finalStateRoot,
                FinalStorageRoot = finalStorageRoot,
            };

        private static byte[] Hash(byte value)
        {
            var hash = new byte[32];
            hash[31] = value;
            return hash;
        }
    }
}
