using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;

namespace Nethereum.EVM.Microbench
{
    public static class BenchmarkShapes
    {
        private const long GasBudget = 14_000_000;
        private const int MaxIterations = 20000;
        private const int MaxCodeSize = 24576;
        private const string Callee = "0xcccccccccccccccccccccccccccccccccccccccc";
        private const string InitcodeTemplate = "0xdddddddddddddddddddddddddddddddddddddddd";
        private static readonly BigInteger MaxU256 = BigInteger.Pow(2, 256) - 1;

        public static IEnumerable<BenchCase> All()
        {
            foreach (var c in LogBenchmark()) yield return c;
            foreach (var c in Log()) yield return c;
            foreach (var c in MemoryAccess()) yield return c;
            foreach (var c in Mcopy()) yield return c;
            foreach (var c in ReturnDataCopy()) yield return c;
            foreach (var c in ReturnDataSizeNonZero()) yield return c;
            foreach (var c in CallDataLoad()) yield return c;
            foreach (var c in Swap()) yield return c;
            foreach (var c in Dup()) yield return c;
            foreach (var c in Push()) yield return c;
            foreach (var c in Create()) yield return c;
            foreach (var c in ReturnRevert()) yield return c;
        }

        private static int IterationsFor(long blockGas, int unrolled, long oneTimeGas)
        {
            return (int)Math.Clamp((GasBudget - oneTimeGas) / (blockGas * unrolled), 1, MaxIterations);
        }

        private static BenchCase Looped(string name, Action<Assembler> setup, Action<Assembler> attack, long blockGas, int unrolled, long oneTimeGas = 0, byte[]? calldata = null, KeyValuePair<string, byte[]>[]? others = null, EvmUInt256? balance = null, int opsPerBlock = 1)
        {
            int iterations = IterationsFor(blockGas, unrolled, oneTimeGas);
            return new BenchCase
            {
                Name = name,
                Code = Cases.Loop(setup, attack, iterations, unrolled),
                Operations = (long)iterations * unrolled * opsPerBlock,
                Calldata = calldata ?? Array.Empty<byte>(),
                OtherContracts = others ?? Array.Empty<KeyValuePair<string, byte[]>>(),
                ContractBalance = balance ?? EvmUInt256.Zero
            };
        }

        private static long ExpansionGas(long bytes)
        {
            long words = (bytes + 31) / 32;
            return 3 * words + words * words / 512;
        }

        private static void PushOffset(Assembler a, bool fixedOffset)
        {
            if (fixedOffset) a.Op(Opcodes.Push0);
            else a.Push(7).Op(Opcodes.Gas, Opcodes.Mod);
        }

        private static long OffsetGas(bool fixedOffset) => fixedOffset ? 2 : 13;

        private static void Log(Assembler a, int topics, Action<Assembler> offset)
        {
            for (int i = 0; i < topics; i++) a.Dup(2 + i);
            a.Dup(topics + 3);
            offset(a);
            a.Op((byte)(Opcodes.Log0 + topics));
        }

        private static IEnumerable<BenchCase> LogBenchmark()
        {
            for (int topics = 0; topics <= 4; topics++)
                foreach (var memSize in new[] { 0, 32, 256, 1024 })
                    foreach (var logSize in new[] { 0, 32, 256, 1024 })
                    {
                        int t = topics;
                        int m = memSize;
                        int l = logSize;
                        yield return Looped(
                            $"test_log_benchmark LOG{t} mem={m} size={l}",
                            a => a.Push(m).Op(Opcodes.Push0, Opcodes.Push0, Opcodes.CodeCopy).Push(l).Push32(MaxU256),
                            a => Log(a, t, o => o.Op(Opcodes.Push0)),
                            3 * (t + 1) + 2 + 375 * (1 + t) + 8 * l,
                            16,
                            ExpansionGas(Math.Max(m, l)));
                    }
        }

