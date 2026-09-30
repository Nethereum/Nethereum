using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Precompiles;
using Nethereum.EVM.Witness;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.Core.Tests.GeneralStateTests
{
    public class Eip7702WitnessAuthorityContractTests
    {
        private const string FirstAuthorityKey = "0x59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690d";
        private const string SecondAuthorityKey = "0x5de4111afa1a4b94908f83103eb1f1706367c2e68ca870fc3fb9a804cdab365a";
        private const string RecipientAddress = "0x00000000000000000000000000000000000000e1";
        private const string DelegationTargetAddress = "0x00000000000000000000000000000000000000e2";
        private const long BlockChainId = 1;
        private const string ZeroAddress = "0x0000000000000000000000000000000000000000";

        private static string AddressOf(string privateKey) => new EthECKey(privateKey).GetPublicAddress();

        private static Authorisation7702Signed Authorisation(string privateKey) =>
            new Authorisation7702Signer().SignAuthorisation(
                privateKey,
                new Authorisation7702
                {
                    ChainId = new EvmUInt256((ulong)BlockChainId),
                    Address = DelegationTargetAddress,
                    Nonce = EvmUInt256.Zero
                });

        private static BlockExecutionResult ExecuteWithWitnessAuthorities(List<string> authorities)
        {
            var sender = TestTransactionHelper.GetDefaultSenderAddress();

            var transaction = new Transaction7702(
                chainId: new EvmUInt256((ulong)BlockChainId),
                nonce: EvmUInt256.Zero,
                maxPriorityFeePerGas: new EvmUInt256(1UL),
                maxFeePerGas: new EvmUInt256(10UL),
                gasLimit: new EvmUInt256(1_000_000UL),
                receiverAddress: RecipientAddress,
                amount: EvmUInt256.Zero,
                data: "0x",
                accessList: new List<Nethereum.Model.AccessListItem>(),
                authorisationList: new List<Authorisation7702Signed>
                {
                    Authorisation(FirstAuthorityKey),
                    Authorisation(SecondAuthorityKey)
                });

            var block = new BlockWitnessData
            {
                BlockNumber = 1,
                Timestamp = 1000,
                BaseFee = 1,
                BlockGasLimit = 30000000,
                ChainId = BlockChainId,
                Coinbase = "0x2adc25665018aa1fe0e6bc666dac8fc2697ff9ba",
                Difficulty = new byte[32],
                ParentHash = new byte[32],
                ExtraData = new byte[0],
                MixHash = new byte[32],
                Nonce = new byte[8],
                Features = new BlockFeatureConfig { Fork = HardforkName.Amsterdam },
                Transactions = new List<BlockWitnessTransaction>
                {
                    new BlockWitnessTransaction
                    {
                        From = sender,
                        RlpEncoded = new Transaction7702Signer()
                            .SignTransaction(TestTransactionHelper.DefaultPrivateKey, transaction).HexToByteArray(),
                        AuthorisationAuthorities = authorities
                    }
                },
                Accounts = new List<WitnessAccount>
                {
                    Account(sender, new EvmUInt256(1_000_000_000_000_000_000UL), 0, new byte[0]),
                    Account(RecipientAddress, EvmUInt256.Zero, 1, new byte[] { 0x00 }),
                    Account(AddressOf(FirstAuthorityKey), EvmUInt256.Zero, 0, new byte[0]),
                    Account(AddressOf(SecondAuthorityKey), EvmUInt256.Zero, 0, new byte[0])
                }
            };

            return BlockExecutor.Execute(
                block.AddRequestPredeploys(),
                RlpBlockEncodingProvider.Instance,
                DefaultMainnetHardforkRegistry.Instance);
        }

        private static WitnessAccount Account(string address, EvmUInt256 balance, ulong nonce, byte[] code) =>
            new WitnessAccount
            {
                Address = address,
                Balance = balance,
                Nonce = nonce,
                Code = code,
                Storage = new List<WitnessStorageSlot>()
            };

        private static AccountChanges Entry(BlockExecutionResult result, string address) =>
            result.BlockAccessList.SingleOrDefault(
                a => string.Equals(a.Address, address, StringComparison.OrdinalIgnoreCase));

        [Fact]
        [Trait("Category", "Witness")]
        public void Given_AType4TransactionWhoseWitnessOmittedItsAuthorities_When_Executed_Then_ExecutionHalts()
        {
            var halt = Assert.Throws<EvmHostException>(() => ExecuteWithWitnessAuthorities(null));

            Assert.Contains("2 authorization tuple(s)", halt.Message);
        }

        [Fact]
        [Trait("Category", "Witness")]
        public void Given_AWitnessAuthorityListShorterThanItsTuples_When_Executed_Then_ExecutionHalts()
        {
            Assert.Throws<EvmHostException>(() => ExecuteWithWitnessAuthorities(
                new List<string> { AddressOf(FirstAuthorityKey) }));
        }

        [Fact]
        [Trait("Category", "Witness")]
        public void Given_AWitnessThatRecoveredNoAuthorityForATuple_When_Executed_Then_OnlyThatTupleIsSkipped()
        {
            var result = ExecuteWithWitnessAuthorities(
                new List<string> { null, AddressOf(SecondAuthorityKey) });

            Assert.True(Entry(result, AddressOf(FirstAuthorityKey)) == null,
                "a tuple with no recovered authority names no account, so nothing about it is read");
            Assert.True(Entry(result, ZeroAddress) == null,
                "the skipped tuple normalised to the zero address and was read there");
            var zeroAccount = result.StateReader.GetAccountState(ZeroAddress);
            Assert.True(zeroAccount == null || zeroAccount.Code == null || zeroAccount.Code.Length == 0,
                "the skipped tuple normalised to the zero address and delegated it");
            Assert.NotEmpty(Entry(result, AddressOf(SecondAuthorityKey)).CodeChanges);
        }

        [Fact]
        [Trait("Category", "Witness")]
        public void Given_AWitnessThatRecoveredEveryAuthority_When_Executed_Then_EveryTupleDelegates()
        {
            var result = ExecuteWithWitnessAuthorities(
                new List<string> { AddressOf(FirstAuthorityKey), AddressOf(SecondAuthorityKey) });

            Assert.NotEmpty(Entry(result, AddressOf(FirstAuthorityKey)).CodeChanges);
            Assert.NotEmpty(Entry(result, AddressOf(SecondAuthorityKey)).CodeChanges);
        }

        [Fact]
        [Trait("Category", "Witness")]
        public void Given_ASignedType4Transaction_When_ItsAuthoritiesAreRecovered_Then_EachTupleYieldsItsSigner()
        {
            var transaction = new Transaction7702(
                chainId: new EvmUInt256((ulong)BlockChainId),
                nonce: EvmUInt256.Zero,
                maxPriorityFeePerGas: new EvmUInt256(1UL),
                maxFeePerGas: new EvmUInt256(10UL),
                gasLimit: new EvmUInt256(1_000_000UL),
                receiverAddress: RecipientAddress,
                amount: EvmUInt256.Zero,
                data: "0x",
                accessList: new List<Nethereum.Model.AccessListItem>(),
                authorisationList: new List<Authorisation7702Signed>
                {
                    Authorisation(FirstAuthorityKey),
                    Authorisation(SecondAuthorityKey)
                });

            var signed = TransactionFactory.CreateTransaction(
                new Transaction7702Signer().SignTransaction(TestTransactionHelper.DefaultPrivateKey, transaction).HexToByteArray());

            Assert.Equal(
                new[] { AddressOf(FirstAuthorityKey), AddressOf(SecondAuthorityKey) },
                signed.RecoverAuthorities());
        }

        [Fact]
        [Trait("Category", "Witness")]
        public void Given_AnAuthorizationWithANonCanonicalSignature_When_Recovered_Then_NoAuthorityIsProduced()
        {
            var authorisation = Authorisation(FirstAuthorityKey);

            var overLargeS = new Authorisation7702Signed(
                authorisation.ChainId, authorisation.Address, authorisation.Nonce,
                authorisation.R, Secp256K1Order, authorisation.V);

            Assert.Null(overLargeS.TryRecoverSignerAddress());
            Assert.NotNull(authorisation.TryRecoverSignerAddress());
        }

        private static readonly byte[] Secp256K1Order =
            "0xfffffffffffffffffffffffffffffffebaaedce6af48a03bbfd25e8cd0364141".HexToByteArray();

        [Fact]
        [Trait("Category", "Witness")]
        public void Given_ATransactionThatIsNotType4_When_ItsAuthoritiesAreRecovered_Then_ThereIsNoList()
        {
            var legacy = TransactionFactory.CreateTransaction(
                TestTransactionHelper.CreateSignedTransfer(
                    RecipientAddress, EvmUInt256.Zero, EvmUInt256.Zero,
                    new EvmUInt256(10UL), new EvmUInt256(21000UL)).RlpEncoded);

            Assert.Null(legacy.RecoverAuthorities());
        }
    }
}
