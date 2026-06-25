using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.EVM.UnitTests.GeneralStateTests
{
    /// <summary>
    /// Drives the existing <see cref="GeneralStateTestRunner"/> against the
    /// pre-EEST legacy fork fixtures at
    /// <c>external/legacytests/Constantinople/GeneralStateTests/</c>. The
    /// runner already supports any fork via its constructor; the gap was just
    /// that no existing test pointed it at legacy data.
    /// <para>
    /// Smoking-gun validation for the pre-EIP-158 recipient empty-account
    /// fix landed in <see cref="Nethereum.CoreChain.TransactionProcessor"/>:
    /// <c>stTransitionTest/createNameRegistratorPerTxsBefore.json</c> has
    /// post-state assertions for Frontier, Homestead, EIP150, EIP158,
    /// Byzantium, Constantinople, ConstantinopleFix — exercising both the
    /// "create empty touched account" and "don't create empty touched account"
    /// halves of the EIP-158 transition.
    /// </para>
    /// </summary>
    public class FrontierLegacyTests
    {
        private readonly ITestOutputHelper _output;
        public FrontierLegacyTests(ITestOutputHelper output) { _output = output; }

        private static string LegacyTestsPath => LegacyTestsRoot("Constantinople");
        private static string LegacyTestsCancunPath => LegacyTestsRoot("Cancun");

        private static string LegacyTestsRoot(string branch)
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "Nethereum.slnx")) ||
                    File.Exists(Path.Combine(dir.FullName, "Nethereum.sln")))
                    return Path.Combine(dir.FullName, "external", "legacytests",
                        branch, "GeneralStateTests");
                dir = dir.Parent;
            }
            return null;
        }

        [Fact]
        [Trait("Category", "LegacyFork-Frontier")]
        public async Task StTransitionTest_createNameRegistratorPerTxsBefore_AtFrontier()
        {
            var root = LegacyTestsPath;
            if (root == null || !Directory.Exists(root))
            {
                _output.WriteLine("legacytests not cloned. Run: git -C external clone --depth=1 https://github.com/ethereum/legacytests");
                return;
            }

            var filePath = Path.Combine(root, "stTransitionTest", "createNameRegistratorPerTxsBefore.json");
            Assert.True(File.Exists(filePath), $"Expected fixture at {filePath}");

            var runner = new GeneralStateTestRunner(_output, targetHardfork: "Frontier");
            var result = await runner.RunTestWithExecutorAsync(filePath);

            var passed = result.PassedCount;
            var failed = result.FailedCount;
            var skipped = result.SkippedCount;
            _output.WriteLine($"Frontier results: passed={passed} failed={failed} skipped={skipped}");

            foreach (var r in result.Results.Where(x => !x.Passed && !x.Skipped))
                _output.WriteLine($"  FAIL d{r.DataIndex} g{r.GasIndex} v{r.ValueIndex}: {r.Message}");

            Assert.True(failed == 0,
                $"{failed} Frontier sub-test(s) failed in createNameRegistratorPerTxsBefore — pre-EIP-158 recipient creation may still be missing");
            Assert.True(passed > 0,
                "Test ran 0 sub-tests; check that legacytests has Frontier post-state");
        }

        [Theory]
        [Trait("Category", "LegacyFork-Frontier")]
        [InlineData("stTransitionTest", "createNameRegistratorPerTxsBefore.json")]
        [InlineData("stTransitionTest", "createNameRegistratorPerTxsAt.json")]
        [InlineData("stTransitionTest", "createNameRegistratorPerTxsAfter.json")]
        [InlineData("stTransitionTest", "delegatecallBeforeTransition.json")]
        [InlineData("stTransitionTest", "delegatecallAtTransition.json")]
        [InlineData("stTransitionTest", "delegatecallAfterTransition.json")]
        public async Task StTransitionTest_AcrossFiles_AtFrontier(string category, string fileName)
        {
            var root = LegacyTestsPath;
            if (root == null || !Directory.Exists(root))
            {
                _output.WriteLine("legacytests not cloned; skipping.");
                return;
            }

            var filePath = Path.Combine(root, category, fileName);
            Assert.True(File.Exists(filePath), $"Fixture missing: {filePath}");

            var runner = new GeneralStateTestRunner(_output, targetHardfork: "Frontier");
            var result = await runner.RunTestWithExecutorAsync(filePath);

            var fails = result.Results.Where(r => !r.Passed && !r.Skipped).ToList();
            _output.WriteLine($"{category}/{fileName} @ Frontier: passed={result.PassedCount} failed={result.FailedCount} skipped={result.SkippedCount}");
            foreach (var r in fails)
                _output.WriteLine($"  FAIL {r.TestName}[d{r.DataIndex},g{r.GasIndex},v{r.ValueIndex}]: {r.Message}");

            Assert.Empty(fails);
        }

        /// <summary>
        /// Print every account in our post-state for a single test case. The
        /// runner's built-in diff only shows accounts that changed from
        /// pre-state, which hides extra/missing accounts. This dumps the
        /// full computed post-state via reflection on the test runner's
        /// internal ExtractPostState.
        /// </summary>
        [Theory]
        [Trait("Category", "LegacyFork-Debug")]
        [InlineData("stCallCodes", "callcodeEmptycontract.json", "Frontier")]
        [InlineData("stCallCodes", "callcall_00_OOGE_valueTransfer.json", "Frontier")]
        [InlineData("stCreateTest", "CREATE_EmptyContractAndCallIt_0wei.json", "Frontier")]
        [InlineData("stTransitionTest", "delegatecallAtTransition.json", "Homestead")]
        public async Task Debug_DumpFullPostState(string category, string fileName, string fork)
        {
            var root = LegacyTestsPath;
            if (root == null || !Directory.Exists(root)) return;
            var filePath = Path.Combine(root, category, fileName);
            Assert.True(File.Exists(filePath));

            var runner = new GeneralStateTestRunner(_output, targetHardfork: fork);
            var result = await runner.RunTestWithExecutorAsync(filePath);

            _output.WriteLine($"=== {category}/{fileName} @ {fork} ===");
            foreach (var r in result.Results)
            {
                _output.WriteLine($"  {(r.Passed ? "PASS" : "FAIL")} {r.TestName}: {r.Message}");
                _output.WriteLine($"    expected stateRoot: {r.ExpectedStateRoot}");
                _output.WriteLine($"    actual   stateRoot: {r.ActualStateRoot}");
                if (r.AccountDiffs != null)
                {
                    _output.WriteLine($"    AccountDiffs ({r.AccountDiffs.Count}):");
                    foreach (var d in r.AccountDiffs) _output.WriteLine($"      {d}");
                }
                if (r.FullPostState != null)
                {
                    _output.WriteLine($"    FullPostState ({r.FullPostState.Count} accounts):");
                    foreach (var kvp in r.FullPostState)
                        _output.WriteLine($"      {kvp.Key}: {kvp.Value}");
                }
            }
        }

        /// <summary>
        /// Debug-focused single-file run that ALWAYS dumps the AccountDiffs
        /// produced by the runner on a state-root mismatch. Use when a sweep
        /// has flagged a specific JSON and we need account-level visibility.
        /// </summary>
        [Theory]
        [Trait("Category", "LegacyFork-Debug")]
        [InlineData("stCallCodes", "callcodeEmptycontract.json", "Frontier")]
        [InlineData("stCallCodes", "callcall_00_OOGE_valueTransfer.json", "Frontier")]
        [InlineData("stCallCodes", "callcodeInInitcodeToEmptyContract.json", "Frontier")]
        [InlineData("stCallCodes", "callcall_00_OOGE_valueTransfer.json", "Frontier")]
        [InlineData("stTransitionTest", "delegatecallBeforeTransition.json", "Homestead")]
        [InlineData("stTransitionTest", "delegatecallAtTransition.json", "Homestead")]
        [InlineData("stTransitionTest", "delegatecallAfterTransition.json", "Homestead")]
        [InlineData("stCreateTest", "CREATE_ContractRETURNBigOffset.json", "Frontier")]
        public async Task Debug_SingleFile_DumpDiffs(string category, string fileName, string fork)
        {
            var root = LegacyTestsPath;
            if (root == null || !Directory.Exists(root))
            {
                _output.WriteLine("legacytests not cloned; skipping.");
                return;
            }

            var filePath = Path.Combine(root, category, fileName);
            Assert.True(File.Exists(filePath), $"Fixture missing: {filePath}");

            var runner = new GeneralStateTestRunner(_output, targetHardfork: fork);
            var result = await runner.RunTestWithExecutorAsync(filePath);

            _output.WriteLine($"\n=== {category}/{fileName} @ {fork} ===");
            _output.WriteLine($"passed={result.PassedCount} failed={result.FailedCount} skipped={result.SkippedCount}");

            foreach (var r in result.Results)
            {
                if (r.Skipped)
                {
                    _output.WriteLine($"  SKIP {r.TestName}: {r.SkipReason}");
                    continue;
                }
                _output.WriteLine($"  {(r.Passed ? "PASS" : "FAIL")} {r.TestName}[d{r.DataIndex},g{r.GasIndex},v{r.ValueIndex}]: {r.Message}");
                if (!r.Passed && r.AccountDiffs != null)
                {
                    foreach (var diff in r.AccountDiffs)
                        _output.WriteLine($"      {diff}");
                }
            }
        }

        [Fact]
        [Trait("Category", "LegacyFork-Debug")]
        public async Task Debug_TransactionCollisionToEmpty_London()
        {
            var root = LegacyTestsCancunPath;
            if (root == null || !Directory.Exists(root)) { _output.WriteLine("skip"); return; }
            var filePath = Path.Combine(root, "stCreateTest", "TransactionCollisionToEmpty.json");
            Assert.True(File.Exists(filePath), $"Fixture missing: {filePath}");
            var runner = new GeneralStateTestRunner(_output, targetHardfork: "London");
            var result = await runner.RunTestWithExecutorAsync(filePath);
            _output.WriteLine($"\n=== TransactionCollisionToEmpty @ London ===");
            _output.WriteLine($"passed={result.PassedCount} failed={result.FailedCount}");
            foreach (var r in result.Results)
            {
                if (r.Skipped) continue;
                _output.WriteLine($"  {(r.Passed ? "PASS" : "FAIL")} [d{r.DataIndex},g{r.GasIndex},v{r.ValueIndex}]: {r.Message}");
                if (!r.Passed && r.AccountDiffs != null)
                    foreach (var diff in r.AccountDiffs)
                        _output.WriteLine($"      {diff}");
            }
        }

        [Theory]
        [Trait("Category", "LegacyFork-Debug")]
        [InlineData("TouchToEmptyAccountRevert.json")]
        [InlineData("TouchToEmptyAccountRevert2.json")]
        [InlineData("TouchToEmptyAccountRevert3.json")]
        [InlineData("RevertPrecompiledTouch_storage.json")]
        [InlineData("RevertPrefoundEmptyOOG.json")]
        [InlineData("RevertPrefoundEmptyCallOOG.json")]
        public async Task Debug_TouchRevert_Constantinople(string fileName)
        {
            var root = LegacyTestsPath;
            if (root == null || !Directory.Exists(root)) { _output.WriteLine("skip"); return; }
            var filePath = Path.Combine(root, "stRevertTest", fileName);
            Assert.True(File.Exists(filePath), $"Fixture missing: {filePath}");
            var runner = new GeneralStateTestRunner(_output, targetHardfork: "Constantinople");
            var result = await runner.RunTestWithExecutorAsync(filePath);
            _output.WriteLine($"\n=== {fileName} @ Constantinople ===");
            _output.WriteLine($"passed={result.PassedCount} failed={result.FailedCount}");
            foreach (var r in result.Results)
            {
                if (r.Skipped) continue;
                _output.WriteLine($"  {(r.Passed ? "PASS" : "FAIL")} [d{r.DataIndex},g{r.GasIndex},v{r.ValueIndex}]: {r.Message}");
            }
        }

        [Theory]
        [Trait("Category", "LegacyFork-Debug")]
        [InlineData("TouchToEmptyAccountRevert.json", "Cancun")]
        [InlineData("TouchToEmptyAccountRevert2.json", "Cancun")]
        [InlineData("RevertPrecompiledTouch.json", "Cancun")]
        [InlineData("RevertPrefoundEmptyOOG.json", "Cancun")]
        public async Task Debug_TouchRevert_Cancun(string fileName, string fork)
        {
            var root = LegacyTestsCancunPath;
            if (root == null || !Directory.Exists(root)) { _output.WriteLine("skip"); return; }
            var filePath = Path.Combine(root, "stRevertTest", fileName);
            Assert.True(File.Exists(filePath), $"Fixture missing: {filePath}");
            var runner = new GeneralStateTestRunner(_output, targetHardfork: fork);
            var result = await runner.RunTestWithExecutorAsync(filePath);
            _output.WriteLine($"\n=== {fileName} @ {fork} ===");
            _output.WriteLine($"passed={result.PassedCount} failed={result.FailedCount}");
            foreach (var r in result.Results)
            {
                if (r.Skipped) continue;
                _output.WriteLine($"  {(r.Passed ? "PASS" : "FAIL")} [d{r.DataIndex},g{r.GasIndex},v{r.ValueIndex}]: {r.Message}");
            }
        }

        [Fact]
        [Trait("Category", "LegacyFork-Debug")]
        public async Task Debug_Buffer_Cancun()
        {
            var root = LegacyTestsCancunPath;
            if (root == null || !Directory.Exists(root)) { _output.WriteLine("skip"); return; }
            var filePath = Path.Combine(root, "stMemoryTest", "buffer.json");
            Assert.True(File.Exists(filePath), $"Fixture missing: {filePath}");
            var runner = new GeneralStateTestRunner(_output, targetHardfork: "Cancun");
            var result = await runner.RunTestWithExecutorAsync(filePath);
            _output.WriteLine($"\n=== buffer.json @ Cancun ===");
            _output.WriteLine($"passed={result.PassedCount} failed={result.FailedCount}");
        }

        [Fact]
        [Trait("Category", "LegacyFork-Debug")]
        public async Task Debug_RevertPrecompiledTouch_Constantinople()
        {
            var root = LegacyTestsPath;
            if (root == null || !Directory.Exists(root))
            {
                _output.WriteLine("legacytests not cloned; skipping.");
                return;
            }

            var filePath = Path.Combine(root, "stRevertTest", "RevertPrecompiledTouch.json");
            Assert.True(File.Exists(filePath), $"Fixture missing: {filePath}");

            var runner = new GeneralStateTestRunner(_output, targetHardfork: "Constantinople");
            var result = await runner.RunTestWithExecutorAsync(filePath);

            _output.WriteLine($"\n=== RevertPrecompiledTouch @ Constantinople ===");
            _output.WriteLine($"passed={result.PassedCount} failed={result.FailedCount} skipped={result.SkippedCount}");

            foreach (var r in result.Results)
            {
                if (r.Skipped) continue;
                _output.WriteLine($"  {(r.Passed ? "PASS" : "FAIL")} [d{r.DataIndex},g{r.GasIndex},v{r.ValueIndex}]: {r.Message}");
                if (!r.Passed && r.AccountDiffs != null)
                    foreach (var diff in r.AccountDiffs)
                        _output.WriteLine($"      {diff}");
                if (r.FullPostState != null && (r.DataIndex == 0 || r.DataIndex == 1))
                {
                    _output.WriteLine($"      --- d{r.DataIndex} full post-state ({r.FullPostState.Count} accounts) ---");
                    foreach (var entry in r.FullPostState)
                        _output.WriteLine($"      {entry.Key}: {entry.Value}");
                }
            }

            Assert.True(result.FailedCount == 0,
                $"{result.FailedCount} sub-test(s) of RevertPrecompiledTouch failed at Constantinople");
        }

        /// <summary>
        /// Files where the sweep is known to fail because of a separate,
        /// tracked EVM bug. Skipping them keeps the sweep useful as a CI guard
        /// against regressions in the rest of the category while the listed
        /// bugs are worked on independently. Each entry should reference a
        /// task or follow-up note.
        /// </summary>
        private static bool IsKnownEvmDivergence(string category, string fork, string fileName)
        {
            return false;
        }

        /// <summary>
        /// Sweep an entire category folder at Frontier rules and produce a
        /// per-file pass/fail breakdown. Doesn't fail the test on individual
        /// file failures (yet) — its job is to surface the work list so we
        /// can fix bugs systematically rather than one mainnet block at a
        /// time. Tag it [Trait("Category","LegacyFork-Sweep")] so CI can
        /// opt out of the noisy report.
        /// </summary>
        [Theory]
        [Trait("Category", "LegacyFork-Sweep")]
        [InlineData("stTransitionTest", "Frontier")]
        [InlineData("stTransitionTest", "Homestead")]
        [InlineData("stTransitionTest", "EIP150")]
        [InlineData("stTransitionTest", "EIP158")]
        [InlineData("stCreateTest", "Frontier")]
        [InlineData("stCallCodes", "Frontier")]
        public Task Sweep_Category_AtFork(string category, string fork)
            => SweepCategoryAtForkAsync(LegacyTestsPath, category, fork);

        /// <summary>
        /// Forks for which the <c>legacytests/Constantinople</c> fixture tree
        /// has post-state hashes. Anything later (Istanbul+) needs the
        /// <c>legacytests/Cancun</c> fixture tree instead.
        /// </summary>
        public static readonly string[] ConstantinopleForks =
        {
            "Frontier", "Homestead", "EIP150", "EIP158",
            "Byzantium", "Constantinople", "ConstantinopleFix"
        };

        /// <summary>
        /// Forks for which the <c>legacytests/Cancun</c> fixture tree carries
        /// post-state hashes. The same tree also contains some older-fork
        /// entries but the canonical coverage is the modern set.
        /// </summary>
        public static readonly string[] CancunForks =
        {
            "Istanbul", "Berlin", "London", "Paris", "Shanghai", "Cancun"
        };

        public static System.Collections.Generic.IEnumerable<object[]> ConstantinopleFullMatrix()
            => EnumerateMatrix(LegacyTestsRoot("Constantinople"), ConstantinopleForks);

        public static System.Collections.Generic.IEnumerable<object[]> CancunFullMatrix()
            => EnumerateMatrix(LegacyTestsRoot("Cancun"), CancunForks);

        // Per-fork MemberData providers so the matrix can be run one fork at a
        // time (~50 cells per fork instead of 350 all-at-once). The single
        // FullMatrix provider is kept for one-shot CI runs; the per-fork
        // providers exist for fast-feedback dev runs where a single broken
        // fork shouldn't have to wait behind six others.
        public static System.Collections.Generic.IEnumerable<object[]> Constantinople_Frontier()         => EnumerateMatrix(LegacyTestsRoot("Constantinople"), new[] { "Frontier" });
        public static System.Collections.Generic.IEnumerable<object[]> Constantinople_Homestead()        => EnumerateMatrix(LegacyTestsRoot("Constantinople"), new[] { "Homestead" });
        public static System.Collections.Generic.IEnumerable<object[]> Constantinople_EIP150()           => EnumerateMatrix(LegacyTestsRoot("Constantinople"), new[] { "EIP150" });
        public static System.Collections.Generic.IEnumerable<object[]> Constantinople_EIP158()           => EnumerateMatrix(LegacyTestsRoot("Constantinople"), new[] { "EIP158" });
        public static System.Collections.Generic.IEnumerable<object[]> Constantinople_Byzantium()        => EnumerateMatrix(LegacyTestsRoot("Constantinople"), new[] { "Byzantium" });
        public static System.Collections.Generic.IEnumerable<object[]> Constantinople_Constantinople()  => EnumerateMatrix(LegacyTestsRoot("Constantinople"), new[] { "Constantinople" });
        public static System.Collections.Generic.IEnumerable<object[]> Constantinople_ConstantinopleFix()=> EnumerateMatrix(LegacyTestsRoot("Constantinople"), new[] { "ConstantinopleFix" });
        public static System.Collections.Generic.IEnumerable<object[]> Cancun_Istanbul() => EnumerateMatrix(LegacyTestsRoot("Cancun"), new[] { "Istanbul" });
        public static System.Collections.Generic.IEnumerable<object[]> Cancun_Berlin()   => EnumerateMatrix(LegacyTestsRoot("Cancun"), new[] { "Berlin" });
        public static System.Collections.Generic.IEnumerable<object[]> Cancun_London()   => EnumerateMatrix(LegacyTestsRoot("Cancun"), new[] { "London" });
        public static System.Collections.Generic.IEnumerable<object[]> Cancun_Paris()    => EnumerateMatrix(LegacyTestsRoot("Cancun"), new[] { "Paris" });
        public static System.Collections.Generic.IEnumerable<object[]> Cancun_Shanghai() => EnumerateMatrix(LegacyTestsRoot("Cancun"), new[] { "Shanghai" });
        public static System.Collections.Generic.IEnumerable<object[]> Cancun_Cancun()   => EnumerateMatrix(LegacyTestsRoot("Cancun"), new[] { "Cancun" });

        /// <summary>
        /// Categories excluded from the full-matrix sweep — usually because
        /// they contain absurdly-large sub-test grids that would either swamp
        /// the test host's per-test output buffer (xunit's StringBuilder caps
        /// at ~2 GB) or take many minutes to run, dwarfing the rest of the
        /// matrix. They can still be exercised individually by adding to the
        /// targeted <c>Sweep_Category_AtFork</c> InlineData list above.
        /// </summary>
        private static readonly System.Collections.Generic.HashSet<string> MatrixSkipCategories =
            new(System.StringComparer.OrdinalIgnoreCase)
            {
                "stTimeConsuming",
                // VMTests is an old VM-test format (not GeneralStateTests JSON);
                // our GeneralStateTestRunner hangs trying to parse it. Excluding
                // until a VM-test-aware adapter is wired up.
                "VMTests",
                // Pyspecs and Shanghai/Cancun subfolders inside the Cancun
                // legacytests tree are nested fixture sets, not flat category
                // dirs of state-test JSON; the runner would try to load files
                // it can't deserialize and either hang or spam errors.
                "Pyspecs",
                "Shanghai",
                "Cancun"
            };

        private static System.Collections.Generic.IEnumerable<object[]> EnumerateMatrix(string root, string[] forks)
        {
            if (root == null || !Directory.Exists(root)) yield break;
            foreach (var dir in Directory.EnumerateDirectories(root).OrderBy(d => d, System.StringComparer.Ordinal))
            {
                var cat = Path.GetFileName(dir);
                if (MatrixSkipCategories.Contains(cat)) continue;
                foreach (var fork in forks)
                    yield return new object[] { cat, fork };
            }
        }

        /// <summary>
        /// Full-matrix sweep over the <c>Constantinople</c> fixture tree. One
        /// test per (category, fork) cell. Cells where the fixture has no
        /// post-state for the requested fork pass silently with 0 sub-tests.
        /// </summary>
        [Theory]
        [Trait("Category", "LegacyFork-Sweep-Full")]
        [MemberData(nameof(ConstantinopleFullMatrix))]
        public Task Sweep_Constantinople_FullMatrix(string category, string fork)
            => SweepCategoryAtForkAsync(LegacyTestsPath, category, fork);

        /// <summary>
        /// Full-matrix sweep over the <c>Cancun</c> fixture tree. Covers the
        /// modern hardforks (Istanbul through Cancun) that the Constantinople
        /// tree predates.
        /// </summary>
        [Theory]
        [Trait("Category", "LegacyFork-Sweep-Full")]
        [MemberData(nameof(CancunFullMatrix))]
        public Task Sweep_Cancun_FullMatrix(string category, string fork)
            => SweepCategoryAtForkAsync(LegacyTestsCancunPath, category, fork);

        // Per-fork sweep entry points. Run individually for fast feedback:
        //   dotnet test --filter FullyQualifiedName~Sweep_Frontier
        // Each is ~50 cells (one per category) — finishes in 1-3 minutes
        // depending on which categories have heavy precompile / recursion tests.

        [Theory][Trait("Category","LegacyFork-Sweep-PerFork")][MemberData(nameof(Constantinople_Frontier))]         public Task SweepFork_Frontier(string category, string fork)         => SweepCategoryAtForkAsync(LegacyTestsPath, category, fork);
        [Theory][Trait("Category","LegacyFork-Sweep-PerFork")][MemberData(nameof(Constantinople_Homestead))]        public Task SweepFork_Homestead(string category, string fork)        => SweepCategoryAtForkAsync(LegacyTestsPath, category, fork);
        [Theory][Trait("Category","LegacyFork-Sweep-PerFork")][MemberData(nameof(Constantinople_EIP150))]           public Task SweepFork_EIP150(string category, string fork)           => SweepCategoryAtForkAsync(LegacyTestsPath, category, fork);
        [Theory][Trait("Category","LegacyFork-Sweep-PerFork")][MemberData(nameof(Constantinople_EIP158))]           public Task SweepFork_EIP158(string category, string fork)           => SweepCategoryAtForkAsync(LegacyTestsPath, category, fork);
        [Theory][Trait("Category","LegacyFork-Sweep-PerFork")][MemberData(nameof(Constantinople_Byzantium))]        public Task SweepFork_Byzantium(string category, string fork)        => SweepCategoryAtForkAsync(LegacyTestsPath, category, fork);
        [Theory][Trait("Category","LegacyFork-Sweep-PerFork")][MemberData(nameof(Constantinople_Constantinople))]  public Task SweepFork_Constantinople(string category, string fork)  => SweepCategoryAtForkAsync(LegacyTestsPath, category, fork);
        [Theory][Trait("Category","LegacyFork-Sweep-PerFork")][MemberData(nameof(Constantinople_ConstantinopleFix))]public Task SweepFork_ConstantinopleFix(string category, string fork)=> SweepCategoryAtForkAsync(LegacyTestsPath, category, fork);
        [Theory][Trait("Category","LegacyFork-Sweep-PerFork")][MemberData(nameof(Cancun_Istanbul))] public Task SweepFork_Istanbul(string category, string fork) => SweepCategoryAtForkAsync(LegacyTestsCancunPath, category, fork);
        [Theory][Trait("Category","LegacyFork-Sweep-PerFork")][MemberData(nameof(Cancun_Berlin))]   public Task SweepFork_Berlin(string category, string fork)   => SweepCategoryAtForkAsync(LegacyTestsCancunPath, category, fork);
        [Theory][Trait("Category","LegacyFork-Sweep-PerFork")][MemberData(nameof(Cancun_London))]   public Task SweepFork_London(string category, string fork)   => SweepCategoryAtForkAsync(LegacyTestsCancunPath, category, fork);
        [Theory][Trait("Category","LegacyFork-Sweep-PerFork")][MemberData(nameof(Cancun_Paris))]    public Task SweepFork_Paris(string category, string fork)    => SweepCategoryAtForkAsync(LegacyTestsCancunPath, category, fork);
        [Theory][Trait("Category","LegacyFork-Sweep-PerFork")][MemberData(nameof(Cancun_Shanghai))] public Task SweepFork_Shanghai(string category, string fork) => SweepCategoryAtForkAsync(LegacyTestsCancunPath, category, fork);
        [Theory][Trait("Category","LegacyFork-Sweep-PerFork")][MemberData(nameof(Cancun_Cancun))]   public Task SweepFork_Cancun(string category, string fork)   => SweepCategoryAtForkAsync(LegacyTestsCancunPath, category, fork);

        /// <summary>
        /// Max per-failure deep dumps (AccountDiffs + geth trace divergence) emitted per
        /// sweep cell. Keeps a broken category from blowing the test log out with
        /// hundreds of MB of trace data while still giving an actionable first signal.
        /// </summary>
        private const int MaxFailureDeepDumps = 5;

        private async Task SweepCategoryAtForkAsync(string root, string category, string fork)
        {
            System.Console.Error.WriteLine($"[SWEEP-START] {category} @ {fork}");
            try
            {
                await SweepCategoryAtForkAsyncImpl(root, category, fork);
            }
            finally
            {
                System.Console.Error.WriteLine($"[SWEEP-END]   {category} @ {fork}");
            }
        }

        private async Task SweepCategoryAtForkAsyncImpl(string root, string category, string fork)
        {
            if (root == null || !Directory.Exists(root))
            {
                _output.WriteLine("legacytests not cloned; skipping.");
                return;
            }

            var dir = Path.Combine(root, category);
            if (!Directory.Exists(dir))
            {
                _output.WriteLine($"Category {category} missing in legacytests; skipping.");
                return;
            }

            var runner = new GeneralStateTestRunner(_output, targetHardfork: fork);
            int fileCount = 0, passed = 0, failed = 0, skipped = 0;
            var fileFails = new System.Collections.Generic.List<(string file, int n, string firstMsg)>();
            int deepDumpsEmitted = 0;
            GethEvmRunner gethRunner = null;
            TraceComparer comparer = null;

            // Persist a row per sub-test so a single sweep produces a permanent
            // breakdown without re-running the ~20-minute matrix. One file per
            // (fork, category) cell — no locking needed since cells are distinct
            // test invocations. Consumed by LegacyFailureSignatureTests (task #71)
            // to drive POST_EVM signature classification across all 13 forks.
            var csvDir = Path.Combine(ProjectRoot(), "tmp", "test_results", "sweep");
            Directory.CreateDirectory(csvDir);
            var csvPath = Path.Combine(csvDir, $"sweep_{fork}_{category}.csv");
            using var csv = new StreamWriter(csvPath, append: false);
            csv.WriteLine("Fork,Category,FileName,DataIndex,GasIndex,ValueIndex,Status,Message");

            foreach (var file in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories))
            {
                var name = Path.GetFileName(file);
                if (IsKnownEvmDivergence(category, fork, name))
                {
                    skipped++;
                    WriteCsvRow(csv, fork, category, name, 0, 0, 0, "KNOWN_DIVERGENCE", null);
                    continue;
                }
                fileCount++;
                var result = await runner.RunTestWithExecutorAsync(file);
                passed += result.PassedCount;
                failed += result.FailedCount;
                skipped += result.SkippedCount;

                foreach (var r in result.Results)
                {
                    var status = r.Passed ? "PASS" : r.Skipped ? "SKIP" : "FAIL";
                    WriteCsvRow(csv, fork, category, name, r.DataIndex, r.GasIndex, r.ValueIndex, status, r.Passed ? null : r.Message);
                }

                var fileFail = result.Results.Where(r => !r.Passed && !r.Skipped).ToList();
                if (fileFail.Count == 0) continue;

                fileFails.Add((name, fileFail.Count, fileFail[0].Message));

                // Mirror the Prague-era diagnostic pattern (RunExecutionSpecCategoryAsync at
                // EthereumTestVectors.cs:294-373): on the first MaxFailureDeepDumps failures,
                // log AccountDiffs and run a per-opcode trace comparison against geth's
                // evm.exe so the divergence point is visible without a second debug run.
                foreach (var r in fileFail)
                {
                    if (deepDumpsEmitted >= MaxFailureDeepDumps) break;
                    deepDumpsEmitted++;

                    _output.WriteLine($"  FAILED: {name} [{r.DataIndex},{r.GasIndex},{r.ValueIndex}]: {Trim(r.Message, 240)}");
                    if (r.AccountDiffs != null)
                    {
                        const int MaxDiffLines = 12;
                        int diffLineCount = 0;
                        foreach (var d in r.AccountDiffs)
                        {
                            if (diffLineCount >= MaxDiffLines)
                            {
                                _output.WriteLine($"    … and {r.AccountDiffs.Count - diffLineCount} more diff line(s)");
                                break;
                            }
                            _output.WriteLine($"    {Trim(d, 240)}");
                            diffLineCount++;
                        }
                    }

                    try
                    {
                        gethRunner ??= new GethEvmRunner();
                        comparer ??= new TraceComparer();

                        var gethResult = await gethRunner.RunStateTestAsync(file, r.DataIndex, r.GasIndex, r.ValueIndex, fork);
                        if (!gethResult.Success || gethResult.Steps == null || gethResult.Steps.Count == 0)
                        {
                            _output.WriteLine($"    Geth trace unavailable: {Trim(gethResult.Error, 240) ?? "no steps"}");
                            continue;
                        }

                        var nethResult = await runner.RunTestWithTraceAsync(file);
                        var nethSingle = nethResult.Results.FirstOrDefault(x =>
                            x.DataIndex == r.DataIndex && x.GasIndex == r.GasIndex && x.ValueIndex == r.ValueIndex);
                        if (nethSingle?.Traces == null || nethSingle.Traces.Count == 0)
                        {
                            _output.WriteLine($"    Nethereum trace unavailable");
                            continue;
                        }

                        var nethSteps = comparer.NormalizeNethTrace(nethSingle.Traces);
                        var comparison = comparer.Compare(gethResult.Steps, nethSteps);

                        if (comparison.HasDivergence)
                        {
                            _output.WriteLine($"  TRACE DIVERGENCE at step {comparison.FirstDivergenceStep}: {Trim(comparison.DivergenceReason, 120)}");
                            var divergeStep = comparison.Steps[comparison.FirstDivergenceStep - 1];
                            _output.WriteLine($"    Geth: PC={divergeStep.GethPC} Op={Trim(divergeStep.GethOp, 32)} Gas={divergeStep.GethGas} Cost={divergeStep.GethCost} Depth={divergeStep.GethDepth}");
                            _output.WriteLine($"    Neth: PC={divergeStep.NethPC} Op={Trim(divergeStep.NethOp, 32)} Gas={divergeStep.NethGas} Cost={divergeStep.NethCost} Depth={divergeStep.NethDepth}");

                            int start = System.Math.Max(0, comparison.FirstDivergenceStep - 6);
                            int end = System.Math.Min(comparison.Steps.Count, comparison.FirstDivergenceStep + 5);
                            _output.WriteLine($"  Context (steps {start + 1}..{end}):");
                            for (int i = start; i < end; i++)
                            {
                                var s = comparison.Steps[i];
                                var marker = s.Step == comparison.FirstDivergenceStep ? ">>>" : "   ";
                                _output.WriteLine($"    {marker} Step {s.Step}: PC={s.GethPC}/{s.NethPC} Op={Trim(s.GethOp, 32)} Gas={s.GethGas}/{s.NethGas} Cost={s.GethCost}/{s.NethCost} {Trim(s.DivergenceType, 80)}");
                            }
                        }
                        else
                        {
                            _output.WriteLine($"    Traces MATCH ({gethResult.Steps.Count} steps) — divergence is in final state calculation");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        _output.WriteLine($"    Trace comparison error: {Trim(ex.ToString(), 400)}");
                    }
                }
            }

            _output.WriteLine($"\n=== Sweep: {category} @ {fork} ===");
            _output.WriteLine($"  files={fileCount}, sub-tests: passed={passed}, failed={failed}, skipped={skipped}");
            if (fileFails.Count > 0)
            {
                _output.WriteLine($"  Files with failures ({fileFails.Count}):");
                foreach (var (file, n, msg) in fileFails.Take(20))
                    _output.WriteLine($"    {file}: {n} fail(s)  first: {msg?.Substring(0, System.Math.Min(120, msg?.Length ?? 0))}");
                if (fileFails.Count > 20) _output.WriteLine($"    … and {fileFails.Count - 20} more");
            }

            // Hard assertion: any sub-test failure surfaces in CI. If a fork
            // has no Post entries we accept passed=0 silently (well-tested
            // fork-skip path inside the runner); but failed must be 0.
            Assert.True(failed == 0,
                $"Sweep {category} @ {fork}: {failed} sub-test failure(s) across {fileFails.Count}/{fileCount} files. First message: {Trim(fileFails.FirstOrDefault().firstMsg, 240) ?? "—"}");
        }

        /// <summary>
        /// Trim long diagnostic strings (state-root mismatch messages, account
        /// diff lines, etc.) so a single misbehaving sub-test can't push
        /// megabyte-sized lines through xunit's per-test output StringBuilder
        /// and crash the test host with ArgumentOutOfRangeException.
        /// </summary>
        private static string Trim(string s, int max)
        {
            if (s == null) return null;
            if (s.Length <= max) return s;
            return s.Substring(0, max) + "…";
        }

        private static string ProjectRoot()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "Nethereum.slnx")) ||
                    File.Exists(Path.Combine(dir.FullName, "Nethereum.sln")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            return Directory.GetCurrentDirectory();
        }

        private static void WriteCsvRow(StreamWriter w, string fork, string category, string file,
            int dataIndex, int gasIndex, int valueIndex, string status, string message)
        {
            var msg = message == null ? "" : Trim(message, 400).Replace("\r", " ").Replace("\n", " ").Replace("\"", "\"\"");
            w.WriteLine($"{fork},{category},{file},{dataIndex},{gasIndex},{valueIndex},{status},\"{msg}\"");
        }

        /// <summary>
        /// One-shot trace dump for a specific (category, file, fork) tuple. Captures
        /// our Nethereum opcode trace plus geth's `evm.exe statetest` JSON trace and
        /// prints them side-by-side per step so the first divergence is visible
        /// without parsing TRX output. Add InlineData rows ad-hoc when diagnosing.
        /// </summary>
        /// <summary>
        /// Yields one (branch, category, file, dataIndex, gasIndex, valueIndex)
        /// cell per UNIQUE sub-test that has at least one FAIL row across any
        /// fork's persisted sweep CSV. Deduplicates: the same sub-test failing
        /// at multiple forks → one cell. The cell internally re-runs the
        /// sub-test at every fork the fixture declares post-state for.
        /// </summary>
        public static IEnumerable<object[]> FailingSubTestsFromSweep()
        {
            var sweepDir = Path.Combine(ProjectRoot(), "tmp", "test_results", "sweep");
            int yielded = 0;
            if (Directory.Exists(sweepDir))
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var csv in Directory.EnumerateFiles(sweepDir, "sweep_*_*.csv"))
                {
                    var name = Path.GetFileNameWithoutExtension(csv);
                    var rest = name.Substring("sweep_".Length);
                    var firstUnderscore = rest.IndexOf('_');
                    if (firstUnderscore < 0) continue;
                    var fork = rest.Substring(0, firstUnderscore);
                    var category = rest.Substring(firstUnderscore + 1);
                    var branch = (fork == "Frontier" || fork == "Homestead" || fork == "EIP150" ||
                                  fork == "EIP158" || fork == "Byzantium" || fork == "Constantinople" ||
                                  fork == "ConstantinopleFix") ? "Constantinople" : "Cancun";
                    using var r = new StreamReader(csv);
                    string line; bool first = true;
                    while ((line = r.ReadLine()) != null)
                    {
                        if (first) { first = false; continue; }
                        var parts = line.Split(',');
                        if (parts.Length < 7) continue;
                        if (parts[6] != "FAIL") continue;
                        var file = parts[2];
                        if (!int.TryParse(parts[3], out var d)) continue;
                        if (!int.TryParse(parts[4], out var g)) continue;
                        if (!int.TryParse(parts[5], out var v)) continue;
                        var key = $"{branch}|{category}|{file}|{d}|{g}|{v}";
                        if (seen.Add(key))
                        {
                            yielded++;
                            yield return new object[] { branch, category, file, d, g, v };
                        }
                    }
                }
            }
            if (yielded == 0)
                yield return new object[] { "__no_sweep_data__", "__no_sweep_data__", "__no_sweep_data__", 0, 0, 0 };
        }

        /// <summary>
        /// One Theory cell = one sub-test (unique data/gas/value tuple), validated
        /// at every fork the fixture declares post-state for. Cell is GREEN only
        /// when the sub-test passes at all declared forks — the end state when a
        /// spec rule is correctly handled by per-fork strategy registration.
        /// A previously-green cell going red after a fix = regression on some fork
        /// (caught immediately without a full sweep).
        /// </summary>
        [Theory]
        [Trait("Category", "LegacyFork-Regression")]
        [MemberData(nameof(FailingSubTestsFromSweep))]
        public async Task Regression_SubTestAtAllDeclaredForks(
            string branch, string category, string file,
            int dataIndex, int gasIndex, int valueIndex)
        {
            if (branch == "__no_sweep_data__") { _output.WriteLine("No sweep CSVs."); return; }
            var root = LegacyTestsRoot(branch);
            if (root == null || !Directory.Exists(root)) { _output.WriteLine("legacytests not cloned"); return; }
            var path = Path.Combine(root, category, file);
            Assert.True(File.Exists(path), $"Fixture missing: {path}");

            var json = File.ReadAllText(path);
            var test = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, GeneralStateTest>>(json).Values.First();

            var perFork = new List<(string fork, bool passed, string msg)>();
            foreach (var fork in test.Post.Keys)
            {
                var runner = new GeneralStateTestRunner(_output, targetHardfork: fork);
                var result = await runner.RunTestWithExecutorAsync(path, specificDataIndex: dataIndex);
                var sub = result.Results.FirstOrDefault(r =>
                    r.DataIndex == dataIndex && r.GasIndex == gasIndex && r.ValueIndex == valueIndex);
                if (sub == null) { perFork.Add((fork, true, "no-post")); continue; }
                perFork.Add((fork, sub.Passed || sub.Skipped, sub.Message));
            }

            var failed = perFork.Where(p => !p.passed).ToList();
            if (failed.Count > 0)
            {
                _output.WriteLine($"{category}/{file}[{dataIndex},{gasIndex},{valueIndex}] failed at {failed.Count}/{perFork.Count} forks:");
                foreach (var f in failed)
                    _output.WriteLine($"  {f.fork}: {Trim(f.msg, 160)}");
            }
            Assert.Empty(failed);
        }

        /// <summary>
        /// Targeted validation for the pre-EIP-158 ExistsForFrontierNewAccountCheck
        /// fix in CallGasCost. Runs only the specific (file, fork) cells the fix
        /// is expected to affect, plus Cancun canaries to confirm later forks
        /// are unaffected. Sub-minute end-to-end vs the 20-min full sweep.
        /// Passes = state-root match against fixture's canonical expected hash.
        /// </summary>
        [Theory]
        [Trait("Category", "LegacyFork-CallNewAccountFix")]
        // Suspected fixed at Frontier (pre-EIP-150, NEW_ACCOUNT charged when !Exist)
        [InlineData("Constantinople", "stZeroCallsTest", "ZeroValue_CALL_ToEmpty.json", "Frontier")]
        [InlineData("Constantinople", "stZeroCallsTest", "ZeroValue_CALL_ToOneStorageKey.json", "Frontier")]
        [InlineData("Constantinople", "stNonZeroCallsTest", "NonZeroValue_CALL_ToEmpty.json", "Frontier")]
        [InlineData("Constantinople", "stNonZeroCallsTest", "NonZeroValue_CALL_ToOneStorageKey.json", "Frontier")]
        [InlineData("Constantinople", "stRevertTest", "RevertPrefoundEmptyCall.json", "Frontier")]
        // Same at Homestead and EIP-150 (same code path: newAccountRequiresValue: false)
        [InlineData("Constantinople", "stZeroCallsTest", "ZeroValue_CALL_ToEmpty.json", "Homestead")]
        [InlineData("Constantinople", "stNonZeroCallsTest", "NonZeroValue_CALL_ToEmpty.json", "Homestead")]
        [InlineData("Constantinople", "stZeroCallsTest", "ZeroValue_CALL_ToEmpty.json", "EIP150")]
        [InlineData("Constantinople", "stNonZeroCallsTest", "NonZeroValue_CALL_ToEmpty.json", "EIP150")]
        // Canaries — must keep passing (different code path: newAccountRequiresValue: true)
        [InlineData("Constantinople", "stZeroCallsTest", "ZeroValue_CALL_ToEmpty.json", "EIP158")]
        [InlineData("Cancun", "stZeroCallsTest", "ZeroValue_CALL_ToEmpty.json", "Cancun")]
        [InlineData("Cancun", "stZeroCallsTest", "ZeroValue_CALL_ToEmpty.json", "Shanghai")]
        [InlineData("Cancun", "stZeroCallsTest", "ZeroValue_CALL_ToEmpty.json", "Paris")]
        public async Task CallNewAccountFix_Verification(string branch, string category, string file, string fork)
        {
            var root = LegacyTestsRoot(branch);
            if (root == null || !Directory.Exists(root)) { _output.WriteLine("legacytests not cloned; skipping."); return; }
            var path = Path.Combine(root, category, file);
            Assert.True(File.Exists(path), $"Fixture missing: {path}");

            var runner = new GeneralStateTestRunner(_output, targetHardfork: fork);
            var result = await runner.RunTestWithExecutorAsync(path);
            var fileFail = result.Results.Where(r => !r.Passed && !r.Skipped).ToList();
            _output.WriteLine($"{fork}/{category}/{file}: passed={result.PassedCount} failed={fileFail.Count} skipped={result.SkippedCount}");
            foreach (var r in fileFail)
                _output.WriteLine($"  FAIL [{r.DataIndex},{r.GasIndex},{r.ValueIndex}]: {Trim(r.Message, 200)}");
            Assert.Empty(fileFail);
        }

        [Theory]
        [Trait("Category", "LegacyFork-DebugSubtest")]
        [InlineData("Constantinople", "stRevertTest", "RevertPrecompiledTouchExactOOG.json", "Byzantium", 24, 1, 0)]
        [InlineData("Constantinople", "stCallCodes", "callcallcall_ABCB_RECURSIVE.json", "Constantinople", 0, 0, 0)]
        [InlineData("Constantinople", "stZeroCallsTest", "ZeroValue_TransactionCALL_ToEmpty.json", "EIP158", 0, 0, 0)]
        [InlineData("Cancun", "stRandom", "randomStatetest45.json", "Berlin", 0, 0, 0)]
        [InlineData("Constantinople", "stZeroKnowledge", "pointMulAdd.json", "Homestead", 0, 3, 0)]
        [InlineData("Constantinople", "stZeroKnowledge", "pairingTest.json", "Constantinople", 0, 1, 0)]
        [InlineData("Constantinople", "stStaticCall", "static_callcall_00.json", "Constantinople", 0, 0, 0)]
        [InlineData("Constantinople", "stRefundTest", "refund_CallToSuicideTwice.json", "EIP158", 1, 0, 0)]
        public async Task Debug_DumpSubTest(string branch, string category, string file, string fork,
            int dataIndex, int gasIndex, int valueIndex)
        {
            var root = LegacyTestsRoot(branch);
            if (root == null || !Directory.Exists(root)) { _output.WriteLine("legacytests not cloned"); return; }
            var path = Path.Combine(root, category, file);
            Assert.True(File.Exists(path), $"Fixture missing: {path}");

            var runner = new GeneralStateTestRunner(_output, targetHardfork: fork);
            var result = await runner.RunTestWithExecutorAsync(path, specificDataIndex: dataIndex, captureTraces: true);
            var sub = result.Results.FirstOrDefault(r =>
                r.DataIndex == dataIndex && r.GasIndex == gasIndex && r.ValueIndex == valueIndex);
            Assert.NotNull(sub);

            _output.WriteLine($"=== {fork} {category}/{file}[{dataIndex},{gasIndex},{valueIndex}] ===");
            _output.WriteLine($"Expected stateRoot: {sub.ExpectedStateRoot}");
            _output.WriteLine($"Actual   stateRoot: {sub.ActualStateRoot}");
            _output.WriteLine($"Passed: {sub.Passed}, Skipped: {sub.Skipped}");
            _output.WriteLine($"=== OUR FullPostState ({sub.FullPostState?.Count ?? 0} accounts) ===");
            if (sub.FullPostState != null) foreach (var kvp in sub.FullPostState) _output.WriteLine($"  {kvp.Key}: {kvp.Value}");

            try
            {
                var (ourState, coinbase, sender) = await runner.RunAndCaptureExecutionStateAsync(path, dataIndex, gasIndex, valueIndex);
                var t8n = new GethT8nRunner();
                var t8nResult = await t8n.RunT8nAsync(path, dataIndex, gasIndex, valueIndex, fork);
                if (!t8nResult.Success || t8nResult.PostState == null)
                {
                    _output.WriteLine($"=== GETH T8N: FAILED — {t8nResult.Error} ===");
                    return;
                }
                _output.WriteLine($"=== GETH t8n stateRoot: {t8nResult.StateRoot} ===");
                _output.WriteLine($"=== GETH PostState ({t8nResult.PostState.Count} accounts) ===");
                foreach (var kvp in t8nResult.PostState)
                {
                    var a = kvp.Value;
                    var storageStr = a.Storage.Count > 0 ? "{" + string.Join(",", a.Storage.Select(s => s.Key + "=" + s.Value)) + "}" : "";
                    _output.WriteLine($"  {kvp.Key}: balance={a.Balance} nonce={a.Nonce} code={(a.Code ?? "").Substring(0, System.Math.Min(20, (a.Code ?? "").Length))} {storageStr}");
                }
                var classifier = new PostStateSignatureClassifier();
                var diff = classifier.Compare(ourState.AccountsState, t8nResult.PostState, coinbase, sender);
                _output.WriteLine($"=== DIFF Signature: {diff.Signature} ({diff.Diffs.Count} field diffs) ===");
                foreach (var d in diff.Diffs.Take(20))
                    _output.WriteLine($"  {d.Address} {d.Field}: geth={Trim(d.GethValue, 80)} neth={Trim(d.NethValue, 80)}");
            }
            catch (System.Exception ex)
            {
                _output.WriteLine($"=== GETH T8N error: {Trim(ex.Message, 200)} ===");
            }
        }

        [Theory]
        [Trait("Category", "LegacyFork-Debug")]
        [InlineData("Constantinople", "stZeroCallsTest", "ZeroValue_CALLCODE_ToEmpty.json", "Constantinople")]
        [InlineData("Constantinople", "stZeroCallsTest", "ZeroValue_CALL_ToEmpty.json", "Frontier")]
        [InlineData("Constantinople", "stZeroKnowledge", "pointMulAdd.json", "Homestead")]
        [InlineData("Constantinople", "stRevertTest", "RevertPrecompiledTouchExactOOG.json", "Byzantium")]
        public async Task Debug_DumpTrace(string branch, string category, string file, string fork)
        {
            var root = LegacyTestsRoot(branch);
            if (root == null || !Directory.Exists(root)) { _output.WriteLine("legacytests not cloned; skipping."); return; }
            var path = Path.Combine(root, category, file);
            Assert.True(File.Exists(path), $"Fixture missing: {path}");

            var runner = new GeneralStateTestRunner(_output, targetHardfork: fork);
            var nethResult = await runner.RunTestWithExecutorAsync(path, specificDataIndex: 0, captureTraces: true);
            var single = nethResult.Results.FirstOrDefault(r => !r.Skipped);
            _output.WriteLine($"=== Result: expected={single?.ExpectedStateRoot} actual={single?.ActualStateRoot}");
            _output.WriteLine($"=== AccountDiffs ({single?.AccountDiffs?.Count ?? 0}) ===");
            if (single?.AccountDiffs != null) foreach (var d in single.AccountDiffs) _output.WriteLine($"  {d}");
            _output.WriteLine($"=== FullPostState ({single?.FullPostState?.Count ?? 0} accounts) ===");
            if (single?.FullPostState != null) foreach (var kvp in single.FullPostState) _output.WriteLine($"  {kvp.Key}: {kvp.Value}");
            _output.WriteLine($"=== Nethereum trace ({(single?.Traces?.Count ?? 0)} steps) ===");
            if (single?.Traces != null)
            {
                int step = 1;
                foreach (var t in single.Traces.Take(40))
                {
                    _output.WriteLine($"  step {step++}: depth={t.Depth} PC={t.Instruction?.Step} {t.Instruction?.Instruction} gas={t.GasRemaining} cost={t.GasCost}");
                }
            }

            try
            {
                var gethRunner = new GethEvmRunner();
                var gethResult = await gethRunner.RunStateTestAsync(path, single.DataIndex, single.GasIndex, single.ValueIndex, fork);
                _output.WriteLine($"=== Geth trace ({gethResult.Steps?.Count ?? 0} steps) ===");
                if (gethResult.Steps != null)
                {
                    int step = 1;
                    foreach (var s in gethResult.Steps.Take(40))
                    {
                        _output.WriteLine($"  step {step++}: PC={s.PC} {s.Op} gas={s.Gas} cost={s.GasCost} depth={s.Depth}");
                    }
                }
            }
            catch (System.Exception ex) { _output.WriteLine($"Geth dump error: {ex.Message}"); }
        }
    }
}
