using System.Numerics;
using Nethereum.Documentation;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Xunit;

namespace Nethereum.PrivacyPools.Tests
{
    public class CrossCompatibilityTests
    {
        private const string TEST_MNEMONIC = "test test test test test test test test test test test junk";

        [Fact]
        [Trait("Category", "PrivacyPools-CrossCompat")]
        public void Poseidon_T1_MatchesJavaScript()
        {
            var hasher = new PoseidonHasher(PoseidonParameterPreset.CircomT1);
            var result = hasher.Hash(BigInteger.One);
            Assert.Equal(
                BigInteger.Parse("18586133768512220936620570745912940619677854269274689475585506675881198879027"),
                result);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-CrossCompat")]
        public void Poseidon_T2_MatchesJavaScript()
        {
            var hasher = new PoseidonHasher(PoseidonParameterPreset.CircomT2);
            var result = hasher.Hash(BigInteger.One, new BigInteger(2));
            Assert.Equal(
                BigInteger.Parse("7853200120776062878684798364095072458815029376092732009249414926327459813530"),
                result);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-CrossCompat")]
        public void Poseidon_T3_MatchesJavaScript()
        {
            var hasher = new PoseidonHasher(PoseidonParameterPreset.CircomT3);
            var result = hasher.Hash(BigInteger.One, new BigInteger(2), new BigInteger(3));
            Assert.Equal(
                BigInteger.Parse("6542985608222806190361240322586112750744169038454362455181422643027100751666"),
                result);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-CrossCompat")]
        public void Legacy_MasterKeys_MatchJavaScript()
        {
            var account = PrivacyPoolAccount.CreateLegacy(TEST_MNEMONIC);

            Assert.Equal(
                BigInteger.Parse("16629217087516280053769625512741000936965671973118241282486996830438009025879"),
                account.MasterNullifier);

            Assert.Equal(
                BigInteger.Parse("9843793310547505184827673578253843418217689387365691544946232242162772441433"),
                account.MasterSecret);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-CrossCompat")]
        [NethereumDocExample(DocSection.Protocols, "cross-compatibility", "Master keys match 0xbow TypeScript SDK")]
        public void Safe_MasterKeys_MatchJavaScript()
        {
            var account = new PrivacyPoolAccount(TEST_MNEMONIC);

            Assert.Equal(
                BigInteger.Parse("20068762160393292801596226195912281868434195939362930533775271887246872084568"),
                account.MasterNullifier);

            Assert.Equal(
                BigInteger.Parse("4263194520628581151689140073493505946870598678660509318310629023735624352890"),
                account.MasterSecret);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-CrossCompat")]
        public void Legacy_DepositSecrets_Scope12345_Index0_MatchJavaScript()
        {
            var account = PrivacyPoolAccount.CreateLegacy(TEST_MNEMONIC);
            var (nullifier, secret) = account.CreateDepositSecrets(scope: 12345, depositIndex: 0);

            Assert.Equal(
                BigInteger.Parse("8037605194568591191392465154626377131038470523532716359769756390770198964796"),
                nullifier);

            Assert.Equal(
                BigInteger.Parse("19592491235863375805259747141947173604341798525876189673982327289068872141729"),
                secret);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-CrossCompat")]
        public void Safe_DepositSecrets_Scope12345_Index0_MatchJavaScript()
        {
            var account = new PrivacyPoolAccount(TEST_MNEMONIC);
            var (nullifier, secret) = account.CreateDepositSecrets(scope: 12345, depositIndex: 0);

            Assert.Equal(
                BigInteger.Parse("18799083407603226543886233241239845601765199220468928197737203484264534974328"),
                nullifier);

            Assert.Equal(
                BigInteger.Parse("15330187620018206781186163615746153118874798050558698314725491067582470174001"),
                secret);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-CrossCompat")]
        public void Legacy_DepositSecrets_Scope12345_Index1_MatchJavaScript()
        {
            var account = PrivacyPoolAccount.CreateLegacy(TEST_MNEMONIC);
            var (nullifier, secret) = account.CreateDepositSecrets(scope: 12345, depositIndex: 1);

            Assert.Equal(
                BigInteger.Parse("1096705491159121067880455458046171348639617130455573187400247334790578777121"),
                nullifier);

            Assert.Equal(
                BigInteger.Parse("21409253670274892404468152362111458935972242476823037637222428947393939351719"),
                secret);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-CrossCompat")]
        public void Safe_DepositSecrets_Scope12345_Index1_MatchJavaScript()
        {
            var account = new PrivacyPoolAccount(TEST_MNEMONIC);
            var (nullifier, secret) = account.CreateDepositSecrets(scope: 12345, depositIndex: 1);

            Assert.Equal(
                BigInteger.Parse("19680772378616183859773159152052009318547743051040396428045076615829991543806"),
                nullifier);

            Assert.Equal(
                BigInteger.Parse("6904700659503672703719873229706944332624720037700303163123064806497600512042"),
                secret);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-CrossCompat")]
        public void Legacy_Precommitment_MatchesJavaScript()
        {
            var account = PrivacyPoolAccount.CreateLegacy(TEST_MNEMONIC);
            var (nullifier, secret) = account.CreateDepositSecrets(scope: 12345, depositIndex: 0);
            var precommitment = account.ComputePrecommitment(nullifier, secret);

            Assert.Equal(
                BigInteger.Parse("19405370328208453354866988267453738877637909057832996740318332611416247267321"),
                precommitment);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-CrossCompat")]
        public void Safe_Precommitment_MatchesJavaScript()
        {
            var account = new PrivacyPoolAccount(TEST_MNEMONIC);
            var (nullifier, secret) = account.CreateDepositSecrets(scope: 12345, depositIndex: 0);
            var precommitment = account.ComputePrecommitment(nullifier, secret);

            Assert.Equal(
                BigInteger.Parse("20989285794294416915427341900376611790943415285600831049009268118579350954735"),
                precommitment);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-CrossCompat")]
        public void Legacy_CommitmentHash_MatchesJavaScript()
        {
            var account = PrivacyPoolAccount.CreateLegacy(TEST_MNEMONIC);
            var (nullifier, secret) = account.CreateDepositSecrets(scope: 12345, depositIndex: 0);

            var value = BigInteger.Parse("1000000000000000000");
            var label = new BigInteger(42);
            var commitment = PrivacyPoolCommitment.Create(value, label, nullifier, secret);

            Assert.Equal(
                BigInteger.Parse("6581052328044309944572509229741643068124058879852680304301405212883947166909"),
                commitment.CommitmentHash);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-CrossCompat")]
        [NethereumDocExample(DocSection.Protocols, "cross-compatibility", "Commitment hash matches 0xbow SDK", Order = 2)]
        public void Safe_CommitmentHash_MatchesJavaScript()
        {
            var account = new PrivacyPoolAccount(TEST_MNEMONIC);
            var (nullifier, secret) = account.CreateDepositSecrets(scope: 12345, depositIndex: 0);

            var value = BigInteger.Parse("1000000000000000000");
            var label = new BigInteger(42);
            var commitment = PrivacyPoolCommitment.Create(value, label, nullifier, secret);

            Assert.Equal(
                BigInteger.Parse("2437259894778772672765342353008225556665669965602864891664011882809548214291"),
                commitment.CommitmentHash);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-CrossCompat")]
        public void Legacy_NullifierHash_IsPoseidonOfNullifierOnly()
        {
            var account = PrivacyPoolAccount.CreateLegacy(TEST_MNEMONIC);
            var (nullifier, secret) = account.CreateDepositSecrets(scope: 12345, depositIndex: 0);

            var commitment = PrivacyPoolCommitment.Create(
                BigInteger.Parse("1000000000000000000"), new BigInteger(42), nullifier, secret);

            var hasherT1 = new PoseidonHasher(PoseidonParameterPreset.CircomT1);
            var expectedNullifierHash = hasherT1.Hash(nullifier);
            Assert.Equal(expectedNullifierHash, commitment.NullifierHash);

            Assert.Equal(
                BigInteger.Parse("19405370328208453354866988267453738877637909057832996740318332611416247267321"),
                commitment.Precommitment);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-CrossCompat")]
        public void Safe_NullifierHash_IsPoseidonOfNullifierOnly()
        {
            var account = new PrivacyPoolAccount(TEST_MNEMONIC);
            var (nullifier, secret) = account.CreateDepositSecrets(scope: 12345, depositIndex: 0);

            var commitment = PrivacyPoolCommitment.Create(
                BigInteger.Parse("1000000000000000000"), new BigInteger(42), nullifier, secret);

            var hasherT1 = new PoseidonHasher(PoseidonParameterPreset.CircomT1);
            var expectedNullifierHash = hasherT1.Hash(nullifier);
            Assert.Equal(expectedNullifierHash, commitment.NullifierHash);

            Assert.Equal(
                BigInteger.Parse("20989285794294416915427341900376611790943415285600831049009268118579350954735"),
                commitment.Precommitment);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-CrossCompat")]
        public void Legacy_WithdrawalSecrets_Label42_Index0_MatchJavaScript()
        {
            var account = PrivacyPoolAccount.CreateLegacy(TEST_MNEMONIC);
            var (nullifier, secret) = account.CreateWithdrawalSecrets(label: 42, childIndex: 0);

            Assert.Equal(
                BigInteger.Parse("14797163321637190547472125597137771031121503392391460099821757694496210436075"),
                nullifier);

            Assert.Equal(
                BigInteger.Parse("10792736894102772735736614277228776044074280264945699122691537450345638664651"),
                secret);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-CrossCompat")]
        public void Safe_WithdrawalSecrets_Label42_Index0_MatchJavaScript()
        {
            var account = new PrivacyPoolAccount(TEST_MNEMONIC);
            var (nullifier, secret) = account.CreateWithdrawalSecrets(label: 42, childIndex: 0);

            Assert.Equal(
                BigInteger.Parse("1980872316991559359161330914646252222519000137922535899794170500197294191442"),
                nullifier);

            Assert.Equal(
                BigInteger.Parse("21563020234007267038495623498396204986995053023966409630658634968140121948420"),
                secret);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-CrossCompat")]
        [NethereumDocExample(DocSection.Protocols, "cross-compatibility", "Context hash matches 0xbow SDK", Order = 3)]
        public void ContextHash_MatchesJavaScript()
        {
            var withdrawal = new Nethereum.PrivacyPools.Entrypoint.ContractDefinition.Withdrawal
            {
                Processooor = "0xa513E6E4b8f2a923D98304ec87F64353C4D5C853",
                Data = "0x00000000000000000000000070997970c51812dc3a010c7d01b50e0d17dc79c8000000000000000000000000f39fd6e51aad88f6f4ce6ab8827279cfffb92266000000000000000000000000000000000000000000000000000000000000c350".HexToByteArray()
            };
            var scope = BigInteger.Parse("0555c5fdc167f1f1519c1b21a690de24d9be5ff0bde19447a5f28958d9256e50", System.Globalization.NumberStyles.HexNumber);

            var context = WithdrawalContextHelper.ComputeContext(withdrawal, scope);

            var expectedHex = "266f59df0823b7efe6821eba38eb5de1177c6366a214b59f12154cd16079965a";
            var expected = BigInteger.Parse("0" + expectedHex, System.Globalization.NumberStyles.HexNumber);
            Assert.Equal(expected, context);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-CrossCompat")]
        public void Legacy_FullRecoveryFlow_CrossCompatible()
        {
            var account = PrivacyPoolAccount.CreateLegacy(TEST_MNEMONIC);
            BigInteger scope = 12345;
            BigInteger value = BigInteger.Parse("1000000000000000000");
            BigInteger label = 42;

            var (n, s) = account.CreateDepositSecrets(scope, 0);
            var commitment = PrivacyPoolCommitment.Create(value, label, n, s);

            Assert.Equal(
                BigInteger.Parse("6581052328044309944572509229741643068124058879852680304301405212883947166909"),
                commitment.CommitmentHash);

            var (wn, ws) = account.CreateWithdrawalSecrets(label, 0);
            var halfValue = value / 2;
            var newCommitment = PrivacyPoolCommitment.Create(halfValue, label, wn, ws);

            Assert.NotEqual(commitment.CommitmentHash, newCommitment.CommitmentHash);
            Assert.Equal(halfValue, newCommitment.Value);

            var account2 = PrivacyPoolAccount.CreateLegacy(TEST_MNEMONIC);
            var (n2, s2) = account2.CreateDepositSecrets(scope, 0);
            Assert.Equal(n, n2);
            Assert.Equal(s, s2);

            var (wn2, ws2) = account2.CreateWithdrawalSecrets(label, 0);
            Assert.Equal(wn, wn2);
            Assert.Equal(ws, ws2);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-CrossCompat")]
        public void Safe_FullRecoveryFlow_CrossCompatible()
        {
            var account = new PrivacyPoolAccount(TEST_MNEMONIC);
            BigInteger scope = 12345;
            BigInteger value = BigInteger.Parse("1000000000000000000");
            BigInteger label = 42;

            var (n, s) = account.CreateDepositSecrets(scope, 0);
            var commitment = PrivacyPoolCommitment.Create(value, label, n, s);

            Assert.Equal(
                BigInteger.Parse("2437259894778772672765342353008225556665669965602864891664011882809548214291"),
                commitment.CommitmentHash);

            var (wn, ws) = account.CreateWithdrawalSecrets(label, 0);
            var halfValue = value / 2;
            var newCommitment = PrivacyPoolCommitment.Create(halfValue, label, wn, ws);

            Assert.NotEqual(commitment.CommitmentHash, newCommitment.CommitmentHash);
            Assert.Equal(halfValue, newCommitment.Value);

            var account2 = new PrivacyPoolAccount(TEST_MNEMONIC);
            var (n2, s2) = account2.CreateDepositSecrets(scope, 0);
            Assert.Equal(n, n2);
            Assert.Equal(s, s2);

            var (wn2, ws2) = account2.CreateWithdrawalSecrets(label, 0);
            Assert.Equal(wn, wn2);
            Assert.Equal(ws, ws2);
        }
    }
}
