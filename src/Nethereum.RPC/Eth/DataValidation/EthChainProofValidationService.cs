using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.TransactionManagers;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using System.Linq;
using Nethereum.Hex.HexTypes;
using System.Numerics;
using Nethereum.RPC.Eth.Mappers;

namespace Nethereum.RPC.Eth.ChainValidation
{
    public class EthChainProofValidationService : RpcClientWrapper, IEthChainProofValidationService
    {
        public EthChainProofValidationService(IClient client) : this(client, new EthApiService(client, new TransactionManager(client)))
        {

        }

        public EthChainProofValidationService(IClient client, IEthApiService ethApiService) : base(client)
        {
            Client = client;
            EthApiService = ethApiService;
        }

        public IEthApiService EthApiService { get; }
#if !DOTNET35

        public async Task<HexBigInteger> GetAndValidateBalance(string accountAddress, byte[] stateRoot = null, string[] storageKeys = null, BlockParameter blockParameter = null)
        {
            var accountProof = await GetAndValidateAccountProof(accountAddress, stateRoot, storageKeys, blockParameter);
            return accountProof.Balance;
        }

        public async Task<HexBigInteger> GetAndValidateNonce(string accountAddress, byte[] stateRoot = null, string[] storageKeys = null, BlockParameter blockParameter = null)
        {
            var accountProof = await GetAndValidateAccountProof(accountAddress, stateRoot, storageKeys, blockParameter);
            return accountProof.Nonce;
        }

        public async Task<AccountProof> GetAndValidateAccountProof(string accountAddress, byte[] stateRoot = null, string[] storageKeys = null, BlockParameter blockParameter = null)
        {
            if (blockParameter == null)
            {
                var blockNumber = await EthApiService.Blocks.GetBlockNumber.SendRequestAsync();
                blockParameter = new BlockParameter(blockNumber);
            }

            if (stateRoot == null) // validating using the same node.. 
            {
                var block = await EthApiService.Blocks.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(blockParameter);
                stateRoot = block.StateRoot.HexToByteArray();

            }

            if (storageKeys == null)
            {
                storageKeys = new string[] { };
            }

            var accountProof = await EthApiService.GetProof.SendRequestAsync(accountAddress, storageKeys, blockParameter);
            var account = accountProof.ToAccount();

            var valid = AccountProofVerification.VerifyAccountProofs(accountAddress, stateRoot, accountProof.AccountProofs.Select(x => x.HexToByteArray()), account);
            if (valid) return accountProof;
            throw new InvalidChainDataException();
        }

        public async Task<byte[]> GetAndValidateValueFromStorage(string accountAddress, string storageKey, byte[] stateRoot = null, BlockParameter blockParameter = null)
        {
            var accountProof = await GetAndValidateAccountProof(accountAddress, stateRoot, new string[] { storageKey }, blockParameter);
            var valid = ValidateValueFromStorageProof(accountProof.StorageProof[0], accountProof.StorageHash.HexToByteArray());
            if (valid)
            {
                return accountProof.StorageProof[0].Value;
            }
            throw new InvalidChainDataException();
        }

        public bool ValidateValueFromStorageProof(StorageProof storageProof, byte[] stateRoot)
        {
            return StorageProofVerification.ValidateValueFromStorageProof(storageProof.Key, storageProof.Value, storageProof.Proof.Select(x => x.HexToByteArray()), stateRoot);
        }

        public async Task<Transaction[]> GetAndValidateTransactions(BlockParameter blockNumber, string transactionsRoot = null, BigInteger? chainId = null)
        {
            var block = await EthApiService.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(blockNumber);

            if (chainId == null)
            {
                chainId = await EthApiService.ChainId.SendRequestAsync();
            }

            if (transactionsRoot == null)
            {
                transactionsRoot = block.TransactionsRoot;
            }

            var transactions = block.Transactions.ToSignedTransactions(chainId);
            bool valid = TransactionProofVerification.ValidateTransactions(transactionsRoot, transactions);
            if (valid)
            {
                return block.Transactions;
            }

            throw new InvalidChainDataException();
        }

#endif
    }
}
