using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.ChainStateVerification
{
    public interface ITrieProofVerifier
    {
        Account VerifyAccountProof(byte[] stateRoot, AccountProof accountProof);
        byte[] VerifyStorageProof(Account account, StorageProof storageProof);
    }
}
