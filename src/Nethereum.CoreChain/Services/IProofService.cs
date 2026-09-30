using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.CoreChain.Services
{
    public interface IProofService
    {
        Task<AccountProof> GenerateAccountProofAsync(
            string address,
            List<BigInteger> storageKeys,
            byte[] stateRoot);
    }
}
