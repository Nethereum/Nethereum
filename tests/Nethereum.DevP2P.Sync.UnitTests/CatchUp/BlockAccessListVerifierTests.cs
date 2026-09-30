using Nethereum.DevP2P.Sync.Snap.CatchUp;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests.CatchUp
{
    public class BlockAccessListVerifierTests
    {
        private static List<AccountChanges> OneAccountBal()
        {
            var account = new AccountChanges("0x" + new string('1', 40));
            account.BalanceChanges.Add(new BalanceChange(0, new EvmUInt256(500)));
            return new List<AccountChanges> { account };
        }

        [Fact]
        public void Verify_MatchingEntry_ReturnsVerifiedWithDecodedAccessList()
        {
            var bal = OneAccountBal();
            var raw = BlockAccessListRLPEncoder.Current.Encode(bal);
            var hash = BlockAccessListRLPEncoder.Current.Hash(bal);

            var result = new BlockAccessListVerifier().Verify(new[] { hash }, new[] { raw });

            var one = Assert.Single(result);
            Assert.Equal(BlockAccessListStatus.Verified, one.Status);
            Assert.Equal(new EvmUInt256(500), Assert.Single(Assert.Single(one.AccessList).BalanceChanges).PostBalance);
        }

        [Fact]
        public void Verify_ZeroLengthEntry_ReturnsUnavailable()
        {
            var result = new BlockAccessListVerifier().Verify(new[] { new byte[32] }, new[] { new byte[0] });
            Assert.Equal(BlockAccessListStatus.Unavailable, Assert.Single(result).Status);
        }

        [Fact]
        public void Verify_EntryHashDoesNotMatchHeader_ReturnsHashMismatch()
        {
            var raw = BlockAccessListRLPEncoder.Current.Encode(OneAccountBal());
            var result = new BlockAccessListVerifier().Verify(new[] { new byte[32] }, new[] { raw });
            Assert.Equal(BlockAccessListStatus.HashMismatch, Assert.Single(result).Status);
        }

        [Fact]
        public void Verify_EmptyAccessListEntry_IsVerifiedNotUnavailable()
        {
            var empty = new List<AccountChanges>();
            var raw = BlockAccessListRLPEncoder.Current.Encode(empty);
            var hash = BlockAccessListRLPEncoder.Current.Hash(empty);

            var one = Assert.Single(new BlockAccessListVerifier().Verify(new[] { hash }, new[] { raw }));

            Assert.Equal(BlockAccessListStatus.Verified, one.Status);
            Assert.Empty(one.AccessList);
        }

        [Fact]
        public void Verify_MoreEntriesThanRequested_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                new BlockAccessListVerifier().Verify(new[] { new byte[32] }, new[] { new byte[1], new byte[1] }));
        }

        [Fact]
        public void Verify_ShortResponse_VerifiesReturnedPrefixOnly_WithNoPadding()
        {
            var bal = OneAccountBal();
            var raw = BlockAccessListRLPEncoder.Current.Encode(bal);
            var hash = BlockAccessListRLPEncoder.Current.Hash(bal);

            var result = new BlockAccessListVerifier().Verify(
                new[] { hash, hash, new byte[32] },
                new[] { raw, raw });

            Assert.Equal(2, result.Count);
            Assert.Equal(BlockAccessListStatus.Verified, result[0].Status);
            Assert.Equal(BlockAccessListStatus.Verified, result[1].Status);
        }

        [Fact]
        public void Verify_MixedStatuses_AlignByIndex()
        {
            var bal = OneAccountBal();
            var raw = BlockAccessListRLPEncoder.Current.Encode(bal);
            var hash = BlockAccessListRLPEncoder.Current.Hash(bal);

            var result = new BlockAccessListVerifier().Verify(
                new[] { hash, new byte[32], new byte[32] },
                new[] { raw, new byte[0], raw });

            Assert.Equal(BlockAccessListStatus.Verified, result[0].Status);
            Assert.Equal(BlockAccessListStatus.Unavailable, result[1].Status);
            Assert.Equal(BlockAccessListStatus.HashMismatch, result[2].Status);
        }
    }
}
