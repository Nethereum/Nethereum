using System.Text;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util.Keccak;


namespace Nethereum.Util
{
    public class Sha3Keccack
    {
        public static Sha3Keccack Current { get; } = new Sha3Keccack();

        public string CalculateHash(string value)
        {
            var input = Encoding.UTF8.GetBytes(value);
            var output = CalculateHash(input);
            return output.ToHex();
        }

        public string CalculateHashFromHex(params string[] hexValues)
        {
            var parts = new string[hexValues.Length];
            for (int i = 0; i < hexValues.Length; i++)
                parts[i] = hexValues[i].RemoveHexPrefix();
            var joinedHex = string.Join("", parts);
            return CalculateHash(joinedHex.HexToByteArray()).ToHex();
        }

        public byte[] CalculateHash(byte[] value)
        {
            var digest = new KeccakDigest(256);
            var output = new byte[digest.GetDigestSize()];
            digest.BlockUpdate(value, 0, value.Length);
            digest.DoFinal(output, 0);
            return output;
        }

        public byte[] CalculateHashAsBytes(string value)
        {
            var input = Encoding.UTF8.GetBytes(value);
            return CalculateHash(input);
        }
    }
}