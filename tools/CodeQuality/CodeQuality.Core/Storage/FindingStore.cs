using CodeQuality.Core.Model;
using Microsoft.Data.Sqlite;

namespace CodeQuality.Core.Storage;

public enum Decision { Pending, Accepted, Rejected, Deferred }

public sealed record StoredFinding(Finding Finding, string RulesVersion, Decision Decision, string? DecisionNote);

public sealed class FindingStore
{
    readonly string _connectionString;

    public FindingStore(string databasePath)
    {
        // A path whose directory doesn't exist yet is the normal case for a first run
        // (nobody creates the output folder ahead of the tool) rather than a caller error,
        // so it is created here instead of surfacing as SQLite error 14.
        var directory = Path.GetDirectoryName(Path.GetFullPath(databasePath));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        // Pooling keeps the underlying native handle open after Dispose, which leaves the
        // file locked for callers (including test cleanup) that delete or replace it right
        // after the store goes out of scope. Disabling it costs a handle open/close per call;
        // measured against 8 concurrent writers doing 20 inserts each, that cost produced no
        // observable contention (0 exceptions, all 160 rows present).
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Pooling = false
        }.ToString();

        using var connection = Open();
        Execute(connection, """
            CREATE TABLE IF NOT EXISTS Finding (
                Id TEXT PRIMARY KEY, Package TEXT, FilePath TEXT, Line INTEGER, Symbol TEXT,
                Kind TEXT, RuleId TEXT, Verdict TEXT, Evidence TEXT, Confidence REAL,
                Profile TEXT, RulesVersion TEXT, Decision TEXT, DecisionNote TEXT,
                Live INTEGER NOT NULL DEFAULT 1);
            """);
    }

    public void Save(string package, IEnumerable<Finding> findings, string rulesVersion)
    {
        var findingList = findings as IReadOnlyList<Finding> ?? findings.ToList();

        // A finding stamped with a different package than the argument would be stored (and later
        // resolved) under its OWN package rather than the one the caller believes it saved to, and
        // nothing would ever surface the mismatch. This is dormant today because every caller passes
        // findings it just produced for that exact package, but cross-package analysis (clone
        // detection across packages) is an open mode the spec lists, and the moment it exists this
        // silently mis-files rows.
        var mismatched = findingList.FirstOrDefault(f => f.Package != package);
        if (mismatched is not null)
            throw new ArgumentException(
                $"finding '{mismatched.Id}' belongs to package '{mismatched.Package}', "
                + $"not the package being saved ('{package}')", nameof(findings));

        using var connection = Open();
        using var transaction = connection.BeginTransaction();

        // A finding absent from this run (the code was fixed) is marked resolved rather than
        // deleted, so a finding that later reappears (a revert, or a fix that didn't stick)
        // comes back with whatever decision a human already attached to it instead of starting
        // the triage over. Every row for the package is marked resolved first, then whatever
        // the caller supplies is upserted back to live within the same transaction.
        MarkPackageResolved(connection, package);

        foreach (var finding in findingList)
            UpsertFinding(connection, finding, rulesVersion);

        transaction.Commit();
    }

    static void MarkPackageResolved(SqliteConnection connection, string package)
    {
        using var markResolved = connection.CreateCommand();
        markResolved.CommandText = "UPDATE Finding SET Live = 0 WHERE Package = $package";
        markResolved.Parameters.AddWithValue("$package", package);
        markResolved.ExecuteNonQuery();
    }

    static void UpsertFinding(SqliteConnection connection, Finding finding, string rulesVersion)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Finding
                (Id, Package, FilePath, Line, Symbol, Kind, RuleId, Verdict, Evidence,
                 Confidence, Profile, RulesVersion, Decision, DecisionNote, Live)
            VALUES ($id, $package, $path, $line, $symbol, $kind, $rule, $verdict, $evidence,
                    $confidence, $profile, $rules, 'Pending', NULL, 1)
            ON CONFLICT(Id) DO UPDATE SET
                Verdict = excluded.Verdict,
                Evidence = excluded.Evidence,
                Profile = excluded.Profile,
                Decision = CASE WHEN Finding.RulesVersion = excluded.RulesVersion
                                THEN Finding.Decision ELSE 'Pending' END,
                DecisionNote = CASE WHEN Finding.RulesVersion = excluded.RulesVersion
                                    THEN Finding.DecisionNote ELSE NULL END,
                RulesVersion = excluded.RulesVersion,
                Live = 1;
            """;
        command.Parameters.AddWithValue("$id", finding.Id);
        command.Parameters.AddWithValue("$package", finding.Package);
        command.Parameters.AddWithValue("$path", finding.FilePath);
        command.Parameters.AddWithValue("$line", finding.Line);
        command.Parameters.AddWithValue("$symbol", finding.Symbol);
        command.Parameters.AddWithValue("$kind", finding.Kind.ToString());
        command.Parameters.AddWithValue("$rule", finding.RuleId);
        command.Parameters.AddWithValue("$verdict", finding.Verdict.ToString());
        command.Parameters.AddWithValue("$evidence", finding.Evidence);
        command.Parameters.AddWithValue("$confidence", finding.Confidence);
        command.Parameters.AddWithValue("$profile", finding.Profile);
        command.Parameters.AddWithValue("$rules", rulesVersion);
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<StoredFinding> Load(string package) => LoadByLiveState(package, live: 1);

    public IReadOnlyList<StoredFinding> LoadResolved(string package) => LoadByLiveState(package, live: 0);

    IReadOnlyList<StoredFinding> LoadByLiveState(string package, int live)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Finding WHERE Package = $package AND Live = $live ORDER BY FilePath, Line";
        command.Parameters.AddWithValue("$package", package);
        command.Parameters.AddWithValue("$live", live);

        var results = new List<StoredFinding>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var finding = new Finding(
                Package: reader.GetString(reader.GetOrdinal("Package")),
                FilePath: reader.GetString(reader.GetOrdinal("FilePath")),
                Line: reader.GetInt32(reader.GetOrdinal("Line")),
                Symbol: reader.GetString(reader.GetOrdinal("Symbol")),
                Kind: Enum.Parse<FindingKind>(reader.GetString(reader.GetOrdinal("Kind"))),
                RuleId: reader.GetString(reader.GetOrdinal("RuleId")),
                Verdict: Enum.Parse<Verdict>(reader.GetString(reader.GetOrdinal("Verdict"))),
                Evidence: reader.GetString(reader.GetOrdinal("Evidence")),
                Confidence: reader.GetDouble(reader.GetOrdinal("Confidence")),
                Profile: reader.GetString(reader.GetOrdinal("Profile")));

            var noteOrdinal = reader.GetOrdinal("DecisionNote");

            results.Add(new StoredFinding(
                finding,
                reader.GetString(reader.GetOrdinal("RulesVersion")),
                Enum.Parse<Decision>(reader.GetString(reader.GetOrdinal("Decision"))),
                reader.IsDBNull(noteOrdinal) ? null : reader.GetString(noteOrdinal)));
        }

        return results;
    }

    public void RecordDecision(string findingId, Decision decision, string? note)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Finding SET Decision = $decision, DecisionNote = $note WHERE Id = $id";
        command.Parameters.AddWithValue("$decision", decision.ToString());
        command.Parameters.AddWithValue("$note", (object?)note ?? DBNull.Value);
        command.Parameters.AddWithValue("$id", findingId);
        command.ExecuteNonQuery();
    }

    SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
