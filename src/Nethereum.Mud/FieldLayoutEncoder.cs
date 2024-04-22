using Nethereum.RLP;
using Nethereum.Util;

namespace Nethereum.Mud
{
    public static class FieldLayoutEncoder
    {
        public static byte[] EncodeValues(List<FieldInfo> fieldInfos)
        {
            var staticFields = fieldInfos.Where(f => f.ABIType.IsDynamic() == false && f.IsKey == false).OrderBy(f => f.Order);
            var dynamicFields = fieldInfos.Where(f => f.ABIType.IsDynamic() && f.IsKey == false).OrderBy(f => f.Order);
            var staticFieldLengths = staticFields.Select(f => f.ABIType.StaticSize);
            var staticDataLength = staticFieldLengths.Sum();
            return staticDataLength.ToBytesForRLPEncoding().PadBytesLeft(4)
                  .Concat(
                            staticFields.Count().ToBytesForRLPEncoding().PadBytesLeft(2))
                  .Concat(
                           dynamicFields.Count().ToBytesForRLPEncoding().PadBytesLeft(2))
                           .ToArray().PadTo32Bytes();
        }
    }
}
