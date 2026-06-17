using Nethereum.CoreChain.Sync;

namespace Nethereum.DevP2P.SyncNode
{
    /// <summary>
    /// Process exit codes returned by the SyncNode CLI. Eight load-bearing
    /// values that downstream CI / monitoring systems may key off; preserved
    /// verbatim across the Stage 6 architecture flip.
    /// </summary>
    public enum SyncExitCode
    {
        /// <summary>Sync completed cleanly. No state-root mismatches recorded.</summary>
        Success = 0,

        /// <summary>Sync completed but at least one state-root mismatch was tolerated
        /// (only possible with <c>--continue-on-mismatch</c>).</summary>
        CompletedWithMismatches = 2,

        /// <summary>Bad usage — CLI args unparseable, missing required argument, etc.</summary>
        Usage = 64,

        /// <summary>Operation requested a checkpoint that does not exist.</summary>
        NoCheckpoint = 65,

        /// <summary>Software fault — assertion failed, internal invariant violated,
        /// or a known-good post-state did not match. Includes genesis state-root
        /// mismatch (alloc fixture corrupted).</summary>
        Software = 70,

        /// <summary>OS-level fault — fatal divergence verdict, snapshot restore
        /// failed mid-flight, RocksDB lock unavailable.</summary>
        OsError = 71,

        /// <summary><c>--re-execute-from</c> requested a block whose header is not
        /// stored locally.</summary>
        MissingHeader = 75,

        /// <summary><c>--re-execute-from</c> encountered a state-root mismatch and
        /// <c>--continue-on-mismatch</c> was not supplied.</summary>
        ReExecuteMismatch = 76,
    }

    internal static class SyncExitCodeMapper
    {
        /// <summary>Map a follower-loop result to a SyncExitCode.</summary>
        public static SyncExitCode FromFollowerResult(FollowerRunResult result, bool continueOnMismatch)
        {
            switch (result.ExitReason)
            {
                case FollowerExitReason.SourceCompleted:
                    if (result.RootMismatches == 0) return SyncExitCode.Success;
                    return continueOnMismatch
                        ? SyncExitCode.CompletedWithMismatches
                        : SyncExitCode.OsError;
                case FollowerExitReason.Cancelled:
                    return SyncExitCode.Success;
                case FollowerExitReason.FatalVerdict:
                case FollowerExitReason.RewindUnavailable:
                case FollowerExitReason.SnapshotRestoreRequested:
                case FollowerExitReason.SourceUnavailable:
                default:
                    return SyncExitCode.OsError;
            }
        }
    }
}
