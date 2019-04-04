using Nethereum.Hex.HexConvertors.Extensions;
namespace Nethereum.Contracts
{
    public class ByteCodeLibraryLinker
    {
        public string LinkByteCode(string byteCode, params ByteCodeLibrary[] byteCodeLibraries)
        {
            foreach (var byteCodeLibrary in byteCodeLibraries)
            {
                byteCode = byteCode.Replace("__$" + byteCodeLibrary.PlaceholderKey + "$__", byteCodeLibrary.Address.RemoveHexPrefix());
            }
            return byteCode;
        }
    }
}