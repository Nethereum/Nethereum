using System.Collections.Generic;
using System.Linq;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia.Proofs;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;

namespace Nethereum.Merkle.Patricia.Tests
{
    using ProofVerification = Nethereum.Merkle.Patricia.ProofVerification.ProofVerification;

    public class AccountAndStorageProofSoundnessTests
    {
        private const string PresentAddress = "0x12890d2cce102216644c59dae5baed380d84830c";
        private const string AbsentAddress = "0x00000000000000000000000000000000000000aa";
        private static readonly Sha3KeccackHashProvider HashProvider = Sha3KeccackHashProvider.Instance;

        private static readonly byte[] SetSlot = { 0x05 };
        private static readonly byte[] SetSlotValue = { 0x69 };
        private static readonly byte[] UnsetSlot = { 0x7F };

        private static Account EmptyAccount() => new Account
        {
            Nonce = 0,
            Balance = 0,
            CodeHash = DefaultValues.EMPTY_DATA_HASH,
            StateRoot = DefaultValues.EMPTY_TRIE_HASH
        };

        private static (PatriciaTrie trie, byte[] root) BuildStorage()
        {
            var trie = new PatriciaTrie(new InMemoryContentNodeStore());
            for (var i = 1; i <= 32; i++)
                trie.Put(AccountStorage.EncodeKeyForStorage(new[] { (byte)(0x80 + i) }, HashProvider),
                         AccountStorage.EncodeValueForStorage(new byte[] { (byte)i, 0x01 }));
            trie.Put(AccountStorage.EncodeKeyForStorage(SetSlot, HashProvider),
                     AccountStorage.EncodeValueForStorage(SetSlotValue));
            trie.SaveNodesToStorage();
            return (trie, trie.Root.GetHash());
        }

        private static (PatriciaTrie trie, byte[] root, Account present) BuildState()
        {
            var trie = new PatriciaTrie(new InMemoryContentNodeStore());
            var present = new Account { Nonce = 1, Balance = 5, CodeHash = DefaultValues.EMPTY_DATA_HASH, StateRoot = DefaultValues.EMPTY_TRIE_HASH };
            trie.Put(HashProvider.ComputeHash(PresentAddress.HexToByteArray()), AccountEncoder.Current.Encode(present));
            for (var i = 0; i < 64; i++)
                trie.Put(HashProvider.ComputeHash(new[] { (byte)i, (byte)0xAA }),
                         AccountEncoder.Current.Encode(new Account { Nonce = (ulong)i, CodeHash = DefaultValues.EMPTY_DATA_HASH, StateRoot = DefaultValues.EMPTY_TRIE_HASH }));
            trie.SaveNodesToStorage();
            return (trie, trie.Root.GetHash(), present);
        }

        private static List<byte[]> SlotProof(PatriciaTrie trie, byte[] slot)
            => ProofGenerator.GeneratePathProof(trie, AccountStorage.EncodeKeyForStorage(slot, HashProvider));

        [Fact]
        public void Given_ASlotHoldingAValue_When_ZeroIsClaimedWithAnEmptyProof_Then_StorageVerifyRejectsTheForgery()
        {
            var (_, root) = BuildStorage();

            Assert.False(ProofVerification.Current.Storage.Verify(root, new List<byte[]>(), SetSlot, new byte[] { 0x00 }));
            Assert.False(ProofVerification.Current.Storage.Verify(root, new List<byte[]>(), SetSlot, new byte[0]));
        }

        [Fact]
        public void Given_ASlotHoldingAValue_When_ZeroIsClaimedWithOnlyTheRootNode_Then_StorageVerifyRejectsTheTruncatedProof()
        {
            var (trie, root) = BuildStorage();
            var truncated = SlotProof(trie, SetSlot).Take(1).ToList();

            Assert.False(ProofVerification.Current.Storage.Verify(root, truncated, SetSlot, new byte[] { 0x00 }));
        }

        [Fact]
        public void Given_AnUnsetSlot_When_ZeroIsClaimedWithAnEmptyProof_Then_StorageVerifyRejectsItBecauseNothingWasProven()
        {
            var (_, root) = BuildStorage();

            Assert.False(ProofVerification.Current.Storage.Verify(root, new List<byte[]>(), UnsetSlot, new byte[] { 0x00 }));
        }

