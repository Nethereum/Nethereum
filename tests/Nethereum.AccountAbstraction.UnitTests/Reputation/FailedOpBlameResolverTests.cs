using Nethereum.AccountAbstraction.Bundler.Reputation;
using Xunit;

namespace Nethereum.AccountAbstraction.UnitTests.Reputation
{
    public class FailedOpBlameResolverTests
    {
        private const string Sender = "0x1000000000000000000000000000000000000001";
        private const string Factory = "0x2000000000000000000000000000000000000002";
        private const string Paymaster = "0x3000000000000000000000000000000000000003";

        [Fact]
        public void AA25_IsNeverAttributed()
        {
            var result = FailedOpBlameResolver.Resolve("AA25 invalid account nonce", Sender, Factory, Paymaster, isSenderStaked: true, isFactoryStaked: true);

            Assert.Null(result.BlamedAddress);
            Assert.False(result.IsStakedAccountabilityPenalty);
        }

        [Fact]
        public void AA1x_FactoryFailure_BlamesFactory_EvenWithAPaymasterPresent()
        {
            var result = FailedOpBlameResolver.Resolve("AA13 initCode failed or OOG", Sender, Factory, Paymaster, isSenderStaked: false, isFactoryStaked: false);

            Assert.Equal(Factory, result.BlamedAddress);
            Assert.False(result.IsStakedAccountabilityPenalty);
        }

        [Fact]
        public void AA1x_NoFactory_FallsBackToSender()
        {
            var result = FailedOpBlameResolver.Resolve("AA13 initCode failed or OOG", Sender, factory: null, Paymaster, isSenderStaked: false, isFactoryStaked: false);

            Assert.Equal(Sender, result.BlamedAddress);
            Assert.False(result.IsStakedAccountabilityPenalty);
        }

        [Fact]
        public void AA2x_AccountFailure_NoFactory_BlamesSender_NotPaymaster()
        {
            var result = FailedOpBlameResolver.Resolve("AA23 reverted", Sender, factory: null, Paymaster, isSenderStaked: false, isFactoryStaked: false);

            Assert.Equal(Sender, result.BlamedAddress);
            Assert.NotEqual(Paymaster, result.BlamedAddress);
            Assert.False(result.IsStakedAccountabilityPenalty);
        }

        [Fact]
        public void AA2x_UnstakedFactory_BlamesSender_NoHeavyPenalty()
        {
            var result = FailedOpBlameResolver.Resolve("AA23 reverted", Sender, Factory, Paymaster, isSenderStaked: false, isFactoryStaked: false);

            Assert.Equal(Sender, result.BlamedAddress);
            Assert.False(result.IsStakedAccountabilityPenalty);
        }

        [Fact]
        public void AA2x_StakedFactory_BlamesFactoryWithStakedAccountabilityPenalty()
        {
            var result = FailedOpBlameResolver.Resolve("AA23 reverted", Sender, Factory, Paymaster, isSenderStaked: false, isFactoryStaked: true);

            Assert.Equal(Factory, result.BlamedAddress);
            Assert.True(result.IsStakedAccountabilityPenalty);
        }

        [Fact]
        public void AA3x_UnstakedSender_BlamesPaymaster()
        {
            var result = FailedOpBlameResolver.Resolve("AA33 reverted", Sender, factory: null, Paymaster, isSenderStaked: false, isFactoryStaked: false);

            Assert.Equal(Paymaster, result.BlamedAddress);
            Assert.False(result.IsStakedAccountabilityPenalty);
        }

        [Fact]
        public void AA3x_StakedSender_BlamesSender_NotPaymaster()
        {
            var result = FailedOpBlameResolver.Resolve("AA33 reverted", Sender, factory: null, Paymaster, isSenderStaked: true, isFactoryStaked: false);

            Assert.Equal(Sender, result.BlamedAddress);
            Assert.NotEqual(Paymaster, result.BlamedAddress);
            Assert.False(result.IsStakedAccountabilityPenalty);
        }

        [Fact]
        public void UnrecognizedReason_FallsBackToSender()
        {
            var result = FailedOpBlameResolver.Resolve("AA90 something else", Sender, Factory, Paymaster, isSenderStaked: false, isFactoryStaked: false);

            Assert.Equal(Sender, result.BlamedAddress);
            Assert.False(result.IsStakedAccountabilityPenalty);
        }
    }
}
