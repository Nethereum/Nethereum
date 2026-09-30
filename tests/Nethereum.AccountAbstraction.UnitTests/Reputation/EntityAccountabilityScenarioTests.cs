using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.Bundler.Reputation;
using Xunit;

namespace Nethereum.AccountAbstraction.UnitTests.Reputation
{
    public class EntityAccountabilityScenarioTests
    {
        private const string Sender = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string Factory = "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        private const string Paymaster = "0xcccccccccccccccccccccccccccccccccccccccc";

        private static async Task AcceptOpAsync(IReputationService reputation, string? sender, string? factory, string? paymaster, bool isSenderStaked)
        {
            if (!string.IsNullOrEmpty(sender) && isSenderStaked)
            {
                await reputation.RecordSeenAsync(sender, 1);
            }

            if (!string.IsNullOrEmpty(factory))
            {
                await reputation.RecordSeenAsync(factory, 1);
            }

            if (!string.IsNullOrEmpty(paymaster))
            {
                await reputation.RecordSeenAsync(paymaster, 1);
            }
        }

        private static async Task FailOpAsync(
            IReputationService reputation,
            string reason,
            string? sender,
            string? factory,
            string? paymaster,
            bool isSenderStaked,
            bool isFactoryStaked)
        {
            var blame = FailedOpBlameResolver.Resolve(reason, sender, factory, paymaster, isSenderStaked, isFactoryStaked);
            if (string.IsNullOrEmpty(blame.BlamedAddress)) return;

            if (!string.IsNullOrEmpty(sender) && isSenderStaked) await reputation.RecordSeenAsync(sender, -1);
            if (!string.IsNullOrEmpty(factory)) await reputation.RecordSeenAsync(factory, -1);
            if (!string.IsNullOrEmpty(paymaster)) await reputation.RecordSeenAsync(paymaster, -1);

            if (blame.IsStakedAccountabilityPenalty)
            {
                await reputation.ApplyStakedAccountabilityPenaltyAsync(blame.BlamedAddress);
            }
            else
            {
                await reputation.RecordSeenAsync(blame.BlamedAddress, 1);
            }
        }

        [Fact]
        public async Task EREP015_PaymasterOpsSeen_UnchangedAfterAccountFailure()
        {
            var reputation = new InMemoryReputationService();
            await reputation.RecordSeenAsync(Paymaster, 5);
            await reputation.RecordIncludedAsync(Paymaster);
            await reputation.RecordIncludedAsync(Paymaster);
            var pre = (await reputation.GetAsync(Paymaster))!;
            var preOpsSeen = pre.OpsSeen;
            var preOpsIncluded = pre.OpsIncluded;
            var preStatus = pre.Status;
            Assert.Equal(5, preOpsSeen);
            Assert.Equal(2, preOpsIncluded);

            await AcceptOpAsync(reputation, Sender, factory: null, Paymaster, isSenderStaked: false);
            var postSubmit = (await reputation.GetAsync(Paymaster))!;
            Assert.Equal(preOpsSeen + 1, postSubmit.OpsSeen);

            await FailOpAsync(reputation, "AA23 reverted", Sender, factory: null, Paymaster, isSenderStaked: false, isFactoryStaked: false);

            var post = (await reputation.GetAsync(Paymaster))!;
            Assert.Equal(preOpsSeen, post.OpsSeen);
            Assert.Equal(preOpsIncluded, post.OpsIncluded);
            Assert.Equal(preStatus, post.Status);
        }

        [Fact]
        public async Task EREP020_StakedFactoryOpsSeen_BumpedPastBanPenaltyAfterAccountFailure()
        {
            var reputation = new InMemoryReputationService();

            await AcceptOpAsync(reputation, sender: "0xacct1", Factory, paymaster: null, isSenderStaked: false);
            await AcceptOpAsync(reputation, sender: "0xacct2", Factory, paymaster: null, isSenderStaked: false);
            Assert.Equal(2, (await reputation.GetAsync(Factory))!.OpsSeen);

            await FailOpAsync(reputation, "AA23 reverted", sender: "0xacct2", Factory, paymaster: null, isSenderStaked: false, isFactoryStaked: true);

            var factoryRep = (await reputation.GetAsync(Factory))!;
            Assert.True(factoryRep.OpsSeen >= 10000, $"expected opsSeen >= 10000, got {factoryRep.OpsSeen}");
            Assert.Equal(ReputationStatus.Banned, factoryRep.Status);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task EREP030_AccountAccountability_DependsOnSenderStake(bool senderStaked)
        {
            var reputation = new InMemoryReputationService();

            await AcceptOpAsync(reputation, Sender, factory: null, Paymaster, isSenderStaked: senderStaked);

            var senderAfterAccept = (await reputation.GetAsync(Sender));
            var paymasterAfterAccept = (await reputation.GetAsync(Paymaster))!;
            Assert.Equal(senderStaked ? 1 : 0, senderAfterAccept?.OpsSeen ?? 0);
            Assert.Equal(1, paymasterAfterAccept.OpsSeen);

            await FailOpAsync(reputation, "AA31 paymaster deposit too low", Sender, factory: null, Paymaster, isSenderStaked: senderStaked, isFactoryStaked: false);

            var senderAfterFailure = await reputation.GetAsync(Sender);
            var paymasterAfterFailure = (await reputation.GetAsync(Paymaster))!;

            if (senderStaked)
            {
                Assert.Equal(1, senderAfterFailure!.OpsSeen);
                Assert.Equal(0, paymasterAfterFailure.OpsSeen);
            }
            else
            {
                Assert.Equal(0, senderAfterFailure?.OpsSeen ?? 0);
                Assert.Equal(1, paymasterAfterFailure.OpsSeen);
            }
        }

        [Fact]
        public async Task Replace_MovesOpsSeenCreditFromOldPaymasterToNewPaymaster()
        {
            var reputation = new InMemoryReputationService();
            const string pm1 = "0xd000000000000000000000000000000000000d";
            const string pm2 = "0xe000000000000000000000000000000000000e";

            await AcceptOpAsync(reputation, Sender, factory: null, pm1, isSenderStaked: false);
            Assert.Equal(1, (await reputation.GetAsync(pm1))!.OpsSeen);

            await reputation.RecordSeenAsync(pm1, -1);
            await AcceptOpAsync(reputation, Sender, factory: null, pm1, isSenderStaked: false);
            Assert.Equal(1, (await reputation.GetAsync(pm1))!.OpsSeen);

            await reputation.RecordSeenAsync(pm1, -1);
            await AcceptOpAsync(reputation, Sender, factory: null, pm2, isSenderStaked: false);
            Assert.Equal(0, (await reputation.GetAsync(pm1))!.OpsSeen);
            Assert.Equal(1, (await reputation.GetAsync(pm2))!.OpsSeen);

            await reputation.RecordSeenAsync(pm2, -1);
            await AcceptOpAsync(reputation, Sender, factory: null, paymaster: null, isSenderStaked: false);
            Assert.Equal(0, (await reputation.GetAsync(pm2))!.OpsSeen);
        }
    }
}
