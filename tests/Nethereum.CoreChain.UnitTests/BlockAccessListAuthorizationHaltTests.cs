using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class BlockAccessListAuthorizationHaltTests
    {
        private const string SenderPrivateKey = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private const string SenderAddress = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        private const string RecipientAddress = "0x9965507D1a55bcC2695C58ba16FB37d819B0A4dc";
        private const string FeeRecipientAddress = "0x90F79bf6EB2c4f870365E785982E1f101E93b906";
        private const string DelegateAddress = "0x976EA74026E726554dB657fA54763abd0C3a0aa9";

        private const string FirstAuthorityPrivateKey = "59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690d";
        private const string SecondAuthorityPrivateKey = "5de4111afa1a4b94908f83103eb1f1706367c2e68ca870fc3fb9a804cdab365a";

        private const long BaseFee = 7;
        private static readonly BigInteger ChainId = 1337;

        private const long GasLimitThatHaltsOnTheSecondAuthority = 300_000;

        private const long GasLimitThatCompletesBothAuthorizations = 900_000;

        private static readonly byte[] InfiniteLoop = "0x5b600056".HexToByteArray();

        private static readonly Transaction7702Signer TxSigner = new();
        private static readonly Authorisation7702Signer AuthSigner = new();

        private static Authorisation7702Signed Authorisation(string authorityPrivateKey) =>
            AuthSigner.SignAuthorisation(
                new EthECKey(authorityPrivateKey),
                new Authorisation7702 { ChainId = ChainId, Address = DelegateAddress, Nonce = 0 });

        private static string AddressOf(string privateKey) => new EthECKey(privateKey).GetPublicAddress();

        private static ISignedTransaction SetCodeTransaction(long gasLimit, params string[] authorityPrivateKeys)
        {
            var tx = new Transaction7702(
                chainId: ChainId,
                nonce: 0,
                maxPriorityFeePerGas: BaseFee,
                maxFeePerGas: BaseFee,
                gasLimit: gasLimit,
                receiverAddress: RecipientAddress,
                amount: 0,
                data: "",
                accessList: null,
                authorisationList: authorityPrivateKeys.Select(Authorisation).ToList());

            return TransactionFactory.CreateTransaction(TxSigner.SignTransaction(SenderPrivateKey, tx));
        }

        private static ISignedTransaction ValueTransfer(long gasLimit) =>
            TransactionFactory.CreateTransaction(new LegacyTransactionSigner().SignTransaction(
                SenderPrivateKey.HexToByteArray(), ChainId, RecipientAddress, 100, 0, BaseFee, gasLimit, ""));

        private static async Task<BlockExecutionResult> ExecuteAmsterdamBlockAsync(
            ISignedTransaction transaction, byte[] recipientCode = null)
        {
            var stateStore = new InMemoryStateStore();
            await Nethereum.CoreChain.Forks.SystemContractPredeploys
                .ApplyGenesisAllocationAsync(stateStore, Nethereum.EVM.HardforkName.Amsterdam);
            await stateStore.SaveAccountAsync(SenderAddress, new Account { Balance = 1_000_000_000_000_000, Nonce = 0 });
            await stateStore.SaveAccountAsync(FeeRecipientAddress, new Account { Balance = 1_000, Nonce = 0 });

            var recipient = new Account { Balance = 1_000, Nonce = 0 };
            if (recipientCode != null)
            {
                var codeHash = new Sha3Keccack().CalculateHash(recipientCode);
                await stateStore.SaveCodeAsync(codeHash, recipientCode);
                recipient.CodeHash = codeHash;
            }
            await stateStore.SaveAccountAsync(RecipientAddress, recipient);

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

        [Fact]
        public async Task Given_AuthorizationHaltsOutOfGas_When_BlockExecuted_Then_RecipientIsAbsentFromTheAccessList()
        {
            var result = await ExecuteAmsterdamBlockAsync(SetCodeTransaction(
                GasLimitThatHaltsOnTheSecondAuthority, FirstAuthorityPrivateKey, SecondAuthorityPrivateKey));

            var txResult = Assert.Single(result.Receipts);
            Assert.False(txResult.Success);
            Assert.Equal(GasLimitThatHaltsOnTheSecondAuthority, txResult.GasUsed);

            Assert.Null(Entry(result, RecipientAddress));
        }

        [Fact]
        public async Task Given_AuthorizationHaltsOutOfGas_When_BlockExecuted_Then_AuthoritiesAreStillListed()
        {
            var result = await ExecuteAmsterdamBlockAsync(SetCodeTransaction(
                GasLimitThatHaltsOnTheSecondAuthority, FirstAuthorityPrivateKey, SecondAuthorityPrivateKey));

            Assert.NotNull(Entry(result, AddressOf(FirstAuthorityPrivateKey)));
            Assert.NotNull(Entry(result, AddressOf(SecondAuthorityPrivateKey)));
        }

        [Fact]
        public async Task Given_AuthorizationsAllApply_When_BlockExecuted_Then_RecipientIsListed()
        {
            var result = await ExecuteAmsterdamBlockAsync(SetCodeTransaction(
                GasLimitThatCompletesBothAuthorizations, FirstAuthorityPrivateKey, SecondAuthorityPrivateKey));

            Assert.True(Assert.Single(result.Receipts).Success);
            Assert.NotNull(Entry(result, RecipientAddress));
        }

        [Fact]
        public async Task Given_ExecutionHaltsOutOfGas_When_BlockExecuted_Then_RecipientIsListed()
        {
            var result = await ExecuteAmsterdamBlockAsync(ValueTransfer(gasLimit: 100_000), InfiniteLoop);

            Assert.False(Assert.Single(result.Receipts).Success);
            Assert.NotNull(Entry(result, RecipientAddress));
        }

        [Fact]
        public async Task Given_AnOrdinaryTransfer_When_BlockExecuted_Then_RecipientIsListed()
        {
            var result = await ExecuteAmsterdamBlockAsync(ValueTransfer(gasLimit: 21_000));

            Assert.True(Assert.Single(result.Receipts).Success);
            Assert.NotNull(Entry(result, RecipientAddress));
        }
    }
}
