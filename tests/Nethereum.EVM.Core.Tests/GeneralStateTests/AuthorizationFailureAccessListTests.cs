using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.EVM;
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
    /// <summary>
    /// AMS-7928-21 of <c>docs/internal/glamsterdam-traceability-matrix.md</c>:
    /// three DISTINCT EIP-7702 authorization-failure shapes, each pinning
    /// whether the authority had been loaded by the time the tuple was
    /// rejected — and therefore whether it belongs in the block access list.
    ///
    /// <para>
    /// EIP-7702 §Behavior lists the checks in order, and the order is the
    /// rule: <i>"Verify the chain ID is either 0 or the ID of the current
    /// chain"</i> comes before <i>"Verify the code of authority is either
    /// empty or already delegated"</i> and <i>"Verify the nonce of authority
    /// is equal to nonce"</i>. Only the last two need the account, so only
    /// they make it BAL-visible. EIP-7928 lists an account that was read,
    /// with an empty change set when nothing about it changed.
    /// </para>
    ///
    /// <para>
    /// A wrong chain ID must therefore leave the authority ABSENT, while a
    /// pre-existing code or a nonce mismatch must leave it PRESENT with
    /// nothing recorded against it. The delegation target named by the tuple
    /// is read only when the delegation is actually applied, so it is absent
    /// from all three. The accepted tuple is the fourth case, and it is what
    /// stops "present with no changes" from being indistinguishable from
    /// "the recorder never records anything".
    /// </para>
    /// </summary>
    public class AuthorizationFailureAccessListTests
    {
        private const string AuthorityKey = "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";
        private const string RecipientAddress = "0x00000000000000000000000000000000000000e1";
        private const string DelegationTargetAddress = "0x00000000000000000000000000000000000000e2";

        private const long BlockChainId = 1;
        private const long ForeignChainId = 999;

        private static string AuthorityAddress => new EthECKey(AuthorityKey).GetPublicAddress();

        private static BlockExecutionResult ExecuteAuthorizationBlock(
            long authorizationChainId, ulong authorizationNonce, byte[] authorityCode)
        {
            var sender = TestTransactionHelper.GetDefaultSenderAddress();

            var authorisation = new Authorisation7702Signer().SignAuthorisation(
                AuthorityKey,
                new Authorisation7702
                {
                    ChainId = new EvmUInt256((ulong)authorizationChainId),
                    Address = DelegationTargetAddress,
                    Nonce = new EvmUInt256(authorizationNonce)
                });

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
                authorisationList: new List<Authorisation7702Signed> { authorisation });

            var signedHex = new Transaction7702Signer().SignTransaction(TestTransactionHelper.DefaultPrivateKey, transaction);

            var tx = new BlockWitnessTransaction
            {
                From = sender,
                RlpEncoded = signedHex.HexToByteArray(),
                AuthorisationAuthorities = new List<string> { AuthorityAddress }
            };

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
                Transactions = new List<BlockWitnessTransaction> { tx },
                Accounts = new List<WitnessAccount>
                {
                    Account(sender, new EvmUInt256(1_000_000_000_000_000_000UL), 0, new byte[0]),
                    Account(RecipientAddress, EvmUInt256.Zero, 1, new byte[] { 0x00 }),
                    Account(AuthorityAddress, EvmUInt256.Zero, 0, authorityCode)
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

        private static void AssertLoadedButUnchanged(BlockExecutionResult result)
        {
            var authority = Entry(result, AuthorityAddress);
            Assert.True(authority != null, "the authority was loaded before the tuple was rejected and must be listed");
            Assert.Empty(authority.BalanceChanges);
            Assert.Empty(authority.NonceChanges);
            Assert.Empty(authority.CodeChanges);
            Assert.Empty(authority.StorageChanges);
            Assert.True(Entry(result, DelegationTargetAddress) == null,
                "the delegation target is read only when the delegation is applied");
        }

        [Fact]
        [Trait("Category", "EIP7928")]
        public void Given_AnAuthorizationForAnotherChain_When_TheBlockAccessListIsBuilt_Then_TheAuthorityIsAbsent()
        {
            var result = ExecuteAuthorizationBlock(ForeignChainId, authorizationNonce: 0, authorityCode: new byte[0]);

            Assert.True(result.TxResults.Single().Success, result.TxResults.Single().Error);
            Assert.True(Entry(result, RecipientAddress) != null, "the recipient must be listed — the recorder was running");
            Assert.True(Entry(result, AuthorityAddress) == null,
                "the chain-id check precedes every state read, so the authority was never loaded");
            Assert.True(Entry(result, DelegationTargetAddress) == null,
                "the delegation target is read only when the delegation is applied");
        }

        [Fact]
        [Trait("Category", "EIP7928")]
        public void Given_AnAuthorityThatAlreadyCarriesCode_When_TheBlockAccessListIsBuilt_Then_ItIsPresentWithNoChanges()
        {
            var result = ExecuteAuthorizationBlock(BlockChainId, authorizationNonce: 0, authorityCode: new byte[] { 0x00 });

            Assert.True(result.TxResults.Single().Success, result.TxResults.Single().Error);
            AssertLoadedButUnchanged(result);
        }

        [Fact]
        [Trait("Category", "EIP7928")]
        public void Given_AnAuthorizationWithTheWrongNonce_When_TheBlockAccessListIsBuilt_Then_TheAuthorityIsPresentWithNoChanges()
        {
            var result = ExecuteAuthorizationBlock(BlockChainId, authorizationNonce: 7, authorityCode: new byte[0]);

            Assert.True(result.TxResults.Single().Success, result.TxResults.Single().Error);
            AssertLoadedButUnchanged(result);
        }

        [Fact]
        [Trait("Category", "EIP7928")]
        public void Given_AnAcceptedAuthorization_When_TheBlockAccessListIsBuilt_Then_TheAuthorityCarriesItsNonceAndCodeChange()
        {
            var result = ExecuteAuthorizationBlock(BlockChainId, authorizationNonce: 0, authorityCode: new byte[0]);

            Assert.True(result.TxResults.Single().Success, result.TxResults.Single().Error);

            var authority = Entry(result, AuthorityAddress);
            Assert.True(authority != null, "an accepted authorization writes to its authority and must list it");
            Assert.NotEmpty(authority.NonceChanges);
            Assert.NotEmpty(authority.CodeChanges);
        }
    }
}
