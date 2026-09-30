using System;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RpcParity
{
    /// <summary>
    /// Exercises <see cref="DeepComparer"/> without a live RPC node: identical
    /// DTOs must MATCH, a scalar field difference must DIFF at its own
    /// property path, a nested-object and a list-element difference must
    /// each DIFF at their own path, and an ignored property must be
    /// suppressed. It also locks the noise-vs-real-gap distinction the
    /// harness must get right: a null and an empty collection are the same
    /// shape, a tx-type-inapplicable field is skipped, a tx-type-applicable
    /// field is never skipped, and both nodes erroring is agreement while
    /// one node erroring is not. This is the proof that the comparison
    /// engine — the whole point of the tool — actually works.
    /// </summary>
    internal static class SelfTest
    {
        private sealed class Leaf
        {
            public string Name { get; set; }
            public BigInteger Amount { get; set; }
        }

        private sealed class Sample
        {
            public string Id { get; set; }
            public HexBigInteger Balance { get; set; }
            public Leaf Nested { get; set; }
            public List<Leaf> Items { get; set; }
            public string Cosmetic { get; set; }
        }

        private static Sample Baseline() => new Sample
        {
            Id = "0xabc123",
            Balance = new HexBigInteger(1000),
            Nested = new Leaf { Name = "root", Amount = 5 },
            Items = new List<Leaf> { new Leaf { Name = "a", Amount = 1 }, new Leaf { Name = "b", Amount = 2 } },
            Cosmetic = "x"
        };

        private const int LegacyType = 0;
        private const int AccessListType = 1;
        private const int DynamicFeeType = 2;

        private static Transaction BaselineTx(int type) => new Transaction
        {
            TransactionHash = "0xtxhash",
            Type = new HexBigInteger(type),
            From = "0xfrom",
            To = "0xto",
            Gas = new HexBigInteger(21000),
            GasPrice = new HexBigInteger(1_000_000_000),
            Value = new HexBigInteger(0),
            Nonce = new HexBigInteger(1)
        };

        private static Block BaselineBlock() => new Block { Number = new HexBigInteger(100) };

        public static bool Run()
        {
            var results = new[]
            {
                Check("identical DTOs match", () => DeepComparer.Compare(Baseline(), Baseline()).Count == 0),
                Check("scalar field diff reported at its own path", () =>
                {
                    var y = Baseline();
                    y.Id = "0xdef456";
                    var diffs = DeepComparer.Compare(Baseline(), y);
                    return diffs.Count == 1 && diffs[0].Path == "$.Id";
                }),
                Check("nested object diff reported at the nested path", () =>
                {
                    var y = Baseline();
                    y.Nested.Amount = 999;
                    var diffs = DeepComparer.Compare(Baseline(), y);
                    return diffs.Count == 1 && diffs[0].Path == "$.Nested.Amount";
                }),
                Check("array element diff reported at the indexed path", () =>
                {
                    var y = Baseline();
                    y.Items[1].Name = "changed";
                    var diffs = DeepComparer.Compare(Baseline(), y);
                    return diffs.Count == 1 && diffs[0].Path == "$.Items[1].Name";
                }),
                Check("HexBigInteger compared by value, not by reference", () =>
                {
                    var x = Baseline();
                    var y = Baseline();
                    y.Balance = new HexBigInteger(1000);
                    return DeepComparer.Compare(x, y).Count == 0;
                }),
                Check("hex-string leading-zero padding is not a divergence", () =>
                {
                    var x = Baseline();
                    var y = Baseline();
                    x.Cosmetic = "0x0dead";
                    y.Cosmetic = "0xdead";
                    return DeepComparer.Compare(x, y).Count == 0;
                }),
                Check("ignore-list suppresses a named property", () =>
                {
                    var y = Baseline();
                    y.Cosmetic = "totally different";
                    var ignore = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Cosmetic" };
                    return DeepComparer.Compare(Baseline(), y, ignore).Count == 0;
                }),
                Check("accessList null vs [] is not a divergence", () =>
                {
                    var x = BaselineTx(AccessListType);
                    var y = BaselineTx(AccessListType);
                    x.AccessList = null;
                    y.AccessList = new List<AccessList>();
                    return DeepComparer.Compare(x, y).Count == 0;
                }),
                Check("accessList null vs a populated list is a real diff", () =>
                {
                    var x = BaselineTx(AccessListType);
                    var y = BaselineTx(AccessListType);
                    x.AccessList = null;
                    y.AccessList = new List<AccessList> { new AccessList { Address = "0xcontract" } };
                    var diffs = DeepComparer.Compare(x, y);
                    return diffs.Count == 1 && diffs[0].Path == "$.AccessList";
                }),
                Check("gasPrice null vs a value is a real diff on every tx type, never suppressed", () =>
                {
                    var x = BaselineTx(DynamicFeeType);
                    var y = BaselineTx(DynamicFeeType);
                    x.GasPrice = null;
                    var diffs = DeepComparer.Compare(x, y);
                    return diffs.Count == 1 && diffs[0].Path == "$.GasPrice";
                }),
                Check("maxFeePerGas absent-vs-defaulted on a legacy tx is not a divergence (type doesn't define it)", () =>
                {
                    var x = BaselineTx(LegacyType);
                    var y = BaselineTx(LegacyType);
                    x.MaxFeePerGas = null;
                    y.MaxFeePerGas = new HexBigInteger(0);
                    return DeepComparer.Compare(x, y).Count == 0;
                }),
                Check("maxFeePerGas value difference on a 1559 tx is a real diff (type defines it)", () =>
                {
                    var x = BaselineTx(DynamicFeeType);
                    var y = BaselineTx(DynamicFeeType);
                    x.MaxFeePerGas = new HexBigInteger(100);
                    y.MaxFeePerGas = new HexBigInteger(200);
                    var diffs = DeepComparer.Compare(x, y);
                    return diffs.Count == 1 && diffs[0].Path == "$.MaxFeePerGas";
                }),
                Check("withdrawals null vs a populated array is a real diff, never suppressed", () =>
                {
                    var x = BaselineBlock();
                    var y = BaselineBlock();
                    x.Withdrawals = null;
                    y.Withdrawals = new[] { new Withdrawal { Index = new HexBigInteger(1) } };
                    var diffs = DeepComparer.Compare(x, y);
                    return diffs.Count == 1 && diffs[0].Path == "$.Withdrawals";
                }),
                Check("both nodes erroring on the same call is agreement, not a diff", () =>
                    ParityRunner.ClassifyErrorVerdict("execution reverted: revert", "execution reverted") == Verdict.Match),
                Check("only one node erroring is a real divergence", () =>
                    ParityRunner.ClassifyErrorVerdict("boom", null) == Verdict.Error &&
                    ParityRunner.ClassifyErrorVerdict(null, "boom") == Verdict.Error),
                Check("neither node erroring falls through to the value comparison", () =>
                    ParityRunner.ClassifyErrorVerdict(null, null) == null)
            };

            var pass = Array.TrueForAll(results, r => r);
            Console.WriteLine();
            Console.WriteLine(pass ? "SELFTEST PASS" : "SELFTEST FAIL");
            return pass;
        }

        private static bool Check(string name, Func<bool> assertion)
        {
            bool ok;
            try
            {
                ok = assertion();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [ERROR] {name}: {ex.Message}");
                return false;
            }
            Console.WriteLine($"  [{(ok ? "ok" : "FAIL")}] {name}");
            return ok;
        }
    }
}
