using System;
using System.Collections.Generic;
using System.Linq;

namespace Nethereum.RpcParity
{
    public sealed class CliOptions
    {
        public static readonly string[] AllGroups = { "blocks", "tx", "receipts", "logs", "state", "sim", "trace" };

        /// <summary>
        /// Fields that are cosmetic Parity-isms or spec churn, never a real
        /// geth-parity gap — ignored by default so a run FAILs only on real
        /// divergence. Cleared with --ignore-none.
        /// </summary>
        public static readonly string[] DefaultCosmeticIgnores = { "author", "sealFields", "blockTimestamp" };

        public string XUrl { get; private set; }
        public string YUrl { get; private set; }
        public long? From { get; private set; }
        public long? To { get; private set; }
        public List<long> ExplicitBlocks { get; private set; } = new List<long>();
        public HashSet<string> Groups { get; private set; } = new HashSet<string>(AllGroups, StringComparer.OrdinalIgnoreCase);
        public HashSet<string> Ignore { get; private set; }
        public bool SelfTest { get; private set; }

        private readonly HashSet<string> _explicitIgnore = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool _ignoreNone;

        public static CliOptions Parse(string[] argv)
        {
            var options = new CliOptions();
            for (int i = 0; i < argv.Length; i++)
            {
                switch (argv[i])
                {
                    case "--x": options.XUrl = RequireValue(argv, ref i); break;
                    case "--y": options.YUrl = RequireValue(argv, ref i); break;
                    case "--from": options.From = long.Parse(RequireValue(argv, ref i)); break;
                    case "--to": options.To = long.Parse(RequireValue(argv, ref i)); break;
                    case "--blocks":
                        options.ExplicitBlocks = SplitCsv(RequireValue(argv, ref i)).Select(long.Parse).ToList();
                        break;
                    case "--groups":
                        options.Groups = new HashSet<string>(SplitCsv(RequireValue(argv, ref i)), StringComparer.OrdinalIgnoreCase);
                        break;
                    case "--ignore":
                        foreach (var field in SplitCsv(RequireValue(argv, ref i)))
                            options._explicitIgnore.Add(field);
                        break;
                    case "--ignore-none":
                        options._ignoreNone = true;
                        break;
                    case "--selftest":
                        options.SelfTest = true;
                        break;
                    case "--help":
                    case "-h":
                        options.SelfTest = false;
                        throw new ArgumentException("help requested");
                    default:
                        throw new ArgumentException($"unknown argument: {argv[i]}");
                }
            }
            options.Ignore = BuildIgnoreSet(options._ignoreNone, options._explicitIgnore);
            return options;
        }

        private static HashSet<string> BuildIgnoreSet(bool ignoreNone, HashSet<string> explicitIgnore)
        {
            var ignore = ignoreNone
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(DefaultCosmeticIgnores, StringComparer.OrdinalIgnoreCase);
            ignore.UnionWith(explicitIgnore);
            return ignore;
        }

        public void Validate()
        {
            if (SelfTest) return;
            if (string.IsNullOrEmpty(XUrl)) throw new ArgumentException("--x <url> is required");
            if (string.IsNullOrEmpty(YUrl)) throw new ArgumentException("--y <url> is required");
            var unknown = Groups.Except(AllGroups, StringComparer.OrdinalIgnoreCase).ToList();
            if (unknown.Count > 0) throw new ArgumentException($"unknown group(s): {string.Join(",", unknown)}");
        }

        private static string RequireValue(string[] argv, ref int i)
        {
            if (i + 1 >= argv.Length) throw new ArgumentException($"{argv[i]} requires a value");
            return argv[++i];
        }

        private static IEnumerable<string> SplitCsv(string s) =>
            s.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim());
    }
}