        private static IEnumerable<BenchCase> Log()
        {
            for (int topics = 0; topics <= 4; topics++)
                foreach (var (size, nonZero, label) in new[] { (0, false, "0_bytes_data"), (1024 * 1024, false, "1_MiB_zeros_data"), (1024 * 1024, true, "1_MiB_non_zero_data") })
                    foreach (var zerosTopic in new[] { true, false })
                        foreach (var fixedOffset in new[] { true, false })
                        {
                            int t = topics;
                            int s = size;
                            bool nz = nonZero;
                            bool zt = zerosTopic;
                            bool fo = fixedOffset;
                            yield return Looped(
                                $"test_log LOG{t} {label} {(zt ? "zeros_topic" : "non_zero_topic")} {(fo ? "fixed_offset" : "variable_offset")}",
                                a =>
                                {
                                    if (nz) a.Op(Opcodes.CodeSize, Opcodes.Push0, Opcodes.Push0, Opcodes.CodeCopy);
                                    a.Push(s);
                                    if (zt) a.Op(Opcodes.Push0); else a.Push32(MaxU256);
                                },
                                a => Log(a, t, o => PushOffset(o, fo)),
                                3 * (t + 1) + OffsetGas(fo) + 375 * (1 + t) + 8L * s,
                                s > 0 ? 1 : 16,
                                ExpansionGas(s));
                        }
        }

        private static IEnumerable<BenchCase> MemoryAccess()
        {
            foreach (var op in new[] { Opcodes.MLoad, Opcodes.MStore, Opcodes.MStore8 })
                foreach (var offset in new[] { 0, 1, 31 })
                    foreach (var initialized in new[] { true, false })
                        foreach (var memSize in new[] { 0, 32, 256, 1024, 10 * 1024 })
                        {
                            byte opcode = op;
                            int o = offset;
                            bool init = initialized;
                            int m = memSize;
                            string name = opcode == Opcodes.MLoad ? "MLOAD" : opcode == Opcodes.MStore ? "MSTORE" : "MSTORE8";
                            yield return Looped(
                                $"test_memory_access {name} offset={o} initialized={init} mem={m}",
                                a =>
                                {
                                    if (m > 0) a.Push(1).Push(m - 1).Op(Opcodes.MStore8);
                                    if (init) a.Push(43).Push(o).Op(Opcodes.MStore);
                                    a.Push(42).Push(o);
                                },
                                a =>
                                {
                                    if (opcode == Opcodes.MLoad) a.Dup(2).Op(Opcodes.MLoad, Opcodes.Pop);
                                    else a.Dup(3).Dup(3).Op(opcode);
                                },
                                9,
                                64,
                                ExpansionGas(Math.Max(m, 64)));
                        }
        }

        private static IEnumerable<BenchCase> Mcopy()
        {
            foreach (var memSize in new[] { 0, 32, 256, 1024, 10 * 1024, 1024 * 1024 })
                foreach (var copySize in new[] { 0, 32, 256, 1024 })
                    foreach (var fixedSrcDst in new[] { true, false })
                    {
                        int m = memSize;
                        int c = copySize;
                        bool f = fixedSrcDst;
                        yield return Looped(
                            $"test_mcopy MCOPY mem={m} copy={c} {(f ? "fixed_src_dst" : "variable_src_dst")}",
                            a =>
                            {
                                if (m == 0) return;
                                a.Op(Opcodes.Gas).Push(0).Op(Opcodes.MStore8);
                                a.Op(Opcodes.Gas).Push(m / 2).Op(Opcodes.MStore8);
                                a.Op(Opcodes.Gas).Push(m - 1).Op(Opcodes.MStore8);
                            },
                            a =>
                            {
                                a.Push(c);
                                PushOffset(a, f);
                                PushOffset(a, f);
                                a.Op(Opcodes.MCopy);
                            },
                            3 + 3 * ((c + 31) / 32) + 3 + 2 * OffsetGas(f),
                            32,
                            ExpansionGas(Math.Max(m, c + 8)));
                    }
        }

        private static byte[] ReturningHelper(int returnSize)
        {
            return new Assembler()
                .Op(Opcodes.Gas).Push(0).Op(Opcodes.MStore8)
                .Op(Opcodes.Gas).Push(returnSize / 2).Op(Opcodes.MStore8)
                .Op(Opcodes.Gas).Push(returnSize - 1).Op(Opcodes.MStore8)
                .Push(returnSize).Op(Opcodes.Push0, Opcodes.Return).ToArray();
        }

        private static void StaticCall(Assembler a, string address)
        {
            a.Op(Opcodes.Push0, Opcodes.Push0, Opcodes.Push0, Opcodes.Push0).PushAddress(address.HexToByteArray()).Op(Opcodes.Gas, Opcodes.StaticCall, Opcodes.Pop);
        }

