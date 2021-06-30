using Nethereum.Util;

namespace Nethereum.Signer
{
    public abstract class SignedTransaction : ISignedTransaction
    {
        public virtual EthECDSASignature Signature { get; protected set; }

        public abstract TransactionType TransactionType { get; }
        public abstract void Sign(EthECKey key);
        public abstract EthECKey Key { get; }

        public virtual byte[] RawHash
        {
            get
            {
                var plainMsg = GetRLPEncodedRaw();
                return new Sha3Keccack().CalculateHash(plainMsg);
            }
        }

        public virtual byte[] Hash
        {
            get
            {
                var plainMsg = GetRLPEncoded();
                return new Sha3Keccack().CalculateHash(plainMsg);
            }
        }

        public abstract byte[] GetRLPEncoded();

        public abstract byte[] GetRLPEncodedRaw();
    }
}