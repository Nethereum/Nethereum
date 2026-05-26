using System;
using System.Collections.Generic;
using System.IO;

namespace Nethereum.CoreChain.DataAvailability
{
    public sealed class AnchorPayloadHeader
    {
        public byte Version { get; init; }
        public byte StateModel { get; init; }
        public byte AnchorKind { get; init; }
        public byte SectionCount { get; init; }

        public const int HeaderSize = 4;
    }

    public sealed class AnchorPayload
    {
        public AnchorPayloadHeader Header { get; init; }
        public List<AnchorPayloadSection> Sections { get; init; } = new();
    }

    public static class AnchorPayloadCodec
    {
        public const byte CurrentVersion = 1;
        public const int SectionHeaderSize = 6;

        public static byte[] Encode(AnchorPayload payload)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            bw.Write(payload.Header.Version);
            bw.Write(payload.Header.StateModel);
            bw.Write(payload.Header.AnchorKind);
            bw.Write((byte)payload.Sections.Count);

            foreach (var section in payload.Sections)
            {
                bw.Write((ushort)section.Type);
                var bytes = section.Bytes ?? Array.Empty<byte>();
                bw.Write((uint)bytes.Length);
                bw.Write(bytes);
            }

            return ms.ToArray();
        }

        public static AnchorPayload Decode(byte[] data)
        {
            if (data == null || data.Length < AnchorPayloadHeader.HeaderSize)
                throw new ArgumentException("Data too short for anchor payload header");

            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            var header = new AnchorPayloadHeader
            {
                Version = br.ReadByte(),
                StateModel = br.ReadByte(),
                AnchorKind = br.ReadByte(),
                SectionCount = br.ReadByte()
            };

            if (header.Version != CurrentVersion)
                throw new NotSupportedException($"Unsupported anchor payload version: {header.Version}");

            var sections = new List<AnchorPayloadSection>(header.SectionCount);
            for (int i = 0; i < header.SectionCount; i++)
            {
                if (ms.Position + 6 > ms.Length)
                    throw new ArgumentException($"Data truncated at section {i}");

                var type = (AnchorPayloadSectionType)br.ReadUInt16();
                var length = br.ReadUInt32();

                if (ms.Position + length > ms.Length)
                    throw new ArgumentException($"Section {i} (type {type}) declares {length} bytes but only {ms.Length - ms.Position} remain");

                var bytes = br.ReadBytes((int)length);
                sections.Add(new AnchorPayloadSection { Type = type, Bytes = bytes });
            }

            return new AnchorPayload { Header = header, Sections = sections };
        }

        public static AnchorPayload Build(
            StateModel stateModel,
            AnchorKind anchorKind,
            IReadOnlyList<AnchorPayloadSection> sections)
        {
            if (sections.Count > 255)
                throw new ArgumentException($"Too many sections ({sections.Count}); max 255");

            return new AnchorPayload
            {
                Header = new AnchorPayloadHeader
                {
                    Version = CurrentVersion,
                    StateModel = (byte)stateModel,
                    AnchorKind = (byte)anchorKind,
                    SectionCount = (byte)sections.Count
                },
                Sections = new List<AnchorPayloadSection>(sections)
            };
        }

        public static AnchorPayloadSection FindSection(AnchorPayload payload, AnchorPayloadSectionType type)
        {
            foreach (var s in payload.Sections)
                if (s.Type == type) return s;
            return null;
        }

        public static List<AnchorPayloadSection> FindSections(AnchorPayload payload, AnchorPayloadSectionType type)
        {
            var result = new List<AnchorPayloadSection>();
            foreach (var s in payload.Sections)
                if (s.Type == type) result.Add(s);
            return result;
        }
    }
}
