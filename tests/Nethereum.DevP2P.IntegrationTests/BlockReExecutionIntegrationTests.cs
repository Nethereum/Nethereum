using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.EVM;
using Nethereum.DevP2P.Rlpx;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;
using Xunit.Abstractions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.DevP2P.IntegrationTests
{
    [Collection(DevP2PGethFixture.COLLECTION_NAME)]
    [Trait("Requires", "Externals")]
    public class BlockReExecutionIntegrationTests
    {
        private static readonly (string Address, string BalanceHex)[] GenesisAlloc = new[]
        {
            ("0x12890d2cce102216644c59daE5baed380d84830c", "0x900000000000000000000"),
            ("0x27Ef5cDBe01777D62438AfFeb695e33fC2335979", "0x9000000000000000000000000000000"),
            ("0xE65B318b9dECf504d1cb6Ea5C367Ca657a070Db1", "0x1000000000000000000000000000000")
        };

        private readonly DevP2PGethFixture _fixture;
        private readonly ITestOutputHelper _output;

        public BlockReExecutionIntegrationTests(
            DevP2PGethFixture fixture,
            ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [Fact]
        public async Task ReExecuteBlockContainingTx_StateRootMatchesHeader()
        {
            var recipient = "0x27Ef5cDBe01777D62438AfFeb695e33fC2335979";
            var receipt = await _fixture.SendEtherFromSealerAsync(
                recipient, BigInteger.Parse("100000000000000000"));
            var includedBlock = (ulong)receipt.BlockNumber.Value;
            _output.WriteLine($"Tx {receipt.TransactionHash} included in block #{includedBlock}");

            await ReExecuteRangeAsync(fromBlock: 1, toBlock: includedBlock);
        }

        [Fact]
        public async Task ReExecuteFiveSequentialBlocks_AllStateRootsMatch()
        {
            var recipient = "0x27Ef5cDBe01777D62438AfFeb695e33fC2335979";
            for (int i = 0; i < 5; i++)
            {
                var receipt = await _fixture.SendEtherFromSealerAsync(
                    recipient, BigInteger.Parse("10000000000000000") + i);
                _output.WriteLine($"Tx #{i + 1} included in block #{receipt.BlockNumber.Value}");
            }

            var web3 = _fixture.GetWeb3();
            var latest = (ulong)(await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync()).Value;
            await ReExecuteRangeAsync(fromBlock: 1, toBlock: latest);
        }

        private async Task ReExecuteRangeAsync(ulong fromBlock, ulong toBlock)
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
            var (genesisHeader, _) = await FetchBlockAsync(0);
            await blockStore.SaveAsync(genesisHeader, _fixture.GenesisHash);
            var trieNodeStore = new InMemoryContentNodeStore();
            var stateRootCalculator = new IncrementalStateRootCalculator(stateStore, trieNodeStore);
            Assert.Equal(_fixture.GenesisStateRoot.ToHex(), (await stateRootCalculator.ComputeStateRootAsync()).ToHex());
            var activations = new FixedChainActivations(HardforkName.London);
            var engine = new BlockExecutor(
                stateStore,
                blockStore,
                activations,
                chainConfigFactory: _ => chainConfig,
                hardforkConfigFactory: _ => chainConfig.GetHardforkConfig(),
                stateRootCalculator: stateRootCalculator,
                rewardPolicy: NoRewardPolicy.Instance,
                trieNodeStore: trieNodeStore,
                authorResolver: _ => DevP2PGethFixture.SealerAddress,
                logger: null,
                blockRootsProvider: null,
                validation: new BlockValidationOptions { SealedByClique = true });
            var blockImporter = new BlockImporter(engine, blockStore, stateStore);

            _output.WriteLine($"Replaying blocks {fromBlock}..{toBlock} from genesis state");
            for (ulong blockNumber = fromBlock; blockNumber <= toBlock; blockNumber++)
            {
                var (header, transactions) = await FetchBlockAsync(blockNumber);
                var result = await blockImporter.ImportAsync(header, transactions, uncles: null, withdrawals: null);
                _output.WriteLine(
                    $"  Block #{blockNumber}: txs={result.TransactionsExecuted}, match={result.RootMatches}");

                Assert.True(result.Exception == null, $"Block {blockNumber} failed: {result.ErrorMessage}");
                Assert.True(result.RootMatches,
                    $"Block {blockNumber} stateRoot mismatch: " +
                    $"expected {result.ExpectedStateRoot?.ToHex(true)} got {result.ComputedStateRoot?.ToHex(true)}");
            }
            _output.WriteLine($"PASS: {toBlock - fromBlock + 1} blocks re-executed, all stateRoots match");
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

        private async Task<(BlockHeader header, IList<ISignedTransaction> transactions)> FetchBlockAsync(ulong blockNumber)
        {
            var config = new DevP2PConfig
            {
                NetworkId = _fixture.NetworkId,
                ConnectTimeoutMs = 5000,
                HandshakeTimeoutMs = 5000,
                RequestTimeoutMs = 10000
            };

            var connector = new StaticPeerConnector(config: config);
            var conn = await connector.ConnectAsync(_fixture.Enode);
            try
            {
                var ethOffset = conn.GetCapabilityOffset("eth");

                var status = new Eth68StatusMessage
                {
                    ProtocolVersion = 68,
                    NetworkId = _fixture.NetworkId,
                    TotalDifficulty = BigInteger.One,
                    BestHash = _fixture.GenesisHash,
                    GenesisHash = _fixture.GenesisHash,
                    ForkHash = ForkId.ComputeHash(_fixture.GenesisHash, Array.Empty<ulong>()),
                    ForkNext = 0
                };
                await conn.SendMessageAsync(
                    ethOffset + Eth68MessageIds.Status,
                    Eth68StatusMessageEncoder.Encode(status));
                await conn.ReceiveMessageAsync();

                var header = await RequestServedHeaderAsync(conn, ethOffset, blockNumber);

                var blockHash = RlpKeccakBlockHashProvider.Instance.ComputeBlockHash(header);

                var (_, bodiesPayload) = await conn.RequestAsync(
                    ethOffset + Eth68MessageIds.GetBlockBodies,
                    GetBlockBodiesMessageEncoder.Encode(new GetBlockBodiesMessage
                    {
                        RequestId = conn.NextRequestId(),
                        BlockHashes = new[] { blockHash }
                    }),
                    ethOffset + Eth68MessageIds.BlockBodies);
                var bodies = BlockBodiesMessageEncoder.Decode(bodiesPayload).Bodies;
                Assert.True(bodies.Count == 1, $"geth served {bodies.Count} bodies for block #{blockNumber}, whose header it had served");

                return (header, bodies[0].Transactions);
            }
            finally
            {
                await conn.DisconnectAsync();
            }
        }

        private static readonly TimeSpan HeaderServedDeadline = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan HeaderRetryInterval = TimeSpan.FromMilliseconds(200);

        private static async Task<BlockHeader> RequestServedHeaderAsync(RlpxConnection conn, int ethOffset, ulong blockNumber)
        {
            var deadline = DateTime.UtcNow + HeaderServedDeadline;
            while (true)
            {
                var (_, headersPayload) = await conn.RequestAsync(
                    ethOffset + Eth68MessageIds.GetBlockHeaders,
                    GetBlockHeadersMessageEncoder.Encode(new GetBlockHeadersMessage
                    {
                        RequestId = conn.NextRequestId(),
                        StartBlock = blockNumber,
                        Limit = 1,
                        Skip = 0,
                        Reverse = false
                    }),
                    ethOffset + Eth68MessageIds.BlockHeaders);
                var headers = BlockHeadersMessageEncoder.Decode(headersPayload).Headers;
                if (headers.Count > 0)
                {
                    Assert.True(headers.Count == 1, $"geth served {headers.Count} headers for a one-header request of block #{blockNumber}");
                    Assert.Equal(blockNumber, (ulong)headers[0].BlockNumber);
                    return headers[0];
                }
                Assert.True(DateTime.UtcNow < deadline,
                    $"geth served no header for block #{blockNumber} over eth within {HeaderServedDeadline.TotalSeconds:F0} s of its RPC reporting the block");
                await Task.Delay(HeaderRetryInterval);
            }
        }
    }
}
