using System.Numerics;
using Nethereum.Contracts;
using Nethereum.CoreChain.IntegrationTests.Contracts;
using Nethereum.CoreChain.IntegrationTests.Fixtures;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Tracing;
using Nethereum.DevChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RPC.DebugNode.Dtos.Tracing;
using Nethereum.Signer;
using Newtonsoft.Json;
using Xunit;

namespace Nethereum.CoreChain.IntegrationTests.Rpc
{
    public class DebugTraceBlockTests : IClassFixture<DevChainNodeFixture>
    {
        private readonly DevChainNodeFixture _fixture;

        public DebugTraceBlockTests(DevChainNodeFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task TraceBlockByNumber_Default_ReturnsOneEntryPerTxWithStructLogs()
        {
            var contract = await _fixture.DeployERC20Async(BigInteger.Parse("1000000000000000000000"));
            var transfer = await _fixture.TransferERC20Async(contract, _fixture.RecipientAddress, 100);
            Assert.True(transfer.Success);

            var blockNumber = await _fixture.Node.GetBlockNumberAsync();
            var traces = await _fixture.Node.TraceBlockByNumberAsync(blockNumber);

            Assert.Single(traces);
            Assert.Equal(transfer.TransactionHash.ToHex(true), traces[0].TxHash);
            Assert.NotNull(traces[0].Result.StructLogs);
            Assert.NotEmpty(traces[0].Result.StructLogs);
        }

        [Fact]
        public async Task TraceBlockByNumber_PerTx_MatchesTraceTransaction_ByteForByte()
        {
            var contract = await _fixture.DeployERC20Async(BigInteger.Parse("1000000000000000000000"));
            var transfer = await _fixture.TransferERC20Async(contract, _fixture.RecipientAddress, 100);
            var blockNumber = await _fixture.Node.GetBlockNumberAsync();
            var txHash = transfer.TransactionHash.ToHex(true);

            var blockTraces = await _fixture.Node.TraceBlockByNumberAsync(blockNumber);
            var singleTrace = await _fixture.Node.TraceTransactionAsync(txHash);

            Assert.Single(blockTraces);
            Assert.Equal(
                JsonConvert.SerializeObject(singleTrace),
                JsonConvert.SerializeObject(blockTraces[0].Result));
        }

        [Fact]
        public async Task TraceBlockByNumber_CallTracer_PerTx_MatchesTraceTransactionCallTracer_ByteForByte()
        {
            var contract = await _fixture.DeployERC20Async(BigInteger.Parse("1000000000000000000000"));
            var transfer = await _fixture.TransferERC20Async(contract, _fixture.RecipientAddress, 100);
            var blockNumber = await _fixture.Node.GetBlockNumberAsync();
            var txHash = transfer.TransactionHash.ToHex(true);

            var blockTraces = await _fixture.Node.TraceBlockCallTracerByNumberAsync(blockNumber);
            var singleTrace = await _fixture.Node.TraceTransactionCallTracerAsync(txHash);

            Assert.Single(blockTraces);
            Assert.Equal("CALL", blockTraces[0].Result.Type);
            Assert.Equal(
                JsonConvert.SerializeObject(singleTrace),
                JsonConvert.SerializeObject(blockTraces[0].Result));
        }

        [Fact]
        public async Task TraceBlockByHash_MatchesByNumber_ForSameBlock()
        {
            var contract = await _fixture.DeployERC20Async(BigInteger.Parse("1000000000000000000000"));
            await _fixture.TransferERC20Async(contract, _fixture.RecipientAddress, 100);
            var blockNumber = await _fixture.Node.GetBlockNumberAsync();
            var blockHash = await _fixture.Node.GetBlockHashByNumberAsync(blockNumber);

            var byNumber = await _fixture.Node.TraceBlockByNumberAsync(blockNumber);
            var byHash = await _fixture.Node.TraceBlockByHashAsync(blockHash);

            Assert.Equal(
                JsonConvert.SerializeObject(byNumber),
                JsonConvert.SerializeObject(byHash));
        }

        [Fact]
        public async Task TraceBlockByNumber_AtHistoricalBlock_ReturnsTraces()
        {
            var contract = await _fixture.DeployERC20Async(BigInteger.Parse("1000000000000000000000"));
            var transfer = await _fixture.TransferERC20Async(contract, _fixture.RecipientAddress, 100);
            var historicalBlock = await _fixture.Node.GetBlockNumberAsync();

            await _fixture.TransferERC20Async(contract, _fixture.RecipientAddress, 100);
            await _fixture.TransferERC20Async(contract, _fixture.RecipientAddress, 100);

            var traces = await _fixture.Node.TraceBlockByNumberAsync(historicalBlock);

            Assert.Single(traces);
            Assert.Equal(transfer.TransactionHash.ToHex(true), traces[0].TxHash);
            Assert.NotEmpty(traces[0].Result.StructLogs);
        }

        [Fact]
        public async Task TraceBlockByNumber_EmptyBlock_ReturnsEmptyArray()
        {
            var traces = await _fixture.Node.TraceBlockByNumberAsync(0);
            Assert.Empty(traces);
        }

        [Fact]
        public async Task TraceBlockByNumber_UnknownBlock_Throws()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _fixture.Node.TraceBlockByNumberAsync(999_999_999));
        }

