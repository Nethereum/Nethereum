using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Nethereum.CoreChain.IntegrationTests.BlockchainTests;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;
using Xunit.Abstractions;

namespace Nethereum.EVM.Core.Tests.GeneralStateTests
{
    public static class AmsterdamFixtureHeaders
    {
        public class Entry
        {
            public BlockHeader Header { get; set; }
            public byte[] ExpectedHash { get; set; }
            public string Source { get; set; }
        }

        public static List<Entry> Load(ITestOutputHelper output, int maxFiles = 40)
        {
            var result = new List<Entry>();
            var root = FindFixturesRoot();
            if (root == null)
            {
                output?.WriteLine("Amsterdam fixtures not found on disk — see external/execution-spec-tests.");
                return result;
            }

            var files = 0;
            foreach (var file in Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories))
            {
                if (files >= maxFiles) break;
                files++;

                List<BlockchainTestLoader.BlockchainTest> tests;
                try
                {
                    tests = BlockchainTestLoader.LoadFromFile(file);
                }
                catch (Exception ex)
                {
                    output?.WriteLine($"  skipped {Path.GetFileName(file)}: {ex.Message}");
                    continue;
                }

                foreach (var test in tests)
                {
                    if (test.Blocks == null) continue;
                    foreach (var block in test.Blocks)
                    {
                        var h = block.BlockHeader;
                        if (h == null) continue;
                        if (h.BlockAccessListHash == null || h.BlockAccessListHash.Length == 0) continue;
                        if (h.SlotNumber == null) continue;
                        if (h.Hash == null || h.Hash.Length == 0) continue;

                        result.Add(new Entry
                        {
                            Header = ToModel(h),
                            ExpectedHash = h.Hash,
                            Source = $"{Path.GetFileName(file)}::{test.Name}"
                        });
                    }
                }
            }

            output?.WriteLine($"Loaded {result.Count} complete Amsterdam headers from {files} fixture file(s).");
            return result;
        }

        private static BlockHeader ToModel(BlockchainTestLoader.BlockHeader h)
        {
            return new BlockHeader
            {
                ParentHash = h.ParentHash,
                UnclesHash = h.UncleHash,
                Coinbase = "0x" + h.Coinbase.ToHex(),
                StateRoot = h.StateRoot,
                TransactionsHash = h.TransactionsRoot,
                ReceiptHash = h.ReceiptsRoot,
                LogsBloom = h.LogsBloom,
                Difficulty = new EvmUInt256(h.Difficulty),
                BlockNumber = (long)h.Number,
                GasLimit = (long)h.GasLimit,
                GasUsed = (long)h.GasUsed,
                Timestamp = (long)h.Timestamp,
                ExtraData = h.ExtraData,
                MixHash = h.MixHash,
                Nonce = h.Nonce,
                BaseFee = h.BaseFee.HasValue ? (long?)(long)h.BaseFee.Value : null,
                WithdrawalsRoot = h.WithdrawalsRoot,
                BlobGasUsed = h.BlobGasUsed,
                ExcessBlobGas = h.ExcessBlobGas,
                ParentBeaconBlockRoot = h.ParentBeaconBlockRoot,
                RequestsHash = h.RequestsHash,
                BlockAccessListHash = h.BlockAccessListHash,
                SlotNumber = h.SlotNumber.HasValue ? (ulong?)(ulong)h.SlotNumber.Value : null
            };
        }

        private static string FindFixturesRoot()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "Nethereum.slnx")) ||
                    File.Exists(Path.Combine(dir.FullName, "Nethereum.sln")))
                {
                    var path = Path.Combine(dir.FullName, "external", "execution-spec-tests",
                        "fixtures", "blockchain_tests", "amsterdam");
                    if (Directory.Exists(path)) return path;
                }
                dir = dir.Parent;
            }
            return null;
        }
    }
}
