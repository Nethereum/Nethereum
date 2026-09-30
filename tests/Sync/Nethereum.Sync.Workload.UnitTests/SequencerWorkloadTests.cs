using System.Threading.Tasks;
using Nethereum.Chain.TestData.Vectors;
using Xunit;

namespace Nethereum.Chain.TestData.UnitTests
{
    public class SequencerWorkloadTests
    {
        [Fact]
        public async Task WorkloadV1_ProducesRealBlocksOnTheSequencer()
        {
            var node = await InProcessSequencerDriver.CreateAsync();
            var alice = node.Accounts.Alice.Address;
            var heidi = node.Accounts.Heidi.Address;

            var aliceStart = (await node.State.GetAccountAsync(alice)).Balance;
            var heidiStart = (await node.State.GetAccountAsync(heidi)).Balance;

            await new WorkloadV1().BuildAsync(node);

            Assert.Equal(50, node.Produced.Count);
            Assert.Equal(50, (int)(await node.Blocks.GetHeightAsync()));
            Assert.NotNull(await node.Blocks.GetByNumberAsync(0));

            Assert.NotEmpty(await node.Logs.GetLogsByBlockNumberAsync(14));

            Assert.NotEmpty(await node.Logs.GetLogsByBlockNumberAsync(18));

            Assert.True((await node.State.GetAllAccountsAsync()).Count > 40);

            var missing = 0;
            foreach (var addr in node.DeployedContracts)
                if (!await node.State.AccountExistsAsync(addr)) missing++;
            Assert.Equal(1, missing);

            Assert.Equal(2, (await node.Logs.GetLogsByBlockNumberAsync(11)).Count);

            Assert.Equal(2, node.Produced[0].TxCount);
            Assert.Equal(0, node.Produced[2].TxCount);
            Assert.NotEmpty(await node.Logs.GetLogsByBlockNumberAsync(5));
            Assert.NotEmpty(await node.Logs.GetLogsByBlockNumberAsync(7));

            var okStatus = (await node.Receipts.GetByBlockNumberAsync(7))[0].PostStateOrStatus;
            var revertStatus = (await node.Receipts.GetByBlockNumberAsync(9))[0].PostStateOrStatus;
            Assert.NotEqual(okStatus, revertStatus);

            var aliceEnd = (await node.State.GetAccountAsync(alice)).Balance;
            var heidiEnd = (await node.State.GetAccountAsync(heidi)).Balance;
            Assert.True(aliceEnd < aliceStart);
            Assert.True(heidiEnd > heidiStart);
        }
    }
}
