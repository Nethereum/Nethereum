using System;
using System.Collections.Generic;
using System.IO;
using Nethereum.Util;

namespace Nethereum.EVM.Witness
{
    /// <summary>
    /// Binary block witness format for zkVM block-proof execution. v1 layout:
    ///   [u8  version = 1]
    ///   [u8  flags]            bit0 VerifyWitnessProofs
    ///                          bit1 ComputePostStateRoot
    ///                          bit2 ProduceBlockCommitments
    ///   [u8  fork]             HardforkName enum; Unspecified is rejected
    ///   [BlockContext]         number, ts, baseFee, gasLimit, chainId,
    ///                          coinbase, difficulty, parentHash, extraData,
    ///                          mixHash, nonce
    ///   [u16 txCount][Transaction*]
    ///   [u16 accountCount][Account*]
    ///   [pad to 8-byte alignment]
    ///
    /// BLOCKHASH data is NOT carried separately — it lives in the EIP-2935
    /// history contract's storage (address 0x0000F90827F1C53a10cb7A02335B175320002935,
    /// slot = blockNumber % 8191) and is covered by the account/storage section.
    /// </summary>
    public static class BinaryBlockWitness
    {
        public const byte VERSION = 1;

        public static byte[] Serialize(BlockWitnessData data)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);

            w.Write(VERSION);
            // Flags byte:
            //   bit 0: VerifyWitnessProofs
            //   bit 1: ComputePostStateRoot
            //   bit 2: ProduceBlockCommitments
            //   bit 3: StateTree (0=Patricia, 1=Binary)
            //   bit 4-5: HashFunction (00=Keccak, 01=Blake3, 10=Poseidon, 11=Sha256)
            byte flags = 0;
            if (data.VerifyWitnessProofs) flags |= 1;
            if (data.ComputePostStateRoot) flags |= 2;
            if (data.ProduceBlockCommitments) flags |= 4;
            var features = data.Features ?? new BlockFeatureConfig();
            if (features.StateTree == WitnessStateTreeType.Binary) flags |= 8;
            flags |= (byte)(((byte)features.HashFunction & 0x03) << 4);
            w.Write(flags);

            // Fork (HardforkName enum, required)
            var fork = data.Features?.Fork ?? HardforkName.Unspecified;
            if (fork == HardforkName.Unspecified)
                throw new InvalidOperationException(
                    "BlockWitnessData.Features.Fork must be set before serialising — Unspecified is not a valid wire value.");
            w.Write((byte)fork);

            // Block context
            w.Write((ulong)data.BlockNumber);
            w.Write((ulong)data.Timestamp);
            w.Write((ulong)data.BaseFee);
            w.Write((ulong)data.BlockGasLimit);
            w.Write((ulong)data.ChainId);
            BinaryWitness.WriteString(w, data.Coinbase ?? "0x0000000000000000000000000000000000000000");
            BinaryWitness.WriteFixedBytes(w, data.Difficulty, 32);
            BinaryWitness.WriteFixedBytes(w, data.ParentHash, 32);
            BinaryWitness.WriteBytes(w, data.ExtraData ?? new byte[0]);
            BinaryWitness.WriteFixedBytes(w, data.MixHash, 32);
            BinaryWitness.WriteFixedBytes(w, data.Nonce, 8);

            // Transactions — From + signed RLP bytes
            var txs = data.Transactions ?? new List<BlockWitnessTransaction>();
            w.Write((ushort)txs.Count);
            foreach (var tx in txs)
            {
                BinaryWitness.WriteString(w, tx.From ?? "");
                BinaryWitness.WriteBytes(w, tx.RlpEncoded ?? new byte[0]);
            }

            // Accounts (same as v1)
            var accounts = data.Accounts ?? new List<WitnessAccount>();
            w.Write((ushort)accounts.Count);
            foreach (var account in accounts)
            {
                BinaryWitness.WriteString(w, account.Address);
                BinaryWitness.WriteBytes32(w, account.Balance);
                w.Write((ulong)account.Nonce);
                BinaryWitness.WriteBytes(w, account.Code ?? new byte[0]);

                var storage = account.Storage ?? new List<WitnessStorageSlot>();
                w.Write((ushort)storage.Count);
                foreach (var slot in storage)
                {
                    BinaryWitness.WriteBytes32(w, slot.Key);
                    BinaryWitness.WriteBytes32(w, slot.Value);
                }
            }

            // Pad to 8-byte alignment (required by Zisk emulator/prover)
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

            var stateTree = (flags & 8) != 0 ? WitnessStateTreeType.Binary : WitnessStateTreeType.Patricia;
            var hashFunction = (WitnessHashFunction)((flags >> 4) & 0x03);

            // Fork
            var fork = (HardforkName)data[offset++];
            if (fork == HardforkName.Unspecified)
                throw new InvalidOperationException("Witness has Fork=Unspecified — producer did not stamp a fork.");
            result.Features = new BlockFeatureConfig
            {
                Fork = fork,
                StateTree = stateTree,
                HashFunction = hashFunction
            };

            // Block context
            result.BlockNumber = (long)BinaryWitness.ReadU64(data, ref offset);
            result.Timestamp = (long)BinaryWitness.ReadU64(data, ref offset);
            result.BaseFee = (long)BinaryWitness.ReadU64(data, ref offset);
            result.BlockGasLimit = (long)BinaryWitness.ReadU64(data, ref offset);
            result.ChainId = (long)BinaryWitness.ReadU64(data, ref offset);
            result.Coinbase = BinaryWitness.ReadString(data, ref offset);
            result.Difficulty = BinaryWitness.ReadFixedBytes(data, ref offset, 32);
            result.ParentHash = BinaryWitness.ReadFixedBytes(data, ref offset, 32);
            result.ExtraData = BinaryWitness.ReadBytesArray(data, ref offset);
            result.MixHash = BinaryWitness.ReadFixedBytes(data, ref offset, 32);
            result.Nonce = BinaryWitness.ReadFixedBytes(data, ref offset, 8);

            // Transactions — From + signed RLP bytes
            int txCount = BinaryWitness.ReadU16(data, ref offset);
            result.Transactions = new List<BlockWitnessTransaction>(txCount);
            for (int i = 0; i < txCount; i++)
            {
                var tx = new BlockWitnessTransaction();
                tx.From = BinaryWitness.ReadString(data, ref offset);
                tx.RlpEncoded = BinaryWitness.ReadBytesArray(data, ref offset);
                result.Transactions.Add(tx);
            }

            // Accounts (same as v1)
            int accountCount = BinaryWitness.ReadU16(data, ref offset);
            result.Accounts = new List<WitnessAccount>(accountCount);
            for (int i = 0; i < accountCount; i++)
            {
                var account = new WitnessAccount();
                account.Address = BinaryWitness.ReadString(data, ref offset);
                account.Balance = BinaryWitness.ReadBytes32AsEvmUInt256(data, ref offset);
                account.Nonce = (long)BinaryWitness.ReadU64(data, ref offset);
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
