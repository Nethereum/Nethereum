using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.EVM;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Precompiles;
using Nethereum.EVM.Witness;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.Core.Tests.GeneralStateTests
{
    public class BalanceRoundTripAccessListTests
    {
        private const string SpenderAddress = "0x00000000000000000000000000000000000000d1";
        private const string CalleeAddress = "0x00000000000000000000000000000000000000d2";

        private static byte[] Concat(params byte[][] parts)
        {
            var bytes = new List<byte>();
            foreach (var part in parts) bytes.AddRange(part);
            return bytes.ToArray();
        }

        private static byte[] Push20(string address)
        {
            var code = new List<byte> { 0x73 };
            code.AddRange(address.HexToByteArray());
            return code.ToArray();
        }

        private static byte[] SpenderCode()
        {
            var outbound = Concat(
                new byte[] { 0x60, 0x00, 0x60, 0x00, 0x60, 0x00, 0x60, 0x00, 0x60, 0x01 },
                Push20(CalleeAddress),
                new byte[] { 0x5A, 0xF1, 0x50, 0x00 });
            var reentryTarget = (byte)(4 + outbound.Length);
            return Concat(
                new byte[] { 0x34, 0x60, reentryTarget, 0x57 },
                outbound,
                new byte[] { 0x5B, 0x00 });
        }

        private static readonly byte[] CalleeReturningTheValue =
        {
            0x60, 0x00, 0x60, 0x00, 0x60, 0x00, 0x60, 0x00,
            0x34,
            0x33,
            0x5A,
            0xF1,
            0x50,
            0x00
        };

        private static readonly byte[] CalleeKeepingTheValue = { 0x00 };

        private static BlockExecutionResult ExecuteRoundTripBlock(byte[] calleeCode)
        {
            var sender = TestTransactionHelper.GetDefaultSenderAddress();

            var tx = TestTransactionHelper.CreateSignedContractCall(
                SpenderAddress,
                data: new byte[0],
                value: EvmUInt256.Zero,
                nonce: EvmUInt256.Zero,
                gasPrice: new EvmUInt256(1UL),
                gasLimit: new EvmUInt256(1_000_000UL));

            var block = new BlockWitnessData
            {
                BlockNumber = 1,
                Timestamp = 1000,
                BaseFee = 1,
                BlockGasLimit = 30000000,
                ChainId = 1,
                Coinbase = "0x2adc25665018aa1fe0e6bc666dac8fc2697ff9ba",
                Difficulty = new byte[32],
                ParentHash = new byte[32],
                ExtraData = new byte[0],
                MixHash = new byte[32],
                Nonce = new byte[8],
                Features = new BlockFeatureConfig { Fork = HardforkName.Amsterdam },
                Transactions = new List<BlockWitnessTransaction> { tx },
                Accounts = new List<WitnessAccount>
                {
                    new WitnessAccount
                    {
                        Address = sender,
                        Balance = new EvmUInt256(1_000_000_000_000_000_000UL),
                        Nonce = 0,
                        Code = new byte[0],
                        Storage = new List<WitnessStorageSlot>()
                    },
                    new WitnessAccount
                    {
                        Address = SpenderAddress,
                        Balance = new EvmUInt256(1UL),
                        Nonce = 1,
                        Code = SpenderCode(),
                        Storage = new List<WitnessStorageSlot>()
                    },
                    new WitnessAccount
                    {
                        Address = CalleeAddress,
                        Balance = EvmUInt256.Zero,
                        Nonce = 1,
                        Code = calleeCode,
                        Storage = new List<WitnessStorageSlot>()
                    }
                }
            };

            return BlockExecutor.Execute(
                block.AddRequestPredeploys(),
                RlpBlockEncodingProvider.Instance,
                DefaultMainnetHardforkRegistry.Instance);
        }

        private static AccountChanges Entry(BlockExecutionResult result, string address) =>
            result.BlockAccessList.SingleOrDefault(
                a => string.Equals(a.Address, address, StringComparison.OrdinalIgnoreCase));

        [Fact]
        [Trait("Category", "EIP7928")]
        public void Given_ABalanceThatLeavesAndReturnsWithinOneTransaction_When_TheBlockAccessListIsBuilt_Then_BothAccountsArePresentWithNoBalanceChange()
        {
            var result = ExecuteRoundTripBlock(CalleeReturningTheValue);

            Assert.True(result.TxResults.Single().Success, result.TxResults.Single().Error);

            var spender = Entry(result, SpenderAddress);
            Assert.True(spender != null, "the spender was read and must be listed");
            Assert.Empty(spender.BalanceChanges);

            var callee = Entry(result, CalleeAddress);
            Assert.True(callee != null, "the callee was read and must be listed");
            Assert.Empty(callee.BalanceChanges);
        }

        [Fact]
        [Trait("Category", "EIP7928")]
        public void Given_ABalanceThatLeavesAndStaysAway_When_TheBlockAccessListIsBuilt_Then_BothAccountsCarryTheirBalanceChange()
        {
            var result = ExecuteRoundTripBlock(CalleeKeepingTheValue);

            Assert.True(result.TxResults.Single().Success, result.TxResults.Single().Error);

            var spender = Entry(result, SpenderAddress);
            Assert.Equal(EvmUInt256.Zero, Assert.Single(spender.BalanceChanges).PostBalance);

            var callee = Entry(result, CalleeAddress);
            Assert.Equal(new EvmUInt256(1UL), Assert.Single(callee.BalanceChanges).PostBalance);
        }
    }
}
