using System;
using System.Collections.Generic;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.RLP
{
    public class RLP
    {
        /// <summary>
        ///     Reason for threshold according to Vitalik Buterin:
        ///     - 56 bytes maximizes the benefit of both options
        ///     - if we went with 60 then we would have only had 4 slots for long strings
        ///     so RLP would not have been able to store objects above 4gb
        ///     - if we went with 48 then RLP would be fine for 2^128 space, but that's way too much
        ///     - so 56 and 2^64 space seems like the right place to put the cutoff
        ///     - also, that's where Bitcoin's varint does the cutof
        /// </summary>
        private const int SIZE_THRESHOLD = 56;

        /* RLP encoding rules are defined as follows:
		 * For a single byte whose value is in the [0x00, 0x7f] range, that byte is
		 * its own RLP encoding.
		 */

        /// <summary>
        ///     [0x80]
        ///     If a string is 0-55 bytes long, the RLP encoding consists of a single
        ///     byte with value 0x80 plus the length of the string followed by the
        ///     string. The range of the first byte is thus [0x80, 0xb7].
        /// </summary>
        internal const byte OFFSET_SHORT_ITEM = 0x80;

        /// <summary>
        ///     [0xb7]
        ///     If a string is more than 55 bytes long, the RLP encoding consists of a
        ///     single byte with value 0xb7 plus the length of the length of the string
        ///     in binary form, followed by the length of the string, followed by the
        ///     string. For example, a length-1024 string would be encoded as
        ///     \xb9\x04\x00 followed by the string. The range of the first byte is thus
        ///     [0xb8, 0xbf].
        /// </summary>
        internal const byte OFFSET_LONG_ITEM = 0xb7;

        /// <summary>
        ///     [0xc0]
        ///     If the total payload of a list (i.e. the combined length of all its
        ///     items) is 0-55 bytes long, the RLP encoding consists of a single byte
        ///     with value 0xc0 plus the length of the list followed by the concatenation
        ///     of the RLP encodings of the items. The range of the first byte is thus
        ///     [0xc0, 0xf7].
        /// </summary>
        public const byte OFFSET_SHORT_LIST = 0xc0;

        /// <summary>
        ///     [0xf7]
        ///     If the total payload of a list is more than 55 bytes long, the RLP
        ///     encoding consists of a single byte with value 0xf7 plus the length of the
        ///     length of the list in binary form, followed by the length of the list,
        ///     followed by the concatenation of the RLP encodings of the items. The
        ///     range of the first byte is thus [0xf8, 0xff].
        /// </summary>
        private const byte OFFSET_LONG_LIST = 0xf7;

        public const int MaxDecodeDepth = 64;

        private const int MaxErrorHexBytes = 64;

        public static readonly byte[] EMPTY_BYTE_ARRAY = new byte[0];
        public static readonly byte[] ZERO_BYTE_ARRAY = {0};

        public static int ByteArrayToInt(byte[] bytes)
        {
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);

            return BitConverter.ToInt32(bytes, 0);
        }

        public const int MinUntrustedElementBudget = 1024;
        public static int UntrustedElementBudget(int inputLength)
        {
            return (int) Math.Max(MinUntrustedElementBudget, (long) inputLength * 3 / 4);
        }

        public static IRLPElement Decode(byte[] msgData)
        {
            EnsureNotNullOrEmpty(msgData);
            return DecodeSingle(msgData, int.MaxValue);
        }

        public static IRLPElement Decode(byte[] msgData, int maxElements)
        {
            EnsureNotNullOrEmpty(msgData);
            return DecodeSingle(msgData, maxElements);
        }

        public static IRLPElement DecodeCollection(byte[] msgData)
        {
            if (msgData == null)
                throw new Exception("Invalid RLP ");
            return DecodeCollection(msgData, int.MaxValue);
        }

        public static IRLPElement DecodeCollection(byte[] msgData, int maxElements)
        {
            if (msgData == null)
                throw new Exception("Invalid RLP ");
            var rlpCollection = new RLPCollection();
            if (msgData.Length > 0)
                DecodeOwned((byte[]) msgData.Clone(), 0, msgData.Length, rlpCollection, maxElements, false);
            return rlpCollection;
        }

        /// <summary>
        ///     Decodes a message from a starting point to an end point
        /// </summary>
        public static void Decode(byte[] msgData, int level, int startPosition,
            int endPosition, int levelToIndex, RLPCollection rlpCollection)
        {
            if (msgData == null || msgData.Length == 0)
                return;

            DecodeOwned((byte[]) msgData.Clone(), startPosition, endPosition, rlpCollection,
                int.MaxValue, false);
        }

        private static IRLPElement DecodeSingle(byte[] msgData, int maxElements)
        {
            var rlpCollection = new RLPCollection();
            DecodeOwned((byte[]) msgData.Clone(), 0, msgData.Length, rlpCollection, maxElements, true);
            return rlpCollection[0];
        }

        private static void DecodeOwned(byte[] msgData, int startPosition, int endPosition,
            RLPCollection rlpCollection, int maxElements, bool singleTopLevelItem)
        {
            try
            {
                var limit = msgData.Length;
                if (singleTopLevelItem)
                {
                    ReadHeader(msgData, startPosition, limit, out var payloadStart, out var payloadLength);
                    if (payloadStart + payloadLength != endPosition)
                        throw new InvalidOperationException("RLP input has bytes after the top-level value");
                }

                CountElements(msgData, startPosition, endPosition, limit, 0, 0, maxElements);
                DecodeRange(msgData, startPosition, endPosition, limit, rlpCollection, 0);
            }
            catch (Exception ex)
            {
                throw new Exception(
                    "Invalid RLP " + DescribeRange(msgData, startPosition, endPosition), ex);
            }
        }

        private static void EnsureNotNullOrEmpty(byte[] msgData)
        {
            if (msgData == null || msgData.Length == 0)
                throw new Exception("Invalid RLP ");
        }

        private static string DescribeRange(byte[] msgData, int startPosition, int endPosition)
        {
            var start = Math.Max(0, Math.Min(startPosition, msgData.Length));
            var end = Math.Max(start, Math.Min(endPosition, msgData.Length));
            var length = end - start;
            var shown = Math.Min(length, MaxErrorHexBytes);
            var head = new byte[shown];
            Array.Copy(msgData, start, head, 0, shown);
            return head.ToHex() + (shown < length ? "... (" + length + " bytes)" : "");
        }

        internal static bool ReadHeader(byte[] msgData, int position, int limit,
            out int payloadStart, out int payloadLength)
        {
            if (position < 0 || position >= limit)
                throw new InvalidOperationException("RLP element starts beyond the input");

            var prefix = msgData[position];

            if (prefix < OFFSET_SHORT_ITEM)
            {
                payloadStart = position;
                payloadLength = 1;
                return false;
            }

            if (prefix <= OFFSET_LONG_ITEM)
            {
                payloadStart = position + 1;
                payloadLength = prefix - OFFSET_SHORT_ITEM;
                EnsureWithinLimit(payloadStart, payloadLength, limit);
                if (payloadLength == 1 && msgData[payloadStart] < OFFSET_SHORT_ITEM)
                    throw new InvalidOperationException("RLP single byte below 0x80 must not carry a string prefix");
                return false;
            }

            var isList = prefix >= OFFSET_SHORT_LIST;

            if (isList && prefix <= OFFSET_LONG_LIST)
            {
                payloadStart = position + 1;
                payloadLength = prefix - OFFSET_SHORT_LIST;
                EnsureWithinLimit(payloadStart, payloadLength, limit);
                return true;
            }

            var lengthOfLength = prefix - (isList ? OFFSET_LONG_LIST : OFFSET_LONG_ITEM);
            payloadStart = position + 1 + lengthOfLength;
            if (payloadStart > limit)
                throw new InvalidOperationException("RLP length extends beyond the input");
            if (msgData[position + 1] == 0)
                throw new InvalidOperationException("RLP length has a leading zero");

            ulong declared = 0;
            for (var i = 1; i <= lengthOfLength; i++)
                declared = (declared << 8) | msgData[position + i];
            if (declared < SIZE_THRESHOLD)
                throw new InvalidOperationException("RLP long form used for a payload under 56 bytes");
            if (declared > int.MaxValue)
                throw new InvalidOperationException("RLP length exceeds the maximum supported size");

            payloadLength = (int) declared;
            EnsureWithinLimit(payloadStart, payloadLength, limit);
            return isList;
        }

        private static int CountElements(byte[] msgData, int startPosition, int endPosition, int limit,
            int depth, int count, int maxElements)
        {
            var position = startPosition;
            while (position < endPosition)
            {
                var isList = ReadHeader(msgData, position, limit, out var payloadStart, out var payloadLength);
                if (++count > maxElements)
                    throw new InvalidOperationException("RLP holds more than " + maxElements + " elements");
                var next = payloadStart + payloadLength;
                if (isList)
                {
                    EnsureListDepthWithinLimit(depth);
                    count = CountElements(msgData, payloadStart, next, next, depth + 1, count, maxElements);
                }
                position = next;
            }
            return count;
        }

        private static void DecodeRange(byte[] msgData, int startPosition, int endPosition, int limit,
            RLPCollection rlpCollection, int depth)
        {
            var position = startPosition;
            while (position < endPosition)
            {
                var isList = ReadHeader(msgData, position, limit, out var payloadStart, out var payloadLength);
                var next = payloadStart + payloadLength;
                if (isList)
                {
                    EnsureListDepthWithinLimit(depth);
                    var newLevelCollection = new RLPCollection();
                    newLevelCollection.SetSource(msgData, position, next - position);
                    DecodeRange(msgData, payloadStart, next, next, newLevelCollection, depth + 1);
                    rlpCollection.Add(newLevelCollection);
                }
                else
                {
                    var item = EMPTY_BYTE_ARRAY;
                    if (payloadLength > 0)
                    {
                        item = new byte[payloadLength];
                        Array.Copy(msgData, payloadStart, item, 0, payloadLength);
                    }
                    rlpCollection.Add(new RLPItem(item));
                }
                position = next;
            }
        }

        private static void EnsureWithinLimit(int payloadStart, int payloadLength, int limit)
        {
            if (payloadLength < 0 || payloadLength > limit - payloadStart)
                throw new InvalidOperationException("RLP element is larger than containing list");
        }

        private static void EnsureListDepthWithinLimit(int depth)
        {
            if (depth + 1 > MaxDecodeDepth)
                throw new InvalidOperationException("RLP list nesting exceeds " + MaxDecodeDepth);
        }

        public static IRLPElement DecodeFirstElement(byte[] msgData, int startPos)
        {
            EnsureNotNullOrEmpty(msgData);
            var rlpCollection = new RLPCollection();
            DecodeOwned((byte[]) msgData.Clone(), startPos, startPos + 1, rlpCollection,
                int.MaxValue, false);
            return rlpCollection[0];
        }

        public static byte[] EncodeByte(byte singleByte)
        {
            if (singleByte == 0)
                return new[] {OFFSET_SHORT_ITEM};
            if (singleByte <= 0x7F)
                return new[] {singleByte};
            return new[] {(byte) (OFFSET_SHORT_ITEM + 1), singleByte};
        }

        public static byte[] EncodeElement(byte[] srcData)
        {
            if (IsNullOrZeroArray(srcData))
                return new[] {OFFSET_SHORT_ITEM};
            if (IsSingleZero(srcData))
                return srcData;
            if (srcData.Length == 1 && srcData[0] < 0x80)
                return srcData;
            if (srcData.Length < SIZE_THRESHOLD)
            {
                // length = 8X
                var length = (byte) (OFFSET_SHORT_ITEM + srcData.Length);
                var data = new byte[srcData.Length + 1];
                Array.Copy(srcData, 0, data, 1, srcData.Length);
                data[0] = length;

                return data;
            }
            else
            {
                // length of length = BX
                // prefix = [BX, [length]]
                var tmpLength = srcData.Length;
                byte byteNum = 0;
                while (tmpLength != 0)
                {
                    ++byteNum;
                    tmpLength = tmpLength >> 8;
                }
                var lenBytes = new byte[byteNum];
                for (var i = 0; i < byteNum; ++i)
                    lenBytes[byteNum - 1 - i] = (byte) (srcData.Length >> (8 * i));
                // first byte = F7 + bytes.length
                var data = new byte[srcData.Length + 1 + byteNum];
                Array.Copy(srcData, 0, data, 1 + byteNum, srcData.Length);
                data[0] = (byte) (OFFSET_LONG_ITEM + byteNum);
                Array.Copy(lenBytes, 0, data, 1, lenBytes.Length);

                return data;
            }
        }

        public static byte[] EncodeDataItemsAsElementOrListAndCombineAsList(byte[][] dataItems, int[] indexOfListDataItems = null)
        {
            if(indexOfListDataItems == null)
            {
                var encoded = new byte[dataItems.Length][];
                for (int i = 0; i < dataItems.Length; i++)
                    encoded[i] = EncodeElement(dataItems[i]);
                return EncodeList(encoded);
            }
            
            var encodedData = new List<byte[]>();

            for (var i = 0; i < dataItems.Length; i++)
            {
                if (Array.IndexOf(indexOfListDataItems, i) >= 0)
                {
                    var item = dataItems[i];
                    encodedData.Add(EncodeList(item));
                }
                else
                {
                    encodedData.Add(EncodeElement(dataItems[i]));
                }
            }

            return EncodeList(encodedData.ToArray());
        }

        public static byte[] EncodeList(params byte[][] items)
        {
            if (items == null || (items.Length == 1 && items[0] == null))
                return new[] {OFFSET_SHORT_LIST};

            var totalLength = 0;
            for (var i = 0; i < items.Length; i++)
                totalLength += items[i].Length;

            byte[] data;

            int copyPos;

            if (totalLength < SIZE_THRESHOLD)
            {
                var dataLength = 1 + totalLength;
                data = new byte[dataLength];

                //single byte length
                data[0] = (byte) (OFFSET_SHORT_LIST + totalLength);
                copyPos = 1;
            }
            else
            {
                // length of length = BX
                // prefix = [BX, [length]]
                var tmpLength = totalLength;
                byte byteNum = 0;

                while (tmpLength != 0)
                {
                    ++byteNum;
                    tmpLength = tmpLength >> 8;
                }

                tmpLength = totalLength;

                var lenBytes = new byte[byteNum];
                for (var i = 0; i < byteNum; ++i)
                    lenBytes[byteNum - 1 - i] = (byte) (tmpLength >> (8 * i));
                // first byte = F7 + bytes.length
                data = new byte[1 + lenBytes.Length + totalLength];

                data[0] = (byte) (OFFSET_LONG_LIST + byteNum);
                Array.Copy(lenBytes, 0, data, 1, lenBytes.Length);

                copyPos = lenBytes.Length + 1;
            }

            //Combine all elements
            foreach (var item in items)
            {
                Array.Copy(item, 0, data, copyPos, item.Length);
                copyPos += item.Length;
            }
            return data;
        }

        public static bool IsNullOrZeroArray(byte[] array)
        {
            return array == null || array.Length == 0;
        }

        public static bool IsSingleZero(byte[] array)
        {
            return array.Length == 1 && array[0] == 0;
        }

        public static int GetFirstElementLength(byte[] data, int offset = 0)
        {
            ReadHeader(data, offset, data.Length, out var payloadStart, out var payloadLength);
            return payloadStart + payloadLength - offset;
        }

        public static bool TryReadLeadingListUInt64(byte[] payload, out ulong value)
        {
            value = 0;
            if (payload == null || payload.Length < 1) return false;
            byte listPrefix = payload[0];
            if (listPrefix < OFFSET_SHORT_LIST) return false;
            int headerLength = listPrefix <= OFFSET_LONG_LIST
                ? 1
                : 1 + (listPrefix - OFFSET_LONG_LIST);
            if (headerLength >= payload.Length) return false;

            byte itemPrefix = payload[headerLength];
            if (itemPrefix >= OFFSET_SHORT_LIST) return false;

            byte[] data;
            if (itemPrefix < OFFSET_SHORT_ITEM)
                data = new[] {itemPrefix};
            else if (itemPrefix <= OFFSET_LONG_ITEM)
            {
                var length = itemPrefix - OFFSET_SHORT_ITEM;
                if (length > 8 || headerLength + 1 + length > payload.Length) return false;
                data = new byte[length];
                Array.Copy(payload, headerLength + 1, data, 0, length);
            }
            else
                return false;

            if (data.Length == 0) return false;

            ulong result = 0;
            for (var i = 0; i < data.Length; i++)
                result = (result << 8) | data[i];
            value = result;
            return true;
        }
    }
}