        private static IEnumerable<BenchCase> ReturnDataCopy()
        {
            foreach (var memSize in new[] { 0, 32, 256, 1024 })
                foreach (var returnSize in new[] { 0, 32, 256, 1024, 10 * 1024, 1024 * 1024 })
                    foreach (var fixedDst in new[] { true, false })
                    {
                        int m = memSize;
                        int r = returnSize;
                        bool f = fixedDst;
                        yield return Looped(
                            $"test_returndatacopy RETURNDATACOPY mem={m} return={r} {(f ? "fixed_dst" : "variable_dst")}",
                            a =>
                            {
                                if (m > 0) a.Push(0xFF).Push(m - 1).Op(Opcodes.MStore8);
                                if (r > 0) StaticCall(a, Callee);
                            },
                            a =>
                            {
                                a.Op(Opcodes.ReturnDataSize, Opcodes.Push0);
                                PushOffset(a, f);
                                a.Op(Opcodes.ReturnDataCopy);
                            },
                            4 + 3 + OffsetGas(f) + 3 * ((r + 31) / 32),
                            16,
                            2 * ExpansionGas(r) + 1000,
                            others: r > 0 ? new[] { new KeyValuePair<string, byte[]>(Callee, ReturningHelper(r)) } : null);
                    }
        }

        private static IEnumerable<BenchCase> ReturnDataSizeNonZero()
        {
            foreach (var style in new[] { "RETURN", "REVERT", "IDENTITY" })
                foreach (var size in new[] { 0, 32, 256, 1024 })
                {
                    string s = style;
                    int n = size;
                    byte[]? helper = s == "IDENTITY" ? null : new Assembler().Push(n).Op(Opcodes.Push0, s == "RETURN" ? Opcodes.Return : Opcodes.Revert).ToArray();
                    yield return Looped(
                        $"test_returndatasize_nonzero {s} size={n}",
                        a =>
                        {
                            if (s == "IDENTITY")
                            {
                                a.Push(1).Op(Opcodes.Push0, Opcodes.MStore8);
                                a.Op(Opcodes.Push0, Opcodes.Push0).Push(n).Op(Opcodes.Push0).Push(4).Op(Opcodes.Gas, Opcodes.StaticCall, Opcodes.Pop);
                            }
                            else StaticCall(a, Callee);
                        },
                        a => a.Op(Opcodes.ReturnDataSize, Opcodes.Pop),
                        4,
                        64,
                        10000,
                        others: helper == null ? null : new[] { new KeyValuePair<string, byte[]>(Callee, helper) });
                }
        }

        private static IEnumerable<BenchCase> CallDataLoad()
        {
            foreach (var size in new[] { 0, 32, 256, 1024 })
                foreach (var zeroData in new[] { true, false })
                {
                    var calldata = zeroData ? new byte[size] : Enumerable.Range(0, size).Select(i => (byte)(i % 256)).ToArray();
                    yield return Looped(
                        $"test_calldataload CALLDATALOAD size={size} {(zeroData ? "zero_data" : "non_zero_data")}",
                        a => { },
                        a => a.Op(Opcodes.Push0, Opcodes.CallDataLoad, Opcodes.Pop),
                        7,
                        64,
                        calldata: calldata);
                }
        }

        private static IEnumerable<BenchCase> Swap()
        {
            for (int n = 1; n <= 16; n++)
            {
                int depth = n;
                yield return Looped(
                    $"test_swap SWAP{depth} (pair)",
                    a => { for (int i = 0; i <= depth; i++) a.Op(Opcodes.Push0); },
                    a => a.Swap(depth).Swap(depth),
                    6,
                    128,
                    opsPerBlock: 2);
            }
        }

        private static BenchCase CalleeLoop(string name, byte[] calleeCode, long operationsPerCall, long gasPerCall)
        {
            int iterations = (int)Math.Clamp(GasBudget / (gasPerCall + 120), 1, MaxIterations);
            return new BenchCase
            {
                Name = name,
                Code = Cases.Loop(a => { }, a => StaticCall(a, Callee), iterations, 1),
                Operations = iterations * operationsPerCall,
                OtherContracts = new[] { new KeyValuePair<string, byte[]>(Callee, calleeCode) }
            };
        }

        private static IEnumerable<BenchCase> Dup()
        {
            for (int n = 1; n <= 16; n++)
            {
                var callee = new Assembler();
                for (int i = 0; i < n; i++) callee.Op(Opcodes.Push0);
                for (int i = 0; i < 1024 - n; i++) callee.Dup(n);
                callee.Op(Opcodes.Stop);
                yield return CalleeLoop($"test_dup DUP{n}", callee.ToArray(), 1024 - n, 2L * n + 3L * (1024 - n));
            }
        }

