using System.Collections.Generic;

namespace Nethereum.Mud
{
    public interface ITableRecord: ITableRecordSingleton
    {
        void DecodeKey(List<byte[]> encodedKey);
        List<byte[]> GetEncodedKey();
    }
}
