using System;

namespace Nethereum.DevP2P.Peering
{
    /// <summary>
    /// Knobs for <see cref="DialScheduler"/>. Defaults mirror geth's
    /// <c>p2p/dial.go</c> constants — <c>maxActiveDialTasks</c>,
    /// <c>dialHistoryExpiration</c>, <c>trustedDialHistoryExpiration</c> — and
    /// the default <c>MaxPeers</c> from <c>p2p/server.go</c>.
    /// </summary>
    public sealed class DialSchedulerOptions
    {
        /// <summary>
        /// Cap on concurrent in-flight outbound dial attempts. Geth's
        /// <c>maxActiveDialTasks = 16</c>. Excess <see cref="DialScheduler.TryReserveSlotAsync"/>
        /// callers wait until a slot is freed via
        /// <see cref="DialScheduler.ReleaseSlot"/>.
        /// </summary>
        public int MaxActiveDials { get; set; } = 16;

        /// <summary>
        /// Cooldown after a dial attempt (success or failure) before the same
        /// candidate is admitted again. Geth's <c>dialHistoryExpiration = 5m</c>.
        /// Suppresses tight retry loops against a peer that just disconnected
        /// or refused.
        /// </summary>
        public TimeSpan DialHistoryExpiration { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Shorter cooldown for trusted candidates. Geth's
        /// <c>trustedDialHistoryExpiration = 30s</c>. Trusted peers still get
        /// history (so a flapping trusted peer is not hammered) but reconnect
        /// faster than untrusted peers do.
        /// </summary>
        public TimeSpan TrustedHistoryExpiration { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Steady-state pool size used to derive the inbound/outbound ratio
        /// cap. Geth's <c>p2p.Config.MaxPeers</c>. At most
        /// <c>MaxPeers/2 + 1</c> connected peers may be the result of outbound
        /// dials we initiated. This is geth's eclipse-resistance hedge: an
        /// attacker that can only convince us to dial out cannot fill more
        /// than half (plus one) of the pool by itself.
        /// </summary>
        public int MaxPeers { get; set; } = 25;
    }
}
