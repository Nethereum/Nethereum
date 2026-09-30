using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class BlockAccessListDestroyedAccountTests
    {
        private const string SenderPrivateKey = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private const string SenderAddress = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        private const string FeeRecipientAddress = "0x90F79bf6EB2c4f870365E785982E1f101E93b906";
        private const string BeneficiaryAddress = "0x000000000000000000000000000000000000dead";
        private const string CalledContractAddress = "0x0000000000000000000000000000000000000b01";

        private const long BaseFee = 7;
        private static readonly BigInteger ChainId = 1337;

        private static readonly string DestroyedAddress =
            ContractUtils.CalculateContractAddress(SenderAddress, 0);

        private static string Push20(string address) => "73" + address.Substring(2).ToLowerInvariant();

        private static readonly string SweepToBeneficiaryInitCode = Push20(BeneficiaryAddress) + "ff";

        private static readonly byte[] DoNothing = "00".HexToByteArray();

        private sealed class Contract
        {
            public string Address;
            public byte[] Code;
            public long Balance;
        }

        private static Contract Funded(string address, long balance) =>
            new Contract { Address = address, Balance = balance };

        private static Contract Deployed(string address, byte[] code, long balance = 0) =>
            new Contract { Address = address, Code = code, Balance = balance };

        private static ISignedTransaction CreationTransaction(string initCode, long gasLimit) =>
            TransactionFactory.CreateTransaction(new Transaction1559Signer().SignTransaction(
                SenderPrivateKey,
                new Transaction1559(ChainId, 0, BaseFee, BaseFee, gasLimit, "", 0, initCode, null)));

        private static ISignedTransaction CallTo(string to, long gasLimit) =>
            TransactionFactory.CreateTransaction(new Transaction1559Signer().SignTransaction(
                SenderPrivateKey,
                new Transaction1559(ChainId, 0, BaseFee, BaseFee, gasLimit, to, 0, "", null)));

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
            var receipt = Assert.Single(result.Receipts);
            Assert.True(receipt.Success, receipt.RevertReason ?? "transaction did not succeed");
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
        public async Task Given_ADestroyedAccount_When_TheBlockAccessListIsBuilt_Then_ItsBalanceChangeToZeroIsRecordedAtItsOwnTransactionIndex()
        {
            var result = await ExecuteAmsterdamBlockAsync(
                CreationTransaction(SweepToBeneficiaryInitCode, gasLimit: 2_000_000),
                Funded(DestroyedAddress, balance: 1));

            var entry = RequiredEntry(result, DestroyedAddress);
            var change = Assert.Single(entry.BalanceChanges);
            Assert.Equal(EvmUInt256.Zero, change.PostBalance);
            Assert.Equal(1UL, change.BlockAccessIndex);
        }

        [Fact]
        public async Task Given_ADestroyedAccount_When_TheBlockAccessListIsBuilt_Then_TheBeneficiaryIsRecordedHoldingTheBalance()
        {
            var result = await ExecuteAmsterdamBlockAsync(
                CreationTransaction(SweepToBeneficiaryInitCode, gasLimit: 2_000_000),
                Funded(DestroyedAddress, balance: 1));

            var entry = RequiredEntry(result, BeneficiaryAddress);
            Assert.Equal(new EvmUInt256(1), Assert.Single(entry.BalanceChanges).PostBalance);
        }

        [Fact]
        public async Task Given_AnAccountWhoseBalanceIsUnchanged_When_TheBlockAccessListIsBuilt_Then_NoBalanceChangeIsRecorded()
        {
            var result = await ExecuteAmsterdamBlockAsync(
                CallTo(CalledContractAddress, gasLimit: 200_000),
                Deployed(CalledContractAddress, DoNothing, balance: 5));

            var entry = RequiredEntry(result, CalledContractAddress);
            Assert.Empty(entry.BalanceChanges);
        }
    }
}
