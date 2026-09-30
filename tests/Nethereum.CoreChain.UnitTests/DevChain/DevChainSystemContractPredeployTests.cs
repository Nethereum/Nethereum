using System.Threading.Tasks;
using Nethereum.CoreChain.Forks;
using Nethereum.DevChain;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.DevChain
{
    public class DevChainSystemContractPredeployTests
    {
        // EIP-2935 §Specification: "| HISTORY_STORAGE_ADDRESS | 0x0000F90827F1C53a10cb7A02335B175320002935 |".
        private const string SpecifiedHistoryStorageAddress =
            "0x0000F90827F1C53a10cb7A02335B175320002935";

        private const string SpecifiedHistoryStorageRuntimeCode =
            "0x3373fffffffffffffffffffffffffffffffffffffffe14604657602036036042575f356001430381" +
            "11604257611fff81430311604257611fff9006545f5260205ff35b5f5ffd5b5f35611fff6001430306" +
            "5500";

        // EIP-4788 §Specification: "| BEACON_ROOTS_ADDRESS | 0x000F3df6D732807Ef1319fB7B8bB8522d0Beac02 |".
        private const string SpecifiedBeaconRootsAddress =
            "0x000F3df6D732807Ef1319fB7B8bB8522d0Beac02";

        private const string SpecifiedBeaconRootsRuntimeCode =
            "0x3373fffffffffffffffffffffffffffffffffffffffe14604d57602036146024575f5ffd5b5f3580" +
            "1560495762001fff810690815414603c575f5ffd5b62001fff01545f5260205ff35b5f5ffd5b62001f" +
            "ff42064281555f359062001fff015500";

        // EIP-2935 §Specification / EIP-161 handling: "the account at HISTORY_STORAGE_ADDRESS
        // will have code and a nonce of 1, and will be exempt from EIP-161 cleanup."
        private const int SpecifiedPredeployNonce = 1;


        private static readonly byte[] BlockHashProbeCode = "43600290034060005260206000f3".HexToByteArray();

        private const string ProbeAddress = "0x00000000000000000000000000000000000b1a5b";

        [Fact]
        public async Task Given_ADevChainAtPrague_When_BlockHashIsQueriedForARecentBlock_Then_ItResolvesFromTheHistoryContract()
        {
            using var node = DevChainNode.CreateInMemory();
            await node.StartAsync();
            Assert.Equal(HardforkName.Prague, HardforkNames.Parse(node.Config.Hardfork));

            await node.SetCodeAsync(ProbeAddress, BlockHashProbeCode);

            await node.MineBlockAsync();
            await node.MineBlockAsync();
            await node.MineBlockAsync();

            var latest = await node.GetBlockNumberAsync();
            var expected = await node.GetBlockHashByNumberAsync(latest - 1);

            var result = await node.CallAsync(ProbeAddress, System.Array.Empty<byte>());

            Assert.True(result.Success, result.RevertReason);
            Assert.Equal(expected.ToHex(), result.ReturnData.ToHex());
        }

        [Fact]
        public async Task Given_ADevChainGenesis_Then_TheEip2935AndEip4788PredeploysArePresentWithCanonicalBytecode()
        {
            using var node = DevChainNode.CreateInMemory();
            await node.StartAsync();

            var historyCode = await CodeAtGenesisAsync(node, SpecifiedHistoryStorageAddress);
            var beaconRootsCode = await CodeAtGenesisAsync(node, SpecifiedBeaconRootsAddress);

            Assert.Equal(SpecifiedHistoryStorageRuntimeCode, historyCode.ToHex(true));
            Assert.Equal(SpecifiedBeaconRootsRuntimeCode, beaconRootsCode.ToHex(true));

            Assert.Equal(
                SpecifiedPredeployNonce,
                (int)await node.GetNonceAsync(SpecifiedHistoryStorageAddress));
            Assert.Equal(
                SpecifiedPredeployNonce,
                (int)await node.GetNonceAsync(SpecifiedBeaconRootsAddress));
        }

        [Fact]
        public async Task Given_ADevChainPinnedBeforeCancun_Then_NeitherPredeployIsAllocated()
        {
            using var node = DevChainNode.CreateInMemory(new DevChainConfig { Hardfork = "shanghai" });
            await node.StartAsync();

            Assert.Empty(await CodeAtGenesisAsync(node, SpecifiedHistoryStorageAddress));
            Assert.Empty(await CodeAtGenesisAsync(node, SpecifiedBeaconRootsAddress));
        }

        [Fact]
        public async Task Given_ADevChainMiningWithAndWithoutABeaconRoot_When_TheBlockIsMined_Then_TheBeaconRootsPredeployRecordsBoth()
        {
            using var node = DevChainNode.CreateInMemory();
            await node.StartAsync();

            await node.MineBlockAsync();
            var zeroRootBlock = await node.GetBlockByNumberAsync(await node.GetBlockNumberAsync());
            Assert.Equal(new byte[32].ToHex(), zeroRootBlock.ParentBeaconBlockRoot.ToHex());

            var storedTimestamp = await node.GetStorageAtAsync(
                SpecifiedBeaconRootsAddress, TimestampSlot(zeroRootBlock.Timestamp));
            Assert.Equal(
                zeroRootBlock.Timestamp,
                (long)new System.Numerics.BigInteger(storedTimestamp, isUnsigned: true, isBigEndian: true));
            Assert.All(
                await node.GetStorageAtAsync(
                    SpecifiedBeaconRootsAddress, RootSlot(zeroRootBlock.Timestamp))
                    ?? System.Array.Empty<byte>(),
                b => Assert.Equal(0, b));

            var root = new byte[32];
            root[31] = 0x2a;
            await node.MineBlockAsync(root);
            var withRoot = await node.GetBlockByNumberAsync(await node.GetBlockNumberAsync());

            var stored = await node.GetStorageAtAsync(
                SpecifiedBeaconRootsAddress, RootSlot(withRoot.Timestamp));
            Assert.Equal(root.ToHex(), stored.PadTo32Bytes().ToHex());
        }

        private static EvmUInt256 TimestampSlot(long timestamp)
            => new EvmUInt256(Eip4788Helpers.ComputeTimestampSlot(timestamp, Eip4788Constants.HistoryBufferLength));

        private static EvmUInt256 RootSlot(long timestamp)
            => new EvmUInt256(Eip4788Helpers.ComputeRootSlot(timestamp, Eip4788Constants.HistoryBufferLength));

        private static async Task<byte[]> CodeAtGenesisAsync(DevChainNode node, string address)
            => await node.GetCodeAsync(address, 0) ?? System.Array.Empty<byte>();
    }
}
