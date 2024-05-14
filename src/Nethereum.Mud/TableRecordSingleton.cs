using Nethereum.Mud.EncodingDecoding;
using System;

namespace Nethereum.Mud
{

    public abstract class TableRecordSingleton<TValue>: ITableRecordSingleton where TValue : class, new()
    {
        public TableRecordSingleton(string nameSpace, string tableName, bool isOffChainTable = false)
        {
            Namespace = nameSpace;
            Name = tableName;
            IsOffChain = isOffChainTable;
            Values = new TValue();
           
        }

        public TableRecordSingleton(string name)
        {
            Namespace = String.Empty;
            Name = name;
            Values = new TValue();
          
        }
        public string Namespace { get; protected set; }
        public string Name { get; protected set; }

        public string GetTableNameTrimmedForResource()
        {
            return ResourceEncoder.TrimNameAsValidSize(Name);
        }
        public bool IsOffChain { get; protected set; }

        private byte[] _resourceEncoded;
        public byte[] ResourceIdEncoded
        {
            get
            {
                if (_resourceEncoded == null)
                {
                    if (IsOffChain)
                    {
                        _resourceEncoded = ResourceEncoder.EncodeOffchainTable(Namespace, GetTableNameTrimmedForResource());
                    }
                    else
                    {
                        _resourceEncoded = ResourceEncoder.EncodeTable(Namespace, GetTableNameTrimmedForResource());
                    }
                    
                }
                return _resourceEncoded;
            }
        }
      
        public TValue Values { get; set; }

        public byte[] ResourceTypeId
        {
            get
            {
                if (IsOffChain)
                {
                    return Resource.RESOURCE_OFFCHAIN_TABLE;
                }
                else
                {
                    return Resource.RESOURCE_TABLE;
                }
            }
        }

        public virtual SchemaEncoded GetSchemaEncoded()
        {
            return SchemaEncoder.GetSchemaEncodedSingleton<TValue>(ResourceIdEncoded);
        }

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
