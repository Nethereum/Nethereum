using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32.SafeHandles;

namespace Nethereum.Freezer
{
    public sealed class RollingDataFiles : IFreezerDataFiles
    {
        public const uint DefaultMaxFileSize = 2u * 1024 * 1024 * 1024 - 1;

        private enum AccessMode { ReadOnly, Append }

        private readonly string _directory;
        private readonly string _baseName;
        private readonly string _extension;
        private readonly uint _maxFileSize;
        private readonly AccessMode _mode;
        private readonly Dictionary<ushort, SafeFileHandle> _handles = new();
        private readonly object _handlesLock = new();

        public ushort CurrentFileNumber { get; private set; }
        public uint CurrentFileLength => (uint)RandomAccess.GetLength(GetHandle(CurrentFileNumber));

        private RollingDataFiles(string directory, string baseName, bool useCompression, uint maxFileSize,
            AccessMode mode)
        {
            _directory = directory;
            _baseName = baseName;
            _extension = useCompression ? "cdat" : "rdat";
            _maxFileSize = maxFileSize;
            _mode = mode;

            CurrentFileNumber = DiscoverHighestExistingFile();
            GetHandle(CurrentFileNumber);
        }

        public static RollingDataFiles OpenForAppend(string directory, string baseName, bool useCompression,
            uint maxFileSize = DefaultMaxFileSize)
        {
            Directory.CreateDirectory(directory);
            return new RollingDataFiles(directory, baseName, useCompression, maxFileSize, AccessMode.Append);
        }

        public static RollingDataFiles OpenReadOnly(string directory, string baseName, bool useCompression,
            uint maxFileSize = DefaultMaxFileSize)
        {
            return new RollingDataFiles(directory, baseName, useCompression, maxFileSize, AccessMode.ReadOnly);
        }

        public (ushort fileNumber, uint offset) Append(ReadOnlySpan<byte> itemBytes)
        {
            RequireAppendMode();

            if (WouldExceedRoll((uint)itemBytes.Length))
                RollToNextFile();

            var handle = GetHandle(CurrentFileNumber);
            var offset = (uint)RandomAccess.GetLength(handle);
            RandomAccess.Write(handle, itemBytes, offset);
            return (CurrentFileNumber, offset);
        }

        private bool WouldExceedRoll(uint itemLength)
        {
            return (ulong)CurrentFileLength + itemLength > _maxFileSize;
        }

        private void RequireAppendMode()
        {
            if (_mode == AccessMode.ReadOnly)
                throw new InvalidOperationException("cannot write to a read-only freezer data file set");
        }

        private void RollToNextFile()
        {
            CurrentFileNumber = (ushort)(CurrentFileNumber + 1);
            GetHandle(CurrentFileNumber);
        }

        public byte[] Read(ushort fileNumber, uint offset, uint length)
        {
            var buffer = new byte[length];
            if (length == 0)
                return buffer;

            var handle = GetHandle(fileNumber);
            ReadExact(handle, buffer, offset);
            return buffer;
        }

        public void SyncCurrent()
        {
            RandomAccess.FlushToDisk(GetHandle(CurrentFileNumber));
        }

        public uint LengthOf(ushort fileNumber)
        {
            if (!File.Exists(PathFor(fileNumber)))
                return 0;

            return (uint)RandomAccess.GetLength(GetHandle(fileNumber));
        }

        public void TruncateHeadTo(ushort fileNumber, uint length)
        {
            RequireAppendMode();

            DeleteFilesAfter(fileNumber);

            CurrentFileNumber = fileNumber;
            var handle = GetHandle(fileNumber);
            RandomAccess.SetLength(handle, length);
        }

        private void DeleteFilesAfter(ushort fileNumber)
        {
            for (var n = fileNumber; n < ushort.MaxValue; n++)
            {
                var next = (ushort)(n + 1);
                var path = PathFor(next);
                if (!File.Exists(path))
                    break;

                CloseHandle(next);
                File.Delete(path);
            }
        }

        private ushort DiscoverHighestExistingFile()
        {
            ushort current = 0;
            while (current < ushort.MaxValue && File.Exists(PathFor((ushort)(current + 1))))
                current++;
            return current;
        }

        private string PathFor(ushort fileNumber) =>
            Path.Combine(_directory, $"{_baseName}.{fileNumber:D4}.{_extension}");

        private SafeFileHandle GetHandle(ushort fileNumber)
        {
            lock (_handlesLock)
            {
                if (_handles.TryGetValue(fileNumber, out var handle))
                    return handle;

                handle = _mode == AccessMode.ReadOnly
                    ? File.OpenHandle(PathFor(fileNumber), FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
                    : File.OpenHandle(PathFor(fileNumber), FileMode.OpenOrCreate, FileAccess.ReadWrite,
                        FileShare.ReadWrite);
                _handles[fileNumber] = handle;
                return handle;
            }
        }

        private void CloseHandle(ushort fileNumber)
        {
            lock (_handlesLock)
            {
                if (_handles.Remove(fileNumber, out var handle))
                    handle.Dispose();
            }
        }

        private static void ReadExact(SafeFileHandle handle, Span<byte> buffer, long fileOffset)
        {
            var totalRead = 0;
            while (totalRead < buffer.Length)
            {
                var read = RandomAccess.Read(handle, buffer.Slice(totalRead), fileOffset + totalRead);
                if (read == 0)
                    throw new EndOfStreamException("unexpected end of freezer data file");
                totalRead += read;
            }
        }

        public void Dispose()
        {
            lock (_handlesLock)
            {
                foreach (var handle in _handles.Values)
                    handle.Dispose();
                _handles.Clear();
            }
        }
    }
}
