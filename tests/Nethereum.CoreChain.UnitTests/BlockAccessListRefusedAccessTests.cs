using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.EVM;
using Nethereum.EVM.Gas;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class BlockAccessListRefusedAccessTests
    {
        private const string SenderPrivateKey = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private const string SenderAddress = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        private const string FeeRecipientAddress = "0x90F79bf6EB2c4f870365E785982E1f101E93b906";

        private const string CallerAddress = "0x0000000000000000000000000000000000000a01";
        private const string InnerCallerAddress = "0x0000000000000000000000000000000000000a02";
        private const string StorageContractAddress = "0x0000000000000000000000000000000000000a03";
        private const string SelfDestructContractAddress = "0x0000000000000000000000000000000000000a04";
        private const string BeneficiaryAddress = "0x0000000000000000000000000000000000000a05";

        private const string UnfundedTargetAddress = "0x0000000000000000000000000000000000000a06";

        private const string WarmedButUntouchedAddress = "0x0000000000000000000000000000000000000a07";

        private const string WarmedAndCalledAddress = "0x0000000000000000000000000000000000000a08";

        private const string FreshBeneficiaryAddress = "0x0000000000000000000000000000000000000a09";

        private const long BaseFee = 7;
        private static readonly BigInteger ChainId = 1337;
        private static readonly EvmUInt256 StorageSlot = new EvmUInt256(1);

        private const long ValueCallPushGas = 7 * GasConstants.G_VERYLOW;
        private const long ValueCallStateIndependentGas =
            GasConstants.EIP8038_COLD_ACCOUNT_ACCESS + GasConstants.EIP8038_CALL_VALUE_TRANSFER;
        private const long GasReachingTheTargetAccess = ValueCallPushGas + ValueCallStateIndependentGas;

        private const long SweepPushGas = GasConstants.G_VERYLOW;
        private const long SweepStateIndependentGas =
            GasConstants.SELFDESTRUCT_COST + GasConstants.EIP8038_COLD_ACCOUNT_ACCESS;
        private const long GasReachingTheBeneficiaryRead = SweepPushGas + SweepStateIndependentGas;

        private const long SstorePushGas = 2 * GasConstants.G_VERYLOW;
        private const long GasStoppingAtTheSentry = SstorePushGas + GasConstants.SSTORE_GAS_STIPEND;
        private const long GasClearingTheSentry = GasStoppingAtTheSentry + 1;
        private const long StorageWriteGas = GasConstants.COLD_SLOAD_COST + GasConstants.EIP8038_STORAGE_WRITE;

        private static string Push1(long value) => "60" + value.ToString("x2");
        private static string Push2(long value) => "61" + value.ToString("x4");
        private static string Push20(string address) => "73" + address.Substring(2).ToLowerInvariant();

        private static byte[] StaticCallTo(string target) =>
            (Push1(0) + Push1(0) + Push1(0) + Push1(0) + Push20(target) + "620f4240" + "fa" + "00").HexToByteArray();

        private static byte[] CallForwarding(string target, long gasToForward) =>
            (Push1(0) + Push1(0) + Push1(0) + Push1(0) + Push1(0) + Push20(target) + Push2(gasToForward) + "f1" + "00")
                .HexToByteArray();

        private static byte[] CallTransferringOneWei(string target) =>
            (Push1(0) + Push1(0) + Push1(0) + Push1(0) + Push1(1) + Push20(target) + Push1(0) + "f1" + "00")
                .HexToByteArray();

        private static readonly byte[] StoreToSlotOne = (Push1(0x42) + Push1(1) + "55").HexToByteArray();

        private static readonly byte[] DoNothing = "00".HexToByteArray();

        private static byte[] SweepTo(string beneficiary) => (Push20(beneficiary) + "ff").HexToByteArray();

        private static readonly byte[] SweepToBeneficiary = SweepTo(BeneficiaryAddress);

        private sealed class Contract
        {
            public string Address;
            public byte[] Code;
            public long Balance;
        }

        private static Contract Deployed(string address, byte[] code, long balance = 0) =>
            new Contract { Address = address, Code = code, Balance = balance };

        private static ISignedTransaction CallTo(string to, long gasLimit, List<AccessListItem> accessList = null) =>
            TransactionFactory.CreateTransaction(new Transaction1559Signer().SignTransaction(
                SenderPrivateKey,
                new Transaction1559(ChainId, 0, BaseFee, BaseFee, gasLimit, to, 0, "", accessList)));

        private static List<AccessListItem> AccessListNaming(params string[] addresses) =>
            addresses.Select(a => new AccessListItem(a, new List<byte[]>())).ToList();

        private static async Task<BlockExecutionResult> ExecuteAmsterdamBlockAsync(
            ISignedTransaction transaction, params Contract[] contracts)
        {
            var keccak = new Sha3Keccack();
            var stateStore = new InMemoryStateStore();
            await Nethereum.CoreChain.Forks.SystemContractPredeploys
                .ApplyGenesisAllocationAsync(stateStore, Nethereum.EVM.HardforkName.Amsterdam);
            await stateStore.SaveAccountAsync(SenderAddress, new Account { Balance = 1_000_000_000_000_000, Nonce = 0 });
            await stateStore.SaveAccountAsync(FeeRecipientAddress, new Account { Balance = 1_000, Nonce = 0 });

            foreach (var contract in contracts)
            {
                var account = new Account { Balance = contract.Balance, Nonce = 0 };
                if (contract.Code != null)
                {
                    var codeHash = keccak.CalculateHash(contract.Code);
                    await stateStore.SaveCodeAsync(codeHash, contract.Code);
                    account.CodeHash = codeHash;
                }
                await stateStore.SaveAccountAsync(contract.Address, account);
            }

            var config = new ChainConfig
            {
                ChainId = ChainId,
                BlockGasLimit = 30_000_000,
                BaseFee = BaseFee,
                Hardfork = nameof(HardforkName.Amsterdam)
            };
            var trieNodeStore = new InMemoryContentNodeStore();
            var engine = new BlockExecutor(
                stateStore,
                new InMemoryBlockStore(),
                new FixedChainActivations(HardforkName.Amsterdam),
                chainConfigFactory: _ => config,
                hardforkConfigFactory: _ => config.GetHardforkConfig(),
                stateRootCalculator: new IncrementalStateRootCalculator(stateStore, trieNodeStore),
                rewardPolicy: NoRewardPolicy.Instance,
                trieNodeStore: trieNodeStore);

            var header = new BlockHeader
            {
                BlockNumber = 1,
                Timestamp = 1_700_000_000,
                GasLimit = 30_000_000,
                BaseFee = BaseFee,
                Coinbase = FeeRecipientAddress,
                ParentHash = new byte[32]
            };

            var result = await engine.ExecuteAsync(
                header, new[] { new TxEntry(transaction) }, uncles: null, withdrawals: null,
                options: new BlockExecutionOptions());

            Assert.Null(result.Exception);
            return result;
        }

        private static AccountChanges Entry(BlockExecutionResult result, string address) =>
            result.BlockAccessList?.FirstOrDefault(
                a => string.Equals(a.Address, address, StringComparison.OrdinalIgnoreCase));

        private static AccountChanges RequiredEntry(BlockExecutionResult result, string address)
        {
            var entry = Entry(result, address);
            Assert.NotNull(entry);
            return entry;
        }

        [Fact]
        public async Task Given_SstoreRefusedInAStaticContext_When_TheBlockAccessListIsBuilt_Then_TheSlotIsNotRecordedAsRead()
        {
            var result = await ExecuteAmsterdamBlockAsync(
                CallTo(CallerAddress, gasLimit: 200_000),
                Deployed(CallerAddress, StaticCallTo(StorageContractAddress)),
                Deployed(StorageContractAddress, StoreToSlotOne));

            var entry = RequiredEntry(result, StorageContractAddress);
            Assert.Empty(entry.StorageReads);
            Assert.Empty(entry.StorageChanges);
        }

        [Fact]
        public async Task Given_SstoreCompletes_When_TheBlockAccessListIsBuilt_Then_TheSlotIsRecordedAsWritten()
        {
            var result = await ExecuteAmsterdamBlockAsync(
                CallTo(StorageContractAddress, gasLimit: 200_000),
                Deployed(StorageContractAddress, StoreToSlotOne));

            var entry = RequiredEntry(result, StorageContractAddress);
            Assert.Empty(entry.StorageReads);
            var slot = Assert.Single(entry.StorageChanges);
            Assert.Equal(StorageSlot, slot.Slot);
            Assert.Equal(new EvmUInt256(0x42), Assert.Single(slot.Changes).PostValue);
        }

        [Fact]
        public async Task Given_SstoreRunsOutOfGasAtTheSentry_When_TheBlockAccessListIsBuilt_Then_TheSlotIsNotRecordedAsRead()
        {
            var result = await ExecuteAmsterdamBlockAsync(
                CallTo(CallerAddress, gasLimit: 200_000),
                Deployed(CallerAddress, CallForwarding(StorageContractAddress, GasStoppingAtTheSentry)),
                Deployed(StorageContractAddress, StoreToSlotOne));

            var entry = RequiredEntry(result, StorageContractAddress);
            Assert.Empty(entry.StorageReads);
            Assert.Empty(entry.StorageChanges);
        }

        [Fact]
        public async Task Given_SstoreClearsTheSentryThenRunsOutOfGasOnTheWrite_When_TheBlockAccessListIsBuilt_Then_TheSlotIsRecordedAsRead()
        {
            var result = await ExecuteAmsterdamBlockAsync(
                CallTo(CallerAddress, gasLimit: 200_000),
                Deployed(CallerAddress, CallForwarding(StorageContractAddress, GasClearingTheSentry)),
                Deployed(StorageContractAddress, StoreToSlotOne));

            var entry = RequiredEntry(result, StorageContractAddress);
            Assert.Equal(StorageSlot, Assert.Single(entry.StorageReads));
            Assert.Empty(entry.StorageChanges);
        }

        [Fact]
        public void Given_TheSstoreSentryBoundary_Then_TheTwoGasBudgetsDifferByOneUnit()
        {
            Assert.Equal(GasStoppingAtTheSentry + 1, GasClearingTheSentry);
            Assert.True(StorageWriteGas > GasClearingTheSentry - SstorePushGas);
        }

        [Fact]
        public async Task Given_SelfdestructRefusedInAStaticContext_When_TheBlockAccessListIsBuilt_Then_TheBeneficiaryIsAbsent()
        {
            var result = await ExecuteAmsterdamBlockAsync(
                CallTo(CallerAddress, gasLimit: 200_000),
                Deployed(CallerAddress, StaticCallTo(SelfDestructContractAddress)),
                Deployed(SelfDestructContractAddress, SweepToBeneficiary, balance: 100),
                Deployed(BeneficiaryAddress, code: null, balance: 1));

            Assert.Null(Entry(result, BeneficiaryAddress));
        }

        [Fact]
        public async Task Given_SelfdestructCompletes_When_TheBlockAccessListIsBuilt_Then_TheBeneficiaryIsRecorded()
        {
            var result = await ExecuteAmsterdamBlockAsync(
                CallTo(SelfDestructContractAddress, gasLimit: 200_000),
                Deployed(SelfDestructContractAddress, SweepToBeneficiary, balance: 100),
                Deployed(BeneficiaryAddress, code: null, balance: 1));

            var entry = RequiredEntry(result, BeneficiaryAddress);
            Assert.Equal(new EvmUInt256(101), Assert.Single(entry.BalanceChanges).PostBalance);
        }

        [Fact]
        public async Task Given_ASelfDestructThatCannotAffordTheColdAccountCharge_When_TheBlockAccessListIsBuilt_Then_TheBeneficiaryIsNotRecorded()
        {
            var result = await ExecuteAmsterdamBlockAsync(
                CallTo(CallerAddress, gasLimit: 200_000),
                Deployed(CallerAddress, CallForwarding(SelfDestructContractAddress, GasReachingTheBeneficiaryRead - 1)),
                Deployed(SelfDestructContractAddress, SweepToBeneficiary, balance: 100),
                Deployed(BeneficiaryAddress, code: null, balance: 1));

            Assert.Null(Entry(result, BeneficiaryAddress));
        }

        [Fact]
        public async Task Given_ASelfDestructThatCanAffordIt_When_Built_Then_TheBeneficiaryIsRecorded()
        {
            var result = await ExecuteAmsterdamBlockAsync(
                CallTo(CallerAddress, gasLimit: 200_000),
                Deployed(CallerAddress, CallForwarding(SelfDestructContractAddress, GasReachingTheBeneficiaryRead)),
                Deployed(SelfDestructContractAddress, SweepToBeneficiary, balance: 100),
                Deployed(BeneficiaryAddress, code: null, balance: 1));

            var entry = RequiredEntry(result, BeneficiaryAddress);
            Assert.Equal(new EvmUInt256(101), Assert.Single(entry.BalanceChanges).PostBalance);
        }

        [Fact]
        public async Task Given_ASelfDestructWhoseBeneficiaryIsAlreadyWarm_When_TheBlockAccessListIsBuilt_Then_TheBeneficiaryIsRecorded()
        {
            var result = await ExecuteAmsterdamBlockAsync(
                CallTo(CallerAddress, gasLimit: 200_000, AccessListNaming(BeneficiaryAddress)),
                Deployed(CallerAddress, CallForwarding(SelfDestructContractAddress, GasReachingTheBeneficiaryRead - 1)),
                Deployed(SelfDestructContractAddress, SweepToBeneficiary, balance: 100),
                Deployed(BeneficiaryAddress, code: null, balance: 1));

            var entry = RequiredEntry(result, BeneficiaryAddress);
            Assert.Equal(new EvmUInt256(101), Assert.Single(entry.BalanceChanges).PostBalance);
        }

        [Fact]
        public async Task Given_ASelfDestructRunsOutOfGasAfterTheBeneficiaryRead_When_TheBlockAccessListIsBuilt_Then_TheBeneficiaryIsListed()
        {
            var result = await ExecuteAmsterdamBlockAsync(
                CallTo(CallerAddress, gasLimit: 200_000),
                Deployed(CallerAddress, CallForwarding(SelfDestructContractAddress, GasReachingTheBeneficiaryRead)),
                Deployed(SelfDestructContractAddress, SweepTo(FreshBeneficiaryAddress), balance: 100));

            var entry = RequiredEntry(result, FreshBeneficiaryAddress);
            Assert.Empty(entry.BalanceChanges);
        }

        [Fact]
        public async Task Given_ACallRunsOutOfGasAfterTheTargetAccess_When_TheBlockAccessListIsBuilt_Then_TheTargetIsListed()
        {
            var result = await ExecuteAmsterdamBlockAsync(
                CallTo(CallerAddress, gasLimit: 400_000),
                Deployed(CallerAddress, CallForwarding(InnerCallerAddress, GasReachingTheTargetAccess)),
                Deployed(InnerCallerAddress, CallTransferringOneWei(UnfundedTargetAddress), balance: 1));

            var entry = RequiredEntry(result, UnfundedTargetAddress);
            Assert.Empty(entry.BalanceChanges);
            Assert.Empty(entry.StorageChanges);
        }

        [Fact]
        public async Task Given_ACallRunsOutOfGasBeforeTheTargetAccess_When_TheBlockAccessListIsBuilt_Then_TheTargetIsAbsent()
        {
            var result = await ExecuteAmsterdamBlockAsync(
                CallTo(CallerAddress, gasLimit: 400_000),
                Deployed(CallerAddress, CallForwarding(InnerCallerAddress, GasReachingTheTargetAccess - 1)),
                Deployed(InnerCallerAddress, CallTransferringOneWei(UnfundedTargetAddress), balance: 1));

            Assert.Null(Entry(result, UnfundedTargetAddress));
        }

        [Fact]
        public async Task Given_AnAccessListNamesAnAddressExecutionNeverTouches_When_TheBlockAccessListIsBuilt_Then_ItIsAbsent()
        {
            var result = await ExecuteAmsterdamBlockAsync(
                CallTo(StorageContractAddress, gasLimit: 200_000, AccessListNaming(WarmedButUntouchedAddress)),
                Deployed(StorageContractAddress, StoreToSlotOne),
                Deployed(WarmedButUntouchedAddress, code: null, balance: 1));

            Assert.Null(Entry(result, WarmedButUntouchedAddress));
        }

        [Fact]
        public async Task Given_AnAccessListNamesACallTargetExecutionDoesTouch_When_TheBlockAccessListIsBuilt_Then_ItIsListed()
        {
            var result = await ExecuteAmsterdamBlockAsync(
                CallTo(CallerAddress, gasLimit: 200_000,
                    AccessListNaming(WarmedButUntouchedAddress, WarmedAndCalledAddress)),
                Deployed(CallerAddress, CallForwarding(WarmedAndCalledAddress, 20_000)),
                Deployed(WarmedButUntouchedAddress, code: null, balance: 1),
                Deployed(WarmedAndCalledAddress, DoNothing));

            Assert.Null(Entry(result, WarmedButUntouchedAddress));

            var entry = RequiredEntry(result, WarmedAndCalledAddress);
            Assert.Empty(entry.StorageChanges);
            Assert.Empty(entry.BalanceChanges);
        }
    }
}
