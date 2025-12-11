using System;
using System.Collections.Concurrent;

namespace Nethereum.Ssz
{
    public interface ISszElementReader<T>
    {
        T Read(ref SszReader reader);
    }

    public static class SszElementReaderRegistry
    {
        private static readonly ConcurrentDictionary<Type, object> Readers = new ConcurrentDictionary<Type, object>();

        static SszElementReaderRegistry()
        {
            Register(new UInt64Reader());
        }

        public static void Register<T>(ISszElementReader<T> reader)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            Readers[typeof(T)] = reader;
        }

        public static ISszElementReader<T> Get<T>()
        {
            if (Readers.TryGetValue(typeof(T), out var reader))
            {
                return (ISszElementReader<T>)reader;
            }

            throw new InvalidOperationException($"No SSZ element reader found for type {typeof(T).FullName}. Register one via {nameof(Register)}.");
        }

        private sealed class UInt64Reader : ISszElementReader<ulong>
        {
            public ulong Read(ref SszReader reader) => reader.ReadUInt64();
        }
    }
}
