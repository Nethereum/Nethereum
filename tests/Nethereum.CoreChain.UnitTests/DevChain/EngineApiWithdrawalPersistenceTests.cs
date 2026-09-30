using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.CoreChain.Engine;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Rpc.Handlers.Standard;
using Nethereum.CoreChain.Storage;
using Nethereum.DevChain.Configuration;
using Nethereum.DevChain.Hosting;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.DTOs.Engine;
using Nethereum.Util;
using Xunit;
using ModelWithdrawal = Nethereum.Model.Withdrawal;
using EngineWithdrawal = Nethereum.RPC.Eth.DTOs.Withdrawal;

namespace Nethereum.CoreChain.UnitTests.DevChain
{
    public class EngineApiWithdrawalPersistenceTests
    {
        private const string FeeRecipient = "0x0000000000000000000000000000000000009999";
        private static readonly byte[] WithdrawalRecipient = new byte[20] { 0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0x5a,0x5a,0x5a };
        private static readonly byte[] ZeroHash32Bytes = new byte[32];
        private static readonly string ZeroHash32 = "0x" + new string('0', 64);

        private static async Task<(Nethereum.DevChain.DevChainNode Node, IEngineApiService Engine, IChainStoreBundle Bundle, IServiceProvider Provider)> CreateComposedNodeAsync()
        {
            var config = new DevChainServerConfig
            {
                Storage = "memory",
                EngineApiEnabled = true,
                Hardfork = "cancun",
            };

            var services = new ServiceCollection();
            services.AddDevChainServer(config);
            var provider = services.BuildServiceProvider();

            var node = provider.GetRequiredService<Nethereum.DevChain.DevChainNode>();
            await node.StartAsync();

            var engine = provider.GetRequiredService<IEngineApiService>();
            var bundle = provider.GetRequiredService<IChainStoreBundle>();
            return (node, engine, bundle, provider);
        }

        private static List<ModelWithdrawal> OneWithdrawal() => new()
        {
            new ModelWithdrawal { Index = 3, ValidatorIndex = 9, Address = WithdrawalRecipient, AmountInGwei = 55 }
        };

        private static async Task<ExecutionPayloadV3> ProduceRealPostShanghaiBlockAsPayloadAsync(
            Nethereum.DevChain.DevChainNode node, List<ModelWithdrawal> withdrawals)
        {
            var head = await node.Blocks.GetLatestAsync();

            var options = new BlockProductionOptions
            {
                Timestamp = head.Timestamp + 1,
                Coinbase = FeeRecipient,
                BaseFee = node.Config.BaseFee,
                BlockGasLimit = node.Config.BlockGasLimit,
                Difficulty = 0,
                PrevRandao = new byte[32] { 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7 },
                ExtraData = Array.Empty<byte>(),
                ChainId = node.Config.ChainId,
                ParentBeaconBlockRoot = ZeroHash32Bytes,
                Withdrawals = withdrawals
            };

            var production = await node.BlockManager.BlockProducer.ProduceBlockAsync(
                new List<ISignedTransaction>(), options);

            var header = production.Header;

            return new ExecutionPayloadV3
            {
                ParentHash = header.ParentHash.ToHex(true),
                FeeRecipient = header.Coinbase,
                StateRoot = header.StateRoot.ToHex(true),
                ReceiptsRoot = header.ReceiptHash.ToHex(true),
                LogsBloom = header.LogsBloom.ToHex(true),
                PrevRandao = header.MixHash.ToHex(true),
                BlockNumber = header.BlockNumber.ToHexBigInteger(),
                GasLimit = new HexBigInteger(header.GasLimit),
                GasUsed = new HexBigInteger(header.GasUsed),
                Timestamp = new HexBigInteger(header.Timestamp),
                ExtraData = (header.ExtraData ?? Array.Empty<byte>()).ToHex(true),
                BaseFeePerGas = new HexBigInteger((header.BaseFee ?? Nethereum.Util.EvmUInt256.Zero).ToBigInteger()),
                BlockHash = production.BlockHash.ToHex(true),
                Transactions = new List<string>(),
                Withdrawals = ToEngineWithdrawals(withdrawals),
                BlobGasUsed = new HexBigInteger(header.BlobGasUsed ?? 0),
                ExcessBlobGas = new HexBigInteger(header.ExcessBlobGas ?? 0)
            };
        }

        private static List<EngineWithdrawal> ToEngineWithdrawals(List<ModelWithdrawal> withdrawals)
        {
            var result = new List<EngineWithdrawal>(withdrawals.Count);
            foreach (var w in withdrawals)
            {
                result.Add(new EngineWithdrawal
                {
                    Index = new HexBigInteger(w.Index),
                    ValidatorIndex = new HexBigInteger(w.ValidatorIndex),
                    Address = w.Address.ToHex(true),
                    Amount = new HexBigInteger(w.AmountInGwei)
                });
            }
            return result;
        }

        [Fact]
        public async Task Given_engine_newPayload_with_withdrawals_When_the_payload_is_valid_Then_the_withdrawal_store_holds_them()
        {
            var (producerNode, _, _, _) = await CreateComposedNodeAsync();
            var (node, engine, bundle, _) = await CreateComposedNodeAsync();
            var withdrawals = OneWithdrawal();

            var payload = await ProduceRealPostShanghaiBlockAsPayloadAsync(producerNode, withdrawals);
            var status = await engine.NewPayloadAsync(payload, ZeroHash32);

            Assert.True(EnginePayloadStatus.Valid == status.Status, status.ValidationError);

            var stored = await bundle.Withdrawals.GetByBlockHashAsync(payload.BlockHash.HexToByteArray());

            Assert.NotNull(stored);
            var withdrawal = Assert.Single(stored);
            Assert.Equal(3UL, withdrawal.Index);
            Assert.Equal(9UL, withdrawal.ValidatorIndex);
            Assert.Equal(55UL, withdrawal.AmountInGwei);
        }

        [Fact]
        public async Task Given_engine_newPayload_with_withdrawals_When_eth_getBlockByNumber_is_called_after_Then_withdrawals_are_non_empty()
        {
            var (producerNode, _, _, _) = await CreateComposedNodeAsync();
            var (node, engine, bundle, provider) = await CreateComposedNodeAsync();
            var withdrawals = OneWithdrawal();

            var payload = await ProduceRealPostShanghaiBlockAsPayloadAsync(producerNode, withdrawals);
            var status = await engine.NewPayloadAsync(payload, ZeroHash32);
            Assert.True(EnginePayloadStatus.Valid == status.Status, status.ValidationError);

            var context = provider.GetRequiredService<RpcContext>();
            var handler = new EthGetBlockByNumberHandler();

            var hashesResponse = await handler.HandleAsync(
                new RpcRequestMessage(1, "eth_getBlockByNumber", payload.BlockNumber.HexValue, false), context);
            var hashesBlock = Assert.IsType<BlockWithTransactionHashes>(hashesResponse.ResultNewtonsoft);
            Assert.NotNull(hashesBlock.Withdrawals);
            Assert.Single(hashesBlock.Withdrawals);

            var fullResponse = await handler.HandleAsync(
                new RpcRequestMessage(1, "eth_getBlockByNumber", payload.BlockNumber.HexValue, true), context);
            var fullBlock = Assert.IsType<BlockWithTransactions>(fullResponse.ResultNewtonsoft);
            Assert.NotNull(fullBlock.Withdrawals);
            Assert.Single(fullBlock.Withdrawals);
        }
    }
}
