namespace Nethereum.RpcParity
{
    /// <summary>
    /// A single property-path mismatch between the subject (X) and reference
    /// (Y) node's response to the same RPC call.
    /// </summary>
    public sealed class Diff
    {
        public Diff(string path, object xValue, object yValue)
        {
            Path = path;
            XValue = xValue;
            YValue = yValue;
        }

        public string Path { get; }
        public object XValue { get; }
        public object YValue { get; }
    }
}
