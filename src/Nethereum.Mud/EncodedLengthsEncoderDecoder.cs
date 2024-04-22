using Nethereum.ABI;
using Nethereum.Util;


namespace Nethereum.Mud
{
    public class EncodedLengthsEncoderDecoder
    {
        public static EncodedLengths Decode(byte[] encodedLenghtsBytes)
        {
            var byteLenghtData = encodedLenghtsBytes.Skip(32 - 7).Take(7).ToArray();
            var totalLength =  ABIType.CreateABIType("uint56").DecodePacked<int>(byteLenghtData);

            var sizes = (List<int>)((ArrayType)ABIType.CreateABIType("uint40[]")).DecodePackedUsingElementPacked(encodedLenghtsBytes.Take(32-7).ToArray(), typeof(List<int>));
            sizes.Reverse();
            return new EncodedLengths() { TotalLength = totalLength, Lengths = sizes.ToArray()};
        }

        public static byte[] Encode(byte[][] dynamicFields)
        {
            var totalLengthBytes = ABIType.CreateABIType("uint56").EncodePacked(dynamicFields.Sum(x => x.Length));
            var sizesBytes = ((ArrayType)ABIType.CreateABIType("uint40[]")).EncodePackedUsingElementPacked(dynamicFields.Reverse().Select(x => x.Length).ToArray());
            
            return sizesBytes.Concat(totalLengthBytes).ToArray().PadBytesLeft(32);
        }
    }
}
