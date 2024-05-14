using Nethereum.Contracts;
using Nethereum.Mud.EncodingDecoding;
using Nethereum.Web3;
using System;

namespace Nethereum.Mud
{
    public interface ITableRecordSingleton: IResource 
    {
        void DecodeValues(byte[] staticData, byte[] encodedLengths, byte[] dynamicData);
        EncodedValues GetEncodeValues();
        void DecodeValues(EncodedValues encodedValues);
        void DecodeValues(byte[] encodedValues);
   
    }
}
