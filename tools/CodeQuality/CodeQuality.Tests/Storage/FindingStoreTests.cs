using CodeQuality.Core.Model;
using CodeQuality.Core.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace CodeQuality.Tests.Storage;

public class FindingStoreTests : IDisposable
{
    readonly string _path = Path.Combine(Path.GetTempPath(), $"cq-{Guid.NewGuid():N}.db");

    public void Dispose() { if (File.Exists(_path)) File.Delete(_path); }

    static Finding Make(int line = 42) => new(
        Package: "Pkg", FilePath: "src/Pkg/Thing.cs", Line: line, Symbol: "Thing.Do",
        Kind: FindingKind.Structure, RuleId: "structure.long-method", Verdict: Verdict.Review,
        Evidence: "702 lines", Confidence: 1.0, Profile: "default");

    [Fact]
    public void SavesAndLoadsFindings()
    {
        var store = new FindingStore(_path);
        store.Save("Pkg", new[] { Make() }, "v1");

        var loaded = Assert.Single(store.Load("Pkg"));
        Assert.Equal(Make().Id, loaded.Finding.Id);
        Assert.Equal(Decision.Pending, loaded.Decision);
    }

    [Fact]
    public void ResavingTheSameFindingDoesNotDuplicateIt()
    {
        var store = new FindingStore(_path);
        store.Save("Pkg", new[] { Make() }, "v1");
        store.Save("Pkg", new[] { Make() }, "v1");

        Assert.Single(store.Load("Pkg"));
    }

    [Fact]
    public void DecisionSurvivesReanalysisAtTheSameRulesVersion()
    {
        var store = new FindingStore(_path);
        store.Save("Pkg", new[] { Make() }, "v1");
        store.RecordDecision(Make().Id, Decision.Rejected, "intentional, protocol state machine");

        store.Save("Pkg", new[] { Make() }, "v1");

        var loaded = Assert.Single(store.Load("Pkg"));
        Assert.Equal(Decision.Rejected, loaded.Decision);
        Assert.Equal("intentional, protocol state machine", loaded.DecisionNote);
    }

    [Fact]
    public void DecisionIsResetWhenTheRulesVersionChanges()
    {
        var store = new FindingStore(_path);
        store.Save("Pkg", new[] { Make() }, "v1");
        store.RecordDecision(Make().Id, Decision.Rejected, "was fine under v1");

        store.Save("Pkg", new[] { Make() }, "v2");

        var loaded = Assert.Single(store.Load("Pkg"));
        Assert.Equal(Decision.Pending, loaded.Decision);
    }

    [Fact]
    public void DecisionNoteIsClearedWhenTheRulesVersionChanges()
    {
        var store = new FindingStore(_path);
        store.Save("Pkg", new[] { Make() }, "v1");
        store.RecordDecision(Make().Id, Decision.Rejected, "was fine under v1");

        store.Save("Pkg", new[] { Make() }, "v2");

        var loaded = Assert.Single(store.Load("Pkg"));
        Assert.Null(loaded.DecisionNote);
    }

    [Fact]
    public void LoadFiltersByPackage()
    {
        var store = new FindingStore(_path);
        store.Save("Pkg", new[] { Make() }, "v1");
        store.Save("Other", new[] { Make() with { Package = "Other" } }, "v1");

        Assert.Single(store.Load("Pkg"));
        Assert.Single(store.Load("Other"));
    }

    [Fact]
    public void LoadOfUnknownPackageReturnsEmpty() =>
        Assert.Empty(new FindingStore(_path).Load("Nothing"));