        [Fact]
        public void Given_AnUnsetSlot_When_ItsExclusionProofClaimsZero_Then_StorageVerifyAcceptsIt_AndRejectsANonZeroClaim()
        {
            var (trie, root) = BuildStorage();
            var exclusion = SlotProof(trie, UnsetSlot);

            Assert.True(ProofVerification.Current.Storage.Verify(root, exclusion, UnsetSlot, new byte[] { 0x00 }));
            Assert.True(ProofVerification.Current.Storage.Verify(root, exclusion, UnsetSlot, new byte[0]));
            Assert.False(ProofVerification.Current.Storage.Verify(root, exclusion, UnsetSlot, new byte[] { 0x01 }));
        }

        [Fact]
        public void Given_ASetSlot_When_ItsInclusionProofIsVerified_Then_OnlyTheStoredValueIsAccepted()
        {
            var (trie, root) = BuildStorage();
            var inclusion = SlotProof(trie, SetSlot);

            Assert.True(ProofVerification.Current.Storage.Verify(root, inclusion, SetSlot, SetSlotValue));
            Assert.False(ProofVerification.Current.Storage.Verify(root, inclusion, SetSlot, new byte[] { 0x00 }));
            Assert.False(ProofVerification.Current.Storage.Verify(root, inclusion, SetSlot, new byte[] { 0x6A }));
        }

        [Fact]
        public void Given_ANullRoot_When_StorageVerifyIsCalledWithAProverBuiltTrie_Then_ItIsRejectedBecauseTheProverCannotSupplyTheRoot()
        {
            var forged = new PatriciaTrie(new InMemoryContentNodeStore());
            forged.Put(AccountStorage.EncodeKeyForStorage(SetSlot, HashProvider), AccountStorage.EncodeValueForStorage(new byte[] { 0x55 }));
            forged.SaveNodesToStorage();
            var forgedProof = ProofGenerator.GeneratePathProof(forged, AccountStorage.EncodeKeyForStorage(SetSlot, HashProvider));

            Assert.False(ProofVerification.Current.Storage.Verify(null, forgedProof, SetSlot, new byte[] { 0x55 }));
        }

        [Fact]
        public void Given_TheEmptyStorageRoot_When_ZeroIsClaimedWithTheEmptyProofGethServes_Then_StorageVerifyAcceptsIt()
        {
            Assert.True(ProofVerification.Current.Storage.Verify(DefaultValues.EMPTY_TRIE_HASH, new List<byte[]>(), UnsetSlot, new byte[] { 0x00 }));
            Assert.False(ProofVerification.Current.Storage.Verify(DefaultValues.EMPTY_TRIE_HASH, new List<byte[]>(), UnsetSlot, new byte[] { 0x01 }));
        }

        [Fact]
        public void Given_AGethAbsenceProof_When_TheEmptyAccountIsClaimed_Then_AccountVerifyAcceptsIt_AndRejectsAnyOtherAccount()
        {
            var (trie, root, _) = BuildState();
            var exclusion = ProofGenerator.GeneratePathProof(trie, HashProvider.ComputeHash(AbsentAddress.HexToByteArray()));

            Assert.True(ProofVerification.Current.Account.Verify(root, exclusion, AbsentAddress, EmptyAccount()));

            var withBalance = EmptyAccount();
            withBalance.Balance = 1;
            var withNonce = EmptyAccount();
            withNonce.Nonce = 1;
            var withCode = EmptyAccount();
            withCode.CodeHash = HashProvider.ComputeHash(new byte[] { 0x60 });
            Assert.False(ProofVerification.Current.Account.Verify(root, exclusion, AbsentAddress, withBalance));
            Assert.False(ProofVerification.Current.Account.Verify(root, exclusion, AbsentAddress, withNonce));
            Assert.False(ProofVerification.Current.Account.Verify(root, exclusion, AbsentAddress, withCode));
        }

        [Fact]
        public void Given_APresentAccount_When_AbsenceIsClaimed_Then_AccountVerifyRejectsIt_AndAcceptsOnlyTheStoredAccount()
        {
            var (trie, root, present) = BuildState();
            var inclusion = ProofGenerator.GeneratePathProof(trie, HashProvider.ComputeHash(PresentAddress.HexToByteArray()));

            Assert.False(ProofVerification.Current.Account.Verify(root, inclusion, PresentAddress, EmptyAccount()));
            Assert.False(ProofVerification.Current.Account.Verify(root, new List<byte[]>(), PresentAddress, EmptyAccount()));
            Assert.True(ProofVerification.Current.Account.Verify(root, inclusion, PresentAddress, present));
        }

