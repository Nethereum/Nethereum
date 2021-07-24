namespace Nethereum.Quorum.Tests
{
    public class DefaultSettings
    {
        public static string QuorumIPAddress = "http://192.168.2.200";
        public static string QuorumPort = "22000";

        public static string GetDefaultUrl()
        {
            return QuorumIPAddress + ":" + QuorumPort;
        }
    }
}