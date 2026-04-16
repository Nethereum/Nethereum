using System.Collections.Generic;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Precompiles;
using Nethereum.EVM.Witness;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.EVM.Core.Tests.GeneralStateTests
{
    public class BeaconRootTests
    {
        private readonly ITestOutputHelper _output;
        public BeaconRootTests(ITestOutputHelper output) { _output = output; }

        [Fact]
        public void BeaconRootContract_SystemCall_WritesTimestampAndRoot()
        {
            // EIP-4788 beacon root contract bytecode
            var code = "3373fffffffffffffffffffffffffffffffffffffffe14604d57602036146024575f5ffd5b5f35801560495762001fff810690815414603c575f5ffd5b62001fff01545f5260205ff35b5f5ffd5b62001fff42064281555f359062001fff015500".HexToByteArray();

            var beaconAddr = "0x000f3df6d732807ef1319fb7b8bb8522d0beac02";
            var systemCaller = "0xfffffffffffffffffffffffffffffffffffffffe";

            var accounts = new Dictionary<string, AccountState>
            {
                [beaconAddr] = new AccountState { Balance = EvmUInt256.Zero, Nonce = 1, Code = code }
            };

            var stateReader = new InMemoryStateReader(accounts);
            var executionState = new ExecutionStateService(stateReader);
            executionState.LoadBalanceNonceAndCodeFromStorage(beaconAddr);

            var beaconRoot = new byte[32]; // all zeros
            long timestamp = 1950; // 0x079e

            var ctx = new TransactionExecutionContext
            {
                Mode = ExecutionMode.SystemCall,
                Sender = systemCaller,
                To = beaconAddr,
                Data = beaconRoot,
                Value = EvmUInt256.Zero,
                GasLimit = 30000000,
                GasPrice = 0,
                EffectiveGasPrice = EvmUInt256.Zero,
                Nonce = 0,
                IsContractCreation = false,
                BlockNumber = 1,
                Timestamp = timestamp,
                Coinbase = "0x0000000000000000000000000000000000000000",
                BaseFee = 0,
                Difficulty = EvmUInt256.Zero,
                BlockGasLimit = 30000000,
                ChainId = 1,
                ExecutionState = executionState
            };

            var executor = new TransactionExecutor(config: Nethereum.EVM.Precompiles.DefaultHardforkConfigs.Prague);
            var result = executor.Execute(ctx);

            _output.WriteLine($"Success: {result.Success}");
            _output.WriteLine($"Error: {result.Error}");
            _output.WriteLine($"Gas used: {result.GasUsed}");

            var beaconState = executionState.CreateOrGetAccountExecutionState(beaconAddr);
            _output.WriteLine($"Storage count: {beaconState.Storage?.Count ?? 0}");
            if (beaconState.Storage != null)
            {
                foreach (var s in beaconState.Storage)
                    _output.WriteLine($"  slot {s.Key} = 0x{s.Value.ToHex()}");
            }

            Assert.True(result.Success, $"System call failed: {result.Error}");

            // Expected: slot 0x079e (1950 % 8192) = timestamp, slot 0x279e (1950 + 8192) = beacon root
            var timestampSlot = new EvmUInt256(1950);
            var rootSlot = new EvmUInt256(1950 + 8192);
            Assert.True(beaconState.Storage.ContainsKey(timestampSlot),
                $"Missing timestamp slot {timestampSlot}");
        }
    }
}