        [Fact]
        public void Given_AnAbsentAccount_When_TheEmptyAccountIsClaimedWithAnEmptyOrTruncatedProof_Then_AccountVerifyRejectsIt()
        {
            var (trie, root, _) = BuildState();
            var absentKey = HashProvider.ComputeHash(AbsentAddress.HexToByteArray());
            var exclusion = ProofGenerator.GeneratePathProof(trie, absentKey);

            Assert.True(exclusion.Count > 1);
            Assert.False(ProofVerification.Current.Account.Verify(root, new List<byte[]>(), AbsentAddress, EmptyAccount()));
            Assert.False(ProofVerification.Current.Account.Verify(root, exclusion.Take(exclusion.Count - 1), AbsentAddress, EmptyAccount()));
        }

        private static Account GethAbsentAccount() => new Account
        {
            Nonce = 0,
            Balance = 0,
            CodeHash = new byte[32],
            StateRoot = new byte[32]
        };

        [Fact]
        public void Given_AGethAbsenceProof_When_GethsZeroHashAccountIsClaimed_Then_AccountVerifyAcceptsIt_AndRejectsANonZeroBalance()
        {
            var (trie, root, _) = BuildState();
            var exclusion = ProofGenerator.GeneratePathProof(trie, HashProvider.ComputeHash(AbsentAddress.HexToByteArray()));
            var withBalance = GethAbsentAccount();
            withBalance.Balance = 1;

            Assert.True(ProofVerification.Current.Account.Verify(root, exclusion, AbsentAddress, GethAbsentAccount()));
            Assert.False(ProofVerification.Current.Account.Verify(root, exclusion, AbsentAddress, withBalance));
        }

        [Fact]
        public void Given_APresentAccount_When_GethsZeroHashAbsentShapeIsClaimed_Then_AccountVerifyRejectsIt()
        {
            var (trie, root, _) = BuildState();
            var inclusion = ProofGenerator.GeneratePathProof(trie, HashProvider.ComputeHash(PresentAddress.HexToByteArray()));

            Assert.False(ProofVerification.Current.Account.Verify(root, inclusion, PresentAddress, GethAbsentAccount()));
        }

        [Fact]
        public void Given_AnAddressWithLeadingZeroBytes_When_ItsStrippedFormIsVerifiedWithAnHonestAbsenceProof_Then_AccountVerifyRejectsTheShortAddress()
        {
            const string stripped = "0x219ab540356cbb839cbe05303d7705fa";
            var (trie, root, _) = BuildState();
            var aliasProof = ProofGenerator.GeneratePathProof(trie, HashProvider.ComputeHash(stripped.HexToByteArray()));

            Assert.True(Nethereum.Merkle.Patricia.ProofVerification.PatriciaProofVerifier.TryVerify(
                root, HashProvider.ComputeHash(stripped.HexToByteArray()), aliasProof, out var aliasValue));
            Assert.Null(aliasValue);
            Assert.False(ProofVerification.Current.Account.Verify(root, aliasProof, stripped, EmptyAccount()));
            Assert.False(ProofVerification.Current.Account.Verify(root, aliasProof, "0x00" + PresentAddress.Substring(2), EmptyAccount()));
        }

        [Fact]
        public void Given_ASlotKeyLongerThan32Bytes_When_ZeroIsClaimedWithAnHonestAbsenceProofOfThatKey_Then_StorageVerifyRejectsIt()
        {
            var (trie, root) = BuildStorage();
            var aliasSlot = new byte[33];
            aliasSlot[32] = SetSlot[0];
            var aliasProof = ProofGenerator.GeneratePathProof(trie, HashProvider.ComputeHash(aliasSlot));

            Assert.True(Nethereum.Merkle.Patricia.ProofVerification.PatriciaProofVerifier.TryVerify(
                root, HashProvider.ComputeHash(aliasSlot), aliasProof, out var aliasValue));
            Assert.Null(aliasValue);
            Assert.False(ProofVerification.Current.Storage.Verify(root, aliasProof, aliasSlot, new byte[] { 0x00 }));
            Assert.True(ProofVerification.Current.Storage.Verify(root, SlotProof(trie, SetSlot), SetSlot.PadTo32Bytes(), SetSlotValue));
        }

        [Fact]
        public void Given_TheEmptyStateRoot_When_TheEmptyAccountIsClaimedWithAnEmptyProof_Then_AccountVerifyAcceptsIt()
        {
            Assert.True(ProofVerification.Current.Account.Verify(DefaultValues.EMPTY_TRIE_HASH, new List<byte[]>(), AbsentAddress, EmptyAccount()));
        }
    }
}
