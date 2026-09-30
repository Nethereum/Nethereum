using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.EVM.Core.Tests.GeneralStateTests
{
    public class BlockAccessListHashSweepTests
    {
        private readonly ITestOutputHelper _output;
        private readonly string _amsterdamFixtures;

        public BlockAccessListHashSweepTests(ITestOutputHelper output)
        {
            _output = output;
            _amsterdamFixtures = FindAmsterdamFixtures();
        }

        private static string FindAmsterdamFixtures()
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

        private static EvmUInt256 Num(string hex)
        {
            var bytes = hex.HexToByteArray();
            if (bytes.Length == 32) return EvmUInt256.FromBigEndian(bytes);
            var padded = new byte[32];
            Array.Copy(bytes, 0, padded, 32 - bytes.Length, bytes.Length);
            return EvmUInt256.FromBigEndian(padded);
        }

        private static ulong Index(string hex)
        {
            ulong v = 0;
            foreach (var b in hex.HexToByteArray()) v = (v << 8) | b;
            return v;
        }

        private static List<AccountChanges> ReadAccessList(JsonElement array)
        {
            var list = new List<AccountChanges>();
            foreach (var a in array.EnumerateArray())
            {
                var account = new AccountChanges(a.GetProperty("address").GetString());

                foreach (var s in a.GetProperty("storageChanges").EnumerateArray())
                {
                    var slot = new SlotChanges(Num(s.GetProperty("slot").GetString()));
                    foreach (var c in s.GetProperty("slotChanges").EnumerateArray())
                        slot.Changes.Add(new StorageChange(
                            Index(c.GetProperty("blockAccessIndex").GetString()),
                            Num(c.GetProperty("postValue").GetString())));
                    account.StorageChanges.Add(slot);
                }

                foreach (var r in a.GetProperty("storageReads").EnumerateArray())
                    account.StorageReads.Add(Num(r.GetString()));

                foreach (var c in a.GetProperty("balanceChanges").EnumerateArray())
                    account.BalanceChanges.Add(new BalanceChange(
                        Index(c.GetProperty("blockAccessIndex").GetString()),
                        Num(c.GetProperty("postBalance").GetString())));

                foreach (var c in a.GetProperty("nonceChanges").EnumerateArray())
                    account.NonceChanges.Add(new NonceChange(
                        Index(c.GetProperty("blockAccessIndex").GetString()),
                        Index(c.GetProperty("postNonce").GetString())));

                foreach (var c in a.GetProperty("codeChanges").EnumerateArray())
                    account.CodeChanges.Add(new CodeChange(
                        Index(c.GetProperty("blockAccessIndex").GetString()),
                        c.GetProperty("newCode").GetString().HexToByteArray()));

                list.Add(account);
            }
            return list;
        }

        [Fact]
        [Trait("Category", "EIP7928")]
        public void Given_EveryAmsterdamFixtureAccessList_When_Hashed_Then_ItMatchesTheHeader()
        {
            Assert.True(_amsterdamFixtures != null,
                "Amsterdam fixtures not found - this sweep cannot report a meaningful result without them");

            var checkedBlocks = 0;
            var rejectionBlocks = 0;
            var mismatches = new List<string>();
            var coverage = new HashSet<string>();

            foreach (var file in Directory.EnumerateFiles(_amsterdamFixtures, "*.json", SearchOption.AllDirectories))
            {
                JsonDocument doc;
                try { doc = JsonDocument.Parse(File.ReadAllText(file)); }
                catch (JsonException) { continue; }

                using (doc)
                {
                    foreach (var test in doc.RootElement.EnumerateObject())
                    {
                        if (!test.Value.TryGetProperty("blocks", out var blocks)) continue;

                        foreach (var block in blocks.EnumerateArray())
                        {
                            if (!block.TryGetProperty("rlp_decoded", out var decoded)) continue;
                            if (!decoded.TryGetProperty("blockAccessList", out var accessListJson)) continue;
                            if (!decoded.TryGetProperty("blockHeader", out var header)) continue;
                            if (!header.TryGetProperty("blockAccessListHash", out var expected)) continue;

                            var accessList = ReadAccessList(accessListJson);
                            RecordCoverage(accessList, coverage);

                            var actual = "0x" + BlockAccessListRLPEncoder.Current.Hash(accessList).ToHex();
                            var agrees = string.Equals(actual, expected.GetString(), StringComparison.OrdinalIgnoreCase);

                            if (DeclaresAWrongAccessListHash(block))
                            {
                                rejectionBlocks++;
                                if (agrees)
                                    mismatches.Add($"{Path.GetFileName(file)} :: {test.Name}: reproduced a hash the fixture declares INVALID ({actual})");
                                continue;
                            }

                            checkedBlocks++;
                            if (!agrees)
                                mismatches.Add($"{Path.GetFileName(file)} :: {test.Name}: expected {expected.GetString()} got {actual}");
                        }
                    }
                }
            }

            _output.WriteLine($"block access lists checked: {checkedBlocks}");
            _output.WriteLine($"blocks declaring a deliberately wrong hash: {rejectionBlocks}");
            _output.WriteLine($"encoding cases exercised: {string.Join(", ", coverage.OrderBy(c => c))}");

            Assert.True(mismatches.Count == 0,
                $"{mismatches.Count} of {checkedBlocks} access lists hashed differently:" +
                Environment.NewLine + string.Join(Environment.NewLine, mismatches.Take(10)));

            Assert.True(checkedBlocks >= 100,
                $"expected the Amsterdam corpus to yield at least 100 access lists; got {checkedBlocks}");

            Assert.True(rejectionBlocks > 0,
                "the corpus no longer contains a block declaring a deliberately wrong access-list hash, so the rejection half of this sweep proved nothing");

            Assert.Contains("zero-value", coverage);
            Assert.Contains("repeated-slot", coverage);

            _output.WriteLine("not exercised by this corpus: multi-byte-index, empty-code");

        }

        private static bool DeclaresAWrongAccessListHash(JsonElement block)
            => block.TryGetProperty("expectException", out var thrown)
               && thrown.ValueKind == JsonValueKind.String
               && thrown.GetString().IndexOf("INVALID_BAL_HASH", StringComparison.OrdinalIgnoreCase) >= 0;

        private static void RecordCoverage(List<AccountChanges> accessList, HashSet<string> coverage)
        {
            foreach (var account in accessList)
            {
                if (account.StorageReads.Any()) coverage.Add("storage-read");
                if (account.BalanceChanges.Any()) coverage.Add("balance-change");
                if (account.NonceChanges.Any()) coverage.Add("nonce-change");
                if (account.CodeChanges.Any()) coverage.Add("code-change");
                if (account.CodeChanges.Any(c => c.NewCode == null || c.NewCode.Length == 0)) coverage.Add("empty-code");

                if (account.BalanceChanges.Any(c => c.PostBalance.IsZero)) coverage.Add("zero-value");
                if (account.NonceChanges.Any(c => c.NewNonce == 0)) coverage.Add("zero-value");
                if (account.BalanceChanges.Any(c => c.BlockAccessIndex > 255)
                    || account.NonceChanges.Any(c => c.BlockAccessIndex > 255)
                    || account.CodeChanges.Any(c => c.BlockAccessIndex > 255)) coverage.Add("multi-byte-index");
                if (account.StorageReads.Any(r => r.IsZero)) coverage.Add("zero-value");

                foreach (var slot in account.StorageChanges)
                {
                    coverage.Add("storage-change");
                    if (slot.Changes.Count > 1) coverage.Add("repeated-slot");
                    if (slot.Slot.IsZero) coverage.Add("zero-slot");
                    foreach (var change in slot.Changes)
                    {
                        if (change.PostValue.IsZero) coverage.Add("zero-value");
                        if (change.BlockAccessIndex > 255) coverage.Add("multi-byte-index");
                    }
                }
            }
        }
    }
}
