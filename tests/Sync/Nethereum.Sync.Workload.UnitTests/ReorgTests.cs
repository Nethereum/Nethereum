using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.Chain.TestData.UnitTests
{
    public class ReorgTests
    {
        private static BigInteger Eth(long n) => new BigInteger(n) * BigInteger.Pow(10, 18);

        private static async Task DrivePrefixAsync(InProcessSequencerDriver s)
        {
            for (var i = 0; i < 3; i++)
            {
                s.QueueTransfer(s.Accounts.Alice, s.Accounts.Bob.Address, Eth(1));
                await s.ProduceBlockAsync();
            }
        }

        private static async Task DriveForkAsync(InProcessSequencerDriver s, string recipient)
        {
            for (var i = 0; i < 2; i++)
            {
                s.QueueTransfer(s.Accounts.Alice, recipient, Eth(1));
                await s.ProduceBlockAsync();
            }
        }

        [Fact]
        public async Task Follower_OnWrongFork_RewindsAndConvergesToCanonical()
        {
            var seqA = await InProcessSequencerDriver.CreateAsync();
            var seqB = await InProcessSequencerDriver.CreateAsync();
            await DrivePrefixAsync(seqA);
            await DrivePrefixAsync(seqB);
            await DriveForkAsync(seqA, seqA.Accounts.Carol.Address);
            await DriveForkAsync(seqB, seqB.Accounts.Dave.Address);

            Assert.Equal(await seqA.Blocks.GetHashByNumberAsync(3), await seqB.Blocks.GetHashByNumberAsync(3));
            Assert.NotEqual(await seqA.Blocks.GetHashByNumberAsync(4), await seqB.Blocks.GetHashByNumberAsync(4));

            using var follower = await ReorgFollowerNode.CreateAsync(seqA);
            await follower.ImportAsync(seqA.ProducedBlockData);
            Assert.Equal(await seqA.Blocks.GetHashByNumberAsync(5), await follower.Blocks.GetHashByNumberAsync(5));

            await follower.RewindToAsync(3);
            await follower.ImportAsync(seqB.ProducedBlockData.Skip(3));

            await Nethereum.Chain.TestData.ChainEquivalence.AssertEquivalentAsync(seqB.Stores, follower.Stores);
        }

        [Fact]
        public async Task Reorg_UndoesContractState_WhenForkDeployedAContract()
        {
            var seqA = await InProcessSequencerDriver.CreateAsync();
            var seqB = await InProcessSequencerDriver.CreateAsync();
            await DrivePrefixAsync(seqA);
            await DrivePrefixAsync(seqB);

            var contract = seqA.QueueDeploy(seqA.Accounts.Carol, WorkloadContracts.StorageLoggerBytecode);
            await seqA.ProduceBlockAsync();
            seqB.QueueTransfer(seqB.Accounts.Carol, seqB.Accounts.Dave.Address, Eth(1));
            await seqB.ProduceBlockAsync();

            Assert.NotEqual(await seqA.Blocks.GetHashByNumberAsync(4), await seqB.Blocks.GetHashByNumberAsync(4));

            using var follower = await ReorgFollowerNode.CreateAsync(seqA);
            await follower.ImportAsync(seqA.ProducedBlockData);
            Assert.True(await follower.State.AccountExistsAsync(contract));
            Assert.NotEmpty(await follower.State.GetAllStorageAsync(contract));

            await follower.RewindToAsync(3);
            await follower.ImportAsync(seqB.ProducedBlockData.Skip(3));

            Assert.False(await follower.State.AccountExistsAsync(contract));
            Assert.Empty(await follower.State.GetAllStorageAsync(contract));
            await Nethereum.Chain.TestData.ChainEquivalence.AssertEquivalentAsync(seqB.Stores, follower.Stores);
        }
    }
}
