using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.CoreChain.Sync
{
    public sealed class DifficultyForkChoice : IChainForkChoice
    {
        public const int DefaultMaxReorgDepth = 64;

        private readonly IBlockStore _blocks;
        private readonly int _maxReorgDepth;

        public DifficultyForkChoice(IBlockStore blocks, int maxReorgDepth = DefaultMaxReorgDepth)
        {
            _blocks = blocks ?? throw new ArgumentNullException(nameof(blocks));
            if (maxReorgDepth <= 0) throw new ArgumentOutOfRangeException(nameof(maxReorgDepth));
            _maxReorgDepth = maxReorgDepth;
        }

        public async Task<ForkChoiceVerdict> ShouldAdoptAsync(
            BlockHeader incomingHead, byte[] incomingHash, CancellationToken ct)
        {
            if (incomingHead == null) throw new ArgumentNullException(nameof(incomingHead));
            if (incomingHash == null) throw new ArgumentNullException(nameof(incomingHash));

            if (await IsOnOurCanonicalChainAsync(incomingHead.BlockNumber, incomingHash).ConfigureAwait(false))
                return ForkChoiceVerdict.KeepLocal("already canonical");

            var branch = await WalkBackToCommonAncestorAsync(incomingHead, incomingHash, ct).ConfigureAwait(false);
            if (branch.Ancestor == null) return ForkChoiceVerdict.Undecidable(branch.Reason);

            var ancestor = branch.Ancestor.Value;
            var ours = await OurBranchFromAsync(ancestor, ct).ConfigureAwait(false);
            if (ours == null) return ForkChoiceVerdict.Undecidable("local branch is not readable above the common ancestor");

            return Choose(branch, ours.Value, ancestor, incomingHash);
        }

        private ForkChoiceVerdict Choose(Branch incoming, Branch ours, ulong ancestor, byte[] incomingHash)
        {
            var difference = incoming.Difficulty.CompareTo(ours.Difficulty);
            if (difference > 0) return ForkChoiceVerdict.AdoptIncoming(ancestor, "heavier branch");
            if (difference < 0) return ForkChoiceVerdict.KeepLocal("local branch is heavier");

            if (incoming.HeadNumber < ours.HeadNumber)
                return ForkChoiceVerdict.AdoptIncoming(ancestor, "equal weight, shorter branch");
            if (incoming.HeadNumber > ours.HeadNumber)
                return ForkChoiceVerdict.KeepLocal("equal weight, local branch is shorter");

            return ByteArrayComparer.Current.Compare(incomingHash, ours.HeadHash) < 0
                ? ForkChoiceVerdict.AdoptIncoming(ancestor, "equal weight and height, lower head hash")
                : ForkChoiceVerdict.KeepLocal("equal weight and height, local head hash is lower or identical");
        }

        private async Task<bool> IsOnOurCanonicalChainAsync(BigInteger number, byte[] hash)
        {
            var ours = await _blocks.GetHashByNumberAsync(number).ConfigureAwait(false);
            return ours != null && ByteUtil.AreEqual(ours, hash);
        }

        private readonly struct Branch
        {
            public Branch(ulong? ancestor, BigInteger difficulty, ulong headNumber, byte[] headHash, string reason)
            {
                Ancestor = ancestor;
                Difficulty = difficulty;
                HeadNumber = headNumber;
                HeadHash = headHash;
                Reason = reason;
            }

            public ulong? Ancestor { get; }
            public BigInteger Difficulty { get; }
            public ulong HeadNumber { get; }
            public byte[] HeadHash { get; }
            public string Reason { get; }

            public static Branch Unreadable(string reason) => new Branch(null, 0, 0, null, reason);
        }

        private async Task<Branch> WalkBackToCommonAncestorAsync(
            BlockHeader head, byte[] headHash, CancellationToken ct)
        {
            var difficulty = BigInteger.Zero;
            var header = head;
            var headNumber = (ulong)head.BlockNumber;

            for (var depth = 0; depth < _maxReorgDepth; depth++)
            {
                ct.ThrowIfCancellationRequested();
                difficulty += (BigInteger)header.Difficulty;

                if (header.BlockNumber == 0 || header.ParentHash == null)
                    return Branch.Unreadable("the incoming branch reaches genesis without meeting our chain");

                var parentNumber = (ulong)header.BlockNumber - 1;
                if (await IsOnOurCanonicalChainAsync(parentNumber, header.ParentHash).ConfigureAwait(false))
                    return new Branch(parentNumber, difficulty, headNumber, headHash, null);

                var parent = await _blocks.GetByHashAsync(header.ParentHash).ConfigureAwait(false);
                if (parent == null)
                    return Branch.Unreadable("the incoming branch is not held locally above the common ancestor");

                header = parent;
            }

            return Branch.Unreadable($"the incoming branch diverges deeper than {_maxReorgDepth} blocks");
        }

        private async Task<Branch?> OurBranchFromAsync(ulong ancestor, CancellationToken ct)
        {
            var head = await _blocks.GetLatestAsync().ConfigureAwait(false);
            if (head == null) return null;

            var difficulty = BigInteger.Zero;
            var headNumber = (ulong)head.BlockNumber;
            var headHash = await _blocks.GetHashByNumberAsync(head.BlockNumber).ConfigureAwait(false);

            for (var number = ancestor + 1; number <= headNumber; number++)
            {
                ct.ThrowIfCancellationRequested();
                var header = await _blocks.GetByNumberAsync(number).ConfigureAwait(false);
                if (header == null) return null;
                difficulty += (BigInteger)header.Difficulty;
            }

            return new Branch(ancestor, difficulty, headNumber, headHash ?? Array.Empty<byte>(), null);
        }
    }
}
