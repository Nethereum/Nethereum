using System;
using System.Collections.Generic;
using System.IO;
using Nethereum.Util;

namespace Nethereum.EVM.Witness
{
    public static class BinaryBlockWitness
    {
        public const byte VERSION = 3;

        public static byte[] Serialize(BlockWitnessData data)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);

            w.Write(VERSION);
            byte flags = 0;
            if (data.VerifyWitnessProofs) flags |= 1;
            if (data.ComputePostStateRoot) flags |= 2;
            if (data.ProduceBlockCommitments) flags |= 4;
            if (data.SkipsRequestSystemCalls) flags |= 0x40;
            var features = data.Features ?? new BlockFeatureConfig();
            if (features.StateTree == WitnessStateTreeType.Binary) flags |= 8;
            flags |= (byte)(((byte)features.HashFunction & 0x03) << 4);
            w.Write(flags);

            var fork = data.Features?.Fork ?? HardforkName.Unspecified;
            if (fork == HardforkName.Unspecified)
                throw new InvalidOperationException(
                    "BlockWitnessData.Features.Fork must be set before serialising — Unspecified is not a valid wire value.");
            w.Write((byte)fork);

            w.Write((ulong)data.BlockNumber);
            w.Write((ulong)data.Timestamp);
            w.Write((ulong)data.BaseFee);
            w.Write((ulong)data.BlockGasLimit);
            w.Write((ulong)data.ChainId);
            BinaryWitness.WriteString(w, data.Coinbase ?? "0x0000000000000000000000000000000000000000");
            BinaryWitness.WriteFixedBytes(w, data.Difficulty, 32);
            BinaryWitness.WriteFixedBytes(w, data.PreStateRoot, 32);
            BinaryWitness.WriteFixedBytes(w, data.ParentHash, 32);
            BinaryWitness.WriteBytes(w, data.ExtraData ?? new byte[0]);
            BinaryWitness.WriteFixedBytes(w, data.MixHash, 32);
            BinaryWitness.WriteFixedBytes(w, data.Nonce, 8);

            Sections.WitnessSections.Write(w, data);

            WitnessTransactionRecords.Write(w, data.Transactions);

            var accounts = data.Accounts ?? new List<WitnessAccount>();
            w.Write((ushort)accounts.Count);
            foreach (var account in accounts)
            {
                BinaryWitness.WriteString(w, account.Address);
                BinaryWitness.WriteBytes32(w, account.Balance);
                w.Write(account.Nonce);
                BinaryWitness.WriteBytes(w, account.Code ?? new byte[0]);

                var storage = account.Storage ?? new List<WitnessStorageSlot>();
                w.Write((ushort)storage.Count);
                foreach (var slot in storage)
                {
                    BinaryWitness.WriteBytes32(w, slot.Key);
                    BinaryWitness.WriteBytes32(w, slot.Value);
                }
            }

            var pos = (int)ms.Position;
            var padding = (8 - (pos % 8)) % 8;
            for (int i = 0; i < padding; i++)
                w.Write((byte)0);

            return ms.ToArray();
        }

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        public static BlockWitnessData Deserialize(ReadOnlySpan<byte> data)
#else
        public static BlockWitnessData Deserialize(byte[] data)
#endif
        {
            var result = new BlockWitnessData();
            int offset = 0;

            byte version = data[offset++];
            if (version != VERSION)
                throw new InvalidOperationException("Expected witness version " + VERSION + ", got: " + version);

            byte flags = data[offset++];
            result.VerifyWitnessProofs = (flags & 1) != 0;
            result.ComputePostStateRoot = (flags & 2) != 0;
            result.ProduceBlockCommitments = (flags & 4) != 0;
            result.SkipsRequestSystemCalls = (flags & 0x40) != 0;

            var stateTree = (flags & 8) != 0 ? WitnessStateTreeType.Binary : WitnessStateTreeType.Patricia;
            var hashFunction = (WitnessHashFunction)((flags >> 4) & 0x03);

            var fork = (HardforkName)data[offset++];
            if (fork == HardforkName.Unspecified)
                throw new InvalidOperationException("Witness has Fork=Unspecified — producer did not stamp a fork.");
            result.Features = new BlockFeatureConfig
            {
                Fork = fork,
                StateTree = stateTree,
                HashFunction = hashFunction
            };

            result.BlockNumber = (long)BinaryWitness.ReadU64(data, ref offset);
            result.Timestamp = (long)BinaryWitness.ReadU64(data, ref offset);
            result.BaseFee = (long)BinaryWitness.ReadU64(data, ref offset);
            result.BlockGasLimit = (long)BinaryWitness.ReadU64(data, ref offset);
            result.ChainId = (long)BinaryWitness.ReadU64(data, ref offset);
            result.Coinbase = BinaryWitness.ReadString(data, ref offset);
            result.Difficulty = BinaryWitness.ReadFixedBytes(data, ref offset, 32);
            result.PreStateRoot = BinaryWitness.ReadFixedBytes(data, ref offset, 32);
            result.ParentHash = BinaryWitness.ReadFixedBytes(data, ref offset, 32);
            result.ExtraData = BinaryWitness.ReadBytesArray(data, ref offset);
            result.MixHash = BinaryWitness.ReadFixedBytes(data, ref offset, 32);
            result.Nonce = BinaryWitness.ReadFixedBytes(data, ref offset, 8);

            Sections.WitnessSections.Read(data, ref offset, result);

            result.Transactions = WitnessTransactionRecords.Read(data, ref offset);

            int accountCount = BinaryWitness.ReadU16(data, ref offset);
            result.Accounts = new List<WitnessAccount>(accountCount);
            for (int i = 0; i < accountCount; i++)
            {
                var account = new WitnessAccount();
                account.Address = BinaryWitness.ReadString(data, ref offset);
                account.Balance = BinaryWitness.ReadBytes32AsEvmUInt256(data, ref offset);
                account.Nonce = BinaryWitness.ReadU64(data, ref offset);
                account.Code = BinaryWitness.ReadBytesArray(data, ref offset);

                int storageCount = BinaryWitness.ReadU16(data, ref offset);
                account.Storage = new List<WitnessStorageSlot>(storageCount);
                for (int j = 0; j < storageCount; j++)
                {
                    account.Storage.Add(new WitnessStorageSlot
                    {
                        Key = BinaryWitness.ReadBytes32AsEvmUInt256(data, ref offset),
                        Value = BinaryWitness.ReadBytes32AsEvmUInt256(data, ref offset)
                    });
                }
                result.Accounts.Add(account);
            }

            return result;
        }
    }
}
