using System;
using System.Collections.Generic;

namespace Nethereum.AppChain.Sequencer.Builder
{
    public class OperatorRecoveryConfig
    {
        public string? RecoveryAddress { get; set; }
        public TimeSpan? InactivityThreshold { get; set; }
        public IReadOnlyList<string>? Guardians { get; set; }
        public int GuardianThreshold { get; set; }
        public TimeSpan? KeyRotationReminder { get; set; }
        public bool EnforceRotation { get; set; } = false;

        public static OperatorRecoveryConfig WithRecoveryAddress(string recoveryAddress) => new()
        {
            RecoveryAddress = recoveryAddress
        };

        public static OperatorRecoveryConfig WithInactivityRecovery(
            string recoveryAddress,
            TimeSpan inactivityThreshold) => new()
        {
            RecoveryAddress = recoveryAddress,
            InactivityThreshold = inactivityThreshold
        };

        public static OperatorRecoveryConfig WithGuardians(
            IReadOnlyList<string> guardians,
            int threshold) => new()
        {
            Guardians = guardians,
            GuardianThreshold = threshold
        };

        public static OperatorRecoveryConfig Full(
            string recoveryAddress,
            TimeSpan inactivityThreshold,
            IReadOnlyList<string> guardians,
            int guardianThreshold,
            TimeSpan? keyRotationReminder = null) => new()
        {
            RecoveryAddress = recoveryAddress,
            InactivityThreshold = inactivityThreshold,
            Guardians = guardians,
            GuardianThreshold = guardianThreshold,
            KeyRotationReminder = keyRotationReminder
        };
    }
}
