using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions; 

namespace Nethereum.ABI.Util.RLP
{
    /// <summary>
	/// Recursive Length Prefix (RLP) encoding.
	/// <para>
	/// The purpose of RLP is to encode arbitrarily nested arrays of binary data, and
	/// RLP is the main encoding method used to serialize objects in Ethereum. The
	/// only purpose of RLP is to encode structure; encoding specific atomic data
	/// types (eg. strings, integers, floats) is left up to higher-order protocols; in
	/// Ethereum the standard is that integers are represented in big endian binary
	/// form. If one wishes to use RLP to encode a dictionary, the two suggested
	/// canonical forms are to either use [[k1,v1],[k2,v2]...] with keys in
	/// lexicographic order or to use the higher-level Patricia Tree encoding as
	/// Ethereum does.
	/// </para>
	/// <para>
	/// The RLP encoding function takes in an item. An item is defined as follows:
	/// </para>
	/// <para>
	/// - A string (ie. byte array) is an item - A list of items is an item
	/// </para>
	/// <para>
	/// For example, an empty string is an item, as is the string containing the word
	/// "cat", a list containing any number of strings, as well as more complex data
	/// structures like ["cat",["puppy","cow"],"horse",[[]],"pig",[""],"sheep"]. Note
	/// that in the context of the rest of this article, "string" will be used as a
	/// synonym for "a certain number of bytes of binary data"; no special encodings
	/// are used and no knowledge about the content of the strings is implied.
	/// </para>
	/// <para>
	/// See: 
	/// </para>
	/// <see cref="https://github.com/ethereum/wiki/wiki/RLP"/>
	/// </summary>
    public class RLP
    {

        /// <summary>
        /// Allow for content up to size of 2^64 bytes *
        /// </summary>
        private static readonly double MAX_ITEM_LENGTH = Math.Pow(256, 8);

        /// <summary>
        /// Reason for threshold according to Vitalik Buterin:
        /// - 56 bytes maximizes the benefit of both options
        /// - if we went with 60 then we would have only had 4 slots for long strings
        /// so RLP would not have been able to store objects above 4gb
        /// - if we went with 48 then RLP would be fine for 2^128 space, but that's way too much
        /// - so 56 and 2^64 space seems like the right place to put the cutoff
        /// - also, that's where Bitcoin's varint does the cutof
        /// </summary>
        private const int SIZE_THRESHOLD = 56;

        /* RLP encoding rules are defined as follows:
		 * For a single byte whose value is in the [0x00, 0x7f] range, that byte is
		 * its own RLP encoding.
		 */

        /// <summary>
        /// [0x80]
        /// If a string is 0-55 bytes long, the RLP encoding consists of a single
        /// byte with value 0x80 plus the length of the string followed by the
        /// string. The range of the first byte is thus [0x80, 0xb7].
        /// </summary>
        private const int OFFSET_SHORT_ITEM = 0x80;

        /// <summary>
        /// [0xb7]
        /// If a string is more than 55 bytes long, the RLP encoding consists of a
        /// single byte with value 0xb7 plus the length of the length of the string
        /// in binary form, followed by the length of the string, followed by the
        /// string. For example, a length-1024 string would be encoded as
        /// \xb9\x04\x00 followed by the string. The range of the first byte is thus
        /// [0xb8, 0xbf].
        /// </summary>
        private const int OFFSET_LONG_ITEM = 0xb7;

        /// <summary>
        /// [0xc0]
        /// If the total payload of a list (i.e. the combined length of all its
        /// items) is 0-55 bytes long, the RLP encoding consists of a single byte
        /// with value 0xc0 plus the length of the list followed by the concatenation
        /// of the RLP encodings of the items. The range of the first byte is thus
        /// [0xc0, 0xf7].
        /// </summary>
        private const int OFFSET_SHORT_LIST = 0xc0;

        /// <summary>
        /// [0xf7]
        /// If the total payload of a list is more than 55 bytes long, the RLP
        /// encoding consists of a single byte with value 0xf7 plus the length of the
        /// length of the list in binary form, followed by the length of the list,
        /// followed by the concatenation of the RLP encodings of the items. The
        /// range of the first byte is thus [0xf8, 0xff].
        /// </summary>
        private const int OFFSET_LONG_LIST = 0xf7;

        public static readonly byte[] EMPTY_BYTE_ARRAY = new byte[0];
        public static readonly byte[] ZERO_BYTE_ARRAY = { 0 };


