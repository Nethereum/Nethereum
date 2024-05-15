using Nethereum.Util;
using System.IO;
namespace Nethereum.ABI.FunctionEncoding
{
    public class ByteCodeLibrary
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="path">The full path of the solidity file for example: "C:/MyLibrary.sol"</param>
        /// <param name="libraryName">The name of the library "MyLibrary" not "MyLibrary.sol"</param>
        /// <param name="libraryAddress"></param>
        /// <returns></returns>
        public static ByteCodeLibrary CreateFromPath(string path, string libraryName, string libraryAddress)
        {
            var library = new ByteCodeLibrary() { Address = libraryAddress, Path = path, LibraryName = libraryName };
            library.CalculatePlaceHolderKey();
            return library;
        }

        public string PlaceholderKey { get; set; }
        public string Address { get; set; }
        public string LibraryName { get; set; }
        public string Path { get; set; }
        public string ByteCode { get; set; }

        public void CalculatePlaceHolderKey()
        {
            var path = Path.Replace("\\", "/");
            PlaceholderKey = Sha3Keccack.Current.CalculateHash(path + ":" + LibraryName).Substring(0, 34);
        }   
    }
}