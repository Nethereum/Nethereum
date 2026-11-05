using System;
using System.Collections.Generic;
using System.IO;
using Nethereum.CoreChain.RocksDB.History;
using Nethereum.Util;
using RocksDbSharp;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    internal static class SstIngest
    {
        private static readonly IngestExternalFileOptions DefaultIngest = new IngestExternalFileOptions();

        internal static (byte[] Key, byte[] Value, int Seq)[] Sequence(IReadOnlyList<KeyValuePair<byte[], byte[]>> entries)
        {
            var rows = new (byte[] Key, byte[] Value, int Seq)[entries.Count];
            for (var i = 0; i < rows.Length; i++) rows[i] = (entries[i].Key, entries[i].Value, i);
            return rows;
        }

        internal sealed class RowComparer : IComparer<(byte[] Key, byte[] Value, int Seq)>
        {
            internal static readonly RowComparer Instance = new RowComparer();

            public int Compare((byte[] Key, byte[] Value, int Seq) a, (byte[] Key, byte[] Value, int Seq) b)
            {
                var c = ByteArrayComparer.Current.Compare(a.Key, b.Key);
                return c != 0 ? c : a.Seq.CompareTo(b.Seq);
            }
        }

        internal static (byte[] Key, byte[] Value, int Seq)[] SortLastWins(IReadOnlyList<KeyValuePair<byte[], byte[]>> entries)
        {
            var rows = Sequence(entries);
            Array.Sort(rows, RowComparer.Instance);
            return rows;
        }

        internal static long Write(string path, IEnumerable<(byte[] Key, byte[] Value, int Seq)> sorted)
        {
            if (sorted is (byte[] Key, byte[] Value, int Seq)[] array) return Write(path, array);
            long written = 0;
            using var writer = new SstFileWriter(new EnvOptions(), RocksProfiles.IndexSstOptions());
            writer.Open(path);
            using var rows = sorted.GetEnumerator();
            if (rows.MoveNext())
            {
                var current = rows.Current;
                while (rows.MoveNext())
                {
                    if (!ByteArrayComparer.Current.Equals(current.Key, rows.Current.Key))
                    {
                        Add(writer, current);
                        written++;
                    }
                    current = rows.Current;
                }
                Add(writer, current);
                written++;
            }
            writer.Finish();
            return written;
        }

        internal static long Write(string path, (byte[] Key, byte[] Value, int Seq)[] sorted)
        {
            long written = 0;
            using var writer = new SstFileWriter(new EnvOptions(), RocksProfiles.IndexSstOptions());
            writer.Open(path);
            if (sorted.Length > 0)
            {
                var last = sorted.Length - 1;
                for (var i = 0; i < last; i++)
                {
                    if (ByteArrayComparer.Current.Equals(sorted[i].Key, sorted[i + 1].Key)) continue;
                    Add(writer, sorted[i]);
                    written++;
                }
                Add(writer, sorted[last]);
                written++;
            }
            writer.Finish();
            return written;
        }

        internal static void IngestAndDelete(
            RocksDb db, ColumnFamilyHandle cf, string path, IngestExternalFileOptions options = null)
        {
            try
            {
                db.IngestExternalFiles(new[] { path }, options ?? DefaultIngest, cf);
            }
            finally
            {
                try { File.Delete(path); } catch { }
            }
        }

        private static void Add(SstFileWriter writer, (byte[] Key, byte[] Value, int Seq) row)
        {
            if (row.Value == null) writer.Delete(row.Key);
            else writer.Put(row.Key, row.Value);
        }
    }
}
