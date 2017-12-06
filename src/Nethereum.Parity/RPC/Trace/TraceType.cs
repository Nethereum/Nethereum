namespace Nethereum.Parity.RPC.Trace
{
    public enum TraceType
    {
        vmTrace,
        trace,
        stateDiff
    }

    public static class TraceTypeExtensions
    {
        public static string[] ConvertToStringArray(this TraceType[] value)
        {
            var returnArray = new string[value.Length];
            for (int i = 0; i < value.Length; i++)
            {
                returnArray[i] = value[i].ToString();
            }

            return returnArray;
        }
    }
}