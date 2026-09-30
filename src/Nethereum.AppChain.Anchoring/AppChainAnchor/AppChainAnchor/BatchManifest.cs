using System;
using System.Collections.Generic;
using System.IO;
using Nethereum.Util;

namespace Nethereum.AppChain.Anchoring.AppChainAnchor
{
    public class ManifestCore
    {
        public byte[] AppChainGenesisHash { get; set; }
        public byte AnchorVersion { get; set; }
        public byte ProofSystem { get; set; }
        public ulong StartBlock { get; set; }
        public ulong EndBlock { get; set; }
        public byte[] PreStateRoot { get; set; }
        public byte[] PostStateRoot { get; set; }
        public byte[] EndBlockHash { get; set; }
        public byte[] BlockHashesRoot { get; set; }
        public byte[] TxDataBundleHash { get; set; }
    }

    public class ProofEnvelope
    {
        public byte[] ProofBytes { get; set; }
        public byte[] ElfHash { get; set; }
        public string ProverMode { get; set; }
        public long GasUsed { get; set; }
    }

    public class BatchManifest
    {
        public ManifestCore Core { get; set; }
        public ProofEnvelope Proof { get; set; }
        public List<byte[]> BlockHeaders { get; set; } = new();

        public byte[] ComputeManifestHash()
        {
            if (Core == null) return new byte[32];

            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            WriteBytes(bw, Core.AppChainGenesisHash);
            bw.Write(Core.AnchorVersion);
            bw.Write(Core.ProofSystem);
            bw.Write(Core.StartBlock);
            bw.Write(Core.EndBlock);
            WriteBytes(bw, Core.PreStateRoot);
            WriteBytes(bw, Core.PostStateRoot);
            WriteBytes(bw, Core.EndBlockHash);
            WriteBytes(bw, Core.BlockHashesRoot);
            WriteBytes(bw, Core.TxDataBundleHash);

            return Sha3Keccack.Current.CalculateHash(ms.ToArray());
        }

        public byte[] Serialize()
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            bw.Write((byte)1);
            WriteBytes(bw, Core?.AppChainGenesisHash);
            bw.Write(Core?.AnchorVersion ?? (byte)0);
            bw.Write(Core?.ProofSystem ?? (byte)0);
            bw.Write(Core?.StartBlock ?? 0UL);
            bw.Write(Core?.EndBlock ?? 0UL);
            WriteBytes(bw, Core?.PreStateRoot);
            WriteBytes(bw, Core?.PostStateRoot);
            WriteBytes(bw, Core?.EndBlockHash);
            WriteBytes(bw, Core?.BlockHashesRoot);
            WriteBytes(bw, Core?.TxDataBundleHash);
            WriteBytes(bw, Proof?.ProofBytes);
            WriteBytes(bw, Proof?.ElfHash);
            bw.Write(BlockHeaders?.Count ?? 0);
            if (BlockHeaders != null)
                foreach (var h in BlockHeaders) WriteBytes(bw, h);
            return ms.ToArray();
        }

        public static BatchManifest Deserialize(byte[] data)
        {
            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            var version = br.ReadByte();
            if (version != 1) throw new NotSupportedException($"Manifest version {version}");

            var manifest = new BatchManifest
            {
                Core = new ManifestCore
                {
                    AppChainGenesisHash = ReadBytes(br),
                    AnchorVersion = br.ReadByte(),
                    ProofSystem = br.ReadByte(),
                    StartBlock = br.ReadUInt64(),
                    EndBlock = br.ReadUInt64(),
                    PreStateRoot = ReadBytes(br),
                    PostStateRoot = ReadBytes(br),
                    EndBlockHash = ReadBytes(br),
                    BlockHashesRoot = ReadBytes(br),
                    TxDataBundleHash = ReadBytes(br)
                },
                Proof = new ProofEnvelope
                {
                    ProofBytes = ReadBytes(br),
                    ElfHash = ReadBytes(br)
                }
            };

            var headerCount = br.ReadInt32();
            for (int i = 0; i < headerCount; i++)
                manifest.BlockHeaders.Add(ReadBytes(br));
            return manifest;
        }

        private static void WriteBytes(BinaryWriter bw, byte[] data)
        {
            if (data == null || data.Length == 0) { bw.Write(0); return; }
            bw.Write(data.Length);
            bw.Write(data);
        }

        private static byte[] ReadBytes(BinaryReader br)
        {
            var len = br.ReadInt32();
            if (len <= 0) return Array.Empty<byte>();
            return br.ReadBytes(len);
        }
    }
}
