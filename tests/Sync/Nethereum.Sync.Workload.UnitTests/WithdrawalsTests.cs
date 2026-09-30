using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Xunit;

namespace Nethereum.Chain.TestData.UnitTests
{
    /// <summary>
    /// EIP-4895 withdrawals are a configurable core block-production feature. Appchains don't emit them by
    /// default, but the producer/executor/header/sync paths must handle them (a mainnet follower sees them on
    /// every post-Shanghai block). These tests exercise that surface through the real sequencer producer.
    /// </summary>
    public class WithdrawalsTests
    {
        // EIP-4895 credits amount(gwei) * 1e9 wei.
        private const ulong Gwei = 1_000_000_000;

        [Fact]
        public async Task Producer_Withdrawal_CreditsState_AndStampsRoot()
        {
            var sequencer = await InProcessSequencerDriver.CreateAsync(generatedAccounts: 5);

            await sequencer.ProduceBlockAsync();
            var emptyRoot = (await sequencer.Blocks.GetByNumberAsync(1)).WithdrawalsRoot;

            var recipient = "0x00000000000000000000000000000000000000ab";
            sequencer.QueueWithdrawal(recipient, 1_000_000);
            await sequencer.ProduceBlockAsync();

            var acct = await sequencer.State.GetAccountAsync(recipient);
            Assert.NotNull(acct);
            Assert.Equal(new Nethereum.Util.EvmUInt256(1_000_000UL * Gwei), acct.Balance);

            var wRoot = (await sequencer.Blocks.GetByNumberAsync(2)).WithdrawalsRoot;
            Assert.False(Nethereum.Util.ByteUtil.AreEqual(emptyRoot, wRoot),
                "a block with a withdrawal must carry a different withdrawals root than an empty block");

            var stored = await sequencer.Withdrawals.GetByBlockNumberAsync(2);
            Assert.Single(stored);
            Assert.Equal(1_000_000UL, stored[0].AmountInGwei);
        }
    }
}
