using System;

namespace Nethereum.Freezer
{
    public readonly struct FreezerIndexEntry
    {
        public const int Size = 6;

        public ushort FileNumber { get; }
        public uint Offset { get; }

        public FreezerIndexEntry(ushort fileNumber, uint offset)
        {
            FileNumber = fileNumber;
            Offset = offset;
        }

        public void WriteTo(Span<byte> destination)
        {
            if (destination.Length < Size)
                throw new ArgumentException($"destination must be at least {Size} bytes long", nameof(destination));

            destination[0] = (byte)(FileNumber >> 8);
            destination[1] = (byte)FileNumber;
            destination[2] = (byte)(Offset >> 24);
            destination[3] = (byte)(Offset >> 16);
            destination[4] = (byte)(Offset >> 8);
            destination[5] = (byte)Offset;
        }

        public static FreezerIndexEntry ReadFrom(ReadOnlySpan<byte> source)
        {
            if (source.Length < Size)
                throw new ArgumentException($"source must be at least {Size} bytes long", nameof(source));

            var fileNumber = (ushort)((source[0] << 8) | source[1]);
            var offset = ((uint)source[2] << 24) | ((uint)source[3] << 16) | ((uint)source[4] << 8) | source[5];
            return new FreezerIndexEntry(fileNumber, offset);
        }
    }
}
