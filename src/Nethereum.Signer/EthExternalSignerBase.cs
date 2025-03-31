using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Signer.Crypto;
using Nethereum.Util;

namespace Nethereum.Signer
{
#if !DOTNET35
    public abstract class EthExternalSignerBase : IEthExternalSigner
    {
        protected abstract Task<byte[]> GetPublicKeyAsync();
        protected abstract Task<ECDSASignature> SignExternallyAsync(byte[] bytes);
        public abstract Task SignAsync(LegacyTransactionChainId transaction);
        public abstract Task SignAsync(LegacyTransaction transaction);
        public abstract Task SignAsync(Transaction1559 transaction);
        public abstract Task SignAsync(Transaction7702 transaction);
        public abstract bool Supported1559 { get; }
        public abstract ExternalSignerTransactionFormat ExternalSignerTransactionFormat { get; protected set; }
        public abstract bool CalculatesV { get; protected set; }

        public virtual async Task<string> GetAddressAsync()
        {
            var publicKey = await GetPublicKeyAsync().ConfigureAwait(false);
            return new EthECKey(publicKey, false).GetPublicAddress();
        }

        public async Task<EthECDSASignature> SignAsync(byte[] rawBytes, BigInteger chainId)
        {
            var signature = await SignExternallyAsync(rawBytes).ConfigureAwait(false);
            if (CalculatesV) return new EthECDSASignature(signature);

            if (ExternalSignerTransactionFormat == ExternalSignerTransactionFormat.RLP)
            {
                rawBytes = new Sha3Keccack().CalculateHash(rawBytes);
            }
            var publicKey = await GetPublicKeyAsync().ConfigureAwait(false);
            var recId = EthECKey.CalculateRecId(signature, rawBytes, publicKey);
            var vChain = EthECKey.CalculateV(chainId, recId);
            signature.V = vChain.ToBytesForRLPEncoding();
            return new EthECDSASignature(signature);
        }

        public async Task<EthECDSASignature> SignAsync(byte[] rawBytes)
        {
            var signature = await SignExternallyAsync(rawBytes).ConfigureAwait(false);
            if (CalculatesV) return new EthECDSASignature(signature);

            if (ExternalSignerTransactionFormat == ExternalSignerTransactionFormat.RLP)
            {
                rawBytes = new Sha3Keccack().CalculateHash(rawBytes);
            }

            var publicKey = await GetPublicKeyAsync().ConfigureAwait(false);
            var recId = EthECKey.CalculateRecId(signature, rawBytes, publicKey);
            signature.V = new[] {(byte) (recId + 27)};
            return new EthECDSASignature(signature);
        }

        public Task<EthECDSASignature> SignEthereumMessageAsync(byte[] rawBytes)
        {
            var messageSigner = new EthereumMessageSigner();
            var hash = messageSigner.HashPrefixedMessage(rawBytes);
            return SignAsync(hash);
        }

        public async Task<EthECDSASignature> SignAndCalculateYParityVAsync(byte[] rawBytes)
        {
            var signature = await SignExternallyAsync(rawBytes).ConfigureAwait(false);
            if (CalculatesV) return new EthECDSASignature(signature);

            if (ExternalSignerTransactionFormat == ExternalSignerTransactionFormat.RLP)
            {
                rawBytes = new Sha3Keccack().CalculateHash(rawBytes);
            }

            var publicKey = await GetPublicKeyAsync().ConfigureAwait(false);
            var recId = EthECKey.CalculateRecId(signature, rawBytes, publicKey);
            signature.V = new[] { (byte)(recId) };
            return new EthECDSASignature(signature);
        }

        protected async Task SignHashTransactionAsync(LegacyTransaction transaction)
        {
            if (ExternalSignerTransactionFormat == ExternalSignerTransactionFormat.Hash)
            {
                var signature = await SignAsync(transaction.RawHash).ConfigureAwait(false);
                transaction.SetSignature(signature);
            }
        }

        protected async Task SignRLPTransactionAsync(LegacyTransaction transaction)
        {
            if (ExternalSignerTransactionFormat == ExternalSignerTransactionFormat.RLP)
            {
                var signature = await SignAsync(transaction.GetRLPEncodedRaw()).ConfigureAwait(false);
                transaction.SetSignature(signature);
            }
        }

        protected async Task SignHashTransactionAsync(LegacyTransactionChainId transaction)
        {
            if (ExternalSignerTransactionFormat == ExternalSignerTransactionFormat.Hash)
            {
                var signature = await SignAsync(transaction.RawHash, transaction.GetChainIdAsBigInteger()).ConfigureAwait(false);
                transaction.SetSignature(signature);
            }
        }

        protected async Task SignRLPTransactionAsync(LegacyTransactionChainId transaction)
        {
        
            if (ExternalSignerTransactionFormat == ExternalSignerTransactionFormat.RLP)
            {
                var signature = await SignAsync(transaction.GetRLPEncodedRaw(), transaction.GetChainIdAsBigInteger()).ConfigureAwait(false);
                transaction.SetSignature(signature);
            }
        }


        protected async Task SignHashTransactionAsync(Transaction1559 transaction)
        {
            if (ExternalSignerTransactionFormat == ExternalSignerTransactionFormat.Hash)
            {
                var signature = await SignAndCalculateYParityVAsync(transaction.RawHash).ConfigureAwait(false);
                transaction.SetSignature(signature);
            }
        }

        protected async Task SignRLPTransactionAsync(Transaction1559 transaction)
        {
            if (ExternalSignerTransactionFormat == ExternalSignerTransactionFormat.RLP)
            {
                var signature = await SignAndCalculateYParityVAsync(transaction.GetRLPEncodedRaw()).ConfigureAwait(false);
                transaction.SetSignature(signature);
            }
        }


        protected async Task SignHashTransactionAsync(Transaction7702 transaction)
        {
            if (ExternalSignerTransactionFormat == ExternalSignerTransactionFormat.Hash)
            {
                var signature = await SignAndCalculateYParityVAsync(transaction.RawHash).ConfigureAwait(false);
                transaction.SetSignature(signature);
            }
        }

        protected async Task SignRLPTransactionAsync(Transaction7702 transaction)
        {
            if (ExternalSignerTransactionFormat == ExternalSignerTransactionFormat.RLP)
            {
                var signature = await SignAndCalculateYParityVAsync(transaction.GetRLPEncodedRaw()).ConfigureAwait(false);
                transaction.SetSignature(signature);
            }
        }


    }
#endif
}