        /// <summary>
        /// Parses byte[] message into RLP items
        /// </summary>
        /// <param name="msgData">raw RLP data </param>
        /// <returns> RlpList: outcome of recursive RLP structure </returns>
        public static RLPCollection Decode(byte[] msgData)
        {
            var rlpCollection = new RLPCollection();
            Decode(msgData, 0, 0, msgData.Length, 1, rlpCollection);
            return rlpCollection;
        }

        public static IRLPElement DecodeFirstElement(byte[] msgData, int startPos)
        {
            var rlpCollection = new RLPCollection();
            Decode(msgData, 0, startPos, startPos + 1, 1, rlpCollection);
            return rlpCollection[0];
        }

        /// <summary>
        /// Decodes a message from a starting point to an end point
        /// </summary>
        public static void Decode(byte[] msgData, int level, int startPosition,
            int endPosition, int levelToIndex, RLPCollection rlpCollection)
        {
            
                if (msgData == null || msgData.Length == 0)
                {
                    return;
                }

                var currentData = new byte[endPosition - startPosition];
                Array.Copy(msgData, startPosition, currentData, 0, currentData.Length);

                try
                {

                var currentPosition = startPosition;
                
                while (currentPosition < endPosition)
                {
                    // It's a list with a payload more than 55 bytes
                    // data[0] - 0xF7 = how many next bytes allocated
                    // for the length of the list
                    if (msgData[currentPosition] > OFFSET_LONG_LIST)
                    {
                        var lengthOfLength = (byte)(msgData[currentPosition] - OFFSET_LONG_LIST);
                        var length = CalculateLength(lengthOfLength, msgData, currentPosition);

                        var rlpDataLength = lengthOfLength + length + 1;
                        var rlpData = new byte[rlpDataLength];

                        Array.Copy(msgData, currentPosition, rlpData, 0, rlpDataLength);
                        var newLevelCollection = new RLPCollection { RLPData = rlpData };

                        Decode(msgData, level + 1, currentPosition + lengthOfLength + 1,
                            currentPosition + rlpDataLength, levelToIndex,
                            newLevelCollection);
                        rlpCollection.Add(newLevelCollection);

                        currentPosition += rlpDataLength;
                        continue;
                    }

                    // It's a list with a payload less than 55 bytes
                    if ((msgData[currentPosition] >= OFFSET_SHORT_LIST)
                        && (msgData[currentPosition] <= OFFSET_LONG_LIST))
                    {
                        var length = msgData[currentPosition] - OFFSET_SHORT_LIST;
                        var rlpDataLength = length + 1;
                        var rlpData = new byte[length + 1];

                        Array.Copy(msgData, currentPosition, rlpData, 0, rlpDataLength);

                        var newLevelCollection = new RLPCollection { RLPData = rlpData };

                        if (length > 0)
                            Decode(msgData, level + 1, currentPosition + 1, currentPosition + rlpDataLength,
                                levelToIndex,
                                newLevelCollection);

                        rlpCollection.Add(newLevelCollection);

                        currentPosition += rlpDataLength;
                        continue;
                    }
                    // It's an item with a payload more than 55 bytes
                    // data[0] - 0xB7 = how much next bytes allocated for
                    // the length of the string
                    if (msgData[currentPosition] > OFFSET_LONG_ITEM
                        && msgData[currentPosition] < OFFSET_SHORT_LIST)
                    {
                        var lengthOfLength = (byte)(msgData[currentPosition] - OFFSET_LONG_ITEM);
                        var length = CalculateLength(lengthOfLength, msgData, currentPosition);

                        // now we can parse an item for data[1]..data[length]
                        var item = new byte[length];
                        Array.Copy(msgData, currentPosition + lengthOfLength + 1, item,
                            0, length);

                        var rlpPrefix = new byte[lengthOfLength + 1];
                        Array.Copy(msgData, currentPosition, rlpPrefix, 0,
                            lengthOfLength + 1);

                        var rlpItem = new RLPItem(item);
                        rlpCollection.Add(rlpItem);
                        currentPosition += lengthOfLength + length + 1;

                        continue;
                    }
                    // It's an item less than 55 bytes long,
                    // data[0] - 0x80 == length of the item
                    if (msgData[currentPosition] > OFFSET_SHORT_ITEM
                        && msgData[currentPosition] <= OFFSET_LONG_ITEM)
                    {
                        var length = (byte)(msgData[currentPosition] - OFFSET_SHORT_ITEM);

                        var item = new byte[length];
                        Array.Copy(msgData, currentPosition + 1, item, 0, length);

                        var rlpPrefix = new byte[2];
                        Array.Copy(msgData, currentPosition, rlpPrefix, 0, 2);

                        var rlpItem = new RLPItem(item);
                        rlpCollection.Add(rlpItem);
                        currentPosition += 1 + length;

                        continue;
                    }
                    // null item
                    if (msgData[currentPosition] == OFFSET_SHORT_ITEM)
                    {
                        var item = EMPTY_BYTE_ARRAY;
                        var rlpItem = new RLPItem(item);
                        rlpCollection.Add(rlpItem);
                        currentPosition += 1;
                        continue;
                    }
                    // single byte item
                    if (msgData[currentPosition] < OFFSET_SHORT_ITEM)
                    {
                        byte[] item = { msgData[currentPosition] };

                        var rlpItem = new RLPItem(item);
                        rlpCollection.Add(rlpItem);
                        currentPosition += 1;
                    }
                }
            }
            catch (OutOfMemoryException ex)
            {
                throw new Exception(
                    "Invalid RLP (excessive mem allocation while parsing) " + currentData.ToHex(), ex);
               
            }
            catch (Exception ex)
            {
                throw new Exception(
                    "Invalid RLP " + currentData.ToHex(), ex);
            }
        }

