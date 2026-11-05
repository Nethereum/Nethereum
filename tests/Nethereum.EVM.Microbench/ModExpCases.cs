using System.Numerics;
using Nethereum.EVM.Execution.Precompiles.GasCalculators;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.EVM.Microbench
{
    public static class ModExpCases
    {
        private const long GasBudget = 14_000_000;
        private const int MaxIterations = 4000;
        private const int MinOperations = 200;

        public static IEnumerable<BenchCase> All()
        {
            foreach (var shape in ModExpExplicitShapes.All)
                yield return Case(shape.Id, shape.Base, shape.Exponent, shape.Modulus);
            foreach (var c in Random()) yield return c;
            yield return Case("mod_vul_pawel_3_exp_8", "000000000000000000000000000000000000000000000000000000000000001700000000000000000000000000000000000000000000000000000000000000080000000000000000000000000000000000000000000000000000000000000017bffffffffffffffffffffffffffffffffffffffffffffbffffffffffffffffff7ffffffffffffffffffffffffffffffffffffffffffe".HexToByteArray());
            foreach (var bytes in new[] { 32, 64, 128 })
                yield return Case($"uncachable mod_odd_{bytes}b_exp_{bytes * 8}", Repeat("ff", bytes), Repeat("ff", bytes), Repeat("ff", bytes - 1) + "01");
        }

        private static string Repeat(string hex, int count) => string.Concat(Enumerable.Repeat(hex, count));

        private static IEnumerable<BenchCase> Random()
        {
            var rng = new System.Random(42);
            foreach (var modulusBits in new[] { 32, 64, 196, 256 })
                foreach (var exponentBits in new[] { 17, 33, 65 })
                    foreach (var arity in new[] { 0, 8 })
                        yield return RandomCase($"mod_{modulusBits}_exp_{exponentBits}_2arity_{arity}", rng, modulusBits, exponentBits, arity);
            foreach (var (modulusBits, exponentBits) in new[] { (64, 33), (128, 33), (256, 65) })
                yield return RandomCase($"mod_pow2_{modulusBits}_exp_{exponentBits}", rng, modulusBits, exponentBits, modulusBits - 1);
        }

        private static BenchCase RandomCase(string id, System.Random rng, int modulusBits, int exponentBits, int arity)
        {
            var modulus = RandomBits(rng, modulusBits);
            modulus &= ~((BigInteger.One << arity) - 1);
            modulus |= BigInteger.One << arity;
            return Case(id, Hex(RandomBits(rng, modulusBits), modulusBits), Hex(RandomBits(rng, exponentBits), exponentBits), Hex(modulus, modulusBits));
        }

        private static BigInteger RandomBits(System.Random rng, int bits)
        {
            var raw = new byte[(bits + 7) / 8 + 1];
            rng.NextBytes(raw);
            raw[^1] = 0;
            var value = new BigInteger(raw) & ((BigInteger.One << (bits - 1)) - 1);
            return value | (BigInteger.One << (bits - 1));
        }

        private static string Hex(BigInteger value, int bits)
        {
            var bytes = value.ToByteArray(isUnsigned: true, isBigEndian: true);
            var width = (bits + 7) / 8;
            var padded = new byte[Math.Max(width, bytes.Length)];
            Array.Copy(bytes, 0, padded, padded.Length - bytes.Length, bytes.Length);
            return padded.ToHex();
        }

        private static BenchCase Case(string id, string baseHex, string exponentHex, string modulusHex) =>
            Case(id, Calldata(baseHex.HexToByteArray(), exponentHex.HexToByteArray(), modulusHex.HexToByteArray()));

        private static BenchCase Case(string id, byte[] calldata)
        {
            long gas = new Eip7883ModExpGasCalculator().GetGasCost(calldata);
            int iterations = (int)Math.Clamp(GasBudget / Math.Max(gas, 1), 1, MaxIterations);
            var code = Cases.Loop(
                a => a.Op(Opcodes.CalldataSize, Opcodes.Push0, Opcodes.Push0, Opcodes.CalldataCopy),
                a => a.Op(Opcodes.Push0, Opcodes.Push0, Opcodes.CalldataSize, Opcodes.Push0).Push(5).Op(Opcodes.Gas, Opcodes.StaticCall, Opcodes.Pop),
                iterations,
                1);
            return new BenchCase { Name = "MODEXP " + id, Code = code, Calldata = calldata, Operations = iterations };
        }

        private static byte[] Calldata(byte[] b, byte[] e, byte[] m)
        {
            var data = new byte[96 + b.Length + e.Length + m.Length];
            data[31] = (byte)b.Length; data[30] = (byte)(b.Length >> 8);
            data[63] = (byte)e.Length; data[62] = (byte)(e.Length >> 8);
            data[95] = (byte)m.Length; data[94] = (byte)(m.Length >> 8);
            Array.Copy(b, 0, data, 96, b.Length);
            Array.Copy(e, 0, data, 96 + b.Length, e.Length);
            Array.Copy(m, 0, data, 96 + b.Length + e.Length, m.Length);
            return data;
        }
    }
}
