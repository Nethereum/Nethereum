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

        private static bool IsKnownEvmDivergence(string category, string fork, string fileName)
        {
            return false;
        }

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

        public static readonly string[] ConstantinopleForks =
        {
            "Frontier", "Homestead", "EIP150", "EIP158",
            "Byzantium", "Constantinople", "ConstantinopleFix"
        };

        public static readonly string[] CancunForks =
        {
            "Istanbul", "Berlin", "London", "Paris", "Shanghai", "Cancun"
        };

        public static System.Collections.Generic.IEnumerable<object[]> ConstantinopleFullMatrix()
            => EnumerateMatrix(LegacyTestsRoot("Constantinople"), ConstantinopleForks);

        public static System.Collections.Generic.IEnumerable<object[]> CancunFullMatrix()
            => EnumerateMatrix(LegacyTestsRoot("Cancun"), CancunForks);

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

        private static readonly System.Collections.Generic.HashSet<string> MatrixSkipCategories =
            new(System.StringComparer.OrdinalIgnoreCase)
            {
                "stTimeConsuming",
                "VMTests",
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

        [Theory]
        [Trait("Category", "LegacyFork-Sweep-Full")]
        [MemberData(nameof(ConstantinopleFullMatrix))]
        public Task Sweep_Constantinople_FullMatrix(string category, string fork)
            => SweepCategoryAtForkAsync(LegacyTestsPath, category, fork);

        [Theory]
        [Trait("Category", "LegacyFork-Sweep-Full")]
        [MemberData(nameof(CancunFullMatrix))]
        public Task Sweep_Cancun_FullMatrix(string category, string fork)
            => SweepCategoryAtForkAsync(LegacyTestsCancunPath, category, fork);


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

                    await GethTraceDivergenceReporter.ReportAsync(_output, runner, file, r, fork);
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

            Assert.True(failed == 0,
                $"Sweep {category} @ {fork}: {failed} sub-test failure(s) across {fileFails.Count}/{fileCount} files. First message: {Trim(fileFails.FirstOrDefault().firstMsg, 240) ?? "—"}");
        }

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
            var realPasses = 0;
            foreach (var fork in test.Post.Keys)
            {
                var runner = new GeneralStateTestRunner(_output, targetHardfork: fork);
                var result = await runner.RunTestWithExecutorAsync(path, specificDataIndex: dataIndex);
                var sub = result.Results.FirstOrDefault(r =>
                    r.DataIndex == dataIndex && r.GasIndex == gasIndex && r.ValueIndex == valueIndex);
                if (sub == null) { perFork.Add((fork, true, "no-post")); continue; }
                if (sub.Passed && !sub.Skipped) realPasses++;
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

            Assert.True(realPasses > 0,
                $"{branch}/{category}/{file}[{dataIndex},{gasIndex},{valueIndex}] executed NO sub-test at " +
                $"any of its {perFork.Count} declared fork(s) — every cell was absent or skipped, so this " +
                "canary asserted nothing. Repoint it at a fixture that carries a post-state for these " +
                "indices, or remove the cell; do not leave it green and empty.");
        }

        [Theory]
        [Trait("Category", "LegacyFork-CallNewAccountFix")]
        [InlineData("Constantinople", "stZeroCallsTest", "ZeroValue_CALL_ToEmpty.json", "Frontier")]
        [InlineData("Constantinople", "stZeroCallsTest", "ZeroValue_CALL_ToOneStorageKey.json", "Frontier")]
        [InlineData("Constantinople", "stNonZeroCallsTest", "NonZeroValue_CALL_ToEmpty.json", "Frontier")]
        [InlineData("Constantinople", "stNonZeroCallsTest", "NonZeroValue_CALL_ToOneStorageKey.json", "Frontier")]
        [InlineData("Constantinople", "stRevertTest", "RevertPrefoundEmptyCall.json", "Frontier")]
        [InlineData("Constantinople", "stZeroCallsTest", "ZeroValue_CALL_ToEmpty.json", "Homestead")]
        [InlineData("Constantinople", "stNonZeroCallsTest", "NonZeroValue_CALL_ToEmpty.json", "Homestead")]
        [InlineData("Constantinople", "stZeroCallsTest", "ZeroValue_CALL_ToEmpty.json", "EIP150")]
        [InlineData("Constantinople", "stNonZeroCallsTest", "NonZeroValue_CALL_ToEmpty.json", "EIP150")]
        [InlineData("Constantinople", "stZeroCallsTest", "ZeroValue_CALL_ToEmpty.json", "EIP158")]
        [InlineData("Cancun", "stZeroCallsTest", "ZeroValue_CALL_ToEmpty.json", "Berlin")]
        [InlineData("Cancun", "stZeroCallsTest", "ZeroValue_CALL_ToEmpty.json", "Istanbul")]
        [InlineData("Cancun", "stZeroCallsTest", "ZeroValue_CALL_ToEmpty.json", "London")]
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

            Assert.True(result.PassedCount > 0,
                $"{fork}/{category}/{file}: {result.SkippedCount} skipped and NOTHING executed. " +
                "The fixture carries no post-state for this fork, so the cell asserts nothing.");
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
                var diff = classifier.Compare(
                    ourState.AccountsState.ToDictionary(kvp => kvp.Key.ToHexLower(), kvp => kvp.Value),
                    t8nResult.PostState, coinbase, sender);
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
