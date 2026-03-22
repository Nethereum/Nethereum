namespace Nethereum.Merkle.Sparse
{
    public interface ISmtKeyHasher
    {
        byte[] ComputePath(byte[] key);
        int PathBitLength { get; }
    }

    public class IdentitySmtKeyHasher : ISmtKeyHasher
    {
        private readonly int _bitLength;

        public IdentitySmtKeyHasher(int bitLength)
        {
            _bitLength = bitLength;
        }

        public int PathBitLength => _bitLength;

        public byte[] ComputePath(byte[] key) => key;
    }

    public class Sha256SmtKeyHasher : ISmtKeyHasher
    {
        [System.ThreadStatic] private static System.Security.Cryptography.SHA256 _sha256;

        public int PathBitLength => 256;

        public byte[] ComputePath(byte[] key)
        {
            var sha = _sha256 ?? (_sha256 = System.Security.Cryptography.SHA256.Create());
            return sha.ComputeHash(key);
        }
    }
}
