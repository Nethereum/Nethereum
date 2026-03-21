using System.Numerics;
using Nethereum.Accounts.Bip32;
using Nethereum.Documentation;
using Nethereum.Util;
using Xunit;

namespace Nethereum.PrivacyPools.Tests
{
    public class AccountTests
    {
        private const string TEST_MNEMONIC = "test test test test test test test test test test test junk";

        [Fact]
        [Trait("Category", "PrivacyPools-Account")]
        [NethereumDocExample(DocSection.Protocols, "account-recovery", "Create account from mnemonic")]
        public void CreateAccount_FromMnemonic_DerivesMasterKeys()
        {
            var account = new PrivacyPoolAccount(TEST_MNEMONIC);

            Assert.NotEqual(BigInteger.Zero, account.MasterNullifier);
            Assert.NotEqual(BigInteger.Zero, account.MasterSecret);
            Assert.NotEqual(account.MasterNullifier, account.MasterSecret);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Account")]
        public void CreateAccount_SameMnemonic_SameKeys()
        {
            var account1 = new PrivacyPoolAccount(TEST_MNEMONIC);
            var account2 = new PrivacyPoolAccount(TEST_MNEMONIC);

            Assert.Equal(account1.MasterNullifier, account2.MasterNullifier);
            Assert.Equal(account1.MasterSecret, account2.MasterSecret);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Account")]
        public void CreateAccount_DifferentMnemonics_DifferentKeys()
        {
            var mnemonic2 = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
            var account1 = new PrivacyPoolAccount(TEST_MNEMONIC);
            var account2 = new PrivacyPoolAccount(mnemonic2);

            Assert.NotEqual(account1.MasterNullifier, account2.MasterNullifier);
            Assert.NotEqual(account1.MasterSecret, account2.MasterSecret);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Account")]
        public void MasterKeys_MatchExpectedDerivation()
        {
            var wallet = new MinimalHDWallet(TEST_MNEMONIC);
            var key1Bytes = wallet.GetKeyFromPath("m/44'/60'/0'/0/0").GetPrivateKeyAsBytes();
            var key2Bytes = wallet.GetKeyFromPath("m/44'/60'/1'/0/0").GetPrivateKeyAsBytes();

            var hasherT1 = new PoseidonHasher(PoseidonParameterPreset.CircomT1);
            var expectedNullifier = hasherT1.Hash(PrivacyPoolAccount.BytesToBigIntViaDouble(key1Bytes));
            var expectedSecret = hasherT1.Hash(PrivacyPoolAccount.BytesToBigIntViaDouble(key2Bytes));

            var account = new PrivacyPoolAccount(TEST_MNEMONIC);
            Assert.Equal(expectedNullifier, account.MasterNullifier);
            Assert.Equal(expectedSecret, account.MasterSecret);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Account")]
        public void DepositSecrets_Deterministic_SameScopeAndIndex()
        {
            var account = new PrivacyPoolAccount(TEST_MNEMONIC);
            BigInteger scope = 12345;

            var (n1, s1) = account.CreateDepositSecrets(scope, 0);
            var (n2, s2) = account.CreateDepositSecrets(scope, 0);

            Assert.Equal(n1, n2);
            Assert.Equal(s1, s2);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Account")]
        public void DepositSecrets_DifferentPerIndex()
        {
            var account = new PrivacyPoolAccount(TEST_MNEMONIC);
            BigInteger scope = 12345;

            var (n0, s0) = account.CreateDepositSecrets(scope, 0);
            var (n1, s1) = account.CreateDepositSecrets(scope, 1);

            Assert.NotEqual(n0, n1);
            Assert.NotEqual(s0, s1);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Account")]
        public void DepositSecrets_DifferentPerScope()
        {
            var account = new PrivacyPoolAccount(TEST_MNEMONIC);

            var (n1, s1) = account.CreateDepositSecrets(100, 0);
            var (n2, s2) = account.CreateDepositSecrets(200, 0);

            Assert.NotEqual(n1, n2);
            Assert.NotEqual(s1, s2);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Account")]
        public void WithdrawalSecrets_Deterministic()
        {
            var account = new PrivacyPoolAccount(TEST_MNEMONIC);
            BigInteger label = 99999;

            var (n1, s1) = account.CreateWithdrawalSecrets(label, 0);
            var (n2, s2) = account.CreateWithdrawalSecrets(label, 0);

            Assert.Equal(n1, n2);
            Assert.Equal(s1, s2);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Account")]
        public void WithdrawalSecrets_DifferentFromDepositSecrets_WhenDifferentDomains()
        {
            var account = new PrivacyPoolAccount(TEST_MNEMONIC);
            BigInteger scope = 12345;
            BigInteger label = PrivacyPoolCommitment.ComputeLabel(scope, BigInteger.Zero);

            var (dn, ds) = account.CreateDepositSecrets(scope, 0);
            var (wn, ws) = account.CreateWithdrawalSecrets(label, 0);

            Assert.NotEqual(dn, wn);
            Assert.NotEqual(ds, ws);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Account")]
        public void CreateDepositCommitment_MatchesManualCreation()
        {
            var account = new PrivacyPoolAccount(TEST_MNEMONIC);
            BigInteger scope = 12345;
            BigInteger value = 1_000_000_000_000_000_000;
            BigInteger label = 42;

            var commitment = account.CreateDepositCommitment(scope, 0, value, label);

            var (nullifier, secret) = account.CreateDepositSecrets(scope, 0);
            var manual = PrivacyPoolCommitment.Create(value, label, nullifier, secret);

            Assert.Equal(manual.CommitmentHash, commitment.CommitmentHash);
            Assert.Equal(manual.NullifierHash, commitment.NullifierHash);
            Assert.Equal(manual.Precommitment, commitment.Precommitment);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Account")]
        public void Precommitment_MatchesOnChainDeposit()
        {
            var account = new PrivacyPoolAccount(TEST_MNEMONIC);
            BigInteger scope = 12345;

            var (nullifier, secret) = account.CreateDepositSecrets(scope, 0);
            var precommitment = account.ComputePrecommitment(nullifier, secret);

            var commitment = PrivacyPoolCommitment.Create(100, 1, nullifier, secret);
            Assert.Equal(commitment.Precommitment, precommitment);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Account")]
        [NethereumDocExample(DocSection.Protocols, "account-recovery", "Deposit and recover from mnemonic", Order = 2)]
        public void FullUserJourney_DepositAndRecover()
        {
            var scope = BigInteger.Parse("98765");
            var depositValue = BigInteger.Parse("1000000000000000000");
            var label = PrivacyPoolCommitment.ComputeLabel(scope, BigInteger.Zero);

            var account = new PrivacyPoolAccount(TEST_MNEMONIC);
            var (nullifier, secret) = account.CreateDepositSecrets(scope, depositIndex: 0);
            var precommitment = account.ComputePrecommitment(nullifier, secret);

            var commitment = PrivacyPoolCommitment.Create(depositValue, label, nullifier, secret);
            Assert.Equal(precommitment, commitment.Precommitment);

            var recoveredAccount = new PrivacyPoolAccount(TEST_MNEMONIC);
            var (recoveredN, recoveredS) = recoveredAccount.CreateDepositSecrets(scope, depositIndex: 0);
            var recoveredPrecommitment = recoveredAccount.ComputePrecommitment(recoveredN, recoveredS);

            Assert.Equal(precommitment, recoveredPrecommitment);

            var recoveredCommitment = PrivacyPoolCommitment.Create(depositValue, label, recoveredN, recoveredS);
            Assert.Equal(commitment.CommitmentHash, recoveredCommitment.CommitmentHash);
            Assert.Equal(commitment.NullifierHash, recoveredCommitment.NullifierHash);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Account")]
        [NethereumDocExample(DocSection.Protocols, "account-recovery", "Full deposit-withdraw-recover cycle", Order = 3)]
        public void FullUserJourney_DepositWithdrawRecover()
        {
            var scope = BigInteger.Parse("98765");
            var depositValue = BigInteger.Parse("1000000000000000000");
            var label = PrivacyPoolCommitment.ComputeLabel(scope, BigInteger.Zero);
            var withdrawnValue = depositValue / 2;

            var account = new PrivacyPoolAccount(TEST_MNEMONIC);

            var deposit = account.CreateDepositCommitment(scope, 0, depositValue, label);

            var tree = new PoseidonMerkleTree();
            tree.InsertCommitment(deposit.CommitmentHash);

            var (wNullifier, wSecret) = account.CreateWithdrawalSecrets(label, childIndex: 0);
            var newCommitment = PrivacyPoolCommitment.Create(
                depositValue - withdrawnValue, label, wNullifier, wSecret);
            tree.InsertCommitment(newCommitment.CommitmentHash);

            var recoveredAccount = new PrivacyPoolAccount(TEST_MNEMONIC);
            var recoveredDeposit = recoveredAccount.CreateDepositCommitment(scope, 0, depositValue, label);
            Assert.Equal(deposit.CommitmentHash, recoveredDeposit.CommitmentHash);

            var (rwN, rwS) = recoveredAccount.CreateWithdrawalSecrets(label, childIndex: 0);
            var recoveredNewCommitment = PrivacyPoolCommitment.Create(
                depositValue - withdrawnValue, label, rwN, rwS);
            Assert.Equal(newCommitment.CommitmentHash, recoveredNewCommitment.CommitmentHash);

            Assert.True(tree.VerifyInclusionProof(
                tree.GenerateInclusionProof(1), newCommitment.CommitmentHash));
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Account")]
        public void PoolAccount_TrackSpendableValue()
        {
            var depositValue = BigInteger.Parse("1000000000000000000");

            var poolAccount = new PoolAccount
            {
                Scope = 12345,
                Deposit = AccountCommitment.FromCommitment(
                    PrivacyPoolCommitment.CreateRandom(depositValue, 1),
                    0, 100, "0xabc")
            };

            Assert.Equal(depositValue, poolAccount.SpendableValue);
            Assert.True(poolAccount.IsSpendable);

            var halfValue = depositValue / 2;
            poolAccount.Withdrawals.Add(AccountCommitment.FromCommitment(
                PrivacyPoolCommitment.CreateRandom(halfValue, 1),
                1, 200, "0xdef"));

            Assert.Equal(halfValue, poolAccount.SpendableValue);
            Assert.True(poolAccount.IsSpendable);

            poolAccount.IsRagequitted = true;
            Assert.False(poolAccount.IsSpendable);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Account")]
        public void MnemonicGeneration_ValidRoundTrip()
        {
            var mnemonic = Bip39.GenerateMnemonic(12);
            var words = mnemonic.Split(' ');
            Assert.Equal(12, words.Length);

            var account1 = new PrivacyPoolAccount(mnemonic);
            var account2 = new PrivacyPoolAccount(mnemonic);
            Assert.Equal(account1.MasterNullifier, account2.MasterNullifier);
            Assert.Equal(account1.MasterSecret, account2.MasterSecret);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Account")]
        public void MultipleDeposits_EachHasUniqueSecrets()
        {
            var account = new PrivacyPoolAccount(TEST_MNEMONIC);
            BigInteger scope = 12345;

            var precommitments = new System.Collections.Generic.HashSet<BigInteger>();
            for (int i = 0; i < 10; i++)
            {
                var (n, s) = account.CreateDepositSecrets(scope, i);
                var pre = account.ComputePrecommitment(n, s);
                Assert.True(precommitments.Add(pre), $"Duplicate precommitment at index {i}");
            }
        }
    }
}
