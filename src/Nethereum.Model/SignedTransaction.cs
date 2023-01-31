using Nethereum.Util;
using System.Numerics;

namespace Nethereum.Model
{
    public class IndexedSignedTransaction
    {
        public BigInteger Index { get; set; }
        public ISignedTransaction SignedTransaction { get; set; }

    }

    public abstract class SignedTransaction : ISignedTransaction
    {
        public abstract TransactionType TransactionType { get; }

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

        public virtual ISignature Signature { get; protected set; }

        public abstract void SetSignature(ISignature signature);
       
    }
}