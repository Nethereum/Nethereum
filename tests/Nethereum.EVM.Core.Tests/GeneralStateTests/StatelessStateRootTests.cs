using System.Collections.Generic;
using Nethereum.CoreChain;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Core.Tests;
using Nethereum.EVM.Witness;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.EVM.Core.Tests.GeneralStateTests
{
    public class StatelessStateRootTests
    {
        private readonly ITestOutputHelper _output;
        private static readonly Sha3KeccackHashProvider HashProvider = new Sha3KeccackHashProvider();

        public StatelessStateRootTests(ITestOutputHelper output) { _output = output; }

        [Fact]
        public void StatelessRoot_MatchesFullRoot_SimpleTransfer()
        {
            // Setup: 2 accounts in pre-state
            var sender = "0xa94f5374fce5edbc8e2a8697c15331677e6ebf0b";
            var receiver = "0x1000000000000000000000000000000000000001";

            var preAccounts = new List<WitnessAccount>
            {
                new WitnessAccount { Address = sender, Balance = new EvmUInt256(10000000), Nonce = 0, Code = new byte[0], Storage = new List<WitnessStorageSlot>() },
                new WitnessAccount { Address = receiver, Balance = EvmUInt256.Zero, Nonce = 0, Code = new byte[0], Storage = new List<WitnessStorageSlot>() }
            };

            // Step 1: Build full trie to get pre-state root
            var fullTrie = new PatriciaTrie();
            var trieStorage = new InMemoryTrieStorage();
            var emptyCodeHash = Sha3Keccack.Current.CalculateHash(new byte[0]);
            var emptyTrieRoot = "56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".HexToByteArray();

            foreach (var acc in preAccounts)
            {
                var nonceBytes = acc.Nonce == 0 ? new byte[0] : new EvmUInt256(acc.Nonce).ToBytesForRLPEncoding();
                var balanceBytes = acc.Balance.IsZero ? new byte[0] : TrimLeadingZeros(acc.Balance.ToBigEndian());

                var accountRlp = RLP.RLP.EncodeList(
                    RLP.RLP.EncodeElement(nonceBytes),
                    RLP.RLP.EncodeElement(balanceBytes),
                    RLP.RLP.EncodeElement(emptyTrieRoot),
                    RLP.RLP.EncodeElement(emptyCodeHash));

                var addressKey = Sha3Keccack.Current.CalculateHash(acc.Address.HexToByteArray());
                fullTrie.Put(addressKey, accountRlp, trieStorage);
            }

            var preStateRoot = fullTrie.Root.GetHash();
            _output.WriteLine($"Pre-state root: 0x{preStateRoot.ToHex()}");

            // Step 2: Generate proofs for both accounts
            foreach (var acc in preAccounts)
            {
                var addressKey = Sha3Keccack.Current.CalculateHash(acc.Address.HexToByteArray());
                var proofStorage = fullTrie.GenerateProof(addressKey, trieStorage);

                var proofNodes = new List<byte[]>();
                foreach (var kvp in proofStorage.Storage)
                    if (kvp.Value != null) proofNodes.Add(kvp.Value);

                acc.AccountProof = proofNodes.ToArray();
                acc.StorageRoot = emptyTrieRoot;
            }

            // Step 3: Execute a transfer (simulate post-state)
            var accounts = WitnessStateBuilder.BuildAccountState(preAccounts);
            var stateReader = new InMemoryStateReader(accounts);
            var executionState = new ExecutionStateService(stateReader);
            WitnessStateBuilder.LoadAllAccountsAndStorage(executionState, preAccounts);

            // Simulate: sender sends 1000 to receiver, nonce increments
            var senderState = executionState.CreateOrGetAccountExecutionState(sender);
            senderState.Balance.DebitExecutionBalance(new EvmUInt256(1000));
            senderState.Nonce = 1;
            var receiverState = executionState.CreateOrGetAccountExecutionState(receiver);
            receiverState.Balance.CreditExecutionBalance(new EvmUInt256(1000));

            // Step 4: Compute root with FULL calculator
            var encoding = Nethereum.Model.RlpBlockEncodingProvider.Instance;
            var fullCalc = new PatriciaStateRootCalculator(encoding);
            var fullRoot = fullCalc.ComputeStateRoot(executionState);
            _output.WriteLine($"Full root:      0x{fullRoot.ToHex()}");

            // Step 5: Compute root with STATELESS calculator
            var statelessCalc = new StatelessStateRootCalculator(preStateRoot, preAccounts, encoding);
            var statelessRoot = statelessCalc.ComputeStateRoot(executionState);
            _output.WriteLine($"Stateless root: 0x{statelessRoot.ToHex()}");

            // Step 6: They MUST match
            Assert.True(fullRoot.AreTheSame(statelessRoot),
                $"Roots differ: full=0x{fullRoot.ToHex()} stateless=0x{statelessRoot.ToHex()}");
        }

        [Fact]
        public void StatelessRoot_MatchesFullRoot_WithStorageChanges()
        {
            var contract = "0xcccccccccccccccccccccccccccccccccccccccc";
            var contractCode = new byte[] { 0x60, 0x00 }; // PUSH1 0x00
            var slot0Value = EvmUInt256.FromBigEndian(new byte[] { 0x42 }.PadTo32Bytes());

            var preAccounts = new List<WitnessAccount>
            {
                new WitnessAccount
                {
                    Address = contract, Balance = EvmUInt256.Zero, Nonce = 0,
                    Code = contractCode,
                    Storage = new List<WitnessStorageSlot>
                    {
                        new WitnessStorageSlot { Key = EvmUInt256.Zero, Value = slot0Value }
                    }
                }
            };

            // Build full trie with storage
            var emptyCodeHash = Sha3Keccack.Current.CalculateHash(new byte[0]);
            var emptyTrieRoot = "56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".HexToByteArray();
            var codeHash = Sha3Keccack.Current.CalculateHash(contractCode);

            // Build storage trie
            var storageTrie = new PatriciaTrie();
            var storageTrieStorage = new InMemoryTrieStorage();
            var slotKey = Sha3Keccack.Current.CalculateHash(EvmUInt256.Zero.ToBigEndian());
            storageTrie.Put(slotKey, RLP.RLP.EncodeElement(TrimLeadingZeros(slot0Value.ToBigEndian())), storageTrieStorage);
            var storageRoot = storageTrie.Root.GetHash();

            // Generate storage proof
            var storageProofStorage = storageTrie.GenerateProof(slotKey, storageTrieStorage);
            var storageProofNodes = new List<byte[]>();
            foreach (var kvp in storageProofStorage.Storage)
                if (kvp.Value != null) storageProofNodes.Add(kvp.Value);

            preAccounts[0].StorageRoot = storageRoot;
            preAccounts[0].StorageProofs = new List<WitnessStorageProof>
            {
                new WitnessStorageProof { Key = EvmUInt256.Zero, Value = slot0Value, Proof = storageProofNodes.ToArray() }
            };

            // Build account trie
            var accountTrie = new PatriciaTrie();
            var accountTrieStorage = new InMemoryTrieStorage();
            var accountRlp = RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(new byte[0]),
                RLP.RLP.EncodeElement(new byte[0]),
                RLP.RLP.EncodeElement(storageRoot),
                RLP.RLP.EncodeElement(codeHash));
            var addressKey = Sha3Keccack.Current.CalculateHash(contract.HexToByteArray());
            accountTrie.Put(addressKey, accountRlp, accountTrieStorage);
            var preStateRoot = accountTrie.Root.GetHash();

            // Account proof
            var accountProofStorage = accountTrie.GenerateProof(addressKey, accountTrieStorage);
            var accountProofNodes = new List<byte[]>();
            foreach (var kvp in accountProofStorage.Storage)
                if (kvp.Value != null) accountProofNodes.Add(kvp.Value);
            preAccounts[0].AccountProof = accountProofNodes.ToArray();

            _output.WriteLine($"Pre-state root: 0x{preStateRoot.ToHex()}");

            // Execute: change storage slot 0 from 0x42 to 0xFF
            var accounts = WitnessStateBuilder.BuildAccountState(preAccounts);
            var stateReader = new InMemoryStateReader(accounts);
            var executionState = new ExecutionStateService(stateReader);
            WitnessStateBuilder.LoadAllAccountsAndStorage(executionState, preAccounts);

            var contractState = executionState.CreateOrGetAccountExecutionState(contract);
            contractState.Storage[EvmUInt256.Zero] = new EvmUInt256(0xFF).ToBigEndian();

            // Full root
            var encoding2 = Nethereum.Model.RlpBlockEncodingProvider.Instance;
            var fullRoot = new PatriciaStateRootCalculator(encoding2).ComputeStateRoot(executionState);
            _output.WriteLine($"Full root:      0x{fullRoot.ToHex()}");

            // Stateless root
            var statelessRoot = new StatelessStateRootCalculator(preStateRoot, preAccounts, encoding2).ComputeStateRoot(executionState);
            _output.WriteLine($"Stateless root: 0x{statelessRoot.ToHex()}");

            Assert.True(fullRoot.AreTheSame(statelessRoot),
                $"Roots differ: full=0x{fullRoot.ToHex()} stateless=0x{statelessRoot.ToHex()}");
        }

        [Fact]
        public void ProofVerification_ValidProofs_Pass()
        {
            var sender = "0xa94f5374fce5edbc8e2a8697c15331677e6ebf0b";
            var preAccounts = new List<WitnessAccount>
            {
                new WitnessAccount { Address = sender, Balance = new EvmUInt256(10000000), Nonce = 0, Code = new byte[0], Storage = new List<WitnessStorageSlot>() }
            };

            var preStateRoot = BuildTrieAndGenerateProofs(preAccounts);
            var result = WitnessProofVerifier.VerifyAll(preStateRoot, preAccounts);

            Assert.True(result.IsValid, result.Error);
        }

        [Fact]
        public void ProofVerification_TamperedBalance_Fails()
        {
            var sender = "0xa94f5374fce5edbc8e2a8697c15331677e6ebf0b";
            var preAccounts = new List<WitnessAccount>
            {
                new WitnessAccount { Address = sender, Balance = new EvmUInt256(10000000), Nonce = 0, Code = new byte[0], Storage = new List<WitnessStorageSlot>() }
            };

            var preStateRoot = BuildTrieAndGenerateProofs(preAccounts);

            // Tamper: change the balance after proofs were generated
            preAccounts[0].Balance = new EvmUInt256(99999999);

            var result = WitnessProofVerifier.VerifyAll(preStateRoot, preAccounts);
            Assert.False(result.IsValid);
            _output.WriteLine($"Expected failure: {result.Error}");
        }

        private byte[] BuildTrieAndGenerateProofs(List<WitnessAccount> accounts)
        {
            var emptyTrieRoot = "56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".HexToByteArray();
            var emptyCodeHash = Sha3Keccack.Current.CalculateHash(new byte[0]);

            var trie = new PatriciaTrie();
            var storage = new InMemoryTrieStorage();

            foreach (var acc in accounts)
            {
                var nonceBytes = acc.Nonce == 0 ? new byte[0] : TrimLeadingZeros(new EvmUInt256(acc.Nonce).ToBigEndian());
                var balanceBytes = acc.Balance.IsZero ? new byte[0] : TrimLeadingZeros(acc.Balance.ToBigEndian());
                var codeHash = acc.Code != null && acc.Code.Length > 0 ? Sha3Keccack.Current.CalculateHash(acc.Code) : emptyCodeHash;
                var storageRoot = acc.StorageRoot ?? emptyTrieRoot;

                var rlp = RLP.RLP.EncodeList(
                    RLP.RLP.EncodeElement(nonceBytes),
                    RLP.RLP.EncodeElement(balanceBytes),
                    RLP.RLP.EncodeElement(storageRoot),
                    RLP.RLP.EncodeElement(codeHash));

                var key = Sha3Keccack.Current.CalculateHash(acc.Address.HexToByteArray());
                trie.Put(key, rlp, storage);
            }

            var root = trie.Root.GetHash();

            // Generate proofs for each account
            foreach (var acc in accounts)
            {
                var key = Sha3Keccack.Current.CalculateHash(acc.Address.HexToByteArray());
                var proofStorage = trie.GenerateProof(key, storage);
                var nodes = new List<byte[]>();
                foreach (var kvp in proofStorage.Storage)
                    if (kvp.Value != null) nodes.Add(kvp.Value);
                acc.AccountProof = nodes.ToArray();
                acc.StorageRoot = emptyTrieRoot;
            }

            return root;
        }

        private static byte[] TrimLeadingZeros(byte[] bytes)
        {
            int start = 0;
            while (start < bytes.Length && bytes[start] == 0) start++;
            if (start == bytes.Length) return new byte[0];
            if (start == 0) return bytes;
            var result = new byte[bytes.Length - start];
            System.Array.Copy(bytes, start, result, 0, result.Length);
            return result;
        }
    }
}