        private static IEnumerable<BenchCase> Push()
        {
            for (int n = 0; n <= 32; n++)
            {
                var callee = new Assembler();
                for (int i = 0; i < 1024; i++)
                {
                    if (n == 0) callee.Op(Opcodes.Push0);
                    else callee.Op((byte)(0x5f + n)).Append(new byte[n - 1]).Append(new byte[] { 1 });
                }
                callee.Op(Opcodes.Stop);
                yield return CalleeLoop($"test_push PUSH{n}", callee.ToArray(), 1024, (n == 0 ? 2L : 3L) * 1024);
            }
        }

        private static IEnumerable<BenchCase> Create()
        {
            foreach (var create2 in new[] { false, true })
                foreach (var (ratio, nonZero, value) in new[]
                {
                    (0.0, false, 0), (0.0, false, 1), (0.25, true, 0), (0.25, false, 0), (0.5, true, 0), (0.5, false, 0),
                    (0.75, true, 0), (0.75, false, 0), (1.0, true, 0), (1.0, false, 0)
                })
                {
                    bool two = create2;
                    int codeSize = (int)(MaxCodeSize * ratio);
                    bool nz = nonZero;
                    int v = value;
                    var body = new Assembler();
                    if (nz) body.Push(codeSize).Op(Opcodes.Push0, Opcodes.Push0, Opcodes.CodeCopy);
                    body.Push(codeSize).Op(Opcodes.Push0, Opcodes.Return);
                    var initcode = body.ToArray();
                    if (nz && initcode.Length < codeSize)
                    {
                        var padded = new byte[codeSize];
                        Array.Copy(initcode, padded, initcode.Length);
                        for (int i = initcode.Length; i < codeSize; i++) padded[i] = (byte)(i % 256);
                        initcode = padded;
                    }
                    long words = (initcode.Length + 31) / 32;
                    long blockGas = 32000 + 200L * codeSize + 2 * ExpansionGas(Math.Max(codeSize, initcode.Length)) + 2 * words + 100 + (two ? 6 * words : 0);
                    var size = ratio == 0 ? "0 bytes " + (v > 0 ? "with value" : "without value") : ratio.ToString("0.00") + "x max code size " + (nz ? "non-zero data" : "zero data");
                    yield return Looped(
                        $"test_create {(two ? "CREATE2" : "CREATE")} {size}",
                        a => a.Push(initcode.Length).Op(Opcodes.Push0, Opcodes.Push0).PushAddress(InitcodeTemplate.HexToByteArray()).Op(Opcodes.ExtCodeCopy),
                        a =>
                        {
                            if (two) a.Dup(1);
                            a.Push(initcode.Length).Op(Opcodes.Push0).Push(v).Op(two ? Opcodes.Create2 : Opcodes.Create, Opcodes.Pop);
                        },
                        blockGas,
                        1,
                        ExpansionGas(initcode.Length) + 3000,
                        others: new[] { new KeyValuePair<string, byte[]>(InitcodeTemplate, initcode) },
                        balance: v > 0 ? new EvmUInt256(1_000_000_000) : null);
                }
        }

        private static IEnumerable<BenchCase> ReturnRevert()
        {
            foreach (var op in new[] { Opcodes.Return, Opcodes.Revert })
                foreach (var (size, nonZero, label) in new[] { (0, false, "empty"), (1024, true, "1KiB of non-zero data"), (1024, false, "1KiB of zero data"), (1024 * 1024, true, "1MiB of non-zero data"), (1024 * 1024, false, "1MiB of zero data") })
                {
                    byte opcode = op;
                    var callee = new Assembler();
                    if (nonZero) callee.Push(size).Op(Opcodes.Push0, Opcodes.Push0, Opcodes.CodeCopy);
                    callee.Push(size).Op(Opcodes.Push0, opcode);
                    var code = callee.PaddedTo(MaxCodeSize, Opcodes.Invalid);
                    long gasPerCall = 100 + 20 + (nonZero ? 3L * ((size + 31) / 32) : 0) + ExpansionGas(size);
                    yield return CalleeLoop($"test_return_revert {(opcode == Opcodes.Return ? "RETURN" : "REVERT")} {label}", code, 1, gasPerCall);
                }
        }
    }
}
