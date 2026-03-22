using System.Collections.Generic;

namespace Nethereum.Merkle
{
    public class LeanIMTNodeEntry
    {
        public int Level { get; set; }
        public int Index { get; set; }
        public byte[] Value { get; set; }

        public LeanIMTNodeEntry(int level, int index, byte[] value)
        {
            Level = level;
            Index = index;
            Value = value;
        }
    }

    public interface ILeanIMTNodeStorage
    {
        byte[] GetNode(int level, int index);
        void SetNode(int level, int index, byte[] value);
        void SetNodesBatch(IEnumerable<LeanIMTNodeEntry> nodes);

        int GetNodeCount(int level);
        void EnsureLevel(int level);
        int GetLevelCount();

        void Clear();
    }

    public class InMemoryLeanIMTNodeStorage : ILeanIMTNodeStorage
    {
        private readonly List<List<byte[]>> _layers = new List<List<byte[]>>();

        public byte[] GetNode(int level, int index)
        {
            if (level < 0 || level >= _layers.Count) return null;
            var layer = _layers[level];
            if (index < 0 || index >= layer.Count) return null;
            return layer[index];
        }

        public void SetNode(int level, int index, byte[] value)
        {
            EnsureLevel(level);
            var layer = _layers[level];
            while (layer.Count <= index)
                layer.Add(null);
            layer[index] = value;
        }

        public void SetNodesBatch(IEnumerable<LeanIMTNodeEntry> nodes)
        {
            foreach (var entry in nodes)
            {
                SetNode(entry.Level, entry.Index, entry.Value);
            }
        }

        public int GetNodeCount(int level)
        {
            if (level < 0 || level >= _layers.Count) return 0;
            return _layers[level].Count;
        }

        public void EnsureLevel(int level)
        {
            while (_layers.Count <= level)
                _layers.Add(new List<byte[]>());
        }

        public int GetLevelCount()
        {
            return _layers.Count;
        }

        public void Clear()
        {
            _layers.Clear();
        }
    }
}
