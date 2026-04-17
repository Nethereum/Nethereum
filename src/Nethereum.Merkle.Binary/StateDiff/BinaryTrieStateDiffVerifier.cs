using System;
using Nethereum.Util.HashProviders;

namespace Nethereum.Merkle.Binary.StateDiff
{
    public class BinaryTrieStateDiffVerifier
    {
        private readonly IHashProvider _hashProvider;

        public BinaryTrieStateDiffVerifier(IHashProvider hashProvider)
        {
            _hashProvider = hashProvider ?? throw new ArgumentNullException(nameof(hashProvider));
        }

        public StateDiffVerificationResult Verify(BinaryTrieStateDiff diff, BinaryTrie preTrie)
        {
            if (diff == null) throw new ArgumentNullException(nameof(diff));
            if (preTrie == null) throw new ArgumentNullException(nameof(preTrie));

            var preRoot = preTrie.ComputeRoot();

            if (diff.PreStateRoot != null && !ByteArrayEquals(preRoot, diff.PreStateRoot))
            {
                return StateDiffVerificationResult.Fail(
                    "Pre-state root mismatch: trie root does not match diff.PreStateRoot",
                    preRoot, diff.PreStateRoot);
            }

            var postTrie = preTrie.Copy();
            int appliedStems = 0;
            int appliedSuffixes = 0;

            foreach (var stemDiff in diff.StemDiffs)
            {
                foreach (var suffixDiff in stemDiff.SuffixDiffs)
                {
                    var key = new byte[BinaryTrieConstants.HashSize];
                    Array.Copy(stemDiff.Stem, 0, key, 0, BinaryTrieConstants.StemSize);
                    key[BinaryTrieConstants.StemSize] = suffixDiff.SuffixIndex;

                    if (suffixDiff.NewValue != null)
                    {
                        postTrie.Put(key, suffixDiff.NewValue);
                    }
                    else
                    {
                        postTrie.Delete(key);
                    }
                    appliedSuffixes++;
                }
                appliedStems++;
            }

            var computedPostRoot = postTrie.ComputeRoot();

            if (diff.PostStateRoot != null && !ByteArrayEquals(computedPostRoot, diff.PostStateRoot))
            {
                return StateDiffVerificationResult.Fail(
                    "Post-state root mismatch: applying diff produces a different root",
                    computedPostRoot, diff.PostStateRoot);
            }

            return StateDiffVerificationResult.Pass(appliedStems, appliedSuffixes, computedPostRoot);
        }

        public StateDiffVerificationResult VerifySequence(
            BinaryTrieStateDiff[] diffs, BinaryTrie startTrie)
        {
            if (diffs == null || diffs.Length == 0)
                throw new ArgumentException("At least one diff required");

            var currentTrie = startTrie ?? throw new ArgumentNullException(nameof(startTrie));
            int totalStems = 0;
            int totalSuffixes = 0;

            for (int i = 0; i < diffs.Length; i++)
            {
                var result = Verify(diffs[i], currentTrie);
                if (!result.Success)
                    return StateDiffVerificationResult.Fail(
                        $"Block {diffs[i].BlockNumber} (diff {i}): {result.ErrorMessage}",
                        result.ComputedRoot, result.ExpectedRoot);

                totalStems += result.StemsApplied;
                totalSuffixes += result.SuffixesApplied;

                currentTrie = ReapplyDiff(currentTrie, diffs[i]);
            }

            var finalRoot = currentTrie.ComputeRoot();
            return StateDiffVerificationResult.Pass(totalStems, totalSuffixes, finalRoot);
        }

        private BinaryTrie ReapplyDiff(BinaryTrie trie, BinaryTrieStateDiff diff)
        {
            var copy = trie.Copy();
            foreach (var stemDiff in diff.StemDiffs)
            {
                foreach (var suffixDiff in stemDiff.SuffixDiffs)
                {
                    var key = new byte[BinaryTrieConstants.HashSize];
                    Array.Copy(stemDiff.Stem, 0, key, 0, BinaryTrieConstants.StemSize);
                    key[BinaryTrieConstants.StemSize] = suffixDiff.SuffixIndex;

                    if (suffixDiff.NewValue != null)
                        copy.Put(key, suffixDiff.NewValue);
                    else
                        copy.Delete(key);
                }
            }
            return copy;
        }

        private static bool ByteArrayEquals(byte[] a, byte[] b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }
    }

    public class StateDiffVerificationResult
    {
        public bool Success { get; private set; }
        public string ErrorMessage { get; private set; }
        public byte[] ComputedRoot { get; private set; }
        public byte[] ExpectedRoot { get; private set; }
        public int StemsApplied { get; private set; }
        public int SuffixesApplied { get; private set; }

        public static StateDiffVerificationResult Pass(int stems, int suffixes, byte[] root)
        {
            return new StateDiffVerificationResult
            {
                Success = true,
                StemsApplied = stems,
                SuffixesApplied = suffixes,
                ComputedRoot = root
            };
        }

        public static StateDiffVerificationResult Fail(string message, byte[] computed, byte[] expected)
        {
            return new StateDiffVerificationResult
            {
                Success = false,
                ErrorMessage = message,
                ComputedRoot = computed,
                ExpectedRoot = expected
            };
        }
    }
}
