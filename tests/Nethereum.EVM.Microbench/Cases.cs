using System.Globalization;
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;

namespace Nethereum.EVM.Microbench
{
    public static class Cases
    {
        private const int Unrolled = 8;
        private const int MaxCodeSize = 24576;
        private const string ExtTarget = "0xcccccccccccccccccccccccccccccccccccccccc";
        private const string ReturnHelper = "0xdddddddddddddddddddddddddddddddddddddddd";
        private const string ProxyAddress = "0xeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
        private static readonly BigInteger MaxU256 = BigInteger.Pow(2, 256) - 1;

        public static IEnumerable<BenchCase> All()
        {
            foreach (var c in Arithmetic()) yield return c;
            foreach (var c in Memory()) yield return c;
            foreach (var c in ModExpCases.All()) yield return c;
            foreach (var c in BenchmarkShapes.All()) yield return c;
            foreach (var c in Dispatch()) yield return c;
            yield return Create2Collision();
            foreach (var t in TransferCase.All()) yield return t;
        }

        private static int CopyIterations(int size) => size > 10000 ? 500 : 1000;

        private static BigInteger Hex(string hex) => BigInteger.Parse("0" + hex, NumberStyles.HexNumber);

        internal static byte[] Loop(Action<Assembler> setup, Action<Assembler> attack, int iterations, int unrolled = Unrolled)
        {
            var a = new Assembler();
            setup(a);
            a.Push(iterations);
            int start = a.Length;
            a.Op(Opcodes.JumpDest);
            for (int i = 0; i < unrolled; i++) attack(a);
            a.Push(1).Op(Opcodes.Swap1, Opcodes.Sub).Dup(1).Push(start).Op(Opcodes.JumpI, Opcodes.Stop);
            return a.PaddedTo(MaxCodeSize, Opcodes.Stop);
        }

        private static BenchCase Arith(string name, byte op, BigInteger[] popOrder, int iterations)
        {
            int n = popOrder.Length;
            var code = Loop(
                a => { for (int i = n - 1; i >= 0; i--) a.Push32(popOrder[i]); },
                a =>
                {
                    for (int i = 0; i < n; i++) a.Dup(n + 1);
                    a.Op(op, Opcodes.Pop);
                },
                iterations);
            return new BenchCase { Name = name, Code = code, Operations = (long)iterations * Unrolled };
        }

        private static BigInteger RandomWithBits(Random rng, int bits)
        {
            var raw = new byte[32];
            rng.NextBytes(raw);
            var v = new BigInteger(raw, isUnsigned: true, isBigEndian: true);
            var mask = (BigInteger.One << bits) - 1;
            return (v & mask) | (BigInteger.One << (bits - 1));
        }

        private static IEnumerable<BenchCase> Arithmetic()
        {
            var rng = new Random(5);
            foreach (var bits in new[] { 255, 191, 127, 63 })
            {
                var a = RandomWithBits(rng, 256);
                var mod = RandomWithBits(rng, bits);
                yield return Arith($"MULMOD mod_bits={bits}", Opcodes.MulMod, new[] { a, MaxU256, mod }, 4000);
                yield return Arith($"ADDMOD mod_bits={bits}", Opcodes.AddMod, new[] { a, MaxU256, mod }, 4000);
            }
            var p = Hex("FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFEFFFFFC2F");
            var d2 = Hex("100000000000000000000000000000033");
            var d1 = Hex("10000000000000033");
            yield return Arith("DIV 2-limb divisor", Opcodes.Div, new[] { p, d2 }, 8000);
            yield return Arith("DIV 1-limb divisor", Opcodes.Div, new[] { p, d1 }, 8000);
            yield return Arith("MOD 2-limb divisor", Opcodes.Mod, new[] { p, d2 }, 8000);
            yield return Arith("MOD 4-limb divisor", Opcodes.Mod, new[] { MaxU256, p }, 8000);
            var sp = Hex("7FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFEFFFFFC2F");
            var sn = MaxU256 - 0x32;
            yield return Arith("SDIV negative divisor", Opcodes.SDiv, new[] { sp, sn }, 8000);
            yield return Arith("SMOD negative divisor", Opcodes.SMod, new[] { sp, sn }, 8000);
            yield return Arith("EXP all ones", Opcodes.Exp, new[] { MaxU256, MaxU256 }, 800);
            yield return Arith("EXP 136279841^136279841", Opcodes.Exp, new BigInteger[] { 136279841, 136279841 }, 8000);
            yield return Arith("EXP 3^3", Opcodes.Exp, new BigInteger[] { 3, 3 }, 20000);
            yield return Arith("SIGNEXTEND byte=3", Opcodes.SignExtend, new BigInteger[] { 3, 0xFFDADADA }, 40000);
            yield return Arith("SIGNEXTEND byte=15", Opcodes.SignExtend, new BigInteger[] { 15, MaxU256 - 0x1234 }, 40000);
            yield return Arith("MUL", Opcodes.Mul, new[] { MaxU256, p }, 40000);
        }

