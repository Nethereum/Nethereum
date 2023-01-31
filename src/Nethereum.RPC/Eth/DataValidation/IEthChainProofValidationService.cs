using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.RPC.Eth.ChainValidation
{
    public interface IEthChainProofValidationService
    {
#if !DOTNET35
        Task<AccountProof> GetAndValidateAccountProof(string accountAddress, byte[] stateRoot = null, string[] storageKeys = null, BlockParameter blockParameter = null);
        Task<HexBigInteger> GetAndValidateBalance(string accountAddress, byte[] stateRoot = null, string[] storageKeys = null, BlockParameter blockParameter = null);
        Task<HexBigInteger> GetAndValidateNonce(string accountAddress, byte[] stateRoot = null, string[] storageKeys = null, BlockParameter blockParameter = null);
        Task<Transaction[]> GetAndValidateTransactions(BlockParameter blockNumber, string transactionsRoot = null, BigInteger? chainId = null);
        Task<byte[]> GetAndValidateValueFromStorage(string accountAddress, string storageKey, byte[] stateRoot = null, BlockParameter blockParameter = null);
        bool ValidateValueFromStorageProof(StorageProof storageProof, byte[] stateRoot);
#endif
    }
}