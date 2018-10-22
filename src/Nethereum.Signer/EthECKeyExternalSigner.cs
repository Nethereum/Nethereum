using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;
using Nethereum.Signer.Crypto;

namespace Nethereum.Signer
{
#if !DOTNET35
    public abstract class EthExternalSignerBase : IEthExternalSigner
    {
        public abstract Task<byte[]> GetPublicKeyAsync();
        protected abstract Task<ECDSASignature> SignExternallyAsync(byte[] bytes);
        public abstract Task SignAsync(TransactionChainId transaction);
        public abstract Task SignAsync(Transaction transaction);
        public abstract ExternalSignerTransactionFormat ExternalSignerTransactionFormat { get; protected set; }

        public abstract bool CalculatesV { get; protected set; }

        public async Task<string> GetAddressAsync()
        {
            var publicKey = await GetPublicKeyAsync();
            return new EthECKey(publicKey, false).GetPublicAddress();
        }

        public async Task<EthECDSASignature> SignAsync(byte[] rawBytes, BigInteger chainId)
        {
            var signature = await SignExternallyAsync(rawBytes);
            if (CalculatesV) return new EthECDSASignature(signature);

            var publicKey = await GetPublicKeyAsync();
            var recId = EthECKey.CalculateRecId(signature, rawBytes, publicKey);
            var vChain = EthECKey.CalculateV(chainId, recId);
            signature.V = vChain.ToBytesForRLPEncoding();
            return new EthECDSASignature(signature);
        }

        public async Task<EthECDSASignature> SignAsync(byte[] hash)
        {
            var signature = await SignExternallyAsync(hash);
            if (CalculatesV) return new EthECDSASignature(signature);

            var publicKey = await GetPublicKeyAsync();
            var recId = EthECKey.CalculateRecId(signature, hash, publicKey);
            signature.V = new[] {(byte) (recId + 27)};
            return new EthECDSASignature(signature);
        }

        protected async Task SignHashTransactionAsync(Transaction transaction)
        {
            if (ExternalSignerTransactionFormat == ExternalSignerTransactionFormat.Hash)
            {
                var signature = await SignAsync(transaction.RawHash);
                transaction.SetSignature(signature);
            }
        }

        protected async Task SignRLPTransactionAsync(Transaction transaction)
        {
            if (ExternalSignerTransactionFormat == ExternalSignerTransactionFormat.RLP)
            {
                var signature = await SignAsync(transaction.GetRLPEncodedRaw());
                transaction.SetSignature(signature);
            }
        }

        protected async Task SignHashTransactionAsync(TransactionChainId transaction)
        {
            if (ExternalSignerTransactionFormat == ExternalSignerTransactionFormat.Hash)
            {
                var signature = await SignAsync(transaction.RawHash, transaction.GetChainIdAsBigInteger());
                transaction.SetSignature(signature);
            }
        }

        protected async Task SignRLPTransactionAsync(TransactionChainId transaction)
        {
            if (ExternalSignerTransactionFormat == ExternalSignerTransactionFormat.RLP)
            {
                var signature = await SignAsync(transaction.RawHash, transaction.GetChainIdAsBigInteger());
                transaction.SetSignature(signature);
            }
        }
    }
#endif
}