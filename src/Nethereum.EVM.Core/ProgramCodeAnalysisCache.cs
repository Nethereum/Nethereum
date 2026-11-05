#if !EVM_ZISK
using System;
using System.Runtime.CompilerServices;
using Nethereum.Util;

namespace Nethereum.EVM
{
    internal static class ProgramCodeAnalysisCache
    {
        public const long MaxEstimatedBytes = 64L * 1024 * 1024;

        private const int CodeHashLength = 32;

        private static readonly LeastRecentlyUsedMap<CodeHashKey, ProgramCodeAnalysisEntry> Entries =
            new LeastRecentlyUsedMap<CodeHashKey, ProgramCodeAnalysisEntry>(MaxEstimatedBytes, entry => entry.EstimatedBytes);
        private static readonly ConditionalWeakTable<byte[], byte[]> HashByCodeArray =
            new ConditionalWeakTable<byte[], byte[]>();

        public static byte[] HashOf(byte[] byteCode)
        {
            if (HashByCodeArray.TryGetValue(byteCode, out var known)) return known;
            return HashByCodeArray.GetValue(byteCode, code => Sha3Keccack.Current.CalculateHash(code));
        }

        public static void RequireValidCodeHash(byte[] codeHash)
        {
            if (codeHash != null && codeHash.Length != CodeHashLength)
                throw new ArgumentException("A code hash is a 32-byte keccak256 digest.", nameof(codeHash));
        }

        public static ProgramCodeAnalysis GetOrBuild(byte[] byteCode, byte[] codeHash, bool eip8024Enabled)
        {
            RequireValidCodeHash(codeHash);
            var key = new CodeHashKey(codeHash);
            var entry = Entries.GetOrAdd(key, () => new ProgramCodeAnalysisEntry());
            var analysis = entry.GetOrBuildInstructions(byteCode, eip8024Enabled, out var built);
            if (built) Entries.Reweigh(key);
            return analysis;
        }

        public static byte[] GetOrBuildPushDataBitmap(byte[] byteCode, byte[] codeHash)
        {
            RequireValidCodeHash(codeHash);
            var key = new CodeHashKey(codeHash);
            var entry = Entries.GetOrAdd(key, () => new ProgramCodeAnalysisEntry());
            var bitmap = entry.GetOrBuildPushDataBitmap(byteCode, out var built);
            if (built) Entries.Reweigh(key);
            return bitmap;
        }

        private readonly struct CodeHashKey : IEquatable<CodeHashKey>
        {
            private readonly ulong _first;
            private readonly ulong _second;
            private readonly ulong _third;
            private readonly ulong _fourth;

            public CodeHashKey(byte[] codeHash)
            {
                _first = BitConverter.ToUInt64(codeHash, 0);
                _second = BitConverter.ToUInt64(codeHash, 8);
                _third = BitConverter.ToUInt64(codeHash, 16);
                _fourth = BitConverter.ToUInt64(codeHash, 24);
            }

            public bool Equals(CodeHashKey other) =>
                _first == other._first && _second == other._second && _third == other._third && _fourth == other._fourth;

            public override bool Equals(object obj) => obj is CodeHashKey other && Equals(other);

            public override int GetHashCode() => (int)_first ^ (int)(_first >> 32);
        }
    }
}
#endif
