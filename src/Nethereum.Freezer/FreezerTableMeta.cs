using System;
using System.IO;
using Nethereum.RLP;

namespace Nethereum.Freezer
{
    public class FreezerTableMeta
    {
        public const ushort SupportedVersion = 2;

        public ushort Version { get; }
        public long VirtualTail { get; }
        public long FlushOffset { get; }

        public FreezerTableMeta(ushort version, long virtualTail, long flushOffset)
        {
            Version = version;
            VirtualTail = virtualTail;
            FlushOffset = flushOffset;
        }

        public byte[] Encode()
        {
            var fields = new byte[][]
            {
                ((int)Version).ToBytesForRLPEncoding(),
                ((ulong)VirtualTail).ToBytesForRLPEncoding(),
                ((ulong)FlushOffset).ToBytesForRLPEncoding(),
            };
            return RLP.RLP.EncodeDataItemsAsElementOrListAndCombineAsList(fields);
        }

        public static FreezerTableMeta Decode(ReadOnlySpan<byte> rlp)
        {
            var fields = DecodeFields(rlp);
            if (fields.Count != 3)
                throw new FreezerFormatException($"freezer .meta must have exactly 3 fields, got {fields.Count}");

            var version = (ushort)fields[0].RLPData.ToIntFromRLPDecoded();
            if (version != SupportedVersion)
                throw new FreezerFormatException(
                    $"unsupported freezer .meta version {version}; only v{SupportedVersion} is supported (declared divergence, spec §A2)");

            var virtualTail = ReadNonNegativeLong(fields[1].RLPData, "virtualTail");
            var flushOffset = ReadNonNegativeLong(fields[2].RLPData, "flushOffset");

            return new FreezerTableMeta(version, virtualTail, flushOffset);
        }

        private static RLPCollection DecodeFields(ReadOnlySpan<byte> rlp)
        {
            try
            {
                return (RLPCollection)RLP.RLP.Decode(rlp.ToArray());
            }
            catch (Exception ex) when (!(ex is FreezerFormatException))
            {
                throw new FreezerFormatException("freezer .meta is not valid RLP", ex);
            }
        }

        private static long ReadNonNegativeLong(byte[] rlpData, string fieldName)
        {
            var value = rlpData.ToULongFromRLPDecoded();
            if (value > long.MaxValue)
                throw new FreezerFormatException(
                    $"freezer .meta field '{fieldName}' ({value}) exceeds long.MaxValue");

            return (long)value;
        }

        public void WriteAtomic(string path)
        {
            var tempPath = path + ".tmp";
            try
            {
                using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    WriteTempContent(stream, Encode());
                    stream.Flush(flushToDisk: true);
                }

                File.Move(tempPath, path, overwrite: true);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        protected virtual void WriteTempContent(Stream stream, byte[] bytes)
        {
            stream.Write(bytes, 0, bytes.Length);
        }
    }
}
