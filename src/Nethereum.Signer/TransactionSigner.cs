using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.Signer
{
    public class TransactionSigner
    {
        public byte[] GetPublicKey(string rlp)
        {
            var transaction = TransactionFactory.CreateTransaction(rlp);
            return transaction.Key.GetPubKey();
        }

        public string GetSenderAddress(string rlp)
        {
            var transaction = TransactionFactory.CreateTransaction(rlp);
            return transaction.Key.GetPublicAddress();
        }

        public bool VerifyTransaction(string rlp)
        {
            var transaction = TransactionFactory.CreateTransaction(rlp);
            return transaction.Key.VerifyAllowingOnlyLowS(transaction.RawHash, transaction.Signature);
        }

        public byte[] GetPublicKey(string rlp, Chain chain)
        {
            return GetPublicKey(rlp, (int)chain);
        }

        public byte[] GetPublicKey(string rlp, BigInteger chainId)
        {
            var transaction = new TransactionChainId(rlp.HexToByteArray(), chainId);
            return transaction.Key.GetPubKey();
        }

        public string GetSenderAddress(string rlp, Chain chain)
        {
            return GetSenderAddress(rlp, (int)chain);
        }

        public string GetSenderAddress(string rlp, BigInteger chainId)
        {
            var transaction = new TransactionChainId(rlp.HexToByteArray(), chainId);
            return transaction.Key.GetPublicAddress();
        }

        public bool VerifyTransaction(string rlp, Chain chain)
        {
            return VerifyTransaction(rlp, (int)chain);
        }

        public bool VerifyTransaction(string rlp, BigInteger chainId)
        {
            var transaction = new TransactionChainId(rlp.HexToByteArray(), chainId);
            return transaction.Key.VerifyAllowingOnlyLowS(transaction.RawHash, transaction.Signature);
        }

        public string SignTransaction(string privateKey, string to, BigInteger amount, BigInteger nonce)
        {
            return SignTransaction(privateKey.HexToByteArray(), to, amount, nonce);
        }

        public string SignTransaction(string privateKey, string to, BigInteger amount, BigInteger nonce, string data)
        {
            return SignTransaction(privateKey.HexToByteArray(), to, amount, nonce, data);
        }

        public string SignTransaction(string privateKey, string to, BigInteger amount, BigInteger nonce,
            BigInteger gasPrice,
            BigInteger gasLimit)
        {
            return SignTransaction(privateKey.HexToByteArray(), to, amount, nonce, gasPrice, gasLimit);
        }

        public string SignTransaction(string privateKey, string to, BigInteger amount, BigInteger nonce,
            BigInteger gasPrice,
            BigInteger gasLimit, string data)
        {
            return SignTransaction(privateKey.HexToByteArray(), to, amount, nonce, gasPrice, gasLimit, data);
        }

        public string SignTransaction(byte[] privateKey, string to, BigInteger amount, BigInteger nonce)
        {
            var transaction = new Transaction(to, amount, nonce);
            return SignTransaction(privateKey, transaction);
        }

        public string SignTransaction(byte[] privateKey, string to, BigInteger amount, BigInteger nonce, string data)
        {
            var transaction = new Transaction(to, amount, nonce, data);
            return SignTransaction(privateKey, transaction);
        }

        public string SignTransaction(byte[] privateKey, string to, BigInteger amount, BigInteger nonce,
            BigInteger gasPrice,
            BigInteger gasLimit)
        {
            var transaction = new Transaction(to, amount, nonce, gasPrice, gasLimit);
            return SignTransaction(privateKey, transaction);
        }

        public string SignTransaction(byte[] privateKey, string to, BigInteger amount, BigInteger nonce,
            BigInteger gasPrice,
            BigInteger gasLimit, string data)
        {
            var transaction = new Transaction(to, amount, nonce, gasPrice, gasLimit, data);
            return SignTransaction(privateKey, transaction);
        }

        public string SignTransaction(string privateKey, Chain chain, string to, BigInteger amount,
            BigInteger nonce)
        {
            return SignTransaction(privateKey, new BigInteger((int)chain), to, amount, nonce);
        }

        public string SignTransaction(string privateKey, BigInteger chainId, string to, BigInteger amount,
            BigInteger nonce)
        {
            return SignTransaction(privateKey.HexToByteArray(), chainId, to, amount, nonce);
        }

        public string SignTransaction(string privateKey, Chain chain, string to, BigInteger amount,
            BigInteger nonce, string data)
        {
            return SignTransaction(privateKey, new BigInteger((int)chain), to, amount, nonce, data);
        }

        public string SignTransaction(string privateKey, BigInteger chainId, string to, BigInteger amount,
            BigInteger nonce, string data)
        {
            return SignTransaction(privateKey.HexToByteArray(), chainId, to, amount, nonce, data);
        }

        public string SignTransaction(string privateKey, Chain chain, string to, BigInteger amount,
            BigInteger nonce, BigInteger gasPrice,
            BigInteger gasLimit)
        {
            return SignTransaction(privateKey, new BigInteger((int)chain), to, amount, nonce, gasPrice, gasLimit);
        }

        public string SignTransaction(string privateKey, BigInteger chainId, string to, BigInteger amount,
            BigInteger nonce, BigInteger gasPrice,
            BigInteger gasLimit)
        {
            return SignTransaction(privateKey.HexToByteArray(), chainId, to, amount, nonce, gasPrice, gasLimit);
        }

        public string SignTransaction(string privateKey, Chain chain, string to, BigInteger amount,
            BigInteger nonce, BigInteger gasPrice,
            BigInteger gasLimit, string data)
        {
            return SignTransaction(privateKey, new BigInteger((int)chain), to, amount, nonce, gasPrice, gasLimit, data);
        }

        public string SignTransaction(string privateKey, BigInteger chainId, string to, BigInteger amount,
            BigInteger nonce, BigInteger gasPrice,
            BigInteger gasLimit, string data)
        {
            return SignTransaction(privateKey.HexToByteArray(), chainId, to, amount, nonce, gasPrice, gasLimit, data);
        }

        public string SignTransaction(byte[] privateKey, Chain chain, string to, BigInteger amount,
            BigInteger nonce)
        {
            return SignTransaction(privateKey, (int)chain, to, amount, nonce);
        }

        public string SignTransaction(byte[] privateKey, BigInteger chainId, string to, BigInteger amount,
            BigInteger nonce)
        {
            var transaction = new TransactionChainId(to, amount, nonce, chainId);
            return SignTransaction(privateKey, transaction);
        }

        public string SignTransaction(byte[] privateKey, Chain chain, string to, BigInteger amount,
            BigInteger nonce, string data)
        {
            return SignTransaction(privateKey, (int)chain, to, amount, nonce, data);
        }

        public string SignTransaction(byte[] privateKey, BigInteger chainId, string to, BigInteger amount,
            BigInteger nonce, string data)
        {
            var transaction = new TransactionChainId(to, amount, nonce, data, chainId);
            return SignTransaction(privateKey, transaction);
        }

        public string SignTransaction(byte[] privateKey, Chain chain, string to, BigInteger amount,
            BigInteger nonce, BigInteger gasPrice,
            BigInteger gasLimit)
        {
            return SignTransaction(privateKey, (int)chain, to, amount, nonce, gasPrice, gasLimit);
        }

        public string SignTransaction(byte[] privateKey, BigInteger chainId, string to, BigInteger amount,
            BigInteger nonce, BigInteger gasPrice,
            BigInteger gasLimit)
        {
            var transaction = new TransactionChainId(to, amount, nonce, gasPrice, gasLimit, chainId);
            return SignTransaction(privateKey, transaction);
        }

        public string SignTransaction(byte[] privateKey, Chain chain, string to, BigInteger amount,
            BigInteger nonce, BigInteger gasPrice,
            BigInteger gasLimit, string data)
        {
            return SignTransaction(privateKey, (int)chain, to, amount, nonce, gasPrice, gasLimit, data);
        }

        public string SignTransaction(byte[] privateKey, BigInteger chainId, string to, BigInteger amount,
            BigInteger nonce, BigInteger gasPrice,
            BigInteger gasLimit, string data)
        {
            var transaction = new TransactionChainId(to, amount, nonce, gasPrice, gasLimit, data, chainId);
            return SignTransaction(privateKey, transaction);
        }

        private string SignTransaction(byte[] privateKey, Transaction transaction)
        {
            transaction.Sign(new EthECKey(privateKey, true));
            return transaction.GetRLPEncoded().ToHex();
        }

        private string SignTransaction(byte[] privateKey, TransactionChainId transaction)
        {
            transaction.Sign(new EthECKey(privateKey, true));
            return transaction.GetRLPEncoded().ToHex();
        }

#if !DOTNET35
        private async Task<string> SignTransactionAsync(IEthECKeyExternalSigner externalSigner, Transaction transaction)
        {
            await transaction.SignExternallyAsync(externalSigner).ConfigureAwait(false);
            return transaction.GetRLPEncoded().ToHex();
        }

        private async Task<string> SignTransactionAsync(IEthECKeyExternalSigner externalSigner, TransactionChainId transaction)
        {
            await transaction.SignExternallyAsync(externalSigner).ConfigureAwait(false);
            return transaction.GetRLPEncoded().ToHex();
        }


        public Task<string> SignTransactionAsync(IEthECKeyExternalSigner externalSigner, string to, BigInteger amount, BigInteger nonce)
        {
            var transaction = new Transaction(to, amount, nonce);
            return SignTransactionAsync(externalSigner, transaction);
        }

        public Task<string> SignTransactionAsync(IEthECKeyExternalSigner externalSigner, string to, BigInteger amount, BigInteger nonce, string data)
        {
            var transaction = new Transaction(to, amount, nonce, data);
            return SignTransactionAsync(externalSigner, transaction);
        }

        public Task<string> SignTransactionAsync(IEthECKeyExternalSigner externalSigner, string to, BigInteger amount, BigInteger nonce,
            BigInteger gasPrice,
            BigInteger gasLimit)
        {
            var transaction = new Transaction(to, amount, nonce, gasPrice, gasLimit);
            return SignTransactionAsync(externalSigner, transaction);
        }

        public Task<string> SignTransactionAsync(IEthECKeyExternalSigner externalSigner, string to, BigInteger amount, BigInteger nonce,
            BigInteger gasPrice,
            BigInteger gasLimit, string data)
        {
            var transaction = new Transaction(to, amount, nonce, gasPrice, gasLimit, data);
            return SignTransactionAsync(externalSigner, transaction);
        }

       public Task<string> SignTransactionAsync(IEthECKeyExternalSigner externalSigner, Chain chain, string to, BigInteger amount,
            BigInteger nonce)
        {
            return SignTransactionAsync(externalSigner, (int)chain, to, amount, nonce);
        }

        public Task<string> SignTransactionAsync(IEthECKeyExternalSigner externalSigner, BigInteger chainId, string to, BigInteger amount,
            BigInteger nonce)
        {
            var transaction = new TransactionChainId(to, amount, nonce, chainId);
            return SignTransactionAsync(externalSigner, transaction);
        }

        public Task<string> SignTransactionAsync(IEthECKeyExternalSigner externalSigner, Chain chain, string to, BigInteger amount,
            BigInteger nonce, string data)
        {
            return SignTransactionAsync(externalSigner, (int)chain, to, amount, nonce, data);
        }

        public Task<string> SignTransactionAsync(IEthECKeyExternalSigner externalSigner, BigInteger chainId, string to, BigInteger amount,
            BigInteger nonce, string data)
        {
            var transaction = new TransactionChainId(to, amount, nonce, data, chainId);
            return SignTransactionAsync(externalSigner, transaction);
        }

        public Task<string> SignTransactionAsync(IEthECKeyExternalSigner externalSigner, Chain chain, string to, BigInteger amount,
            BigInteger nonce, BigInteger gasPrice,
            BigInteger gasLimit)
        {
            return SignTransactionAsync(externalSigner, (int)chain, to, amount, nonce, gasPrice, gasLimit);
        }

        public Task<string> SignTransactionAsync(IEthECKeyExternalSigner externalSigner, BigInteger chainId, string to, BigInteger amount,
            BigInteger nonce, BigInteger gasPrice,
            BigInteger gasLimit)
        {
            var transaction = new TransactionChainId(to, amount, nonce, gasPrice, gasLimit, chainId);
            return SignTransactionAsync(externalSigner, transaction);
        }

        public Task<string> SignTransactionAsync(IEthECKeyExternalSigner externalSigner, Chain chain, string to, BigInteger amount,
            BigInteger nonce, BigInteger gasPrice,
            BigInteger gasLimit, string data)
        {
            return SignTransactionAsync(externalSigner, (int)chain, to, amount, nonce, gasPrice, gasLimit, data);
        }

        public Task<string> SignTransactionAsync(IEthECKeyExternalSigner externalSigner, BigInteger chainId, string to, BigInteger amount,
            BigInteger nonce, BigInteger gasPrice,
            BigInteger gasLimit, string data)
        {
            var transaction = new TransactionChainId(to, amount, nonce, gasPrice, gasLimit, data, chainId);
            return SignTransactionAsync(externalSigner, transaction);
        }
#endif

    }
}