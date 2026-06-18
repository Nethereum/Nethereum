using System;
using System.Collections.Generic;
using Nethereum.Ssz;

namespace Nethereum.Consensus.Ssz
{
    public class ExecutionPayloadHeader
    {
        private const int FeeRecipientLength = 20;
        private const int ExtraDataMaxLength = 32;

        public ConsensusFork Fork { get; set; } = ConsensusFork.Phase0;

        public byte[] ParentHash { get; set; } = new byte[SszBasicTypes.RootLength];
        public byte[] FeeRecipient { get; set; } = new byte[FeeRecipientLength];
        public byte[] StateRoot { get; set; } = new byte[SszBasicTypes.RootLength];
        public byte[] ReceiptsRoot { get; set; } = new byte[SszBasicTypes.RootLength];
        public byte[] LogsBloom { get; set; } = new byte[SszBasicTypes.LogsBloomLength];
        public byte[] PrevRandao { get; set; } = new byte[SszBasicTypes.RootLength];
        public ulong BlockNumber { get; set; }
        public ulong GasLimit { get; set; }
        public ulong GasUsed { get; set; }
        public ulong Timestamp { get; set; }
        public byte[] ExtraData { get; set; } = Array.Empty<byte>();
        public byte[] BaseFeePerGas { get; set; } = new byte[SszBasicTypes.RootLength];
        public byte[] BlockHash { get; set; } = new byte[SszBasicTypes.RootLength];
        public byte[] TransactionsRoot { get; set; } = new byte[SszBasicTypes.RootLength];
        public byte[] WithdrawalsRoot { get; set; } = new byte[SszBasicTypes.RootLength];
        public ulong BlobGasUsed { get; set; }
        public ulong ExcessBlobGas { get; set; }

        // Fixed-section length depends on which post-Bellatrix fields are present.
        // Bellatrix base: parent_hash + fee_recipient + state_root + receipts_root + logs_bloom
        //                 + prev_randao + block_number + gas_limit + gas_used + timestamp
        //                 + extra_data_offset + base_fee_per_gas + block_hash + transactions_root.
        // Capella adds withdrawals_root. Deneb adds blob_gas_used + excess_blob_gas.
        private static int ComputeFixedSectionLength(ConsensusFork fork)
        {
            var length =
                SszBasicTypes.RootLength +          // parent_hash
                FeeRecipientLength +
                SszBasicTypes.RootLength +          // state_root
                SszBasicTypes.RootLength +          // receipts_root
                SszBasicTypes.LogsBloomLength +
                SszBasicTypes.RootLength +          // prev_randao
                sizeof(ulong) * 4 +                  // block_number, gas_limit, gas_used, timestamp
                sizeof(uint) +                       // extra_data offset
                SszBasicTypes.RootLength +          // base_fee_per_gas
                SszBasicTypes.RootLength +          // block_hash
                SszBasicTypes.RootLength;           // transactions_root
            if (LightClientForkSpec.HasWithdrawalsRoot(fork))
                length += SszBasicTypes.RootLength; // withdrawals_root
            if (LightClientForkSpec.HasBlobGasFields(fork))
                length += sizeof(ulong) * 2;        // blob_gas_used + excess_blob_gas
            return length;
        }

        public byte[] Encode() => Encode(Fork);