    // Guards the constructor's Directory.CreateDirectory call. Without it, opening a store
    // whose folder does not exist yet (the normal case on a first run) throws SQLite error 14
    // instead of just creating the file there, as confirmed with a standalone probe against
    // Microsoft.Data.Sqlite before this was added.
    [Fact]
    public void ConstructorCreatesAMissingParentDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"cq-missing-{Guid.NewGuid():N}", "nested");
        var path = Path.Combine(directory, "cq.db");
        try
        {
            var store = new FindingStore(path);
            store.Save("Pkg", new[] { Make() }, "v1");

            Assert.True(Directory.Exists(directory));
            Assert.Single(store.Load("Pkg"));
        }
        finally
        {
            var root = Path.Combine(Path.GetTempPath(), Path.GetFileName(Path.GetDirectoryName(directory)!));
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    // A corrupt file at the target path is not something the store can recover from: silently
    // swallowing it would mean every decision ever recorded there disappears without anyone
    // being told. The exception is the countable, reportable signal; the caller (CLI layer) is
    // expected to catch and report it rather than the store inventing a fallback that hides data
    // loss. This test documents that choice so a future change does not accidentally add a
    // silent catch here.
    [Fact]
    public void ConstructorOverACorruptFileThrowsRatherThanSilentlyDiscardingIt()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cq-corrupt-{Guid.NewGuid():N}.db");
        File.WriteAllText(path, "not a sqlite database");
        try
        {
            Assert.Throws<SqliteException>(() => new FindingStore(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RecordDecisionForAnUnknownIdDoesNotThrowAndChangesNothing()
    {
        var store = new FindingStore(_path);
        store.Save("Pkg", new[] { Make() }, "v1");

        store.RecordDecision("Pkg:src/Pkg/Thing.cs:999:Missing.Method:structure.long-method", Decision.Accepted, "note");

        var loaded = Assert.Single(store.Load("Pkg"));
        Assert.Equal(Decision.Pending, loaded.Decision);
    }

    [Fact]
    public void EvidenceIsStoredInFullWithoutTruncation()
    {
        var evidence = new string('e', 200_000);
        var store = new FindingStore(_path);
        store.Save("Pkg", new[] { Make() with { Evidence = evidence } }, "v1");

        var loaded = Assert.Single(store.Load("Pkg"));
        Assert.Equal(200_000, loaded.Finding.Evidence.Length);
        Assert.Equal(evidence, loaded.Finding.Evidence);
    }

    [Fact]
    public void EmptyPackageRoundTrips()
    {
        var store = new FindingStore(_path);
        store.Save("", new[] { Make() with { Package = "" } }, "v1");

        var loaded = Assert.Single(store.Load(""));
        Assert.Equal("", loaded.Finding.Package);
    }

    // Guards the Pooling = false connection-string choice. Probed standalone: with the default
    // pooled connection string, this same workload still completes without SqliteException
    // (SQLite's own busy-timeout retry covers it) so this test cannot catch a pooling regression
    // by itself — it exists to prove concurrent writers from two separate FindingStore instances
    // against the same file are safe at all, per the brief's explicit requirement to check this.
    [Fact]
    public async Task TwoStoreInstancesCanSaveConcurrentlyWithoutLosingRowsOrThrowing()
    {
        var storeA = new FindingStore(_path);
        var storeB = new FindingStore(_path);

        var findingsA = Enumerable.Range(0, 30).Select(i => Make(i)).ToArray();
        var findingsB = Enumerable.Range(30, 30).Select(i => Make(i) with { Package = "Other" }).ToArray();

        Exception? failureA = null, failureB = null;
        var taskA = Task.Run(() => { try { storeA.Save("Pkg", findingsA, "v1"); } catch (Exception ex) { failureA = ex; } });
        var taskB = Task.Run(() => { try { storeB.Save("Other", findingsB, "v1"); } catch (Exception ex) { failureB = ex; } });
        await Task.WhenAll(taskA, taskB);

        Assert.Null(failureA);
        Assert.Null(failureB);
        Assert.Equal(30, storeA.Load("Pkg").Count);
        Assert.Equal(30, storeA.Load("Other").Count);
    }

    [Fact]
    public void FindingAbsentFromALaterRunIsNoLongerReturnedByLoad()
    {
        var store = new FindingStore(_path);
        store.Save("Pkg", new[] { Make() }, "v1");

        store.Save("Pkg", Array.Empty<Finding>(), "v1");

        Assert.Empty(store.Load("Pkg"));
    }

    [Fact]
    public void FindingAbsentFromALaterRunIsReturnedByLoadResolved()
    {
        var store = new FindingStore(_path);
        store.Save("Pkg", new[] { Make() }, "v1");

        store.Save("Pkg", Array.Empty<Finding>(), "v1");

        var resolved = Assert.Single(store.LoadResolved("Pkg"));
        Assert.Equal(Make().Id, resolved.Finding.Id);
    }

    [Fact]
    public void FindingThatReappearsIsReturnedByLoadWithItsDecisionAndNoteIntact()
    {
        var store = new FindingStore(_path);
        store.Save("Pkg", new[] { Make() }, "v1");
        store.RecordDecision(Make().Id, Decision.Rejected, "still not worth changing");

        store.Save("Pkg", Array.Empty<Finding>(), "v1");
        store.Save("Pkg", new[] { Make() }, "v1");

        var loaded = Assert.Single(store.Load("Pkg"));
        Assert.Equal(Decision.Rejected, loaded.Decision);
        Assert.Equal("still not worth changing", loaded.DecisionNote);
    }

    [Fact]
    public void PackageWhoseFindingsAreAllFixedHasNoLiveRows()
    {
        var store = new FindingStore(_path);
        store.Save("Pkg", new[] { Make(1), Make(2) }, "v1");

        store.Save("Pkg", Array.Empty<Finding>(), "v1");

        Assert.Empty(store.Load("Pkg"));
        Assert.Equal(2, store.LoadResolved("Pkg").Count);
    }

    // Ground truth: a Finding carries its own Package field independently of the `package`
    // argument Save is called with. Nothing today ever mismatches the two, but cross-package
    // clone analysis (an open mode the spec lists) will, and a mismatched row would otherwise be
    // stored and resolved under its OWN package - filed somewhere the caller never asked about
    // and never looked. This pins that Save rejects the mismatch instead of accepting it.
    [Fact]
    public void SaveThrowsWhenAFindingsPackageDoesNotMatchTheArgument()
    {
        var store = new FindingStore(_path);

        var exception = Assert.Throws<ArgumentException>(() =>
            store.Save("Pkg", new[] { Make() with { Package = "Other" } }, "v1"));

        Assert.Contains("Other", exception.Message);
        Assert.Contains("Pkg", exception.Message);
    }

    [Fact]
    public void SavingFindingsForOnePackageDoesNotResolveAnotherPackagesRows()
    {
        var store = new FindingStore(_path);
        store.Save("Pkg", new[] { Make() }, "v1");
        store.Save("Other", new[] { Make() with { Package = "Other" } }, "v1");

        store.Save("Pkg", Array.Empty<Finding>(), "v1");

        Assert.Empty(store.Load("Pkg"));
        Assert.Single(store.Load("Other"));
    }
}
