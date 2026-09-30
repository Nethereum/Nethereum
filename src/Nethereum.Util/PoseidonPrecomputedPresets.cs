namespace Nethereum.Util
{
    public static partial class PoseidonPrecomputedConstants
    {
        public static PresetData GetPreset(PoseidonParameterPreset preset)
        {
            switch (preset)
            {
                case PoseidonParameterPreset.CircomT2:
                    return new PresetData(3, 2, 8, 57, 5, CircomT2_RoundConstants, CircomT2_MdsMatrix);
                case PoseidonParameterPreset.CircomT3:
                    return new PresetData(4, 3, 8, 56, 5, CircomT3_RoundConstants, CircomT3_MdsMatrix);
                case PoseidonParameterPreset.CircomT6:
                    return new PresetData(7, 6, 8, 63, 5, CircomT6_RoundConstants, CircomT6_MdsMatrix);
                default:
                    return new PresetData(4, 3, 8, 56, 5, CircomT3_RoundConstants, CircomT3_MdsMatrix);
            }
        }

        public class PresetData
        {
            public int StateWidth { get; }
            public int Rate { get; }
            public int FullRounds { get; }
            public int PartialRounds { get; }
            public int SBoxExponent { get; }
            public EvmUInt256[,] RoundConstants { get; }
            public EvmUInt256[,] MdsMatrix { get; }

            public PresetData(int stateWidth, int rate, int fullRounds, int partialRounds,
                int sBoxExponent, EvmUInt256[,] roundConstants, EvmUInt256[,] mdsMatrix)
            {
                StateWidth = stateWidth;
                Rate = rate;
                FullRounds = fullRounds;
                PartialRounds = partialRounds;
                SBoxExponent = sBoxExponent;
                RoundConstants = roundConstants;
                MdsMatrix = mdsMatrix;
            }
        }
    }
}
