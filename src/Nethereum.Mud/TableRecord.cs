using Nethereum.Mud.EncodingDecoding;
using System.Collections.Generic;

namespace Nethereum.Mud
{
    public abstract class TableRecord<TKey, TValue> : TableRecordSingleton<TValue>, ITableRecord where TValue : class, new() where TKey : class, new()
    {
        public TableRecord(string nameSpace, string tableName, bool isOffChainTable = false) : base(nameSpace, tableName, isOffChainTable)
        {
            Keys = new TKey();
        }

        public TableRecord(string tableName) : base(tableName)
        {
            Keys = new TKey();
        }

        public TKey Keys { get; set; }

        public virtual List<byte[]> GetEncodedKey()
        {
            return KeyEncoderDecoder.EncodeKey(Keys);
        }

        public virtual void DecodeKey(List<byte[]> encodedKey)
        {
            Keys = KeyEncoderDecoder.DecodeKey<TKey>(encodedKey);
        }

        public override SchemaEncoded GetSchemaEncoded()
        {
            return SchemaEncoder.GetSchemaEncoded<TKey, TValue>(ResourceIdEncoded);
        }


    }
}
