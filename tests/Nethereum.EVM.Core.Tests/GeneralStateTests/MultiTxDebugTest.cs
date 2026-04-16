using System.Collections.Generic;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Precompiles;
using Nethereum.EVM.Witness;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.EVM.Core.Tests.GeneralStateTests
{
    public class MultiTxDebugTest
    {
        private readonly ITestOutputHelper _output;
        public MultiTxDebugTest(ITestOutputHelper output) { _output = output; }

        [Fact]
        public void FourTransfers_BalancesAccumulate()
        {
            var sender = TestTransactionHelper.GetDefaultSenderAddress();
            var senderKey = "0x45a915e4d060149eb4365960e6a7a45f334393093061116b197e3240065ff2d8";
            var receiver = "0x1000000000000000000000000000000000000001";
            var coinbase = "0x2adc25665018aa1fe0e6bc666dac8fc2697ff9ba";

            var block = new BlockWitnessData
            {
                BlockNumber = 1, Timestamp = 1000, BaseFee = 7,
                BlockGasLimit = 30000000, ChainId = 1, Coinbase = coinbase,
                Difficulty = new byte[32], ParentHash = new byte[32],
                ExtraData = new byte[0], MixHash = new byte[32], Nonce = new byte[8],
                ComputePostStateRoot = true,
                Features = BlockFeatureConfig.Cancun,
                Transactions = new List<BlockWitnessTransaction>
                {
                    TestTransactionHelper.CreateSignedTransfer(receiver, new EvmUInt256(100), 0, 10, 21000, senderKey),
                    TestTransactionHelper.CreateSignedTransfer(receiver, new EvmUInt256(100), 1, 10, 21000, senderKey),
                    TestTransactionHelper.CreateSignedTransfer(receiver, new EvmUInt256(100), 2, 10, 21000, senderKey),
                    TestTransactionHelper.CreateSignedTransfer(receiver, new EvmUInt256(100), 3, 10, 21000, senderKey)
                },
                Accounts = new List<WitnessAccount>
                {
                    new WitnessAccount { Address = sender, Balance = new EvmUInt256(10000000), Nonce = 0, Code = new byte[0], Storage = new List<WitnessStorageSlot>() },
                    new WitnessAccount { Address = receiver, Balance = EvmUInt256.Zero, Nonce = 0, Code = new byte[0], Storage = new List<WitnessStorageSlot>() }
                }
            };

            var encoding = RlpBlockEncodingProvider.Instance;
            var registry = Nethereum.EVM.Precompiles.DefaultMainnetHardforkRegistry.Instance;
            var result = Nethereum.EVM.Execution.BlockExecutor.Execute(
                block,
                encoding,
                registry,
                new CoreChain.PatriciaStateRootCalculator(encoding),
                null);

            for (int i = 0; i < result.TxResults.Count; i++)
                _output.WriteLine($"tx[{i}]: success={result.TxResults[i].Success} gas={result.TxResults[i].GasUsed} error={result.TxResults[i].Error}");

            // Check receiver balance — should be 4 × 100 = 400
            var receiverState = result.FinalExecutionState?.CreateOrGetAccountExecutionState(receiver);
            _output.WriteLine($"Receiver balance: {receiverState?.Balance.GetTotalBalance()}");
            Assert.Equal(new EvmUInt256(400), receiverState?.Balance.GetTotalBalance());
        }
    }
}
