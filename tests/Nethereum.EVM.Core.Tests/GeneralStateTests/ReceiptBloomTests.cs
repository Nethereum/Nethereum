using System.Collections.Generic;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Core.Tests;
using Nethereum.EVM.Types;
using Nethereum.EVM.Witness;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.EVM.Core.Tests.GeneralStateTests
{
    public class ReceiptBloomTests
    {
        private readonly ITestOutputHelper _output;

        public ReceiptBloomTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void EvmLogConverter_ConvertsHexToBytes()
        {
            var evmLog = new EvmLog
            {
                Address = "0xcccccccccccccccccccccccccccccccccccccccc",
                Topics = new[] { "0x0000000000000000000000000000000000000000000000000000000000000001" },
                Data = "0xaabbccdd"
            };

            var modelLog = EvmLogConverter.ToModelLog(evmLog);

            Assert.Equal("0xcccccccccccccccccccccccccccccccccccccccc", modelLog.Address);
            Assert.Single(modelLog.Topics);
            Assert.Equal(32, modelLog.Topics[0].Length);
            Assert.Equal(1, modelLog.Topics[0][31]);
            Assert.Equal(4, modelLog.Data.Length);
            Assert.Equal(0xaa, modelLog.Data[0]);
        }

        [Fact]
        public void EvmLogConverter_HandlesEmptyData()
        {
            var evmLog = new EvmLog
            {
                Address = "0x1234567890123456789012345678901234567890",
                Topics = new string[0],
                Data = "0x"
            };

            var modelLog = EvmLogConverter.ToModelLog(evmLog);

            Assert.Empty(modelLog.Topics);
            Assert.Empty(modelLog.Data);
        }

        [Fact]
        public void LogBloomCalculator_ProducesNonZeroBloom()
        {
            var logs = new List<Log>
            {
                Log.Create(
                    new byte[] { 0xaa, 0xbb },
                    "0xcccccccccccccccccccccccccccccccccccccccc",
                    new byte[32]
                )
            };

            var bloom = LogBloomCalculator.CalculateBloom(logs);

            Assert.Equal(256, bloom.Length);
            // Bloom should have some bits set
            bool hasNonZero = false;
            for (int i = 0; i < bloom.Length; i++)
                if (bloom[i] != 0) { hasNonZero = true; break; }
            Assert.True(hasNonZero, "Bloom should have non-zero bits");
        }

        [Fact]
        public void LogBloomCalculator_EmptyLogsProducesZeroBloom()
        {
            var bloom = LogBloomCalculator.CalculateBloom(new List<Log>());
            Assert.Equal(256, bloom.Length);
            for (int i = 0; i < bloom.Length; i++)
                Assert.Equal(0, bloom[i]);
        }

        [Fact]
        public void LogBloomCalculator_CombineBloomOrsBits()
        {
            var a = new byte[256];
            a[0] = 0x0F;
            var b = new byte[256];
            b[0] = 0xF0;
            b[1] = 0x01;

            LogBloomCalculator.CombineBloom(a, b);

            Assert.Equal(0xFF, a[0]);
            Assert.Equal(0x01, a[1]);
        }

        [Fact]
        public void Receipt_CreateAndEncode_Roundtrip()
        {
            var logs = new List<Log>
            {
                Log.Create(
                    new byte[] { 0x01, 0x02, 0x03 },
                    "0xcccccccccccccccccccccccccccccccccccccccc",
                    new byte[32]
                )
            };

            var bloom = LogBloomCalculator.CalculateBloom(logs);
            var receipt = Receipt.CreateStatusReceipt(true, 21000, bloom, logs);

            var encoded = ReceiptEncoder.Current.Encode(receipt);
            var decoded = ReceiptEncoder.Current.Decode(encoded);

            Assert.True(decoded.HasSucceeded);
            Assert.Equal((EvmUInt256)21000, decoded.CumulativeGasUsed);
            Assert.Single(decoded.Logs);
            Assert.Equal(256, decoded.Bloom.Length);
        }

        [Fact]
        public void BlockSync_ContractEmitsEvent_ReceiptHasLogs()
        {
            // Contract: LOG0(mem[0:32])
            // PUSH1 0x42, PUSH1 0x00, MSTORE, PUSH1 0x20, PUSH1 0x00, LOG0, STOP
            var contractCode = new byte[] { 0x60, 0x42, 0x60, 0x00, 0x52, 0x60, 0x20, 0x60, 0x00, 0xA0, 0x00 };
            var sender = "0xa94f5374fce5edbc8e2a8697c15331677e6ebf0b";
            var contract = "0xcccccccccccccccccccccccccccccccccccccccc";

            var accounts = new Dictionary<string, AccountState>
            {
                [sender] = new AccountState { Balance = new EvmUInt256(1000000000), Nonce = 0 },
                [contract] = new AccountState { Code = contractCode }
            };

            var stateReader = new InMemoryStateReader(accounts);
            var executionState = new ExecutionStateService(stateReader);
            executionState.LoadBalanceNonceAndCodeFromStorage(sender);
            executionState.LoadBalanceNonceAndCodeFromStorage(contract);

            var ctx = new TransactionExecutionContext
            {
                Mode = ExecutionMode.Transaction,
                Sender = sender,
                To = contract,
                Data = new byte[0],
                Value = EvmUInt256.Zero,
                GasLimit = 100000,
                GasPrice = 10,
                EffectiveGasPrice = 10,
                Nonce = 0,
                BlockNumber = 1,
                Timestamp = 1000,
                Coinbase = "0x0000000000000000000000000000000000000000",
                BaseFee = 7,
                BlockGasLimit = 30000000,
                ChainId = 1,
                ExecutionState = executionState
            };

            var executor = new TransactionExecutor(config: Nethereum.EVM.Precompiles.DefaultHardforkConfigs.Prague);
            var result = executor.Execute(ctx);

            Assert.True(result.Success, $"Execution failed: {result.Error}");
            Assert.NotNull(result.Logs);
            Assert.True(result.Logs.Count > 0, "Should have emitted at least one log");

            // Convert EvmLog to Model.Log and compute bloom
            var modelLogs = EvmLogConverter.ToModelLogs(result.Logs);
            var bloom = LogBloomCalculator.CalculateBloom(modelLogs);

            // Create receipt
            var receipt = Receipt.CreateStatusReceipt(true, result.GasUsed, bloom, modelLogs);

            Assert.True(receipt.HasSucceeded.Value);
            Assert.True(receipt.CumulativeGasUsed > 0);
            Assert.Single(receipt.Logs);

            // Verify bloom has bits set
            bool hasBloomBits = false;
            for (int i = 0; i < bloom.Length; i++)
                if (bloom[i] != 0) { hasBloomBits = true; break; }
            Assert.True(hasBloomBits, "Bloom should have bits set from the log");

            // Encode/decode roundtrip
            var encoded = ReceiptEncoder.Current.Encode(receipt);
            var decoded = ReceiptEncoder.Current.Decode(encoded);
            Assert.True(decoded.HasSucceeded.Value);
            Assert.Single(decoded.Logs);

            _output.WriteLine($"Gas used: {result.GasUsed}");
            _output.WriteLine($"Log address: {modelLogs[0].Address}");
            _output.WriteLine($"Bloom non-zero bytes: {CountNonZero(bloom)}");
        }

        private static int CountNonZero(byte[] data)
        {
            int count = 0;
            foreach (var b in data)
                if (b != 0) count++;
            return count;
        }
    }
}
