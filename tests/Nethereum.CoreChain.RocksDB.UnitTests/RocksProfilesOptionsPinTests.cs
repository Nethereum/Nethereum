using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Nethereum.CoreChain.RocksDB.History;
using RocksDbSharp;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class RocksProfilesOptionsPinTests : IDisposable
    {
        private static readonly HashSet<string> Pinned = new()
        {
            "write_buffer_size", "max_write_buffer_number", "target_file_size_base",
            "level0_file_num_compaction_trigger", "level0_slowdown_writes_trigger", "level0_stop_writes_trigger",
            "soft_pending_compaction_bytes_limit", "hard_pending_compaction_bytes_limit",
            "compression", "bottommost_compression", "compaction_style", "level_compaction_dynamic_level_bytes",
            "periodic_compaction_seconds", "compaction_options_universal",
            "block_size", "cache_index_and_filter_blocks", "pin_l0_filter_and_index_blocks_in_cache",
            "whole_key_filtering", "filter_policy", "index_type", "partition_filters",
        };

        private readonly string _root = Path.Combine(Path.GetTempPath(), "necc-profilepin-" + Guid.NewGuid().ToString("N"));

        public RocksProfilesOptionsPinTests() => Directory.CreateDirectory(_root);

        public void Dispose()
        {
            try { Directory.Delete(_root, recursive: true); } catch { }
        }

        private static string Canonical(string value)
        {
            if (!value.StartsWith("{") || !value.EndsWith("}")) return value;
            var fields = value.Substring(1, value.Length - 2).Split(';', StringSplitOptions.RemoveEmptyEntries);
            Array.Sort(fields, StringComparer.Ordinal);
            return "{" + string.Join(";", fields) + "}";
        }

        private static string PinnedOptionsOf(string dbDir)
        {
            var optionsFile = Directory.GetFiles(dbDir, "OPTIONS-*")
                .OrderBy(path => long.Parse(Path.GetFileName(path).Substring("OPTIONS-".Length)))
                .Last();
            var kept = new List<string>();
            var section = "";
            foreach (var raw in File.ReadAllLines(optionsFile))
            {
                var line = raw.Trim();
                if (line.StartsWith("[")) { section = line; continue; }
                var eq = line.IndexOf('=');
                if (eq < 0 || !(section.StartsWith("[CFOptions") || section.StartsWith("[TableOptions"))) continue;
                var name = line.Substring(0, eq);
                if (Pinned.Contains(name)) kept.Add(section + " " + name + "=" + Canonical(line.Substring(eq + 1)));
            }
            kept.Sort(StringComparer.Ordinal);
            return string.Join("\n", kept) + "\n";
        }

        [Fact]
        public void Profiles_Produce_Same_Options_As_Before()
        {
            var dump = new StringBuilder();
            foreach (var preset in Enum.GetValues<StoragePreset>())
            {
                var dir = Path.Combine(_root, "live-" + preset);
                using (new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir, Preset = preset })) { }
                dump.Append("== live ").Append(preset).Append('\n').Append(PinnedOptionsOf(dir));
            }

            var historyDir = Path.Combine(_root, "history");
            var cache = RocksProfiles.CreateSharedCache(16 * 1024 * 1024);
            using (RocksDb.Open(RocksProfiles.Db(2, 32 * 1024 * 1024), historyDir, RocksProfiles.BuildColumnFamilies(cache))) { }
            dump.Append("== history\n").Append(PinnedOptionsOf(historyDir));

            var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(dump.ToString())));
            Assert.True(digest == "53673FB57E112956DEB469DD983E753D4378FE35BE7CD92C6D815CA2EE673073", digest + "\n" + dump);
        }
    }
}