        private static BenchCase Copy(string name, byte op, int size, bool unalignedDestination, byte[] calldata, int iterations)
        {
            var code = Loop(
                a => { },
                a =>
                {
                    a.Push(size).Op(Opcodes.Push0);
                    if (unalignedDestination) a.Push(7).Op(Opcodes.Gas, Opcodes.Mod); else a.Op(Opcodes.Push0);
                    a.Op(op);
                },
                iterations);
            return new BenchCase { Name = name, Code = code, Calldata = calldata, Operations = (long)iterations * Unrolled };
        }

        private static IEnumerable<BenchCase> Memory()
        {
            foreach (var size in new[] { 32, 1024, 24576 })
            {
                yield return Copy($"CODECOPY size={size}", Opcodes.CodeCopy, size, false, Array.Empty<byte>(), CopyIterations(size));
                yield return Copy($"CODECOPY size={size} unaligned", Opcodes.CodeCopy, size, true, Array.Empty<byte>(), CopyIterations(size));
            }
            foreach (var size in new[] { 1024, 24576 })
                yield return Copy($"CALLDATACOPY size={size}", Opcodes.CalldataCopy, size, false, new byte[size], CopyIterations(size));
            foreach (var size in new[] { 512, 1024, 24576 })
                yield return ExtCodeCopy(size);
        }

        private static BenchCase ExtCodeCopy(int size)
        {
            var target = Enumerable.Repeat(Opcodes.JumpDest, size).ToArray();
            var address = ExtTarget.HexToByteArray();
            var code = Loop(a => { }, a =>
            {
                a.Push(size).Op(Opcodes.Push0, Opcodes.Push0).PushAddress(address).Op(Opcodes.ExtCodeCopy);
            }, CopyIterations(size));
            return new BenchCase
            {
                Name = $"EXTCODECOPY warm size={size}",
                Code = code,
                Operations = (long)CopyIterations(size) * Unrolled,
                OtherContracts = new[] { new KeyValuePair<string, byte[]>(ExtTarget, target) }
            };
        }

        private static BenchCase Create2Collision()
        {
            var proxy = new Assembler().Op(Opcodes.Push0, Opcodes.Push0, Opcodes.Push0, Opcodes.Push0, Opcodes.Create2).ToArray();
            var keccak = new Sha3Keccack();
            var emptyHash = keccak.CalculateHash(new byte[0]);
            var preimage = new byte[1 + 20 + 32 + 32];
            preimage[0] = 0xff;
            Array.Copy(ProxyAddress.HexToByteArray(), 0, preimage, 1, 20);
            Array.Copy(emptyHash, 0, preimage, 53, 32);
            var collision = "0x" + keccak.CalculateHash(preimage).Skip(12).ToArray().ToHex();
            int iterations = 300;
            var code = Loop(
                a => a.PushAddress(ProxyAddress.HexToByteArray()).Push(32008),
                a => a.Op(Opcodes.Push0, Opcodes.Push0, Opcodes.Push0, Opcodes.Push0, Opcodes.Push0).Dup(8).Dup(8).Op(Opcodes.Call, Opcodes.Pop),
                iterations, 1);
            return new BenchCase
            {
                Name = "CREATE2 collision via proxy CALL",
                Code = code,
                Operations = iterations,
                OtherContracts = new[]
                {
                    new KeyValuePair<string, byte[]>(ProxyAddress, proxy),
                    new KeyValuePair<string, byte[]>(collision, new byte[] { Opcodes.Invalid })
                }
            };
        }
        private const int LoopOverheadOpcodes = 7;
        private const int DispatchUnrolled = 64;

        private static BenchCase Dispatched(string name, Action<Assembler> setup, Action<Assembler> unit, int opcodesPerUnit, int iterationsAtEightUnits)
        {
            int iterations = iterationsAtEightUnits * Unrolled / DispatchUnrolled;
            var code = Loop(setup, unit, iterations, DispatchUnrolled);
            long opcodes = (long)iterations * (DispatchUnrolled * opcodesPerUnit + LoopOverheadOpcodes);
            return new BenchCase { Name = name, Code = code, Operations = opcodes };
        }

