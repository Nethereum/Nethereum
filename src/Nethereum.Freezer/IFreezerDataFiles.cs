using System;

namespace Nethereum.Freezer
{
    public interface IFreezerDataFiles : IDisposable
    {
        ushort CurrentFileNumber { get; }
        uint CurrentFileLength { get; }

        (ushort fileNumber, uint offset) Append(ReadOnlySpan<byte> itemBytes);
        byte[] Read(ushort fileNumber, uint offset, uint length);
        void SyncCurrent();
        void TruncateHeadTo(ushort fileNumber, uint length);

        uint LengthOf(ushort fileNumber);
    }
}
