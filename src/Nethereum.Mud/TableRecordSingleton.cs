namespace Nethereum.Mud
{
    public abstract class TableRecordSingleton<TValue> where TValue : class, new()
    {
        public static byte[] TableResourceId { get; protected set; }
        public TableRecordSingleton(string nameSpace, string tableName)
        {
            Namespace = nameSpace;
            TableName = tableName;
            Values = new TValue();
            TableResourceId = ResourceId;
        }

        public TableRecordSingleton(string name)
        {
            Namespace = String.Empty;
            TableName = name;
            Values = new TValue();
            TableResourceId = ResourceId;
        }
        public string Namespace { get; protected set; }
        public string TableName { get; protected set; }

        private byte[] _resourceId;
        public byte[] ResourceId
        {
            get
            {
                if (_resourceId == null)
                {
                    _resourceId = ResourceEncoder.EncodeTable(Namespace, TableName);
                }
                return _resourceId;
            }
        }
      
        public TValue Values { get; set; }

        public virtual EncodedValues  GetEncodeValues()
        {
            return ValueEncoderDecoder.EncodedValues(Values);
        }

        public void DecodeValues(EncodedValues encodedValues)
        {
            Values = ValueEncoderDecoder.DecodeValues<TValue>(encodedValues);
        }

        public void DecodeValues(byte[] encodedValues)
        {
            Values = ValueEncoderDecoder.DecodeValues<TValue>(encodedValues);
        }

        public void DecodeValues(byte[] staticData, byte[] encodedLengths, byte[] dynamicData)
        {
            Values = ValueEncoderDecoder.DecodeValues<TValue>(staticData, encodedLengths, dynamicData);
        }

    }
}
