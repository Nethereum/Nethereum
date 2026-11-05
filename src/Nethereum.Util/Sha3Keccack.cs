using System;
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
            var output = new byte[KeccakF1600.Keccak256OutputBytes];
            KeccakF1600.Keccak256(value, 0, value.Length, output, 0);
            return output;
        }

        [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.CoreFoundation, "util-keccak-into-buffer", "Sha3Keccack.CalculateHash into a buffer")]
        public void CalculateHash(byte[] input, int offset, int length, byte[] output, int outputOffset)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (output == null) throw new ArgumentNullException(nameof(output));
            if (offset < 0 || offset > input.Length) throw new ArgumentOutOfRangeException(nameof(offset));
            if (length < 0 || length > input.Length - offset) throw new ArgumentOutOfRangeException(nameof(length));
            if (outputOffset < 0 || outputOffset > output.Length - KeccakF1600.Keccak256OutputBytes) throw new ArgumentOutOfRangeException(nameof(outputOffset));

            KeccakF1600.Keccak256(input, offset, length, output, outputOffset);
        }

        public byte[] CalculateHashAsBytes(string value)
        {
            var input = Encoding.UTF8.GetBytes(value);
            return CalculateHash(input);
        }
    }
}