using System.Linq;
using System.Numerics;

namespace Ethereum.RPC.ABI
{
    public class EncoderDecoderHelpes
    {
        public static int GetNumberOfBytes(byte[] encoded)
        {
            var numberOfBytesEncoded = encoded.Take(32);
            var numberOfBytes = new IntType("int").Decode(numberOfBytesEncoded.ToArray());
            var unboxed = (BigInteger)numberOfBytes;
            return (int)unboxed;
        }
    }
}