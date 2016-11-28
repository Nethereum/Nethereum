using Nethereum.Hex.HexConvertors.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Nethereum.KeyStore.Console.Sample
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var address = "12890d2cce102216644c59dae5baed380d84830c";
            var password = "password";

            var file = File.OpenText("UTC--2015-11-25T05-05-03.116905600Z--12890d2cce102216644c59dae5baed380d84830c");
            var json = file.ReadToEnd();
            
            var service = new KeyStoreService();
            var key = service.DecryptKeyStoreFromJson(password, json);
            

           
            var fileName = service.GenerateUTCFileName(address);
            using (var newfile = File.CreateText(fileName))
            {
                var newJson = service.EncryptAndGenerateDefaultKeyStoreAsJson(password, key, address);
                newfile.Write(newJson);
                newfile.Flush();
            }


            file = File.OpenText(fileName);
            json = file.ReadToEnd();

            var newkey = service.DecryptKeyStoreFromJson(password, json);

            System.Console.WriteLine("Original key: " + key.ToHex());
            System.Console.WriteLine("New key: " + key.ToHex());
            System.Console.ReadLine();
        }
    }
}
