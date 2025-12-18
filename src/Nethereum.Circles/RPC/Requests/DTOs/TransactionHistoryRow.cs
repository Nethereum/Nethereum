namespace Nethereum.Circles.RPC.Requests.DTOs
{


    public class TransactionHistoryRow : EventRow
    {
        public string Operator { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public string Id { get; set; }
        public string Value { get; set; }
        public string Type { get; set; }
        public string TokenType { get; set; }
    }
}