        public byte[] Encode(ConsensusFork fork)
        {
            if (!LightClientForkSpec.HasExecutionPayloadHeader(fork))
                throw new InvalidOperationException(
                    $"ExecutionPayloadHeader is not part of fork {fork}; pre-Bellatrix forks have no execution payload.");

            AssertForkConsistency(fork);

            var fixedLen = ComputeFixedSectionLength(fork);

            using var fixedWriter = new SszWriter();
            fixedWriter.WriteFixedBytes(ParentHash, SszBasicTypes.RootLength);
            fixedWriter.WriteFixedBytes(FeeRecipient, FeeRecipientLength);
            fixedWriter.WriteFixedBytes(StateRoot, SszBasicTypes.RootLength);
            fixedWriter.WriteFixedBytes(ReceiptsRoot, SszBasicTypes.RootLength);
            fixedWriter.WriteFixedBytes(LogsBloom, SszBasicTypes.LogsBloomLength);
            fixedWriter.WriteFixedBytes(PrevRandao, SszBasicTypes.RootLength);
            fixedWriter.WriteUInt64(BlockNumber);
            fixedWriter.WriteUInt64(GasLimit);
            fixedWriter.WriteUInt64(GasUsed);
            fixedWriter.WriteUInt64(Timestamp);

            fixedWriter.WriteUInt32((uint)fixedLen);

            fixedWriter.WriteFixedBytes(BaseFeePerGas, SszBasicTypes.RootLength);
            fixedWriter.WriteFixedBytes(BlockHash, SszBasicTypes.RootLength);
            fixedWriter.WriteFixedBytes(TransactionsRoot, SszBasicTypes.RootLength);

            if (LightClientForkSpec.HasWithdrawalsRoot(fork))
                fixedWriter.WriteFixedBytes(WithdrawalsRoot, SszBasicTypes.RootLength);
            if (LightClientForkSpec.HasBlobGasFields(fork))
            {
                fixedWriter.WriteUInt64(BlobGasUsed);
                fixedWriter.WriteUInt64(ExcessBlobGas);
            }

            var extra = ExtraData ?? Array.Empty<byte>();
            if (extra.Length > ExtraDataMaxLength)
            {
                throw new InvalidOperationException($"ExtraData length {extra.Length} exceeds maximum {ExtraDataMaxLength}.");
            }

            return Combine(fixedWriter.ToArray(), extra);
        }

        public static ExecutionPayloadHeader Decode(ReadOnlySpan<byte> data, ConsensusFork fork)
        {
            if (!LightClientForkSpec.HasExecutionPayloadHeader(fork))
                throw new InvalidOperationException(
                    $"ExecutionPayloadHeader is not part of fork {fork}; pre-Bellatrix forks have no execution payload.");

            var fixedLen = ComputeFixedSectionLength(fork);

            var reader = new SszReader(data);
            var parentHash = reader.ReadFixedBytes(SszBasicTypes.RootLength);
            var feeRecipient = reader.ReadFixedBytes(FeeRecipientLength);
            var stateRoot = reader.ReadFixedBytes(SszBasicTypes.RootLength);
            var receiptsRoot = reader.ReadFixedBytes(SszBasicTypes.RootLength);
            var logsBloom = reader.ReadFixedBytes(SszBasicTypes.LogsBloomLength);
            var prevRandao = reader.ReadFixedBytes(SszBasicTypes.RootLength);
            var blockNumber = reader.ReadUInt64();
            var gasLimit = reader.ReadUInt64();
            var gasUsed = reader.ReadUInt64();
            var timestamp = reader.ReadUInt64();
            var extraDataOffset = reader.ReadUInt32();
            var baseFeePerGas = reader.ReadFixedBytes(SszBasicTypes.RootLength);
            var blockHash = reader.ReadFixedBytes(SszBasicTypes.RootLength);
            var transactionsRoot = reader.ReadFixedBytes(SszBasicTypes.RootLength);
            var withdrawalsRoot = LightClientForkSpec.HasWithdrawalsRoot(fork)
                ? reader.ReadFixedBytes(SszBasicTypes.RootLength)
                : new byte[SszBasicTypes.RootLength];
            var blobGasUsed = LightClientForkSpec.HasBlobGasFields(fork) ? reader.ReadUInt64() : 0UL;
            var excessBlobGas = LightClientForkSpec.HasBlobGasFields(fork) ? reader.ReadUInt64() : 0UL;

            if (extraDataOffset != fixedLen)
            {
                throw new InvalidOperationException($"ExtraData offset {extraDataOffset} is invalid for fork {fork} (expected {fixedLen}).");
            }
            var extraStart = (int)extraDataOffset;
            if (extraStart > data.Length)
            {
                throw new InvalidOperationException("ExtraData offset exceeds buffer length.");
            }
            var extraSpan = data.Slice(extraStart);
            if (extraSpan.Length > ExtraDataMaxLength)
            {
                throw new InvalidOperationException($"ExtraData length {extraSpan.Length} exceeds maximum {ExtraDataMaxLength}.");
            }
            var extraData = extraSpan.ToArray();

            return new ExecutionPayloadHeader
            {
                Fork = fork,
                ParentHash = parentHash,
                FeeRecipient = feeRecipient,
                StateRoot = stateRoot,
                ReceiptsRoot = receiptsRoot,
                LogsBloom = logsBloom,
                PrevRandao = prevRandao,
                BlockNumber = blockNumber,
                GasLimit = gasLimit,
                GasUsed = gasUsed,
                Timestamp = timestamp,
                ExtraData = extraData,
                BaseFeePerGas = baseFeePerGas,
                BlockHash = blockHash,
                TransactionsRoot = transactionsRoot,
                WithdrawalsRoot = withdrawalsRoot,
                BlobGasUsed = blobGasUsed,
                ExcessBlobGas = excessBlobGas
            };
        }

