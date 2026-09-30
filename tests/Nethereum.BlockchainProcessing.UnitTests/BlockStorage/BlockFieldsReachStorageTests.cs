using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Nethereum.BlockchainProcessing.BlockStorage.Entities.Mapping;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Xunit;
using StorageBlock = Nethereum.BlockchainProcessing.BlockStorage.Entities.Block;

namespace Nethereum.BlockchainProcessing.UnitTests.BlockStorage
{
    public class BlockFieldsReachStorageTests
    {
        private static readonly Dictionary<string, string> PersistedAs = new Dictionary<string, string>
        {
            ["Number"] = nameof(StorageBlock.BlockNumber),
            ["BlockHash"] = nameof(StorageBlock.Hash),
            ["ParentHash"] = nameof(StorageBlock.ParentHash),
            ["Nonce"] = nameof(StorageBlock.Nonce),
            ["Sha3Uncles"] = nameof(StorageBlock.Sha3Uncles),
            ["LogsBloom"] = nameof(StorageBlock.LogsBloom),
            ["TransactionsRoot"] = nameof(StorageBlock.TransactionsRoot),
            ["StateRoot"] = nameof(StorageBlock.StateRoot),
            ["ReceiptsRoot"] = nameof(StorageBlock.ReceiptsRoot),
            ["Miner"] = nameof(StorageBlock.Miner),
            ["Difficulty"] = nameof(StorageBlock.Difficulty),
            ["TotalDifficulty"] = nameof(StorageBlock.TotalDifficulty),
            ["MixHash"] = nameof(StorageBlock.MixHash),
            ["ExtraData"] = nameof(StorageBlock.ExtraData),
            ["Size"] = nameof(StorageBlock.Size),
            ["GasLimit"] = nameof(StorageBlock.GasLimit),
            ["GasUsed"] = nameof(StorageBlock.GasUsed),
            ["Timestamp"] = nameof(StorageBlock.Timestamp),
            ["BaseFeePerGas"] = nameof(StorageBlock.BaseFeePerGas),
            ["WithdrawalsRoot"] = nameof(StorageBlock.WithdrawalsRoot),
            ["BlobGasUsed"] = nameof(StorageBlock.BlobGasUsed),
            ["ExcessBlobGas"] = nameof(StorageBlock.ExcessBlobGas),
            ["ParentBeaconBlockRoot"] = nameof(StorageBlock.ParentBeaconBlockRoot),
            ["RequestsHash"] = nameof(StorageBlock.RequestsHash),
            ["BlockAccessListHash"] = nameof(StorageBlock.BlockAccessListHash),
            ["SlotNumber"] = nameof(StorageBlock.SlotNumber)
        };

        private static readonly Dictionary<string, string> NotPersisted = new Dictionary<string, string>
        {
            ["Author"] = "Parity's alias for miner; the node that serves it also serves miner",
            ["SealFields"] = "Parity proof-of-authority sealing detail, not part of the Ethereum header",
            ["Uncles"] = "the uncle hashes are a list; sha3Uncles is the header field and is persisted",
            ["Withdrawals"] = "the withdrawal list is a body field with its own store; withdrawalsRoot is the header field and is persisted"
        };

        private static PropertyInfo[] BlockDtoProperties() =>
            typeof(Block).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        [Fact]
        public void Given_EveryFieldOnTheBlockDto_When_Classified_Then_EachIsEitherPersistedOrDeclaredNotToBe()
        {
            var unclassified = BlockDtoProperties()
                .Select(p => p.Name)
                .Where(name => !PersistedAs.ContainsKey(name) && !NotPersisted.ContainsKey(name))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();

            Assert.True(unclassified.Count == 0,
                $"Block carries {string.Join(", ", unclassified)}, which neither reaches storage nor is " +
                "declared as deliberately not persisted. A field added to the DTO has to be carried through " +
                "IBlockView, the Block entity, BlockMapping, BlockEntityBuilder and the Postgres migration " +
                "before anything downstream - the explorer included - can show it.");
        }

        [Fact]
        public void Given_ABlockCarryingEveryPersistedField_When_MappedToStorage_Then_NoneOfThemIsLost()
        {
            var source = BlockWithEveryFieldSet();

            var stored = source.MapToStorageEntityForUpsert();

            var lost = PersistedAs
                .Where(pair => IsAbsent(stored, pair.Value))
                .Select(pair => $"{pair.Key} -> {pair.Value}")
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();

            Assert.True(lost.Count == 0,
                $"BlockMapping did not carry {string.Join(", ", lost)}. The field is declared as persisted, " +
                "so the mapping has to copy it.");
        }

        [Fact]
        public void Given_AnAmsterdamBlock_When_MappedToStorage_Then_TheAccessListHashAndSlotNumberArriveUnchanged()
        {
            var source = BlockWithEveryFieldSet();

            var stored = source.MapToStorageEntityForUpsert();

            Assert.Equal(source.BlockAccessListHash, stored.BlockAccessListHash);
            Assert.Equal(source.SlotNumber.Value.ToString(), stored.SlotNumber);
        }

        private static bool IsAbsent(StorageBlock stored, string propertyName)
        {
            var value = typeof(StorageBlock).GetProperty(propertyName).GetValue(stored);
            if (value is string text) return string.IsNullOrEmpty(text);
            if (value is long number) return number == 0;

            return value == null;
        }

        private static Block BlockWithEveryFieldSet() => new Block
        {
            Number = new HexBigInteger(0x2a),
            BlockHash = "0x1111111111111111111111111111111111111111111111111111111111111111",
            ParentHash = "0x2222222222222222222222222222222222222222222222222222222222222222",
            Nonce = "0x0000000000000042",
            Sha3Uncles = "0x3333333333333333333333333333333333333333333333333333333333333333",
            LogsBloom = "0x" + new string('4', 512),
            TransactionsRoot = "0x5555555555555555555555555555555555555555555555555555555555555555",
            StateRoot = "0x6666666666666666666666666666666666666666666666666666666666666666",
            ReceiptsRoot = "0x7777777777777777777777777777777777777777777777777777777777777777",
            Miner = "0x8888888888888888888888888888888888888888",
            Difficulty = new HexBigInteger(1),
            TotalDifficulty = new HexBigInteger(2),
            MixHash = "0x9999999999999999999999999999999999999999999999999999999999999999",
            ExtraData = "0xd883010d05",
            Size = new HexBigInteger(0x200),
            GasLimit = new HexBigInteger(30_000_000),
            GasUsed = new HexBigInteger(21_000),
            Timestamp = new HexBigInteger(1_700_000_000),
            BaseFeePerGas = new HexBigInteger(1_000_000_000),
            WithdrawalsRoot = new HexBigInteger(BigInteger.Parse("12345")),
            BlobGasUsed = new HexBigInteger(131_072),
            ExcessBlobGas = new HexBigInteger(262_144),
            ParentBeaconBlockRoot = "0xaaaa111111111111111111111111111111111111111111111111111111111111",
            RequestsHash = "0xbbbb111111111111111111111111111111111111111111111111111111111111",
            BlockAccessListHash = "0xcccc111111111111111111111111111111111111111111111111111111111111",
            SlotNumber = new HexBigInteger(9_876_543)
        };
    }
}
