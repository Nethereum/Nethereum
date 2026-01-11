using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Nethereum.Util
{
    public static class PoseidonParameterFactory
    {
        public const string CircomRoundSeed = "poseidon";
        public const string CircomMdsSeed = "poseidon_mds";
        private const int CircomFullRounds = 8;
        private static readonly IReadOnlyDictionary<PoseidonParameterPreset, PoseidonPresetProfile> CircomProfiles =
            new Dictionary<PoseidonParameterPreset, PoseidonPresetProfile>
            {
                [PoseidonParameterPreset.CircomT3] = new PoseidonPresetProfile(inputs: 3, partialRounds: 56),
                [PoseidonParameterPreset.CircomT6] = new PoseidonPresetProfile(inputs: 6, partialRounds: 63),
                [PoseidonParameterPreset.CircomT14] = new PoseidonPresetProfile(inputs: 14, partialRounds: 60),
                [PoseidonParameterPreset.CircomT16] = new PoseidonPresetProfile(inputs: 16, partialRounds: 68)
            };

        private static readonly ConcurrentDictionary<PoseidonParameterPreset, Lazy<PoseidonParameters>> Cache =
            new ConcurrentDictionary<PoseidonParameterPreset, Lazy<PoseidonParameters>>();

        public static PoseidonParameterPreset DefaultPreset => PoseidonParameterPreset.CircomT3;

        public static PoseidonParameters GetPreset(PoseidonParameterPreset preset)
        {
            if (!CircomProfiles.ContainsKey(preset))
            {
                throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unknown Poseidon preset");
            }

            return Cache
                .GetOrAdd(
                    preset,
                    key => new Lazy<PoseidonParameters>(
                        () => BuildCircomPreset(CircomProfiles[key]),
                        LazyThreadSafetyMode.ExecutionAndPublication))
                .Value;
        }

        private static PoseidonParameters BuildCircomPreset(PoseidonPresetProfile profile)
        {
            return PoseidonCircomParameterGenerator.Generate(profile.StateWidth, profile.PartialRounds);
        }

        private sealed class PoseidonPresetProfile
        {
            public PoseidonPresetProfile(int inputs, int partialRounds)
            {
                if (inputs <= 0) throw new ArgumentOutOfRangeException(nameof(inputs));
                StateWidth = inputs + Capacity;
                Rate = inputs;
                PartialRounds = partialRounds;
            }

            public int StateWidth { get; }
            public int Rate { get; }
            public int PartialRounds { get; }
            private const int Capacity = 1;
        }
    }
}
