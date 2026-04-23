using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Merkle.Binary;
using Nethereum.Merkle.Binary.Keys;
using Nethereum.Merkle.Binary.Proofs;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Nethereum.Util.HashProviders;

namespace Nethereum.CoreChain.Services
{
    public class BinaryProofService : IProofService
    {
        private readonly BinaryTrie _trie;
        private readonly IHashProvider _hashProvider;
        private readonly BinaryTreeKeyDerivation _keyDerivation;
        private readonly BinaryTrieProver _prover;
        private readonly BinaryTrieProofVerifier _verifier;

        public BinaryProofService(BinaryTrie trie, IHashProvider hashProvider)
        {
            _trie = trie ?? throw new ArgumentNullException(nameof(trie));
            _hashProvider = hashProvider ?? throw new ArgumentNullException(nameof(hashProvider));
            _keyDerivation = new BinaryTreeKeyDerivation(hashProvider);
            _prover = new BinaryTrieProver(trie);
            _verifier = new BinaryTrieProofVerifier(hashProvider);
        }

        public BinaryAccountProofResult ProveAccount(string address)
            => ProveAccountInternal(address, _trie.ComputeRoot());

        public BinaryStorageProofResult ProveStorageSlot(string address, EvmUInt256 slot)
            => ProveStorageSlotInternal(address, slot, _trie.ComputeRoot());

        private BinaryAccountProofResult ProveAccountInternal(string address, byte[] rootHash)
        {
            var addressBytes = AddressUtil.Current
                .ConvertToValid20ByteAddress(address).HexToByteArray();

            var basicKey = _keyDerivation.GetTreeKeyForBasicData(addressBytes);
            var basicProof = _prover.BuildProof(basicKey);
            var basicValue = _verifier.VerifyProof(rootHash, basicKey, basicProof);

            var codeHashKey = _keyDerivation.GetTreeKeyForCodeHash(addressBytes);
            var codeHashProof = _prover.BuildProof(codeHashKey);
            var codeHashValue = _verifier.VerifyProof(rootHash, codeHashKey, codeHashProof);

            byte version = 0;
            uint codeSize = 0;
            ulong nonce = 0;
            var balance = EvmUInt256.Zero;
            if (basicValue != null && !BinaryTrieConstants.IsZeroHash(basicValue))
                BasicDataLeaf.Unpack(basicValue, out version, out codeSize, out nonce, out balance);

            return new BinaryAccountProofResult
            {
                Address = address,
                Version = version,
                Nonce = nonce,
                Balance = balance,
                CodeSize = codeSize,
                CodeHash = codeHashValue,
                RootHash = rootHash,
                BasicDataProof = basicProof,
                CodeHashProof = codeHashProof
            };
        }

        private BinaryStorageProofResult ProveStorageSlotInternal(string address, EvmUInt256 slot, byte[] rootHash)
        {
            var addressBytes = AddressUtil.Current
                .ConvertToValid20ByteAddress(address).HexToByteArray();

            var storageKey = _keyDerivation.GetTreeKeyForStorageSlot(addressBytes, slot);
            var proof = _prover.BuildProof(storageKey);
            var value = _verifier.VerifyProof(rootHash, storageKey, proof);

            return new BinaryStorageProofResult
            {
                Address = address,
                Slot = slot,
                Value = value,
                RootHash = rootHash,
                Proof = proof
            };
        }

        public Task<AccountProof> GenerateAccountProofAsync(
            string address, List<BigInteger> storageKeys, byte[] stateRoot)
        {
            var rootHash = _trie.ComputeRoot();
            var acct = ProveAccountInternal(address, rootHash);

            var storageProofs = new List<RPC.Eth.DTOs.StorageProof>();
            if (storageKeys != null)
            {
                foreach (var slot in storageKeys)
                {
                    var sp = ProveStorageSlotInternal(address, EvmUInt256BigIntegerExtensions.FromBigInteger(slot), rootHash);
                    var valueBigInt = sp.Value != null
                        ? new BigInteger(sp.Value, isUnsigned: true, isBigEndian: true)
                        : BigInteger.Zero;

                    storageProofs.Add(new RPC.Eth.DTOs.StorageProof
                    {
                        Key = new HexBigInteger(slot),
                        Value = new HexBigInteger(valueBigInt),
                        Proof = sp.Proof.Nodes.Select(n => n.ToHex(true)).ToList()
                    });
                }
            }

            // In EIP-7864 binary trie there's no per-account storageHash
            // (storage lives in the global trie, not a sub-trie). We return
            // the global state root — consumers should be aware this differs
            // from Patricia's per-account storage root.
            var result = new AccountProof
            {
                Address = address,
                Balance = new HexBigInteger((BigInteger)acct.Balance),
                Nonce = new HexBigInteger(acct.Nonce),
                CodeHash = acct.CodeHash?.ToHex(true) ?? DefaultValues.EMPTY_DATA_HASH.ToHex(true),
                StorageHash = acct.RootHash?.ToHex(true),
                AccountProofs = acct.BasicDataProof.Nodes
                    .Select(n => n.ToHex(true)).ToList(),
                StorageProof = storageProofs
            };

            return Task.FromResult(result);
        }
    }
}
