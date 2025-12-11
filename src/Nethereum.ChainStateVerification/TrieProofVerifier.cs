using System;
using System.Linq;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.RPC.Eth.ChainValidation;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Mappers;

namespace Nethereum.ChainStateVerification
{
    public class TrieProofVerifier : ITrieProofVerifier
    {
        public Account VerifyAccountProof(byte[] stateRoot, AccountProof accountProof)
        {
            if (stateRoot == null) throw new ArgumentNullException(nameof(stateRoot));
            if (accountProof == null) throw new ArgumentNullException(nameof(accountProof));

            var account = accountProof.ToAccount();
            var proofNodes = accountProof.AccountProofs.Select(p => p.HexToByteArray());
            var valid = AccountProofVerification.VerifyAccountProofs(accountProof.Address, stateRoot, proofNodes, account);
            if (!valid)
            {
                throw new InvalidChainDataException("Account proof did not match the provided state root.");
            }

            return account;
        }

        public byte[] VerifyStorageProof(Account account, StorageProof storageProof)
        {
            if (account == null) throw new ArgumentNullException(nameof(account));
            if (storageProof == null) throw new ArgumentNullException(nameof(storageProof));

            var key = storageProof.Key.HexValue.HexToByteArray();
            var value = storageProof.Value.HexValue.HexToByteArray();
            var proofNodes = storageProof.Proof.Select(p => p.HexToByteArray());

            var valid = StorageProofVerification.ValidateValueFromStorageProof(key, value, proofNodes, account.StateRoot);
            if (!valid)
            {
                throw new InvalidChainDataException("Storage proof did not match the account's storage root.");
            }

            return value;
        }
    }
}
