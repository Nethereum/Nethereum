using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;

namespace Nethereum.Signer
{

    public class LegacyTransactionSigner
    {
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
            var transaction = new LegacyTransaction(to, amount, nonce);
            return SignTransaction(privateKey, transaction);
        }

        public string SignTransaction(byte[] privateKey, string to, BigInteger amount, BigInteger nonce, string data)
        {
            var transaction = new LegacyTransaction(to, amount, nonce, data);
            return SignTransaction(privateKey, transaction);
        }

        public string SignTransaction(byte[] privateKey, string to, BigInteger amount, BigInteger nonce,
            BigInteger gasPrice,
            BigInteger gasLimit)
        {
            var transaction = new LegacyTransaction(to, amount, nonce, gasPrice, gasLimit);
            return SignTransaction(privateKey, transaction);
        }

        public string SignTransaction(byte[] privateKey, string to, BigInteger amount, BigInteger nonce,
            BigInteger gasPrice,
            BigInteger gasLimit, string data)
        {
            var transaction = new LegacyTransaction(to, amount, nonce, gasPrice, gasLimit, data);
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
            var transaction = new LegacyTransactionChainId(to, amount, nonce, chainId);
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
            var transaction = new LegacyTransactionChainId(to, amount, nonce, data, chainId);
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
            var transaction = new LegacyTransactionChainId(to, amount, nonce, gasPrice, gasLimit, chainId);
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
            var transaction = new LegacyTransactionChainId(to, amount, nonce, gasPrice, gasLimit, data, chainId);
            return SignTransaction(privateKey, transaction);
        }

        public string SignTransaction(byte[] privateKey, LegacyTransaction transaction)
        {
            var signature = new EthECKey(privateKey, true).SignAndCalculateV(transaction.RawHash);
            transaction.SetSignature(new Signature() { R = signature.R, S = signature.S, V = signature.V });
            return transaction.GetRLPEncoded().ToHex();
        }

        public string SignTransaction(byte[] privateKey, LegacyTransactionChainId transaction)
        {
            var signature = new EthECKey(privateKey, true).SignAndCalculateV(transaction.RawHash, transaction.GetChainIdAsBigInteger());
            transaction.SetSignature(new Signature() { R = signature.R, S = signature.S, V = signature.V });
            return transaction.GetRLPEncoded().ToHex();
        }

        public string SignTransaction(byte[] privateKey, RLPSignedDataHashBuilder rLPSignedDataHashBuilder)
        {
            var signature = new EthECKey(privateKey, true).SignAndCalculateV(rLPSignedDataHashBuilder.RawHash);
            rLPSignedDataHashBuilder.SetSignature(new Signature() { R = signature.R, S = signature.S, V = signature.V });
            return rLPSignedDataHashBuilder.GetRLPEncoded().ToHex();
        }

#if !DOTNET35
        private async Task<string> SignTransactionAsync(IEthExternalSigner externalSigner, LegacyTransaction transaction)
        {
            await externalSigner.SignAsync(transaction).ConfigureAwait(false);
            return transaction.GetRLPEncoded().ToHex();
        }

        private async Task<string> SignTransactionAsync(IEthExternalSigner externalSigner, LegacyTransactionChainId transaction)
        {
            await externalSigner.SignAsync(transaction).ConfigureAwait(false);
            return transaction.GetRLPEncoded().ToHex();
        }


        public Task<string> SignTransactionAsync(IEthExternalSigner externalSigner, string to, BigInteger amount, BigInteger nonce)
        {
            var transaction = new LegacyTransaction(to, amount, nonce);
            return SignTransactionAsync(externalSigner, transaction);
        }

        public Task<string> SignTransactionAsync(IEthExternalSigner externalSigner, string to, BigInteger amount, BigInteger nonce, string data)
        {
            var transaction = new LegacyTransaction(to, amount, nonce, data);
            return SignTransactionAsync(externalSigner, transaction);
        }

        public Task<string> SignTransactionAsync(IEthExternalSigner externalSigner, string to, BigInteger amount, BigInteger nonce,
            BigInteger gasPrice,
            BigInteger gasLimit)
        {
            var transaction = new LegacyTransaction(to, amount, nonce, gasPrice, gasLimit);
            return SignTransactionAsync(externalSigner, transaction);
        }

        public Task<string> SignTransactionAsync(IEthExternalSigner externalSigner, string to, BigInteger amount, BigInteger nonce,
            BigInteger gasPrice,
            BigInteger gasLimit, string data)
        {
            var transaction = new LegacyTransaction(to, amount, nonce, gasPrice, gasLimit, data);
            return SignTransactionAsync(externalSigner, transaction);
        }

       public Task<string> SignTransactionAsync(IEthExternalSigner externalSigner, Chain chain, string to, BigInteger amount,
            BigInteger nonce)
        {
            return SignTransactionAsync(externalSigner, (int)chain, to, amount, nonce);
        }

        public Task<string> SignTransactionAsync(IEthExternalSigner externalSigner, BigInteger chainId, string to, BigInteger amount,
            BigInteger nonce)
        {
            var transaction = new LegacyTransactionChainId(to, amount, nonce, chainId);
            return SignTransactionAsync(externalSigner, transaction);
        }

        public Task<string> SignTransactionAsync(IEthExternalSigner externalSigner, Chain chain, string to, BigInteger amount,
            BigInteger nonce, string data)
        {
            return SignTransactionAsync(externalSigner, (int)chain, to, amount, nonce, data);
        }

        public Task<string> SignTransactionAsync(IEthExternalSigner externalSigner, BigInteger chainId, string to, BigInteger amount,
            BigInteger nonce, string data)
        {
            var transaction = new LegacyTransactionChainId(to, amount, nonce, data, chainId);
            return SignTransactionAsync(externalSigner, transaction);
        }

        public Task<string> SignTransactionAsync(IEthExternalSigner externalSigner, Chain chain, string to, BigInteger amount,
            BigInteger nonce, BigInteger gasPrice,
            BigInteger gasLimit)
        {
            return SignTransactionAsync(externalSigner, (int)chain, to, amount, nonce, gasPrice, gasLimit);
        }

        public Task<string> SignTransactionAsync(IEthExternalSigner externalSigner, BigInteger chainId, string to, BigInteger amount,
            BigInteger nonce, BigInteger gasPrice,
            BigInteger gasLimit)
        {
            var transaction = new LegacyTransactionChainId(to, amount, nonce, gasPrice, gasLimit, chainId);
            return SignTransactionAsync(externalSigner, transaction);
        }

        public Task<string> SignTransactionAsync(IEthExternalSigner externalSigner, Chain chain, string to, BigInteger amount,
            BigInteger nonce, BigInteger gasPrice,
            BigInteger gasLimit, string data)
        {
            return SignTransactionAsync(externalSigner, (int)chain, to, amount, nonce, gasPrice, gasLimit, data);
        }

        public Task<string> SignTransactionAsync(IEthExternalSigner externalSigner, BigInteger chainId, string to, BigInteger amount,
            BigInteger nonce, BigInteger gasPrice,
            BigInteger gasLimit, string data)
        {
            var transaction = new LegacyTransactionChainId(to, amount, nonce, gasPrice, gasLimit, data, chainId);
                return SignTransactionAsync(externalSigner, transaction);
        }
#endif

    }
}