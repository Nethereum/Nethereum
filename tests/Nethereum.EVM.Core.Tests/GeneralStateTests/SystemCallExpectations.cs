using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Witness;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.Core.Tests.GeneralStateTests
{
    public static class SystemCallExpectations
    {
        public const string WithdrawalRequests = "0x00000961ef480eb55e80d19ad83579a64c007002";
        public const string Consolidations = "0x0000bbddc7ce488642fb579f8b00f3a590007251";
        public const string BuilderDeposit = "0x0000bff46984e3725691fa540a8c7589300d8282";
        public const string BuilderExit = "0x000064d678505ad48f8ccb093bc65613800e8282";

        private static byte[] WriteSlotZero() => "600160005500".HexToByteArray();

        public static BlockWitnessData BlockAt(HardforkName fork)
        {
            return new BlockWitnessData
            {
                BlockNumber = 1,
                Timestamp = 1000,
                BaseFee = 7,
                BlockGasLimit = 30000000,
                ChainId = 1,
                Coinbase = "0x2adc25665018aa1fe0e6bc666dac8fc2697ff9ba",
                Difficulty = new byte[32],
                ParentHash = new byte[32],
                ExtraData = new byte[0],
                MixHash = new byte[32],
                Nonce = new byte[8],
                Features = new BlockFeatureConfig { Fork = fork },
                Transactions = new List<BlockWitnessTransaction>(),
                Accounts = new List<WitnessAccount>
                {
                    Predeploy(WithdrawalRequests),
                    Predeploy(Consolidations),
                    Predeploy(BuilderDeposit),
                    Predeploy(BuilderExit)
                }
            };
        }

        private static WitnessAccount Predeploy(string address) => Predeploy(address, WriteSlotZero());

        private static WitnessAccount Predeploy(string address, byte[] code) => new WitnessAccount
        {
            Address = address,
            Balance = EvmUInt256.Zero,
            Nonce = 0,
            Code = code,
            Storage = new List<WitnessStorageSlot>()
        };

        public static byte[] Reverts() => "60006000fd".HexToByteArray();

        public static byte[] RunsOutOfGas() => "600063007a120052".HexToByteArray();

        public static byte[] Throws() => "fe".HexToByteArray();

        public static byte[] WritesSlotZeroThenReverts() => "600160005560006000fd".HexToByteArray();

        public static BlockWitnessData BlockWhereTheRequestPredeployRuns(string contractAddress, byte[] code)
        {
            var block = BlockAt(HardforkName.Amsterdam);
            block.Accounts = new List<WitnessAccount>();
            foreach (var address in RequestPredeploys)
            {
                block.Accounts.Add(Predeploy(
                    address, address.IsTheSameAddress(contractAddress) ? code : WriteSlotZero()));
            }
            return block;
        }

        private static readonly string[] RequestPredeploys =
        {
            WithdrawalRequests, Consolidations, BuilderDeposit, BuilderExit
        };

        public static BlockWitnessData BlockWhereTheBeaconRootsContractRuns(byte[] code)
        {
            var block = BlockAt(HardforkName.Amsterdam);
            block.ParentBeaconBlockRoot = new byte[32];
            block.Accounts.Add(Predeploy(SystemCallContracts.BeaconRoots, code));
            return block;
        }

        public static BlockWitnessData BlockWhereTheHistoryStorageContractRuns(byte[] code)
        {
            var block = BlockAt(HardforkName.Amsterdam);
            block.Accounts.Add(Predeploy(SystemCallContracts.HistoryStorage, code));
            return block;
        }

        /// <summary>
        /// EIP-7002: "If the call to the contract fails or returns an error,
        /// the block MUST be invalidated."
        /// </summary>
        public static void AssertRefusedForSystemCallFailure(Exception thrown, string contractAddress)
        {
            var failure = Assert.IsType<SystemCallFailedException>(thrown);
            Assert.True(failure.TargetAddress.IsTheSameAddress(contractAddress),
                $"the block was refused for {failure.TargetAddress}, not for {contractAddress}");
        }

        public static void AssertAccepted(Exception thrown)
        {
            Assert.True(thrown == null, $"the block was refused with {thrown?.GetType().Name}: {thrown?.Message}");
        }

        public static void AssertSlotZeroIsUnwritten(BlockExecutionResult result, string contractAddress)
        {
            Assert.False(Wrote(result, contractAddress),
                $"the reverted write by {contractAddress} was persisted");
        }

        public static void AssertAmsterdamCallsAllFour(BlockExecutionResult result)
        {
            Assert.True(Wrote(result, WithdrawalRequests), "EIP-7002 withdrawal-request contract was not called");
            Assert.True(Wrote(result, Consolidations), "EIP-7251 consolidation contract was not called");
            Assert.True(Wrote(result, BuilderDeposit), "the builder deposit contract was not called");
            Assert.True(Wrote(result, BuilderExit), "the builder exit contract was not called");
        }

        public static void AssertPragueCallsOnlyTheTwoPragueContracts(BlockExecutionResult result)
        {
            Assert.True(Wrote(result, WithdrawalRequests), "EIP-7002 withdrawal-request contract was not called");
            Assert.True(Wrote(result, Consolidations), "EIP-7251 consolidation contract was not called");
            Assert.False(Wrote(result, BuilderDeposit), "the builder deposit contract ran at Prague, where it does not exist");
            Assert.False(Wrote(result, BuilderExit), "the builder exit contract ran at Prague, where it does not exist");
        }

        public static void AssertCancunCallsNone(BlockExecutionResult result)
        {
            Assert.False(Wrote(result, WithdrawalRequests), "EIP-7002 contract ran at Cancun, before it existed");
            Assert.False(Wrote(result, Consolidations), "EIP-7251 contract ran at Cancun, before it existed");
            Assert.False(Wrote(result, BuilderDeposit), "the builder deposit contract ran at Cancun");
            Assert.False(Wrote(result, BuilderExit), "the builder exit contract ran at Cancun");
        }

        public static void AssertRequestsIndexedAfterTransactions(BlockExecutionResult result, int transactionCount)
        {
            Assert.NotNull(result.BlockAccessList);

            var expectedIndex = (ulong)(transactionCount + 1);
            foreach (var address in new[] { WithdrawalRequests, Consolidations, BuilderDeposit, BuilderExit })
            {
                var entry = result.BlockAccessList.FirstOrDefault(
                    a => string.Equals(a.Address, address, StringComparison.OrdinalIgnoreCase));
                Assert.True(entry != null, $"{address} is absent from the block access list");

                var slot = Assert.Single(entry.StorageChanges);
                var change = Assert.Single(slot.Changes);
                Assert.Equal(expectedIndex, change.BlockAccessIndex);
            }
        }

        private static bool Wrote(BlockExecutionResult result, string address)
        {
            var account = result.StateReader.GetAccountState(address);
            return account?.Storage != null
                && account.Storage.TryGetValue(EvmUInt256.Zero, out var raw)
                && raw != null
                && !EvmUInt256.FromBigEndian(Pad(raw)).IsZero;
        }

        private static byte[] Pad(byte[] value)
        {
            if (value.Length == 32) return value;
            var padded = new byte[32];
            Array.Copy(value, 0, padded, 32 - value.Length, value.Length);
            return padded;
        }
    }
}
