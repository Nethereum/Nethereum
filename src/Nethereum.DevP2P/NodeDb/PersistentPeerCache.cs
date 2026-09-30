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

        private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(30);

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

        public List<string> GetPreferredEnodes(int maxCount = 200)
        {
            return _entries.Values
                .OrderByDescending(e => Score(e))
                .Take(maxCount)
                .Select(e => e.Enode)
                .ToList();
        }

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
            if (!_entries.TryGetValue(enode, out var entry)) return;
            entry.FailedConnects++;
            MaybeSave();
        }

        private void MaybeSave()
        {
            var pending = Interlocked.Increment(ref _pendingChanges);
            if (pending >= EagerFlushThreshold)
            {
                FlushNow();
                return;
            }
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
                        .Take(500)
                        .ToList();
                    var json = JsonSerializer.Serialize(list, JsonOpts);
                    AtomicFile.WriteAllText(_path, json);
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
            var ageSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - e.LastSeenUnix;
            var recency = 1.0 / (1.0 + ageSeconds / 3600.0);
            var ratio = (1.0 + e.SuccessfulConnects) / (1.0 + e.FailedConnects);
            return recency * ratio;
        }
    }
}
