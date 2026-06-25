using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AppChain.Sync;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Sync;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.DevP2P.IntegrationTests
{
    [Collection(DevP2PGethFixture.COLLECTION_NAME)]
    public class TwoFollowerSyncIntegrationTests
    {
        private static readonly (string Address, string BalanceHex)[] GenesisAlloc = new[]
        {
            ("0x12890d2cce102216644c59daE5baed380d84830c", "0x900000000000000000000"),
            ("0x27Ef5cDBe01777D62438AfFeb695e33fC2335979", "0x9000000000000000000000000000000"),
            ("0xE65B318b9dECf504d1cb6Ea5C367Ca657a070Db1", "0x1000000000000000000000000000000")
        };

        private readonly DevP2PGethFixture _fixture;
        private readonly ITestOutputHelper _output;

        public TwoFollowerSyncIntegrationTests(
            DevP2PGethFixture fixture,
            ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [Fact]
        public async Task TwoFollowers_ParallelSync_BothStateRootsMatchGeth()
        {
            var recipient = "0x27Ef5cDBe01777D62438AfFeb695e33fC2335979";
            for (int i = 0; i < 3; i++)
            {
                var receipt = await _fixture.SendEtherFromSealerAsync(
                    recipient, BigInteger.Parse("20000000000000000") + i);
                _output.WriteLine($"Tx #{i + 1} included in block #{receipt.BlockNumber.Value}");
            }

            var web3 = _fixture.GetWeb3();
            var targetBlock = (ulong)(await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync()).Value;
            _output.WriteLine($"Target block: {targetBlock}");

            var followerA = RunFollowerAsync("A", targetBlock);
            var followerB = RunFollowerAsync("B", targetBlock);
            var results = await Task.WhenAll(followerA, followerB);

            var rootA = results[0];
            var rootB = results[1];

            _output.WriteLine($"Follower A final stateRoot: {rootA.ToHex(true)}");
            _output.WriteLine($"Follower B final stateRoot: {rootB.ToHex(true)}");

            Assert.True(ByteUtil.AreEqual(rootA, rootB),
                $"Followers diverged: A={rootA.ToHex(true)} B={rootB.ToHex(true)}");

            var gethTargetHeaderJson = await web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber
                .SendRequestAsync(new RPC.Eth.DTOs.BlockParameter(targetBlock));
            Assert.Equal(gethTargetHeaderJson.StateRoot, rootA.ToHex(true));

            _output.WriteLine($"PASS: 2 followers synced block 1..{targetBlock} in parallel, both stateRoots match Geth");
        }

        private async Task<byte[]> RunFollowerAsync(string label, ulong targetBlock)
        {
            var stateStore = SeedGenesisAlloc();
            var chainConfig = new ChainConfig
            {
                ChainId = DevP2PGethFixture.ChainId,
                BaseFee = BigInteger.Zero,
                Coinbase = DevP2PGethFixture.SealerAddress,
                Hardfork = "london"
            };
            var blockStore = new InMemoryBlockStore();
            var trieNodeStore = new InMemoryTrieNodeStore();
            var stateRootCalculator = new IncrementalStateRootCalculator(stateStore, trieNodeStore);
            var activations = new FixedChainActivations(HardforkName.London);
            var engine = new BlockExecutor(
                stateStore,
                blockStore,
                activations,
                chainConfigFactory: _ => chainConfig,
                hardforkConfigFactory: _ => chainConfig.GetHardforkConfig(),
                stateRootCalculator: stateRootCalculator,
                rewardPolicy: EthereumProofOfWorkRewardPolicy.Instance,
                trieNodeStore: trieNodeStore);
            var blockImporter = new BlockImporter(engine, blockStore, stateStore);

            var devP2PConfig = new DevP2PConfig
            {
                NetworkId = _fixture.NetworkId,
                ConnectTimeoutMs = 5000,
                HandshakeTimeoutMs = 5000,
                RequestTimeoutMs = 10000
            };
            await using var client = new DevP2PSequencerRpcClient(
                _fixture.Enode, devP2PConfig, _fixture.GenesisHash);

            byte[] lastRoot = Array.Empty<byte>();
            for (ulong blockNumber = 1; blockNumber <= targetBlock; blockNumber++)
            {
                var data = await client.GetBlockWithReceiptsAsync(blockNumber);
                Assert.NotNull(data);

                var result = await blockImporter.ImportAsync(data!.Header, data.Transactions, uncles: null, withdrawals: null);
                Assert.True(result.Exception == null, $"[{label}] Block {blockNumber} failed: {result.ErrorMessage}");
                Assert.True(result.RootMatches,
                    $"[{label}] Block {blockNumber} stateRoot mismatch: " +
                    $"expected {result.ExpectedStateRoot?.ToHex(true)} got {result.ComputedStateRoot?.ToHex(true)}");

                lastRoot = result.ComputedStateRoot!;
            }
            _output.WriteLine($"[{label}] synced {targetBlock} blocks, final root {lastRoot.ToHex(true).Substring(0, 18)}...");
            return lastRoot;
        }

        private InMemoryStateStore SeedGenesisAlloc()
        {
            var stateStore = new InMemoryStateStore();
            foreach (var (address, balanceHex) in GenesisAlloc)
            {
                var balance = new HexBigInteger(balanceHex).Value;
                stateStore.SaveAccountAsync(address, new Account
                {
                    Nonce = EvmUInt256.Zero,
                    Balance = EvmUInt256.FromBigEndian(balance.ToByteArray(isUnsigned: true, isBigEndian: true)),
                    CodeHash = DefaultValues.EMPTY_DATA_HASH
                }).GetAwaiter().GetResult();
            }
            return stateStore;
        }
    }
}
