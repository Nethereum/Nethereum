using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.Util.UnitTests
{
    public class PoseidonPrecomputeGeneratorTests
    {
        private readonly ITestOutputHelper _output;

        public PoseidonPrecomputeGeneratorTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void GeneratePrecomputedConstants()
        {
            var presets = new[]
            {
                PoseidonParameterPreset.CircomT2,
                PoseidonParameterPreset.CircomT3,
                PoseidonParameterPreset.CircomT6,
            };

            var sb = new StringBuilder();
            sb.AppendLine("using Nethereum.Util;");
            sb.AppendLine();
            sb.AppendLine("namespace Nethereum.Util");
            sb.AppendLine("{");
            sb.AppendLine("    public static partial class PoseidonPrecomputedConstants");
            sb.AppendLine("    {");
            var primeEvm = EvmUInt256BigIntegerExtensions.FromBigInteger(
                PoseidonParameterFactory.GetPreset(PoseidonParameterPreset.CircomT3).Prime);
            sb.AppendLine($"        public static readonly EvmUInt256 Prime = new EvmUInt256(0x{primeEvm.U3:X16}UL, 0x{primeEvm.U2:X16}UL, 0x{primeEvm.U1:X16}UL, 0x{primeEvm.U0:X16}UL);");
            sb.AppendLine();

            foreach (var preset in presets)
            {
                var parameters = PoseidonParameterFactory.GetPreset(preset);
                var name = preset.ToString();
                var totalRounds = parameters.FullRounds + parameters.PartialRounds;
                var stateWidth = parameters.StateWidth;

                _output.WriteLine($"{name}: stateWidth={stateWidth}, fullRounds={parameters.FullRounds}, partialRounds={parameters.PartialRounds}, total={totalRounds}");

                sb.AppendLine($"        // {name}: stateWidth={stateWidth}, fullRounds={parameters.FullRounds}, partialRounds={parameters.PartialRounds}");
                sb.AppendLine($"        public static readonly EvmUInt256[,] {name}_RoundConstants = new EvmUInt256[{totalRounds}, {stateWidth}]");
                sb.AppendLine("        {");
                for (int r = 0; r < totalRounds; r++)
                {
                    sb.Append("            { ");
                    for (int c = 0; c < stateWidth; c++)
                    {
                        var val = parameters.RoundConstants[r, c];
                        var hex = BigIntegerToEvmUInt256Literal(val);
                        sb.Append(hex);
                        if (c < stateWidth - 1) sb.Append(", ");
                    }
                    sb.AppendLine(" },");
                }
                sb.AppendLine("        };");
                sb.AppendLine();

                sb.AppendLine($"        public static readonly EvmUInt256[,] {name}_MdsMatrix = new EvmUInt256[{stateWidth}, {stateWidth}]");
                sb.AppendLine("        {");
                for (int r = 0; r < stateWidth; r++)
                {
                    sb.Append("            { ");
                    for (int c = 0; c < stateWidth; c++)
                    {
                        var val = parameters.MdsMatrix[r, c];
                        var hex = BigIntegerToEvmUInt256Literal(val);
                        sb.Append(hex);
                        if (c < stateWidth - 1) sb.Append(", ");
                    }
                    sb.AppendLine(" },");
                }
                sb.AppendLine("        };");
                sb.AppendLine();
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            var outputPath = Path.Combine(
                Path.GetDirectoryName(typeof(PoseidonPrecomputeGeneratorTests).Assembly.Location),
                "..", "..", "..", "..", "..", "src", "Nethereum.Util", "PoseidonPrecomputedConstants.cs");
            outputPath = Path.GetFullPath(outputPath);
            File.WriteAllText(outputPath, sb.ToString());

            _output.WriteLine($"Written to: {outputPath}");
            _output.WriteLine($"File size: {sb.Length} chars");
            Assert.True(sb.Length > 1000);
        }

        [Fact]
        public void ValidatePrecomputedConstantsMatchBigInteger()
        {
            var parameters = PoseidonParameterFactory.GetPreset(PoseidonParameterPreset.CircomT2);
            var totalRounds = parameters.FullRounds + parameters.PartialRounds;

            int validated = 0;
            int failed = 0;
            for (int r = 0; r < totalRounds; r++)
            {
                for (int c = 0; c < parameters.StateWidth; c++)
                {
                    var bigIntVal = parameters.RoundConstants[r, c];
                    var evmVal = EvmUInt256BigIntegerExtensions.FromBigInteger(bigIntVal);
                    var roundTrip = EvmUInt256BigIntegerExtensions.ToBigInteger(evmVal);
                    if (bigIntVal != roundTrip)
                    {
                        if (failed == 0)
                        {
                            _output.WriteLine($"FAIL at [{r},{c}]:");
                            _output.WriteLine($"  original={bigIntVal}");
                            _output.WriteLine($"  roundtrip={roundTrip}");
                            _output.WriteLine($"  bits={bigIntVal.GetBitLength()}");
                            var origBytes = bigIntVal.ToByteArray(isUnsigned: true, isBigEndian: true);
                            _output.WriteLine($"  orig bytes[{origBytes.Length}]={BitConverter.ToString(origBytes).Replace("-","")}");
                            var evmBytes = evmVal.ToBigEndian();
                            _output.WriteLine($"  evm  bytes[{evmBytes.Length}]={BitConverter.ToString(evmBytes).Replace("-","")}");
                        }
                        failed++;
                    }
                    validated++;
                }
            }

            _output.WriteLine($"Validated {validated} constants, {failed} failures");
            Assert.Equal(0, failed);
        }

        private static string BigIntegerToEvmUInt256Literal(BigInteger value)
        {
            var bytes = value.ToByteArrayUnsignedBigEndian();
            var padded = Pad32(bytes);

            ulong u3 = ((ulong)padded[0] << 56) | ((ulong)padded[1] << 48) | ((ulong)padded[2] << 40) | ((ulong)padded[3] << 32)
                      | ((ulong)padded[4] << 24) | ((ulong)padded[5] << 16) | ((ulong)padded[6] << 8) | padded[7];
            ulong u2 = ((ulong)padded[8] << 56) | ((ulong)padded[9] << 48) | ((ulong)padded[10] << 40) | ((ulong)padded[11] << 32)
                      | ((ulong)padded[12] << 24) | ((ulong)padded[13] << 16) | ((ulong)padded[14] << 8) | padded[15];
            ulong u1 = ((ulong)padded[16] << 56) | ((ulong)padded[17] << 48) | ((ulong)padded[18] << 40) | ((ulong)padded[19] << 32)
                      | ((ulong)padded[20] << 24) | ((ulong)padded[21] << 16) | ((ulong)padded[22] << 8) | padded[23];
            ulong u0 = ((ulong)padded[24] << 56) | ((ulong)padded[25] << 48) | ((ulong)padded[26] << 40) | ((ulong)padded[27] << 32)
                      | ((ulong)padded[28] << 24) | ((ulong)padded[29] << 16) | ((ulong)padded[30] << 8) | padded[31];

            return $"new EvmUInt256(0x{u3:X16}UL, 0x{u2:X16}UL, 0x{u1:X16}UL, 0x{u0:X16}UL)";
        }

        private static byte[] Pad32(byte[] bytes)
        {
            if (bytes.Length >= 32)
            {
                if (bytes.Length > 32)
                {
                    var trimmed = new byte[32];
                    Array.Copy(bytes, bytes.Length - 32, trimmed, 0, 32);
                    return trimmed;
                }
                return bytes;
            }
            var padded = new byte[32];
            Array.Copy(bytes, 0, padded, 32 - bytes.Length, bytes.Length);
            return padded;
        }
    }
}
