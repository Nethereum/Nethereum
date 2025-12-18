namespace Nethereum.Circles.RPC.Requests.DTOs
{
    public class EventRow
    {
        public long BlockNumber { get; set; }
        public int TransactionIndex { get; set; }
        public int LogIndex { get; set; }
        public int BatchIndex { get; set; }
        public string TransactionHash { get; set; }
        public long Timestamp { get; set; }
        public int Version { get; set; }
    }

}
