using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.EVM;
using Nethereum.EVM.Witness;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class BlockWitnessAuthorityProductionTests
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

        private static string AddressOf(string privateKey) => new EthECKey(privateKey).GetPublicAddress();

        private static ISignedTransaction SetCodeTransaction(params string[] authorityPrivateKeys)
        {
            var authorisationSigner = new Authorisation7702Signer();
            var transaction = new Transaction7702(
                chainId: ChainId,
                nonce: 0,
                maxPriorityFeePerGas: BaseFee,
                maxFeePerGas: BaseFee,
                gasLimit: 900_000,
                receiverAddress: RecipientAddress,
                amount: 0,
                data: "",
                accessList: null,
                authorisationList: authorityPrivateKeys
                    .Select(key => authorisationSigner.SignAuthorisation(
                        new EthECKey(key),
                        new Authorisation7702 { ChainId = ChainId, Address = DelegateAddress, Nonce = 0 }))
                    .ToList());

            return TransactionFactory.CreateTransaction(
                new Transaction7702Signer().SignTransaction(SenderPrivateKey, transaction));
        }

        private static ISignedTransaction ValueTransfer() =>
            TransactionFactory.CreateTransaction(new LegacyTransactionSigner().SignTransaction(
                SenderPrivateKey.HexToByteArray(), ChainId, RecipientAddress, 100, 0, BaseFee, 21_000, ""));

        private static async Task<BlockWitnessData> CaptureWitnessAsync(ISignedTransaction transaction)
        {
            var stateStore = new InMemoryStateStore();
            await Nethereum.CoreChain.Forks.SystemContractPredeploys
                .ApplyGenesisAllocationAsync(stateStore, Nethereum.EVM.HardforkName.Amsterdam);
            await stateStore.SaveAccountAsync(SenderAddress, new Account { Balance = 1_000_000_000_000_000, Nonce = 0 });
            await stateStore.SaveAccountAsync(FeeRecipientAddress, new Account { Balance = 1_000, Nonce = 0 });
            await stateStore.SaveAccountAsync(RecipientAddress, new Account { Balance = 1_000, Nonce = 0 });

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
                options: new BlockExecutionOptions { CaptureWitness = true });

            Assert.Null(result.Exception);
            Assert.NotNull(result.WitnessBytes);
            return BinaryBlockWitness.Deserialize(result.WitnessBytes);
        }

        [Fact]
        public async Task Given_ABlockCarryingAType4Transaction_When_ItsWitnessIsBuilt_Then_EachTupleCarriesItsRecoveredAuthority()
        {
            var witness = await CaptureWitnessAsync(
                SetCodeTransaction(FirstAuthorityPrivateKey, SecondAuthorityPrivateKey));

            Assert.Equal(
                new[] { AddressOf(FirstAuthorityPrivateKey), AddressOf(SecondAuthorityPrivateKey) },
                Assert.Single(witness.Transactions).AuthorisationAuthorities);
        }

        [Fact]
        public async Task Given_ABlockWithNoType4Transaction_When_ItsWitnessIsBuilt_Then_NoAuthorityListIsCarried()
        {
            var witness = await CaptureWitnessAsync(ValueTransfer());

            Assert.Null(Assert.Single(witness.Transactions).AuthorisationAuthorities);
        }
    }
}
