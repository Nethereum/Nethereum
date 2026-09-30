using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Chain.TestData.Vectors;
using Xunit;

namespace Nethereum.Chain.TestData.UnitTests
{
    public class PostSnapFlatStateTests
    {
        private static BigInteger Eth(long n) => new BigInteger(n) * BigInteger.Pow(10, 18);
        private static BigInteger Num(byte[] b) => b == null || b.Length == 0 ? BigInteger.Zero : new BigInteger(b, isUnsigned: true, isBigEndian: true);

        [Fact]
        public async Task PostSnap_ForwardBlock_ReadsAndModifiesAnExistingSnappedSlot()
        {
            // ctor: SSTORE(slot0, 0x2a); runtime on every CALL: SSTORE(slot0, SLOAD(slot0) + 1).
            // This is the read-modify-write of an existing storage slot — the shape of an ERC20 balance update.
            var incrementBytecode = "0x602a600055600a6011600039600a6000f360005460010160005500".HexToByteArray();

            var sequencer = await InProcessSequencerDriver.CreateAsync(generatedAccounts: 50);
            var contract = sequencer.QueueDeploy(sequencer.Accounts.Alice, incrementBytecode);
            await sequencer.ProduceBlockAsync();

            var pivot = (int)(await sequencer.Blocks.GetHeightAsync());
            var pivotHeader = await sequencer.Blocks.GetByNumberAsync(pivot);
            var pivotHash = await sequencer.Blocks.GetHashByNumberAsync(pivot);
            using var follower = await SnapFollowerNode.SnapAsync(sequencer, pivotHeader, pivotHash);

            Assert.Equal(new BigInteger(42), Num(await follower.State.GetStorageAsync(contract, 0)));

            sequencer.QueueCall(sequencer.Accounts.Bob, contract, Array.Empty<byte>());
            await sequencer.ProduceBlockAsync();

            await follower.ForwardExecuteAsync(sequencer.ProducedBlockData.Skip(pivot));

            Assert.Equal(new BigInteger(43), Num(await follower.State.GetStorageAsync(contract, 0)));
            Assert.Equal(
                Num(await sequencer.State.GetStorageAsync(contract, 0)),
                Num(await follower.State.GetStorageAsync(contract, 0)));
        }

        [Fact]
        public async Task PostSnap_FlatStore_ResolvesSnappedAccounts_AndBareStateExecutionMatches()
        {
            var sequencer = await InProcessSequencerDriver.CreateAsync(generatedAccounts: 100);
            await new WorkloadV1().BuildAsync(sequencer);

            var pivot = (int)(await sequencer.Blocks.GetHeightAsync());
            var pivotHeader = await sequencer.Blocks.GetByNumberAsync(pivot);
            var pivotHash = await sequencer.Blocks.GetHashByNumberAsync(pivot);

            using var follower = await SnapFollowerNode.SnapAsync(sequencer, pivotHeader, pivotHash);

            var probe = sequencer.Accounts.All[50].Address;
            var flat = await follower.State.GetAccountAsync(probe);
            Assert.NotNull(flat);
            Assert.Equal((await sequencer.State.GetAccountAsync(probe)).Balance, flat.Balance);
            Assert.Equal((await sequencer.State.GetAccountAsync(probe)).Nonce, flat.Nonce);

            sequencer.QueueTransfer(sequencer.Accounts.Alice, sequencer.Accounts.Bob.Address, Eth(1));
            await sequencer.ProduceBlockAsync();
            sequencer.QueueTransfer(sequencer.Accounts.Dave, sequencer.Accounts.Erin.Address, Eth(2));
            await sequencer.ProduceBlockAsync();

            var matched = await follower.TryForwardExecuteFlatStateOnlyAsync(sequencer.ProducedBlockData.Skip(pivot));
            Assert.True(matched, "post-snap forward execution on bare b.State should match now that flat is populated");
        }

        [Fact]
        public async Task PostSnap_FlatStore_ResolvesSnappedContractStorage()
        {
            var sequencer = await InProcessSequencerDriver.CreateAsync(generatedAccounts: 20);
            await new WorkloadV1().BuildAsync(sequencer);

            var pivot = (int)(await sequencer.Blocks.GetHeightAsync());
            var pivotHeader = await sequencer.Blocks.GetByNumberAsync(pivot);
            var pivotHash = await sequencer.Blocks.GetHashByNumberAsync(pivot);
            using var follower = await SnapFollowerNode.SnapAsync(sequencer, pivotHeader, pivotHash);

            string contractAddr = null;
            System.Collections.Generic.Dictionary<byte[], byte[]> serverStorage = null;
            foreach (var a in await sequencer.State.GetAllAccountsAsync())
            {
                var st = await sequencer.State.GetAllStorageAsync(a.Key);
                if (st.Count > 0) { contractAddr = a.Key; serverStorage = st; break; }
            }
            Assert.NotNull(contractAddr);

            var followerStorage = await follower.State.GetAllStorageAsync(contractAddr);

            foreach (var kv in serverStorage)
            {
                Assert.True(followerStorage.TryGetValue(kv.Key, out var got), "snapped slot missing from the flat store");
                Assert.Equal(
                    new BigInteger(kv.Value, isUnsigned: true, isBigEndian: true),
                    new BigInteger(got, isUnsigned: true, isBigEndian: true));
            }
        }
    }
}
