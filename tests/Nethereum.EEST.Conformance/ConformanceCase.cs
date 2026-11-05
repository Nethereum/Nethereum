namespace Nethereum.EEST.Conformance
{
    public sealed class ConformanceCase
    {
        public ConformanceCase(CaseKind kind, string id, string file, string name, string fork = null, int dataIndex = 0, int gasIndex = 0, int valueIndex = 0)
        {
            Kind = kind;
            Id = id;
            File = file;
            Name = name;
            Fork = fork;
            DataIndex = dataIndex;
            GasIndex = gasIndex;
            ValueIndex = valueIndex;
        }

        public CaseKind Kind { get; }
        public string Id { get; }
        public string File { get; }
        public string Name { get; }
        public string Fork { get; }
        public int DataIndex { get; }
        public int GasIndex { get; }
        public int ValueIndex { get; }

        public string DisplayName => DisplayNameOf(Name, Fork, DataIndex, GasIndex, ValueIndex);

        public static string DisplayNameOf(string name, string fork, int dataIndex, int gasIndex, int valueIndex) =>
            fork == null ? name : $"{name}[{fork}:d{dataIndex},g{gasIndex},v{valueIndex}]";

        public override string ToString() => Id;
    }
}
