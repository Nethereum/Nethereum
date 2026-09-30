using System;
using System.Collections.Generic;
using System.IO;
using Nethereum.Consensus.Ssz;

namespace Nethereum.Consensus.LightClient
{
    public static class LightClientStateCodec
    {
        private const uint Magic = 0x4C43_5354;
        private const byte Version = 1;

        public static byte[] Encode(LightClientState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            byte[] payload;
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                WriteOptionalBeacon(w, state.FinalizedHeader);
                WriteOptionalExecution(w, state.FinalizedExecutionPayload);
                WriteOptionalCommittee(w, state.CurrentSyncCommittee);
                WriteOptionalCommittee(w, state.NextSyncCommittee);
                w.Write(state.FinalizedSlot);
                w.Write(state.CurrentPeriod);
                w.Write(state.LastUpdated.ToUnixTimeMilliseconds());

                WriteOptionalBeacon(w, state.OptimisticHeader);
                WriteOptionalExecution(w, state.OptimisticExecutionPayload);
                w.Write(state.OptimisticSlot);
                w.Write(state.OptimisticLastUpdated.ToUnixTimeMilliseconds());

                var history = state.BlockHashHistory ?? new Dictionary<ulong, ProvenancedBlockHash>();
                w.Write(history.Count);
                foreach (var kv in history)
                {
                    w.Write(kv.Key);
                    w.Write((byte)kv.Value.Finality);
                    WriteBytes(w, kv.Value.BlockHash);
                }
                payload = ms.ToArray();
            }

            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write(Magic);
                w.Write(Version);
                w.Write(payload.Length);
                w.Write(payload);
                w.Write(Crc32(payload));
                return ms.ToArray();
            }
        }

        public static bool TryDecode(byte[] bytes, out LightClientState state)
        {
            state = null;
            if (bytes == null || bytes.Length < 13) return false;
            try
            {
                using var ms = new MemoryStream(bytes);
                using var r = new BinaryReader(ms);
                if (r.ReadUInt32() != Magic) return false;
                if (r.ReadByte() != Version) return false;
                int payloadLen = r.ReadInt32();
                if (payloadLen < 0 || payloadLen > bytes.Length - 13) return false;
                var payload = r.ReadBytes(payloadLen);
                if (payload.Length != payloadLen) return false;
                var crc = r.ReadUInt32();
                if (crc != Crc32(payload)) return false;

                using var pms = new MemoryStream(payload);
                using var pr = new BinaryReader(pms);
                var s = new LightClientState
                {
                    FinalizedHeader = ReadOptionalBeacon(pr),
                    FinalizedExecutionPayload = ReadOptionalExecution(pr),
                    CurrentSyncCommittee = ReadOptionalCommittee(pr),
                    NextSyncCommittee = ReadOptionalCommittee(pr),
                    FinalizedSlot = pr.ReadUInt64(),
                    CurrentPeriod = pr.ReadUInt64(),
                    LastUpdated = DateTimeOffset.FromUnixTimeMilliseconds(pr.ReadInt64()),
                    OptimisticHeader = ReadOptionalBeacon(pr),
                    OptimisticExecutionPayload = ReadOptionalExecution(pr),
                    OptimisticSlot = pr.ReadUInt64(),
                    OptimisticLastUpdated = DateTimeOffset.FromUnixTimeMilliseconds(pr.ReadInt64()),
                };

                int histCount = pr.ReadInt32();
                if (histCount < 0 || histCount > LightClientState.MaxBlockHashHistorySize) return false;
                for (int i = 0; i < histCount; i++)
                {
                    var block = pr.ReadUInt64();
                    var finality = (BlockHashFinality)pr.ReadByte();
                    var hash = ReadBytes(pr);
                    s.BlockHashHistory[block] = new ProvenancedBlockHash(hash, finality);
                }

                state = s;
                return true;
            }
            catch
            {
                state = null;
                return false;
            }
        }

        private static void WriteOptionalBeacon(BinaryWriter w, BeaconBlockHeader h)
        {
            if (h == null) { w.Write(false); return; }
            w.Write(true);
            WriteBytes(w, h.Encode());
        }

        private static BeaconBlockHeader ReadOptionalBeacon(BinaryReader r)
            => r.ReadBoolean() ? BeaconBlockHeader.Decode(ReadBytes(r)) : null;

        private static void WriteOptionalExecution(BinaryWriter w, ExecutionPayloadHeader h)
        {
            if (h == null) { w.Write(false); return; }
            w.Write(true);
            w.Write((int)h.Fork);
            WriteBytes(w, h.Encode(h.Fork));
        }

        private static ExecutionPayloadHeader ReadOptionalExecution(BinaryReader r)
        {
            if (!r.ReadBoolean()) return null;
            var fork = (ConsensusFork)r.ReadInt32();
            var header = ExecutionPayloadHeader.Decode(ReadBytes(r), fork);
            header.Fork = fork;
            return header;
        }

        private static void WriteOptionalCommittee(BinaryWriter w, SyncCommittee c)
        {
            if (c == null || c.PubKeys == null || c.PubKeys.Count == 0) { w.Write(false); return; }
            w.Write(true);
            WriteBytes(w, c.Encode());
        }

        private static SyncCommittee ReadOptionalCommittee(BinaryReader r)
            => r.ReadBoolean() ? SyncCommittee.Decode(ReadBytes(r)) : new SyncCommittee();

        private static void WriteBytes(BinaryWriter w, byte[] b)
        {
            b ??= Array.Empty<byte>();
            w.Write(b.Length);
            w.Write(b);
        }

        private static byte[] ReadBytes(BinaryReader r)
        {
            int len = r.ReadInt32();
            if (len < 0 || len > 1 << 24) throw new InvalidDataException("bad length");
            var b = r.ReadBytes(len);
            if (b.Length != len) throw new EndOfStreamException();
            return b;
        }

        private static uint Crc32(byte[] data)
        {
            uint crc = 0xFFFFFFFF;
            foreach (var b in data)
            {
                crc ^= b;
                for (int i = 0; i < 8; i++)
                    crc = (crc >> 1) ^ (0xEDB88320 & (uint)(-(int)(crc & 1)));
            }
            return ~crc;
        }
    }
}
