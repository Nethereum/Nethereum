using Nethereum.Mud.EncodingDecoding;

namespace Nethereum.Mud
{
    public interface ITableRecordSingleton
    {
        byte[] ResourceId { get; }
        string Namespace { get; }
        string TableName { get; }

        void DecodeValues(byte[] staticData, byte[] encodedLengths, byte[] dynamicData);
        EncodedValues GetEncodeValues();
        void DecodeValues(EncodedValues encodedValues);
        void DecodeValues(byte[] encodedValues);
   
    }
}