        public static byte[] EncodeByte(byte singleByte)
        {
            if ((singleByte) == 0)
            {
                return new byte[] { (byte)OFFSET_SHORT_ITEM };
            }
            else if ((singleByte) <= 0x7F)
            {
                return new byte[] { singleByte };
            }
            else
            {
                return new byte[] { (byte)(OFFSET_SHORT_ITEM + 1), singleByte };
            }
        }

        public static int ByteArrayToInt(byte[] bytes)
        {
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);

            return BitConverter.ToInt32(bytes, 0);
        }


        private static int CalculateLength(int lengthOfLength, byte[] msgData, int pos)
        {
            var pow = (byte)(lengthOfLength - 1);
            var length = 0;
            for (var i = 1; i <= lengthOfLength; ++i)
            {
                length += msgData[pos + i] << (8 * pow);
                pow--;
            }
            return length;
        }

        public static bool IsNullOrZeroArray(byte[] array)
        {
            return (array == null) || (array.Length == 0);
        }

        public static bool IsSingleZero(byte[] array)
        {
            return (array.Length == 1 && array[0] == 0);
        }

       
        public static byte[] EncodeElement(byte[] srcData)
        {
            if (IsNullOrZeroArray(srcData))
            {
                return new byte[] { OFFSET_SHORT_ITEM };
            }
            if (IsSingleZero(srcData))
            {
                return srcData;
            }
            if (srcData.Length == 1 && srcData[0] < 0x80)
            {
                return srcData;
            }
            if (srcData.Length < SIZE_THRESHOLD)
            {
                // length = 8X
                var length = (byte)(OFFSET_SHORT_ITEM + srcData.Length);
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
                {
                    lenBytes[byteNum - 1 - i] = (byte)(srcData.Length >> (8 * i));
                }
                // first byte = F7 + bytes.length
                var data = new byte[srcData.Length + 1 + byteNum];
                Array.Copy(srcData, 0, data, 1 + byteNum, srcData.Length);
                data[0] = (byte)(OFFSET_LONG_ITEM + byteNum);
                Array.Copy(lenBytes, 0, data, 1, lenBytes.Length);

                return data;
            }
        }

        public static byte[] EncodeList(params byte[][] items)
        {
            if (items == null)
            {
                return new[] { (byte)OFFSET_SHORT_LIST };
            }

            var totalLength = 0;
            for (int i = 0; i < items.Length; i++)
            {
                totalLength += items[i].Length;
            }

            byte[] data;

            int copyPos;

            if (totalLength < SIZE_THRESHOLD)
            {
                var dataLength = 1 + totalLength;
                data = new byte[dataLength];
                
                //single byte length
                data[0] = (byte)(OFFSET_SHORT_LIST + totalLength);
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
                {
                    lenBytes[byteNum - 1 - i] = (byte)(tmpLength >> (8 * i));
                }
                // first byte = F7 + bytes.length
                data = new byte[1 + lenBytes.Length + totalLength];

                data[0] = (byte)(OFFSET_LONG_LIST + byteNum);
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
    }
}
