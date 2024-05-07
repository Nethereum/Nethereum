using Nethereum.RLP;
using Nethereum.Util;
using System.Collections.Generic;
using System.Linq;

namespace Nethereum.Mud.EncodingDecoding
{
    public static class FieldLayoutEncoder
    {
        public static byte[] EncodeFieldLayout(List<FieldInfo> fieldInfos)
        {
            var staticFields = fieldInfos.Where(f => f.ABIType.IsDynamic() == false && f.IsKey == false).OrderBy(f => f.Order);
            var dynamicFields = fieldInfos.Where(f => f.ABIType.IsDynamic() && f.IsKey == false).OrderBy(f => f.Order);
            var staticFieldLengths = staticFields.Select(f => f.ABIType.StaticSize);
            var staticDataLength = staticFieldLengths.Sum();
            var staticFieldLengthsAsBytes = ByteUtil.Merge(staticFieldLengths.Select(x => x.ToBytesForRLPEncoding().PadBytesLeft(1)).ToArray());
            
            return staticDataLength.ToBytesForRLPEncoding().PadBytesLeft(2)
                  .Concat(staticFields.Count().ToBytesForRLPEncoding().PadBytesLeft(1))
                  .Concat(dynamicFields.Count().ToBytesForRLPEncoding().PadBytesLeft(1))
                  .Concat(staticFieldLengthsAsBytes)
                  .ToArray().PadBytesRight(32);
        }
    }
}
