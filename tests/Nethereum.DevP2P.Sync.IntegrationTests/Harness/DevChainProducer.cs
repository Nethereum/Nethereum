using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain;
using Nethereum.Chain.TestData;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.DevP2P.Sync.Publish;
using Nethereum.Model.P2P;

namespace Nethereum.DevP2P.Sync.IntegrationTests.Harness
{
    public sealed class DevChainProducer
    {
        private readonly DevChainNode _node;
        private readonly BlockProducer _producer;
        private readonly ChainConfig _chainConfig;
        private readonly IIncrementalStateRootCalculator _stateRootCalculator;

        private DevChainProducer(DevChainNode node, BlockProducer producer, ChainConfig chainConfig,
            IIncrementalStateRootCalculator stateRootCalculator)
        {
            _node = node;
            _producer = producer;
            _chainConfig = chainConfig;
            _stateRootCalculator = stateRootCalculator;
        }

        public static async Task<DevChainProducer> AttachAsync(DevChainNode node, string coinbase = null)
        {
            var bundle = node.Bundle;
            var activations = new FixedChainActivations(HardforkNames.Parse("prague"));

            var chainConfig = new ChainConfig
            {
                ChainId = (long)node.ChainId,
                BaseFee = BigInteger.Zero,
                Coinbase = coinbase ?? "0x0000000000000000000000000000000000000000",
                BlockGasLimit = DevChainGenesis.BlockGasLimit
            };
            var hardforkConfig = chainConfig.GetHardforkConfig();

            var stateRootCalculator = new IncrementalStateRootCalculator(bundle.State, bundle.TrieNodes);
            var engine = new BlockExecutor(
                bundle.State,
                bundle.Blocks,
                activations,
                chainConfigFactory: _ => chainConfig,
                hardforkConfigFactory: _ => hardforkConfig,
                stateRootCalculator: stateRootCalculator,
                rewardPolicy: NoRewardPolicy.Instance,
                trieNodeStore: bundle.TrieNodes,
                logger: null);

            var producer = new BlockProducer(
                engine,
                bundle.Blocks,
                bundle.Transactions,
                bundle.Receipts,
                bundle.Logs,
                bundle.State,
                bundle.TrieNodes,
                stateRootCalculator,
                withdrawalStore: bundle.Withdrawals);

            var attached = new DevChainProducer(node, producer, chainConfig, stateRootCalculator);
            await attached.EnsureGenesisAsync();
            return attached;
        }

        public async Task EnsureGenesisAsync()
        {
            if (await _node.Bundle.Blocks.GetHeightAsync() >= 0) return;

            await ChainGenesisData.ApplyAsync(_node.Bundle.State);

            var stateRoot = await _stateRootCalculator.ComputeFullStateRootAsync();
            var genesis = new BlockHeader
            {
                ParentHash = new byte[32],
                UnclesHash = "1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347".HexToByteArray(),
                Coinbase = _chainConfig.Coinbase,
                StateRoot = stateRoot ?? new byte[32],
                TransactionsHash = "56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".HexToByteArray(),
                ReceiptHash = "56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".HexToByteArray(),
                LogsBloom = new byte[256],
                Difficulty = Nethereum.Util.EvmUInt256.One,
                BlockNumber = 0,
                GasLimit = (long)_chainConfig.BlockGasLimit,
                GasUsed = 0,
                Timestamp = 1700000000,
                ExtraData = Array.Empty<byte>(),
                MixHash = new byte[32],
                Nonce = new byte[8]
            };
            GenesisHeaderFields.Apply(genesis, HardforkNames.Parse("prague"));

            await _node.Bundle.Blocks.SaveAsync(genesis, BlockHashCalculator.ForHeader(genesis));
        }

        public async Task<BigInteger> HeightAsync() => await _node.Bundle.Blocks.GetHeightAsync();

        public async Task<BlockProductionResult> ProduceAsync(bool allowEmpty = true)
        {
            var pending = await _node.TxPool.GetPendingAsync(256);
            if ((pending == null || pending.Count == 0) && !allowEmpty) return null;

            var result = await _producer.ProduceBlockAsync(
                pending ?? new List<ISignedTransaction>(),
                new BlockProductionOptions
                {
                    Coinbase = _chainConfig.Coinbase,
                    BlockGasLimit = _chainConfig.BlockGasLimit,
                    BaseFee = BigInteger.Zero,
                    Difficulty = BigInteger.One,
                    ChainId = _node.ChainId,
                    ExtraData = Array.Empty<byte>(),
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                });

            if (result?.Header == null) return result;

            if (pending != null && pending.Count > 0)
            {
                await _node.TxPool.RemoveBatchAsync(pending.Select(t => t.Hash).ToList());
            }

            await PublishAsync(result);
            return result;
        }

        private async Task PublishAsync(BlockProductionResult result)
        {
            var transactions = await _node.Bundle.Transactions.GetByBlockHashAsync(result.BlockHash)
                               ?? new List<ISignedTransaction>();

            var payload = NewBlockMessageEncoder.Encode(new NewBlockMessage
            {
                Header = result.Header,
                Transactions = transactions,
                Uncles = new List<BlockHeader>(),
                Withdrawals = new List<Withdrawal>(),
                TotalDifficulty = result.Header.Difficulty
            });

            await _node.BroadcastPool.BroadcastAsync(Eth68MessageIds.NewBlock, payload);
        }
    }
}
