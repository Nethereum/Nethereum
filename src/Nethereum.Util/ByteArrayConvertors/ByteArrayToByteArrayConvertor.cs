using System;

namespace Nethereum.Util.ByteArrayConvertors;

/// <summary>
/// Raw byte array to byte array convertor.
/// </summary>
public class ByteArrayToByteArrayConvertor : IByteArrayConvertor<byte[]>
{
    public byte[] ConvertToByteArray(byte[] data) =>  data ?? new byte[0];
    
    public byte[] ConvertFromByteArray(byte[] data) => data ?? new byte[0];
}