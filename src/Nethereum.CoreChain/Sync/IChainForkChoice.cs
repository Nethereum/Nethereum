using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.CoreChain.Sync
{
    public interface IChainForkChoice
    {
        Task<ForkChoiceVerdict> ShouldAdoptAsync(BlockHeader incomingHead, byte[] incomingHash, CancellationToken ct);

        Task<ForkChoiceVerdict> ShouldAdoptAsync(
            BlockHeader incomingHead, byte[] incomingHash, string sourcePeerNodeId, CancellationToken ct)
            => ShouldAdoptAsync(incomingHead, incomingHash, ct);
    }

    public enum ForkChoiceOutcome
    {
        KeepLocal,
        AdoptIncoming,
        Undecidable
    }

    public readonly struct ForkChoiceVerdict
    {
        private ForkChoiceVerdict(ForkChoiceOutcome outcome, ulong commonAncestor, string reason)
        {
            Outcome = outcome;
            CommonAncestor = commonAncestor;
            Reason = reason;
        }

        public ForkChoiceOutcome Outcome { get; }

        public ulong CommonAncestor { get; }

        public string Reason { get; }

        public bool RequiresReorg => Outcome == ForkChoiceOutcome.AdoptIncoming;

        public static ForkChoiceVerdict KeepLocal(string reason)
            => new ForkChoiceVerdict(ForkChoiceOutcome.KeepLocal, 0, reason);

        public static ForkChoiceVerdict AdoptIncoming(ulong commonAncestor, string reason)
            => new ForkChoiceVerdict(ForkChoiceOutcome.AdoptIncoming, commonAncestor, reason);

        public static ForkChoiceVerdict Undecidable(string reason)
            => new ForkChoiceVerdict(ForkChoiceOutcome.Undecidable, 0, reason);
    }
}
