namespace Nethereum.Mud
{
    public abstract class  TableRecord<TKey, TValue>: TableRecordSingleton<TValue> where TValue : class, new() where TKey : class, new()
    {
        public TableRecord(string nameSpace, string tableName):base(nameSpace, tableName)
        {
           Keys = new TKey();
        }

        public TableRecord (string tableName):base(tableName)
        {
            Keys = new TKey();
        }
      
        public TKey Keys { get; set; }

        public virtual List<byte[]> GetEncodedKey()
        {
            return KeyEncoderDecoder.EncodeKey(Keys);
        }


    }
}