        public byte[] HashTreeRoot() => HashTreeRoot(Fork);

        public byte[] HashTreeRoot(ConsensusFork fork)
        {
            if (!LightClientForkSpec.HasExecutionPayloadHeader(fork))
                throw new InvalidOperationException(
                    $"ExecutionPayloadHeader is not part of fork {fork}; pre-Bellatrix forks have no execution payload.");

            var fieldRoots = new List<byte[]>
            {
                SszBasicTypes.HashTreeRootFixedBytes(ParentHash, SszBasicTypes.RootLength),
                SszBasicTypes.HashTreeRootFixedBytes(FeeRecipient, FeeRecipient.Length),
                SszBasicTypes.HashTreeRootFixedBytes(StateRoot, SszBasicTypes.RootLength),
                SszBasicTypes.HashTreeRootFixedBytes(ReceiptsRoot, SszBasicTypes.RootLength),
                SszBasicTypes.HashTreeRootFixedBytes(LogsBloom, SszBasicTypes.LogsBloomLength),
                SszBasicTypes.HashTreeRootFixedBytes(PrevRandao, SszBasicTypes.RootLength),
                SszBasicTypes.HashTreeRootUInt64(BlockNumber),
                SszBasicTypes.HashTreeRootUInt64(GasLimit),
                SszBasicTypes.HashTreeRootUInt64(GasUsed),
                SszBasicTypes.HashTreeRootUInt64(Timestamp),
                SszBasicTypes.HashTreeRootVariableBytes(ExtraData ?? Array.Empty<byte>()),
                SszBasicTypes.HashTreeRootFixedBytes(BaseFeePerGas, SszBasicTypes.RootLength),
                SszBasicTypes.HashTreeRootFixedBytes(BlockHash, SszBasicTypes.RootLength),
                SszBasicTypes.HashTreeRootFixedBytes(TransactionsRoot, SszBasicTypes.RootLength)
            };
            if (LightClientForkSpec.HasWithdrawalsRoot(fork))
                fieldRoots.Add(SszBasicTypes.HashTreeRootFixedBytes(WithdrawalsRoot, SszBasicTypes.RootLength));
            if (LightClientForkSpec.HasBlobGasFields(fork))
            {
                fieldRoots.Add(SszBasicTypes.HashTreeRootUInt64(BlobGasUsed));
                fieldRoots.Add(SszBasicTypes.HashTreeRootUInt64(ExcessBlobGas));
            }

            return SszMerkleizer.Merkleize(fieldRoots);
        }

        private static byte[] Combine(byte[] fixedSection, byte[] dynamicSection)
        {
            var result = new byte[fixedSection.Length + dynamicSection.Length];
            Buffer.BlockCopy(fixedSection, 0, result, 0, fixedSection.Length);
            Buffer.BlockCopy(dynamicSection, 0, result, fixedSection.Length, dynamicSection.Length);
            return result;
        }

        private void AssertForkConsistency(ConsensusFork fork)
        {
            if (Fork != ConsensusFork.Phase0 && Fork != fork)
            {
                throw new InvalidOperationException(
                    $"ExecutionPayloadHeader.Fork={Fork} but outer fork is {fork}.");
            }
        }
    }
}
