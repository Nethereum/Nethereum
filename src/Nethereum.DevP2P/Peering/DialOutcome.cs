namespace Nethereum.DevP2P.Peering
{
    /// <summary>
    /// Result classification fed back to <see cref="DialScheduler.ReleaseSlot"/>
    /// when a reserved dial slot completes. Both <see cref="Success"/> and
    /// <see cref="Failure"/> record the candidate in dial history; the
    /// scheduler treats them identically for suppression — dial history is
    /// recorded on every completed task regardless of whether the peer
    /// actually joined the pool.
    /// </summary>
    public enum DialOutcome
    {
        /// <summary>Dial completed and the peer joined the pool.</summary>
        Success,

        /// <summary>Dial completed unsuccessfully (timeout, refused,
        /// handshake error, useless-peer rejection). Still recorded so we
        /// don't retry immediately.</summary>
        Failure
    }
}
