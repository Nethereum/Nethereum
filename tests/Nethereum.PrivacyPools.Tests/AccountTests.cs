using System;
using System.Collections.Generic;
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

        // ───────────────────────────────────────────────────────────
        //  Legacy tests (53-bit lossy path via CreateLegacy)
        // ───────────────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "PrivacyPools-Account")]
        [NethereumDocExample(DocSection.Protocols, "account-recovery", "Create account from mnemonic")]
        public void Legacy_CreateAccount_FromMnemonic_DerivesMasterKeys()
        {
            var account = PrivacyPoolAccount.CreateLegacy(TEST_MNEMONIC);

            Assert.NotEqual(BigInteger.Zero, account.MasterNullifier);
            Assert.NotEqual(BigInteger.Zero, account.MasterSecret);
            Assert.NotEqual(account.MasterNullifier, account.MasterSecret);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Account")]
        public void Legacy_CreateAccount_SameMnemonic_SameKeys()
        {
            var account1 = PrivacyPoolAccount.CreateLegacy(TEST_MNEMONIC);
            var account2 = PrivacyPoolAccount.CreateLegacy(TEST_MNEMONIC);

            Assert.Equal(account1.MasterNullifier, account2.MasterNullifier);
            Assert.Equal(account1.MasterSecret, account2.MasterSecret);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Account")]
        public void Legacy_CreateAccount_DifferentMnemonics_DifferentKeys()
        {
            var mnemonic2 = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
            var account1 = PrivacyPoolAccount.CreateLegacy(TEST_MNEMONIC);
            var account2 = PrivacyPoolAccount.CreateLegacy(mnemonic2);

            Assert.NotEqual(account1.MasterNullifier, account2.MasterNullifier);
            Assert.NotEqual(account1.MasterSecret, account2.MasterSecret);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Account")]
        public void Legacy_MasterKeys_MatchExpectedDerivation()
        {
            var wallet = new MinimalHDWallet(TEST_MNEMONIC);
            var key1Bytes = wallet.GetKeyFromPath("m/44'/60'/0'/0/0").GetPrivateKeyAsBytes();
            var key2Bytes = wallet.GetKeyFromPath("m/44'/60'/1'/0/0").GetPrivateKeyAsBytes();

            var hasherT1 = new PoseidonHasher(PoseidonParameterPreset.CircomT1);
            var expectedNullifier = hasherT1.Hash(PrivacyPoolAccount.BytesToBigIntViaDouble(key1Bytes));
            var expectedSecret = hasherT1.Hash(PrivacyPoolAccount.BytesToBigIntViaDouble(key2Bytes));

            var account = PrivacyPoolAccount.CreateLegacy(TEST_MNEMONIC);
            Assert.Equal(expectedNullifier, account.MasterNullifier);
            Assert.Equal(expectedSecret, account.MasterSecret);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Account")]
        public void Legacy_DepositSecrets_Deterministic_SameScopeAndIndex()
        {
            var account = PrivacyPoolAccount.CreateLegacy(TEST_MNEMONIC);
            BigInteger scope = 12345;

            var (n1, s1) = account.CreateDepositSecrets(scope, 0);
            var (n2, s2) = account.CreateDepositSecrets(scope, 0);

            Assert.Equal(n1, n2);
            Assert.Equal(s1, s2);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Account")]
        public void Legacy_DepositSecrets_DifferentPerIndex()
        {
            var account = PrivacyPoolAccount.CreateLegacy(TEST_MNEMONIC);
            BigInteger scope = 12345;

            var (n0, s0) = account.CreateDepositSecrets(scope, 0);
            var (n1, s1) = account.CreateDepositSecrets(scope, 1);

            Assert.NotEqual(n0, n1);
            Assert.NotEqual(s0, s1);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Account")]
        public void Legacy_DepositSecrets_DifferentPerScope()
        {
            var account = PrivacyPoolAccount.CreateLegacy(TEST_MNEMONIC);

            var (n1, s1) = account.CreateDepositSecrets(100, 0);
            var (n2, s2) = account.CreateDepositSecrets(200, 0);

            Assert.NotEqual(n1, n2);
            Assert.NotEqual(s1, s2);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Account")]
        public void Legacy_WithdrawalSecrets_Deterministic()
        {
            var account = PrivacyPoolAccount.CreateLegacy(TEST_MNEMONIC);
            BigInteger label = 99999;

            var (n1, s1) = account.CreateWithdrawalSecrets(label, 0);
            var (n2, s2) = account.CreateWithdrawalSecrets(label, 0);

            Assert.Equal(n1, n2);
            Assert.Equal(s1, s2);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Account")]
        public void Legacy_WithdrawalSecrets_DifferentFromDepositSecrets_WhenDifferentDomains()
        {
            var account = PrivacyPoolAccount.CreateLegacy(TEST_MNEMONIC);
            BigInteger scope = 12345;
            BigInteger label = PrivacyPoolCommitment.ComputeLabel(scope, BigInteger.Zero);

            var (dn, ds) = account.CreateDepositSecrets(scope, 0);
            var (wn, ws) = account.CreateWithdrawalSecrets(label, 0);

            Assert.NotEqual(dn, wn);
            Assert.NotEqual(ds, ws);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Account")]
        public void Legacy_CreateDepositCommitment_MatchesManualCreation()
        {
            var account = PrivacyPoolAccount.CreateLegacy(TEST_MNEMONIC);
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
        public void Legacy_Precommitment_MatchesOnChainDeposit()
        {
            var account = PrivacyPoolAccount.CreateLegacy(TEST_MNEMONIC);
            BigInteger scope = 12345;

            var (nullifier, secret) = account.CreateDepositSecrets(scope, 0);
            var precommitment = account.ComputePrecommitment(nullifier, secret);

            var commitment = PrivacyPoolCommitment.Create(100, 1, nullifier, secret);
            Assert.Equal(commitment.Precommitment, precommitment);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Account")]
        [NethereumDocExample(DocSection.Protocols, "account-recovery", "Deposit and recover from mnemonic", Order = 2)]
        public void Legacy_FullUserJourney_DepositAndRecover()
        {
            var scope = BigInteger.Parse("98765");
            var depositValue = BigInteger.Parse("1000000000000000000");
            var label = PrivacyPoolCommitment.ComputeLabel(scope, BigInteger.Zero);

            var account = PrivacyPoolAccount.CreateLegacy(TEST_MNEMONIC);
            var (nullifier, secret) = account.CreateDepositSecrets(scope, depositIndex: 0);
            var precommitment = account.ComputePrecommitment(nullifier, secret);

            var commitment = PrivacyPoolCommitment.Create(depositValue, label, nullifier, secret);
            Assert.Equal(precommitment, commitment.Precommitment);

            var recoveredAccount = PrivacyPoolAccount.CreateLegacy(TEST_MNEMONIC);
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
        public void Legacy_FullUserJourney_DepositWithdrawRecover()
        {
            var scope = BigInteger.Parse("98765");
            var depositValue = BigInteger.Parse("1000000000000000000");
            var label = PrivacyPoolCommitment.ComputeLabel(scope, BigInteger.Zero);
            var withdrawnValue = depositValue / 2;

            var account = PrivacyPoolAccount.CreateLegacy(TEST_MNEMONIC);

            var deposit = account.CreateDepositCommitment(scope, 0, depositValue, label);

            var tree = new PoseidonMerkleTree();
            tree.InsertCommitment(deposit.CommitmentHash);

            var (wNullifier, wSecret) = account.CreateWithdrawalSecrets(label, childIndex: 0);
            var newCommitment = PrivacyPoolCommitment.Create(
                depositValue - withdrawnValue, label, wNullifier, wSecret);
            tree.InsertCommitment(newCommitment.CommitmentHash);

            var recoveredAccount = PrivacyPoolAccount.CreateLegacy(TEST_MNEMONIC);
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

            poolAccount.IsMigrated = true;
            Assert.False(poolAccount.IsSpendable);

            poolAccount.IsMigrated = false;
            poolAccount.IsRagequitted = true;
            Assert.False(poolAccount.IsSpendable);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Account")]
        public void Legacy_MnemonicGeneration_ValidRoundTrip()
        {
            var mnemonic = Bip39.GenerateMnemonic(12);
            var words = mnemonic.Split(' ');
            Assert.Equal(12, words.Length);

            var account1 = PrivacyPoolAccount.CreateLegacy(mnemonic);
            var account2 = PrivacyPoolAccount.CreateLegacy(mnemonic);
            Assert.Equal(account1.MasterNullifier, account2.MasterNullifier);
            Assert.Equal(account1.MasterSecret, account2.MasterSecret);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Account")]
        public void Legacy_MultipleDeposits_EachHasUniqueSecrets()
        {
            var account = PrivacyPoolAccount.CreateLegacy(TEST_MNEMONIC);
            BigInteger scope = 12345;

            var precommitments = new System.Collections.Generic.HashSet<BigInteger>();
            for (int i = 0; i < 10; i++)
            {
                var (n, s) = account.CreateDepositSecrets(scope, i);
                var pre = account.ComputePrecommitment(n, s);
                Assert.True(precommitments.Add(pre), $"Duplicate precommitment at index {i}");
            }
        }

        // ───────────────────────────────────────────────────────────
        //  Safe tests (full 256-bit BytesToBigInt via new constructor)
        // ───────────────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "PrivacyPools-Account")]
        public void Safe_CreateAccount_FromMnemonic_DerivesMasterKeys()
        {
            var account = new PrivacyPoolAccount(TEST_MNEMONIC);

            Assert.NotEqual(BigInteger.Zero, account.MasterNullifier);
            Assert.NotEqual(BigInteger.Zero, account.MasterSecret);
            Assert.NotEqual(account.MasterNullifier, account.MasterSecret);

            Assert.Equal(
                BigInteger.Parse("20068762160393292801596226195912281868434195939362930533775271887246872084568"),
                account.MasterNullifier);
            Assert.Equal(
                BigInteger.Parse("4263194520628581151689140073493505946870598678660509318310629023735624352890"),
                account.MasterSecret);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Account")]
        public void Safe_CreateAccount_SameMnemonic_SameKeys()
        {
            var account1 = new PrivacyPoolAccount(TEST_MNEMONIC);
            var account2 = new PrivacyPoolAccount(TEST_MNEMONIC);

            Assert.Equal(account1.MasterNullifier, account2.MasterNullifier);
            Assert.Equal(account1.MasterSecret, account2.MasterSecret);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Account")]
        public void Safe_CreateAccount_DifferentMnemonics_DifferentKeys()
        {
            var mnemonic2 = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
            var account1 = new PrivacyPoolAccount(TEST_MNEMONIC);
            var account2 = new PrivacyPoolAccount(mnemonic2);

            Assert.NotEqual(account1.MasterNullifier, account2.MasterNullifier);
            Assert.NotEqual(account1.MasterSecret, account2.MasterSecret);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Account")]
        public void Safe_MasterKeys_MatchExpectedDerivation()
        {
            var wallet = new MinimalHDWallet(TEST_MNEMONIC);
            var key1Bytes = wallet.GetKeyFromPath("m/44'/60'/0'/0/0").GetPrivateKeyAsBytes();
            var key2Bytes = wallet.GetKeyFromPath("m/44'/60'/1'/0/0").GetPrivateKeyAsBytes();

            var hasherT1 = new PoseidonHasher(PoseidonParameterPreset.CircomT1);
            var expectedNullifier = hasherT1.Hash(PrivacyPoolAccount.BytesToBigInt(key1Bytes));
            var expectedSecret = hasherT1.Hash(PrivacyPoolAccount.BytesToBigInt(key2Bytes));

            var account = new PrivacyPoolAccount(TEST_MNEMONIC);
            Assert.Equal(expectedNullifier, account.MasterNullifier);
            Assert.Equal(expectedSecret, account.MasterSecret);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Account")]
        public void Safe_DepositSecrets_Deterministic_SameScopeAndIndex()
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
        public void Safe_DepositSecrets_DifferentPerIndex()
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
        public void Safe_DepositSecrets_DifferentPerScope()
        {
            var account = new PrivacyPoolAccount(TEST_MNEMONIC);

            var (n1, s1) = account.CreateDepositSecrets(100, 0);
            var (n2, s2) = account.CreateDepositSecrets(200, 0);

            Assert.NotEqual(n1, n2);
            Assert.NotEqual(s1, s2);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Account")]
        public void Safe_DepositSecrets_MatchExpectedValues()
        {
            var account = new PrivacyPoolAccount(TEST_MNEMONIC);
            BigInteger scope = 12345;

            var (n0, s0) = account.CreateDepositSecrets(scope, 0);
            Assert.Equal(
                BigInteger.Parse("18799083407603226543886233241239845601765199220468928197737203484264534974328"),
                n0);
            Assert.Equal(
                BigInteger.Parse("15330187620018206781186163615746153118874798050558698314725491067582470174001"),
                s0);

            var (n1, s1) = account.CreateDepositSecrets(scope, 1);
            Assert.Equal(
                BigInteger.Parse("19680772378616183859773159152052009318547743051040396428045076615829991543806"),
                n1);
            Assert.Equal(
                BigInteger.Parse("6904700659503672703719873229706944332624720037700303163123064806497600512042"),
                s1);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Account")]
        public void Safe_WithdrawalSecrets_Deterministic()
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
        public void Safe_WithdrawalSecrets_MatchExpectedValues()
        {
            var account = new PrivacyPoolAccount(TEST_MNEMONIC);
            BigInteger label = 42;

            var (n0, s0) = account.CreateWithdrawalSecrets(label, 0);
            Assert.Equal(
                BigInteger.Parse("1980872316991559359161330914646252222519000137922535899794170500197294191442"),
                n0);
            Assert.Equal(
                BigInteger.Parse("21563020234007267038495623498396204986995053023966409630658634968140121948420"),
                s0);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Account")]
        public void Safe_WithdrawalSecrets_DifferentFromDepositSecrets_WhenDifferentDomains()
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
        public void Safe_CreateDepositCommitment_MatchesManualCreation()
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
        public void Safe_Precommitment_MatchesOnChainDeposit()
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
        public void Safe_FullUserJourney_DepositAndRecover()
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
        public void Safe_FullUserJourney_DepositWithdrawRecover()
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
        public void Safe_MnemonicGeneration_ValidRoundTrip()
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
        public void Safe_MultipleDeposits_EachHasUniqueSecrets()
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

        // ───────────────────────────────────────────────────────────
        //  Migration discovery
        // ───────────────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "PrivacyPools-Account")]
        public void RecoverAccounts_WithoutLegacyContext_ThrowsToAvoidSkippingMigratedFunds()
        {
            BigInteger scope = 0;
            BigInteger label = 42;
            BigInteger depositValue = 1000;
            var account = new PrivacyPoolAccount(TEST_MNEMONIC);
            var pp = new PrivacyPool(new Nethereum.Web3.Web3(), "0xentry", "0xpool", account);

            var (nullifier, secret) = account.CreateDepositSecrets(scope, 0);
            var precommitment = account.ComputePrecommitment(nullifier, secret);
            var commitment = PrivacyPoolCommitment.Create(depositValue, label, nullifier, secret);

            var ex = Assert.Throws<InvalidOperationException>(() => pp.RecoverAccounts(
                new List<PoolDepositEventData>
                {
                    new PoolDepositEventData
                    {
                        Commitment = commitment.CommitmentHash,
                        Label = label,
                        Value = depositValue,
                        PrecommitmentHash = precommitment,
                        BlockNumber = 100,
                        TransactionHash = "0xabc"
                    }
                },
                new List<PoolWithdrawalEventData>(),
                new List<PoolRagequitEventData>(),
                new List<PoolLeafEventData>
                {
                    new PoolLeafEventData { Leaf = commitment.CommitmentHash, Index = 0 }
                }));

            Assert.Contains("legacy account", ex.Message);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Account")]
        public void RecoverSafeAccounts_WithSafeAccountOnly_RecoversSafeDeposits()
        {
            BigInteger scope = 0;
            BigInteger label = 42;
            BigInteger depositValue = 1000;
            var account = new PrivacyPoolAccount(TEST_MNEMONIC);
            var pp = new PrivacyPool(new Nethereum.Web3.Web3(), "0xentry", "0xpool", account);

            var (nullifier, secret) = account.CreateDepositSecrets(scope, 0);
            var precommitment = account.ComputePrecommitment(nullifier, secret);
            var commitment = PrivacyPoolCommitment.Create(depositValue, label, nullifier, secret);

            var recovered = pp.RecoverSafeAccounts(
                new List<PoolDepositEventData>
                {
                    new PoolDepositEventData
                    {
                        Commitment = commitment.CommitmentHash,
                        Label = label,
                        Value = depositValue,
                        PrecommitmentHash = precommitment,
                        BlockNumber = 100,
                        TransactionHash = "0xabc"
                    }
                },
                new List<PoolWithdrawalEventData>(),
                new List<PoolRagequitEventData>(),
                new List<PoolLeafEventData>
                {
                    new PoolLeafEventData { Leaf = commitment.CommitmentHash, Index = 0 }
                });

            var poolAccount = Assert.Single(recovered);
            Assert.Equal(commitment.CommitmentHash, poolAccount.Deposit.Commitment.CommitmentHash);
            Assert.True(poolAccount.IsSpendable);
            Assert.Null(pp.LegacyAccount);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Account")]
        public void RecoverAccounts_WithExplicitLegacyContext_RecoversMigratedFunds()
        {
            BigInteger scope = 0;
            var legacyAccount = PrivacyPoolAccount.CreateLegacy(TEST_MNEMONIC);
            var safeAccount = new PrivacyPoolAccount(TEST_MNEMONIC);
            var pp = new PrivacyPool(
                new Nethereum.Web3.Web3(),
                "0xentry",
                "0xpool",
                safeAccount,
                legacyAccount);

            BigInteger depositIndex = 0;
            BigInteger depositValue = 1000;
            var (legN, legS) = legacyAccount.CreateDepositSecrets(scope, depositIndex);
            var legacyPrecommitment = legacyAccount.ComputePrecommitment(legN, legS);
            BigInteger label = 42;
            var legacyCommitment = PrivacyPoolCommitment.Create(depositValue, label, legN, legS);

            var (safeN0, safeS0) = safeAccount.CreateWithdrawalSecrets(label, BigInteger.Zero);
            var migratedCommitment = PrivacyPoolCommitment.Create(depositValue, label, safeN0, safeS0);

            var postMigrationValue = depositValue / 2;
            var (safeN1, safeS1) = safeAccount.CreateWithdrawalSecrets(label, BigInteger.One);
            var postMigrationCommitment = PrivacyPoolCommitment.Create(
                postMigrationValue, label, safeN1, safeS1);

            var recovered = pp.RecoverAccounts(
                new List<PoolDepositEventData>
                {
                    new PoolDepositEventData
                    {
                        Commitment = legacyCommitment.CommitmentHash,
                        Label = label,
                        Value = depositValue,
                        PrecommitmentHash = legacyPrecommitment,
                        BlockNumber = 100,
                        TransactionHash = "0xabc"
                    }
                },
                new List<PoolWithdrawalEventData>
                {
                    new PoolWithdrawalEventData
                    {
                        SpentNullifier = legacyCommitment.NullifierHash,
                        NewCommitment = migratedCommitment.CommitmentHash,
                        Value = 0,
                        BlockNumber = 200,
                        TransactionHash = "0xdef"
                    },
                    new PoolWithdrawalEventData
                    {
                        SpentNullifier = migratedCommitment.NullifierHash,
                        NewCommitment = postMigrationCommitment.CommitmentHash,
                        Value = depositValue - postMigrationValue,
                        BlockNumber = 300,
                        TransactionHash = "0xghi"
                    }
                },
                new List<PoolRagequitEventData>(),
                new List<PoolLeafEventData>
                {
                    new PoolLeafEventData { Leaf = legacyCommitment.CommitmentHash, Index = 0 },
                    new PoolLeafEventData { Leaf = migratedCommitment.CommitmentHash, Index = 1 },
                    new PoolLeafEventData { Leaf = postMigrationCommitment.CommitmentHash, Index = 2 }
                });

            Assert.Equal(2, recovered.Count);

            var legacyRecovered = recovered.Find(a =>
                a.Deposit.Commitment.CommitmentHash == legacyCommitment.CommitmentHash);
            Assert.NotNull(legacyRecovered);
            Assert.True(legacyRecovered.IsMigrated);
            Assert.False(legacyRecovered.IsSpendable);

            var migratedRecovered = recovered.Find(a =>
                a.Deposit.Commitment.CommitmentHash == migratedCommitment.CommitmentHash);
            Assert.NotNull(migratedRecovered);
            Assert.Equal(postMigrationCommitment.CommitmentHash,
                migratedRecovered.LatestCommitment.Commitment.CommitmentHash);
            Assert.Equal(postMigrationValue, migratedRecovered.SpendableValue);
            Assert.True(migratedRecovered.IsSpendable);
            Assert.NotNull(pp.LegacyAccount);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Account")]
        public void RecoverAccounts_MarksLegacyMigrationAsUnspendable()
        {
            BigInteger scope = 99;
            var legacyAccount = PrivacyPoolAccount.CreateLegacy(TEST_MNEMONIC);
            var safeAccount = new PrivacyPoolAccount(TEST_MNEMONIC);

            BigInteger depositIndex = 0;
            BigInteger depositValue = 1000;
            var (legN, legS) = legacyAccount.CreateDepositSecrets(scope, depositIndex);
            var legacyPrecommitment = legacyAccount.ComputePrecommitment(legN, legS);
            BigInteger label = 42;
            var legacyCommitment = PrivacyPoolCommitment.Create(depositValue, label, legN, legS);

            var (safeN, safeS) = safeAccount.CreateWithdrawalSecrets(label, BigInteger.Zero);
            var migratedCommitment = PrivacyPoolCommitment.Create(depositValue, label, safeN, safeS);

            var recovered = PrivacyPoolAccountRecovery.RecoverAccounts(
                legacyAccount,
                scope,
                new List<PoolDepositEventData>
                {
                    new PoolDepositEventData
                    {
                        Commitment = legacyCommitment.CommitmentHash,
                        Label = label,
                        Value = depositValue,
                        PrecommitmentHash = legacyPrecommitment,
                        BlockNumber = 100,
                        TransactionHash = "0xabc"
                    }
                },
                new List<PoolWithdrawalEventData>
                {
                    new PoolWithdrawalEventData
                    {
                        SpentNullifier = legacyCommitment.NullifierHash,
                        NewCommitment = migratedCommitment.CommitmentHash,
                        Value = 0,
                        BlockNumber = 200,
                        TransactionHash = "0xdef"
                    }
                },
                new List<PoolRagequitEventData>(),
                new List<PoolLeafEventData>
                {
                    new PoolLeafEventData { Leaf = legacyCommitment.CommitmentHash, Index = 0 },
                    new PoolLeafEventData { Leaf = migratedCommitment.CommitmentHash, Index = 1 }
                });

            Assert.Single(recovered);
            Assert.True(recovered[0].IsMigrated);
            Assert.False(recovered[0].IsSpendable);
            Assert.Single(recovered[0].Withdrawals);
            Assert.True(recovered[0].Withdrawals[0].IsMigration);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Account")]
        public void DiscoverMigratedCommitments_RecoversSafeChainAfterMigration()
        {
            BigInteger scope = 99;
            var legacyAccount = PrivacyPoolAccount.CreateLegacy(TEST_MNEMONIC);
            var safeAccount = new PrivacyPoolAccount(TEST_MNEMONIC);

            BigInteger depositIndex = 0;
            BigInteger depositValue = 1000;
            var (legN, legS) = legacyAccount.CreateDepositSecrets(scope, depositIndex);
            var legacyPrecommitment = legacyAccount.ComputePrecommitment(legN, legS);
            BigInteger label = 42;
            var legacyCommitment = PrivacyPoolCommitment.Create(depositValue, label, legN, legS);

            var (safeN0, safeS0) = safeAccount.CreateWithdrawalSecrets(label, BigInteger.Zero);
            var migratedCommitment = PrivacyPoolCommitment.Create(depositValue, label, safeN0, safeS0);

            var postMigrationValue = depositValue / 2;
            var (safeN1, safeS1) = safeAccount.CreateWithdrawalSecrets(label, BigInteger.One);
            var postMigrationCommitment = PrivacyPoolCommitment.Create(
                postMigrationValue, label, safeN1, safeS1);

            var withdrawals = new List<PoolWithdrawalEventData>
            {
                new PoolWithdrawalEventData
                {
                    SpentNullifier = legacyCommitment.NullifierHash,
                    NewCommitment = migratedCommitment.CommitmentHash,
                    Value = 0,
                    BlockNumber = 200,
                    TransactionHash = "0xdef"
                },
                new PoolWithdrawalEventData
                {
                    SpentNullifier = migratedCommitment.NullifierHash,
                    NewCommitment = postMigrationCommitment.CommitmentHash,
                    Value = depositValue - postMigrationValue,
                    BlockNumber = 300,
                    TransactionHash = "0xghi"
                }
            };

            var leafInserts = new List<PoolLeafEventData>
            {
                new PoolLeafEventData { Leaf = legacyCommitment.CommitmentHash, Index = 0 },
                new PoolLeafEventData { Leaf = migratedCommitment.CommitmentHash, Index = 1 },
                new PoolLeafEventData { Leaf = postMigrationCommitment.CommitmentHash, Index = 2 }
            };

            var legacyRecovered = PrivacyPoolAccountRecovery.RecoverAccounts(
                legacyAccount,
                scope,
                new List<PoolDepositEventData>
                {
                    new PoolDepositEventData
                    {
                        Commitment = legacyCommitment.CommitmentHash,
                        Label = label,
                        Value = depositValue,
                        PrecommitmentHash = legacyPrecommitment,
                        BlockNumber = 100,
                        TransactionHash = "0xabc"
                    }
                },
                withdrawals,
                new List<PoolRagequitEventData>(),
                leafInserts);

            var result = PrivacyPoolAccountRecovery.DiscoverMigratedCommitments(
                safeAccount,
                scope,
                legacyRecovered,
                withdrawals,
                leafInserts,
                new List<PoolRagequitEventData>());

            Assert.Single(result);
            Assert.Equal(migratedCommitment.CommitmentHash, result[0].Deposit.Commitment.CommitmentHash);
            Assert.Equal(1, result[0].Deposit.LeafIndex);
            Assert.Equal(2, result[0].Withdrawals.Count);
            Assert.Equal(migratedCommitment.CommitmentHash, result[0].Withdrawals[0].Commitment.CommitmentHash);
            Assert.Equal(postMigrationCommitment.CommitmentHash, result[0].LatestCommitment.Commitment.CommitmentHash);
            Assert.Equal(postMigrationValue, result[0].SpendableValue);
            Assert.True(result[0].IsSpendable);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Account")]
        public void DiscoverMigratedCommitments_SkipsNonMigratedAccounts()
        {
            BigInteger scope = 99;
            var legacyAccount = PrivacyPoolAccount.CreateLegacy(TEST_MNEMONIC);
            var safeAccount = new PrivacyPoolAccount(TEST_MNEMONIC);

            var (legN, legS) = legacyAccount.CreateDepositSecrets(scope, 0);
            BigInteger label = 42;
            var legacyCommitment = PrivacyPoolCommitment.Create(1000, label, legN, legS);

            var legacyPoolAccount = new PoolAccount
            {
                Scope = scope,
                Deposit = AccountCommitment.FromCommitment(legacyCommitment, 0, 100, "0xabc")
            };

            var result = PrivacyPoolAccountRecovery.DiscoverMigratedCommitments(
                safeAccount,
                scope,
                new List<PoolAccount> { legacyPoolAccount },
                new List<PoolWithdrawalEventData>(),
                new List<PoolLeafEventData>(),
                new List<PoolRagequitEventData>());

            Assert.Empty(result);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Account")]
        public void RecoverAccounts_RagequitMatchesByLabelAfterWithdrawal()
        {
            BigInteger scope = 99;
            BigInteger depositValue = 1000;
            BigInteger label = 42;
            var account = new PrivacyPoolAccount(TEST_MNEMONIC);

            var (depositNullifier, depositSecret) = account.CreateDepositSecrets(scope, 0);
            var depositPrecommitment = account.ComputePrecommitment(depositNullifier, depositSecret);
            var depositCommitment = PrivacyPoolCommitment.Create(
                depositValue, label, depositNullifier, depositSecret);

            var remainingValue = depositValue / 2;
            var (withdrawNullifier, withdrawSecret) = account.CreateWithdrawalSecrets(label, BigInteger.Zero);
            var withdrawalCommitment = PrivacyPoolCommitment.Create(
                remainingValue, label, withdrawNullifier, withdrawSecret);

            var recovered = PrivacyPoolAccountRecovery.RecoverAccounts(
                account,
                scope,
                new List<PoolDepositEventData>
                {
                    new PoolDepositEventData
                    {
                        Commitment = depositCommitment.CommitmentHash,
                        Label = label,
                        Value = depositValue,
                        PrecommitmentHash = depositPrecommitment,
                        BlockNumber = 100,
                        TransactionHash = "0xabc"
                    }
                },
                new List<PoolWithdrawalEventData>
                {
                    new PoolWithdrawalEventData
                    {
                        SpentNullifier = depositCommitment.NullifierHash,
                        NewCommitment = withdrawalCommitment.CommitmentHash,
                        Value = depositValue - remainingValue,
                        BlockNumber = 200,
                        TransactionHash = "0xdef"
                    }
                },
                new List<PoolRagequitEventData>
                {
                    new PoolRagequitEventData
                    {
                        Commitment = withdrawalCommitment.CommitmentHash,
                        Label = label,
                        Value = remainingValue,
                        BlockNumber = 300,
                        TransactionHash = "0xghi"
                    }
                },
                new List<PoolLeafEventData>
                {
                    new PoolLeafEventData { Leaf = depositCommitment.CommitmentHash, Index = 0 },
                    new PoolLeafEventData { Leaf = withdrawalCommitment.CommitmentHash, Index = 1 }
                });

            Assert.Single(recovered);
            Assert.True(recovered[0].IsRagequitted);
            Assert.False(recovered[0].IsSpendable);
            Assert.Equal(300, recovered[0].RagequitBlockNumber);
        }

        // ───────────────────────────────────────────────────────────
        //  BytesToBigInt entropy preservation test
        // ───────────────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "PrivacyPools-Account")]
        public void BytesToBigInt_PreservesFullEntropy()
        {
            var bytes = new byte[32];
            bytes[0] = 0xFF;
            bytes[31] = 0x01;
            var result = PrivacyPoolAccount.BytesToBigInt(bytes);
            // Should be much larger than 2^53 (the lossy double limit)
            Assert.True(result > BigInteger.Pow(2, 53));
            // Roundtrip: converting back should give same bytes
            var roundtrip = result.ToByteArray(isUnsigned: true, isBigEndian: true);
            Assert.Equal(bytes, roundtrip);
        }
    }
}
