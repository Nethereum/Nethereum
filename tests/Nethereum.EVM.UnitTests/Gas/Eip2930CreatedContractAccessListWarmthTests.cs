using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Gas;
using Nethereum.EVM.Precompiles;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.UnitTests.Gas
{
    /// <summary>
    /// AMS-GST-01. EIP-2929: "When a transaction execution begins,
    /// <c>accessed_storage_keys</c> is initialized to empty, and
    /// <c>accessed_addresses</c> is initialized to include the <c>tx.sender</c>,
    /// <c>tx.to</c> (or the address being created if it is a contract creation
    /// transaction) and the set of all precompiles." EIP-2930: "The address and
    /// storage keys would be immediately loaded into the <c>accessed_addresses</c>
    /// and <c>accessed_storage_keys</c> global sets".
    ///
    /// <para>Neither EIP gives a contract creation any power to take a slot back out
    /// of that set, so a creation transaction whose own access list pre-declares a
    /// slot of the address it creates must find that slot WARM when the init code
    /// reaches it. Gas is the only thing consensus can see, so every case here is a
    /// differential between two runs that differ ONLY in the access list.</para>
    /// </summary>
    public class Eip2930CreatedContractAccessListWarmthTests
    {
        private const string SenderAddress = "0x1111111111111111111111111111111111111111";
        private const string UnrelatedAddress = "0x2222222222222222222222222222222222222222";
        private const string Slot0 = "0x0000000000000000000000000000000000000000000000000000000000000000";
        private const string Slot1 = "0x0000000000000000000000000000000000000000000000000000000000000001";
        private const long GasLimit = 1_000_000;

        private static readonly byte[] SstoreSlotZeroInitCode = "0x60ff60005500".HexToByteArray();

        private static readonly byte[] NestedCreateInitCode = "0x6560ff600055006000526006601a6000f000".HexToByteArray();

        private static string CreatedAddress => ContractUtils.CalculateContractAddress(SenderAddress, 0);

        private static string NestedAddress => ContractUtils.CalculateContractAddress(CreatedAddress, 1);

        public static IEnumerable<object[]> LiveForks() => new[]
        {
            new object[] { "Cancun" },
            new object[] { "Prague" }
        };

        private static HardforkConfig ConfigFor(string fork) =>
            fork == "Cancun" ? DefaultHardforkConfigs.Cancun : DefaultHardforkConfigs.Prague;

        private static List<AccessListEntry> PreDeclaring(string address, string slot) =>
            new List<AccessListEntry>
            {
                new AccessListEntry { Address = address, StorageKeys = new List<string> { slot } }
            };

        private static async Task<TransactionExecutionResult> RunCreationAsync(
            HardforkConfig config,
            byte[] initCode,
            List<AccessListEntry> accessList,
            byte[] preExistingTargetCode = null)
        {
            var node = new EIP7702TestNodeDataService();
            await node.SetBalanceAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            if (preExistingTargetCode != null)
                await node.SetCodeAsync(CreatedAddress, preExistingTargetCode);

            var ctx = new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = "",
                Data = initCode,
                IsContractCreation = true,
                AccessList = accessList,
                GasLimit = GasLimit,
                Value = 0,
                GasPrice = 1,
                Nonce = 0,
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 0,
                ChainId = 1,
                Coinbase = SenderAddress,
                ExecutionState = new ExecutionStateService(node)
            };

            return await new TransactionExecutor(config).ExecuteAsync(ctx);
        }

        [Theory]
        [MemberData(nameof(LiveForks))]
        public async Task Given_ACreationTransaction_When_ItsAccessListPreDeclaresASlotOfTheAddressBeingCreated_Then_TheInitCodeFindsThatSlotWarm(string fork)
        {
            var config = ConfigFor(fork);

            var warm = await RunCreationAsync(config, SstoreSlotZeroInitCode, PreDeclaring(CreatedAddress, Slot0));
            var cold = await RunCreationAsync(config, SstoreSlotZeroInitCode, PreDeclaring(UnrelatedAddress, Slot0));

            Assert.True(warm.Success, warm.Error);
            Assert.True(cold.Success, cold.Error);
            Assert.Equal(GasConstants.COLD_SLOAD_COST, cold.GasUsed - warm.GasUsed);
        }

        [Theory]
        [MemberData(nameof(LiveForks))]
        public async Task Given_ACreationTransaction_When_ItsAccessListNamesAnUnrelatedAddress_Then_TheCreatedAddressSlotStaysCold(string fork)
        {
            var config = ConfigFor(fork);

            var unrelated = await RunCreationAsync(config, SstoreSlotZeroInitCode, PreDeclaring(UnrelatedAddress, Slot0));
            var noAccessList = await RunCreationAsync(config, SstoreSlotZeroInitCode, null);

            Assert.True(unrelated.Success, unrelated.Error);
            Assert.True(noAccessList.Success, noAccessList.Error);

            var accessListIntrinsicCost =
                GasConstants.TX_ACCESS_LIST_ADDRESS_GAS + GasConstants.TX_ACCESS_LIST_STORAGE_KEY_GAS;
            Assert.Equal(accessListIntrinsicCost, unrelated.GasUsed - noAccessList.GasUsed);
        }

        [Theory]
        [MemberData(nameof(LiveForks))]
        public async Task Given_ACreationTransaction_When_ItsAccessListPreDeclaresADifferentSlotOfTheSameAddress_Then_SlotZeroStaysCold(string fork)
        {
            var config = ConfigFor(fork);

            var otherSlot = await RunCreationAsync(config, SstoreSlotZeroInitCode, PreDeclaring(CreatedAddress, Slot1));
            var unrelated = await RunCreationAsync(config, SstoreSlotZeroInitCode, PreDeclaring(UnrelatedAddress, Slot0));

            Assert.True(otherSlot.Success, otherSlot.Error);
            Assert.True(unrelated.Success, unrelated.Error);
            Assert.Equal(unrelated.GasUsed, otherSlot.GasUsed);
        }

        [Theory]
        [MemberData(nameof(LiveForks))]
        public async Task Given_ACreationTransaction_When_ItsAccessListPreDeclaresASlotOfANestedCreatedContract_Then_TheNestedInitCodeFindsThatSlotWarm(string fork)
        {
            var config = ConfigFor(fork);

            var warm = await RunCreationAsync(config, NestedCreateInitCode, PreDeclaring(NestedAddress, Slot0));
            var cold = await RunCreationAsync(config, NestedCreateInitCode, PreDeclaring(UnrelatedAddress, Slot0));

            Assert.True(warm.Success, warm.Error);
            Assert.True(cold.Success, cold.Error);
            Assert.Equal(GasConstants.COLD_SLOAD_COST, cold.GasUsed - warm.GasUsed);
        }

        [Theory]
        [MemberData(nameof(LiveForks))]
        public async Task Given_ACreationTransactionOntoACollidingAddress_When_ItsAccessListPreDeclaresThatAddressSlot_Then_TheWholeGasLimitIsStillForfeited(string fork)
        {
            var config = ConfigFor(fork);

            var colliding = await RunCreationAsync(
                config, SstoreSlotZeroInitCode, PreDeclaring(CreatedAddress, Slot0),
                preExistingTargetCode: "0x00".HexToByteArray());
            var virgin = await RunCreationAsync(config, SstoreSlotZeroInitCode, PreDeclaring(CreatedAddress, Slot0));

            Assert.False(colliding.Success);
            Assert.Equal(GasLimit, colliding.GasUsed);

            Assert.True(virgin.Success, virgin.Error);
            Assert.NotEqual(colliding.GasUsed, virgin.GasUsed);
        }

        [Fact]
        public async Task Given_APreBerlinFork_When_ACreationTransactionCarriesAnAccessList_Then_ItIsRejectedBeforeAnySlotCanBeWarmed()
        {
            var istanbul = await RunCreationAsync(DefaultHardforkConfigs.Istanbul, SstoreSlotZeroInitCode, PreDeclaring(CreatedAddress, Slot0));

            Assert.False(istanbul.Success);
            Assert.Equal(TransactionError.TransactionTypeNotSupported, istanbul.ErrorCode);

            var berlinWarm = await RunCreationAsync(DefaultHardforkConfigs.Berlin, SstoreSlotZeroInitCode, PreDeclaring(CreatedAddress, Slot0));
            var berlinCold = await RunCreationAsync(DefaultHardforkConfigs.Berlin, SstoreSlotZeroInitCode, PreDeclaring(UnrelatedAddress, Slot0));

            Assert.True(berlinWarm.Success, berlinWarm.Error);
            Assert.Equal(GasConstants.COLD_SLOAD_COST, berlinCold.GasUsed - berlinWarm.GasUsed);
        }

        [Fact]
        public async Task Given_APreBerlinFork_When_ACreationTransactionSstoresASlot_Then_NothingChargesAColdSurcharge()
        {
            var istanbul = await RunCreationAsync(DefaultHardforkConfigs.Istanbul, SstoreSlotZeroInitCode, null);
            var berlin = await RunCreationAsync(DefaultHardforkConfigs.Berlin, SstoreSlotZeroInitCode, null);

            Assert.True(istanbul.Success, istanbul.Error);
            Assert.True(berlin.Success, berlin.Error);
            Assert.Equal(GasConstants.COLD_SLOAD_COST, berlin.GasUsed - istanbul.GasUsed);
        }

        [Fact]
        public void Given_AnAccountCarryingWarmSlotsAndStoredValues_When_ItIsPreparedAsANewContract_Then_OnlyTheStorageIsCleared()
        {
            var slot = EvmUInt256.FromBigEndian(Slot0.HexToByteArray());
            var account = new AccountExecutionState { Address = CreatedAddress };
            account.SetPreStateStorage(slot, new byte[] { 0x07 });
            account.MarkStorageKeyAsWarm(slot);

            account.ClearStorageForNewContract();

            Assert.Empty(account.Storage);
            Assert.Empty(account.OriginalStorageValues);
            Assert.True(account.IsStorageKeyWarm(slot));
        }
    }
}