        [Fact]
        public async Task TraceBlockByHash_UnknownHash_Throws()
        {
            var unknown = "0x".PadRight(66, 'a').HexToByteArray();
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _fixture.Node.TraceBlockByHashAsync(unknown));
        }

        [Fact]
        public async Task TraceTransaction_FirstStructLogGas_ExcludesIntrinsic()
        {
            var contract = await _fixture.DeployERC20Async(BigInteger.Parse("1000000000000000000000"));
            var transfer = await _fixture.TransferERC20Async(contract, _fixture.RecipientAddress, 100);
            var trace = await _fixture.Node.TraceTransactionAsync(transfer.TransactionHash.ToHex(true));

            Assert.NotEmpty(trace.StructLogs);
            Assert.True(trace.StructLogs[0].Gas < 500_000,
                $"first struct-log gas {trace.StructLogs[0].Gas} should be below the 500000 gas limit (intrinsic deducted)");
        }

        [Fact]
        public async Task TraceBlock_SequentialState_SecondTxSeesFirstTxWrites()
        {
            var config = new DevChainConfig { ChainId = _fixture.ChainId, BlockGasLimit = 30_000_000, AutoMine = false };
            using var node = new DevChainNode(config);
            await node.StartAsync(new[] { _fixture.Address }, _fixture.InitialBalance);

            var signer = new LegacyTransactionSigner();
            var key = _fixture.PrivateKey.HexToByteArray();

            ISignedTransaction Sign(string to, byte[] data, BigInteger nonce, BigInteger gasLimit) =>
                TransactionFactory.CreateTransaction(signer.SignTransaction(
                    key, _fixture.ChainId, to, BigInteger.Zero, nonce, 1_000_000_000, gasLimit, data.ToHex()));

            var deployTx = TransactionFactory.CreateTransaction(signer.SignTransaction(
                key, _fixture.ChainId, "", BigInteger.Zero, 0, 1_000_000_000, 3_000_000,
                ERC20Contract.GetDeploymentBytecode().ToHex()));
            await node.SendTransactionAsync(deployTx);
            await node.MineBlockAsync();
            var contract = (await node.GetTransactionReceiptInfoAsync(deployTx.Hash)).ContractAddress;

            var mintData = new MintFunction { To = _fixture.Address, Amount = 150 }.GetCallData();
            await node.SendTransactionAsync(Sign(contract, mintData, 1, 500_000));
            await node.MineBlockAsync();

            var transferData = new TransferFunction { To = _fixture.RecipientAddress, Value = 100 }.GetCallData();
            var tx1 = Sign(contract, transferData, 2, 500_000);
            var tx2 = Sign(contract, transferData, 3, 500_000);
            await node.SendTransactionAsync(tx1);
            await node.SendTransactionAsync(tx2);
            await node.MineBlockAsync();

            var blockNumber = await node.GetBlockNumberAsync();
            var traces = await node.TraceBlockCallTracerByNumberAsync(blockNumber);

            Assert.Equal(2, traces.Count);
            Assert.Equal(tx1.Hash.ToHex(true), traces[0].TxHash);
            Assert.Equal(tx2.Hash.ToHex(true), traces[1].TxHash);
            Assert.Null(traces[0].Result.Error);
            Assert.Equal("execution reverted", traces[1].Result.Error);
        }

        [Fact]
        public async Task TraceTransaction_StructLog_OmitsNonGethShapeFields()
        {
            var contract = await _fixture.DeployERC20Async(BigInteger.Parse("1000000000000000000000"));
            var transfer = await _fixture.TransferERC20Async(contract, _fixture.RecipientAddress, 100);
            var trace = await _fixture.Node.TraceTransactionAsync(transfer.TransactionHash.ToHex(true));

            var json = System.Text.Json.JsonSerializer.Serialize(
                trace, typeof(OpcodeTraceResult), CoreChainJsonContext.Default);

            Assert.DoesNotContain("\"memSize\"", json);
            Assert.DoesNotContain("\"address\"", json);
            Assert.DoesNotContain("\"refund\"", json);
            Assert.DoesNotContain("\"error\":null", json);
            Assert.DoesNotContain("\"storage\":{}", json);
            Assert.Contains("\"gas\"", json);
            Assert.Contains("\"op\"", json);
            Assert.Contains("\"stack\"", json);
        }

        [Fact]
        public async Task TraceBlock_CallTracer_OmitsNullFields_AndLowercasesAddresses()
        {
            var contract = await _fixture.DeployERC20Async(BigInteger.Parse("1000000000000000000000"));
            await _fixture.TransferERC20Async(contract, _fixture.RecipientAddress, 100);
            var blockNumber = await _fixture.Node.GetBlockNumberAsync();
            var traces = await _fixture.Node.TraceBlockCallTracerByNumberAsync(blockNumber);

            var json = System.Text.Json.JsonSerializer.Serialize(
                traces, typeof(List<BlockResponseItemDto<CallTraceResult>>), CoreChainJsonContext.Default);

            Assert.DoesNotContain("\"calls\":null", json);
            Assert.DoesNotContain("\"error\":null", json);
            Assert.DoesNotContain("\"revertReason\":null", json);

            var root = traces[0].Result;
            Assert.Equal(root.From.ToLowerInvariant(), root.From);
            Assert.Equal(root.To.ToLowerInvariant(), root.To);
        }
    }
}
