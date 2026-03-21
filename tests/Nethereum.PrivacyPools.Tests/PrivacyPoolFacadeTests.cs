using System.Numerics;
using Nethereum.Accounts.Bip32;
using Nethereum.Documentation;
using Xunit;

namespace Nethereum.PrivacyPools.Tests
{
    public class PrivacyPoolFacadeTests
    {
        private const string TEST_MNEMONIC = "test test test test test test test test test test test junk";

        [Fact]
        [Trait("Category", "PrivacyPools-Facade")]
        public void PrivacyPoolAccount_CanBeCreatedIndependently()
        {
            var account = new PrivacyPoolAccount(TEST_MNEMONIC);
            Assert.NotEqual(BigInteger.Zero, account.MasterNullifier);
            Assert.NotEqual(BigInteger.Zero, account.MasterSecret);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Facade")]
        public void PrivacyPoolAccount_DepositSecretsWorkOffline()
        {
            var account = new PrivacyPoolAccount(TEST_MNEMONIC);
            BigInteger scope = 12345;

            var (nullifier, secret) = account.CreateDepositSecrets(scope, depositIndex: 0);
            var precommitment = account.ComputePrecommitment(nullifier, secret);

            Assert.NotEqual(BigInteger.Zero, precommitment);

            var account2 = new PrivacyPoolAccount(TEST_MNEMONIC);
            var (n2, s2) = account2.CreateDepositSecrets(scope, depositIndex: 0);
            var pre2 = account2.ComputePrecommitment(n2, s2);
            Assert.Equal(precommitment, pre2);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Facade")]
        public void PrivacyPoolAccount_MultipleDepositsPerScope()
        {
            var account = new PrivacyPoolAccount(TEST_MNEMONIC);
            BigInteger scope = 12345;

            var precommitments = new System.Collections.Generic.HashSet<BigInteger>();
            for (int i = 0; i < 5; i++)
            {
                var (n, s) = account.CreateDepositSecrets(scope, i);
                var pre = account.ComputePrecommitment(n, s);
                Assert.True(precommitments.Add(pre), $"Duplicate at index {i}");
            }
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Facade")]
        [NethereumDocExample(DocSection.Protocols, "withdrawal-chain", "Deterministic withdrawal chain from mnemonic")]
        public void PrivacyPoolAccount_WithdrawalChainDeterministic()
        {
            var account = new PrivacyPoolAccount(TEST_MNEMONIC);
            BigInteger scope = 12345;
            BigInteger label = PrivacyPoolCommitment.ComputeLabel(scope, BigInteger.Zero);
            BigInteger depositValue = 1_000_000_000_000_000_000;

            var deposit = account.CreateDepositCommitment(scope, 0, depositValue, label);

            var withdrawal1 = account.CreateWithdrawalCommitment(label, childIndex: 0,
                value: depositValue / 2);
            var withdrawal2 = account.CreateWithdrawalCommitment(label, childIndex: 1,
                value: depositValue / 4);

            Assert.NotEqual(deposit.CommitmentHash, withdrawal1.CommitmentHash);
            Assert.NotEqual(withdrawal1.CommitmentHash, withdrawal2.CommitmentHash);

            var account2 = new PrivacyPoolAccount(TEST_MNEMONIC);
            var recovered1 = account2.CreateWithdrawalCommitment(label, childIndex: 0,
                value: depositValue / 2);
            Assert.Equal(withdrawal1.CommitmentHash, recovered1.CommitmentHash);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Facade")]
        public void PoolAccount_TracksWithdrawalChain()
        {
            var account = new PrivacyPoolAccount(TEST_MNEMONIC);
            BigInteger scope = 12345;
            BigInteger label = PrivacyPoolCommitment.ComputeLabel(scope, BigInteger.Zero);
            BigInteger depositValue = 1_000_000_000_000_000_000;

            var deposit = account.CreateDepositCommitment(scope, 0, depositValue, label);

            var poolAccount = new PoolAccount
            {
                Scope = scope,
                Deposit = AccountCommitment.FromCommitment(deposit, 0, 100, "0xabc")
            };

            Assert.Equal(depositValue, poolAccount.SpendableValue);
            Assert.True(poolAccount.IsSpendable);
            Assert.Same(poolAccount.Deposit, poolAccount.LatestCommitment);

            var halfValue = depositValue / 2;
            var withdrawal = account.CreateWithdrawalCommitment(label, 0, halfValue);
            poolAccount.Withdrawals.Add(
                AccountCommitment.FromCommitment(withdrawal, 1, 200, "0xdef"));

            Assert.Equal(halfValue, poolAccount.SpendableValue);
            Assert.Equal(withdrawal.CommitmentHash, poolAccount.LatestCommitment.Commitment.CommitmentHash);

            poolAccount.IsRagequitted = true;
            Assert.False(poolAccount.IsSpendable);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Facade")]
        public void NewWallet_GenerateAndRecover()
        {
            var mnemonic = Bip39.GenerateMnemonic(12);

            var account1 = new PrivacyPoolAccount(mnemonic);
            var (n, s) = account1.CreateDepositSecrets(scope: 999, depositIndex: 0);
            var precommitment = account1.ComputePrecommitment(n, s);

            var account2 = new PrivacyPoolAccount(mnemonic);
            var (n2, s2) = account2.CreateDepositSecrets(scope: 999, depositIndex: 0);
            var precommitment2 = account2.ComputePrecommitment(n2, s2);

            Assert.Equal(precommitment, precommitment2);
        }
    }
}