        private static IEnumerable<BenchCase> Dispatch()
        {
            yield return Dispatched("loop only", a => { }, a => { }, 0, 3200000);
            foreach (var c in Arithmetic3()) yield return c;
            foreach (var c in Jumps()) yield return c;
            foreach (var c in ExtCalls()) yield return c;
            yield return Dispatched("GAS+POP", a => { }, a => a.Op(Opcodes.Gas, Opcodes.Pop), 2, 100000);
            yield return Dispatched("PUSH1+GAS+MOD+POP", a => { }, a => a.Push(7).Op(Opcodes.Gas, Opcodes.Mod, Opcodes.Pop), 4, 100000);
            yield return Dispatched("RETURNDATASIZE+POP", a => { }, a => a.Op(Opcodes.ReturnDataSize, Opcodes.Pop), 2, 100000);
            yield return Dispatched("CALLDATALOAD(0)+POP", a => { }, a => a.Op(Opcodes.Push0, Opcodes.CallDataLoad, Opcodes.Pop), 3, 100000);
            yield return Dispatched("CALLVALUE+POP", a => { }, a => a.Op(Opcodes.CallValue, Opcodes.Pop), 2, 100000);
            yield return Dispatched("CALLDATASIZE+POP", a => { }, a => a.Op(Opcodes.CalldataSize, Opcodes.Pop), 2, 100000);
            yield return Dispatched("TLOAD+PUSH0+POP", a => { }, a => a.Op(Opcodes.Push0, Opcodes.TLoad, Opcodes.Pop), 3, 15000);
            yield return Dispatched("TSTORE+PUSH1+PUSH0", a => { }, a => a.Push(1).Op(Opcodes.Push0, Opcodes.TStore), 3, 15000);
        }

        private static IEnumerable<BenchCase> Arithmetic3()
        {
            yield return Dispatched("ADD", a => { }, a => a.Push(1).Push(2).Op(Opcodes.Add, Opcodes.Pop), 4, 100000);
            yield return Dispatched("SUB", a => { }, a => a.Push(1).Push(2).Op(Opcodes.Sub, Opcodes.Pop), 4, 100000);
            yield return Dispatched("LT", a => { }, a => a.Push(1).Push(2).Op(Opcodes.Lt, Opcodes.Pop), 4, 100000);
            yield return Dispatched("ISZERO", a => { }, a => a.Push(1).Op(Opcodes.IsZero, Opcodes.Pop), 3, 100000);
        }

        private static IEnumerable<BenchCase> Jumps()
        {
            yield return Dispatched("JUMPI fallthrough", a => { }, a => a.Op(Opcodes.Push0).Push2(0).Op(Opcodes.JumpI), 3, 100000);
            yield return Dispatched("JUMP+JUMPDEST", a => { }, a => a.Push2(a.Length + 4).Op(Opcodes.Jump, Opcodes.JumpDest), 3, 100000);
        }

        private static IEnumerable<BenchCase> ExtCalls()
        {
            yield return ExtCall("STATICCALL target 1024xPUSH1", new byte[] { 0x60, 0x01 }, 1024, 500);
            yield return ExtCall("STATICCALL target 1024xADDRESS", new byte[] { 0x30 }, 1024, 500);
            yield return ExtCall("STATICCALL target 1xADDRESS (ns per call)", new byte[] { 0x30 }, 1, 10000);
        }

        private static BenchCase ExtCall(string name, byte[] attack, int repeats, int iterations)
        {
            var target = new List<byte>();
            for (int i = 0; i < repeats; i++) target.AddRange(attack);
            var address = ExtTarget.HexToByteArray();
            var code = Loop(
                a => { },
                a => a.Op(Opcodes.Push0, Opcodes.Push0, Opcodes.Push0, Opcodes.Push0).PushAddress(address).Op(Opcodes.Gas, Opcodes.StaticCall, Opcodes.Pop),
                iterations);
            bool perCall = repeats == 1;
            long operations = perCall ? (long)iterations * Unrolled : (long)iterations * (Unrolled * (repeats * attack.Length / (attack.Length == 2 ? 2 : 1) + 8) + LoopOverheadOpcodes);
            return new BenchCase
            {
                Name = name,
                Code = code,
                Operations = operations,
                OtherContracts = new[] { new KeyValuePair<string, byte[]>(ExtTarget, target.ToArray()) }
            };
        }
    }
}