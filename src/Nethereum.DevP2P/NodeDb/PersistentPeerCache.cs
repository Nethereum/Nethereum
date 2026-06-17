using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.DevP2P.NodeDb
{
    /// <summary>
    /// Persistent peer cache. Records every enode we successfully bonded with,
    /// keyed by full enode URL. On restart, the cache is loaded and offered
    /// to the dialer first — typically these peers are still up and accept us
    /// immediately, avoiding the cold-start DNS + discv4 overhead. Mirrors
    /// the role of geth's <c>p2p/enode/nodedb.go</c>.
    /// </summary>
    public sealed class PersistentPeerCache
    {
        public sealed class Entry
        {
            public string Enode { get; set; } = "";
            public long LastSeenUnix { get; set; }
            public int SuccessfulConnects { get; set; }
            public int FailedConnects { get; set; }
        }

        private readonly string _path;
        private readonly Action<string> _log;
        private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _writeLock = new object();
        private DateTime _lastWrite = DateTime.MinValue;
        private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

        // Debounce window — at most one flush per this interval, regardless of
        // how many RecordSuccess/RecordFailure events arrive. 30s matches the
        // operational tradeoff: fast enough to survive an unexpected kill
        // within seconds, slow enough that 100 successful dials don't write
        // the file 100 times.
        private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(30);

        // Eager-flush threshold: if N new entries arrive faster than the
        // debounce window, flush immediately. Picked so a cold start that
        // bonds 16 peers in &lt; 30s still persists those 16 before the next
        // crash. 16 is well under the typical 30-peer cap and tracks dial
        // bursts during DNS seeding.
        private const int EagerFlushThreshold = 16;
        private int _pendingChanges;

        public PersistentPeerCache(string path, Action<string> log)
        {
            _path = path ?? throw new ArgumentNullException(nameof(path));
            _log = log ?? (_ => { });
        }

        public int Count => _entries.Count;

        public void Load()
        {
            if (!File.Exists(_path)) return;
            try
            {
                var json = File.ReadAllText(_path);
                var list = JsonSerializer.Deserialize<List<Entry>>(json);
                if (list == null) return;
                foreach (var e in list)
                {
                    if (!string.IsNullOrEmpty(e?.Enode))
                        _entries.TryAdd(e.Enode, e);
                }
                _log($"Peer cache loaded {_entries.Count} entries from {_path}.");
            }
            catch (Exception ex)
            {
                _log($"Peer cache load failed ({ex.GetType().Name}: {ex.Message}); starting empty.");
            }
        }

        /// <summary>
        /// Returns enodes ordered by recency + success score so dialers try the
        /// most likely-to-accept peers first.
        /// </summary>
        public List<string> GetPreferredEnodes(int maxCount = 200)
        {
            return _entries.Values
                .OrderByDescending(e => Score(e))
                .Take(maxCount)
                .Select(e => e.Enode)
                .ToList();
        }

        /// <summary>Try to read a cache entry by enode. Returns false if the
        /// cache has no record of the enode. Consumers (PeerPoolManager,
        /// FetchRequestScheduler) read SuccessfulConnects / FailedConnects /
        /// LastSeenUnix to compute peer scores.</summary>
        public bool TryGetEntry(string enode, out Entry entry)
        {
            return _entries.TryGetValue(enode, out entry!);
        }

        public void RecordSuccess(string enode)
        {
            var entry = _entries.GetOrAdd(enode, _ => new Entry { Enode = enode });
            entry.LastSeenUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            entry.SuccessfulConnects++;
            MaybeSave();
        }

        public void RecordFailure(string enode)
        {
            if (!_entries.TryGetValue(enode, out var entry)) return; // don't track failures for never-seen
            entry.FailedConnects++;
            MaybeSave();
        }

        private void MaybeSave()
        {
            // Eager flush when many new entries accumulate within the debounce
            // window — protects against process crash losing a burst of
            // freshly bonded peers before the next time-based flush.
            var pending = Interlocked.Increment(ref _pendingChanges);
            if (pending >= EagerFlushThreshold)
            {
                FlushNow();
                return;
            }
            // Time-based debounce: at most one write per FlushInterval. Avoids
            // disk thrashing under sustained connect/disconnect churn.
            if (DateTime.UtcNow - _lastWrite < FlushInterval) return;
            FlushNow();
        }

        private void FlushNow()
        {
            Interlocked.Exchange(ref _pendingChanges, 0);
            Save();
        }

        public void Save()
        {
            lock (_writeLock)
            {
                try
                {
                    var dir = Path.GetDirectoryName(_path);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    var list = _entries.Values
                        .OrderByDescending(e => Score(e))
                        .Take(500) // hard cap to keep file small
                        .ToList();
                    var json = JsonSerializer.Serialize(list, JsonOpts);
                    var tmp = _path + ".tmp";
                    File.WriteAllText(tmp, json);
                    if (File.Exists(_path)) File.Replace(tmp, _path, null);
                    else File.Move(tmp, _path);
                    _lastWrite = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    _log($"Peer cache save failed ({ex.GetType().Name}: {ex.Message}); continuing.");
                }
            }
        }

        private static double Score(Entry e)
        {
            // Heavily favour recently-seen peers; lightly favour high
            // success-to-failure ratio.
            var ageSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - e.LastSeenUnix;
            var recency = 1.0 / (1.0 + ageSeconds / 3600.0); // halves every hour
            var ratio = (1.0 + e.SuccessfulConnects) / (1.0 + e.FailedConnects);
            return recency * ratio;
        }
    }
}
