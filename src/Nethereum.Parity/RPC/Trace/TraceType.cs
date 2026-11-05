namespace Nethereum.Parity.RPC.Trace
{
    [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.ClientExtensions, "parity-client", "TraceType")]
    public enum TraceType
    {
        vmTrace,
        trace,
        stateDiff
    }

    [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.ClientExtensions, "parity-client", "TraceTypeExtensions")]
    public static class TraceTypeExtensions
    {
        public static string[] ConvertToStringArray(this TraceType[] value)
        {
            var returnArray = new string[value.Length];
            for (var i = 0; i < value.Length; i++)
                returnArray[i] = value[i].ToString();

            return returnArray;
        }
    }
}