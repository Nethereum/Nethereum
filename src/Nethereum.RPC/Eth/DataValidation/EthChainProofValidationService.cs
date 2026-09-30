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
using Nethereum.Merkle.Patricia.ProofVerification;
using Nethereum.Model;
using Nethereum.Util;

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

            var valid = ProofVerification.Current.Account.Verify(stateRoot, accountProof.AccountProofs.Select(x => x.HexToByteArray()), accountAddress, account);
            if (valid) return accountProof;
            throw new InvalidChainDataException();
        }

        public async Task<byte[]> GetAndValidateValueFromStorage(string accountAddress, string storageKey, byte[] stateRoot = null, BlockParameter blockParameter = null)
        {
            var accountProof = await GetAndValidateAccountProof(accountAddress, stateRoot, new string[] { storageKey }, blockParameter);
            var requestedKey = storageKey.HexToByteArray().PadTo32Bytes();
            var storageProof = accountProof.StorageProof?.FirstOrDefault(p => p?.Key?.HexValue != null
                && p.Key.HexValue.HexToByteArray().PadTo32Bytes().SequenceEqual(requestedKey));
            if (storageProof == null || requestedKey.Length != 32) throw new InvalidChainDataException();

            var storageHash = accountProof.StorageHash.HexToByteArray();
            if (storageHash.All(b => b == 0)) storageHash = DefaultValues.EMPTY_TRIE_HASH;

            if (ValidateValueFromStorageProof(storageProof, storageHash))
            {
                return storageProof.Value;
            }
            throw new InvalidChainDataException();
        }

        public bool ValidateValueFromStorageProof(StorageProof storageProof, byte[] stateRoot)
        {
            return ProofVerification.Current.Storage.Verify(stateRoot, storageProof.Proof.Select(x => x.HexToByteArray()).ToList(), storageProof.Key.HexValue.HexToByteArray(), storageProof.Value.HexValue.HexToByteArray());
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
            bool valid = ProofVerification.Current.Transaction.Verify(transactionsRoot, transactions);
            if (valid)
            {
                return block.Transactions;
            }

            throw new InvalidChainDataException();
        }

#endif
    }
}
