
namespace Nethereum.RPC.Eth.DTOs
{
    public class FilterLogVO
    {
        public FilterLogVO()
        {}

        public FilterLogVO(Transaction transaction, TransactionReceipt receipt, FilterLog log)
        {
            Transaction = transaction;
            Receipt = receipt;
            Log = log;
        }

        public Transaction Transaction { get; private set; }
        public TransactionReceipt Receipt { get; private set; }
        public FilterLog Log { get; private set; }
        public long LogIndex => Log == null ? -1 : (long)Log.LogIndex.Value;
        public string Address => Log?.Address;

        //public string EventSignature => Log?.EventSignature();

        //public virtual bool IsForEvent<TEvent>() where TEvent : new()
        //{
        //    return Log?.IsLogForEvent<TEvent>() ?? false;
        //}

        //public virtual EventLog<TEvent> Decode<TEvent>() where TEvent : new()
        //{
        //    return Log?.DecodeEvent<TEvent>();
        //}

        public virtual bool IsTo(string toAddress)
        {
            return this.Transaction?.IsTo(toAddress) ?? false;
        }
    }
}