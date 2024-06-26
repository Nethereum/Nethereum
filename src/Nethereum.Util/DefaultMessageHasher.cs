using System.Text;

namespace Nethereum.Util
{
    public class DefaultMessageHasher
    {
        public byte[] Hash(string plainMessage)
        {
            return Hash(Encoding.UTF8.GetBytes(plainMessage));
        }

        public byte[] Hash(byte[] plainMessage)
        {
            var hash = new Sha3Keccack().CalculateHash(plainMessage);
            return hash;
        }
    }
}
