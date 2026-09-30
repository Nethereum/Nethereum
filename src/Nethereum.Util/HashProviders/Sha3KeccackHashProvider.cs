
namespace Nethereum.Util.HashProviders
{
    public class Sha3KeccackHashProvider : IHashProvider
    {
        public static readonly Sha3KeccackHashProvider Instance = new Sha3KeccackHashProvider();

        public byte[] ComputeHash(byte[] data)
        {
            return Sha3Keccack.Current.CalculateHash(data);
        }
    }

}
