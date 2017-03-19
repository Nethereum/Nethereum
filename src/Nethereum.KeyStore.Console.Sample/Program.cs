using Nethereum.Hex.HexConvertors.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.Signer;

namespace Nethereum.KeyStore.Console.Sample
{
    public class Program
    {
        public static void Main(string[] args)
        {

           // //Reading an existing file
           // var address = "12890d2cce102216644c59dae5baed380d84830c";
           // var password = "password";
           // //UTC--2016-11-23T09-58-36Z--ca2137bc-e2a1-5c40-d60d-ab8cf5fb302c

           // var file = File.OpenText("UTC--2016-11-23T09-58-36Z--ca2137bc-e2a1-5c40-d60d-ab8cf5fb302c");
           //// var file = File.OpenText("UTC--2015-11-25T05-05-03.116905600Z--12890d2cce102216644c59dae5baed380d84830c");
           // var json = file.ReadToEnd();
            
           // //using the simple key store service
           // var service = new KeyStoreService();
           // //decrypt the private key
           // var key = service.DecryptKeyStoreFromJson(password, json);
            
           ////Generating a new file using the existing private key (key) 
           ////create new file with the standard UTC file name
           // var fileName = service.GenerateUTCFileName(address);
           // using (var newfile = File.CreateText(fileName))
           // {
           //     //generate the encrypted and key store content as json. (The default uses pbkdf2)
           //     var newJson = service.EncryptAndGenerateDefaultKeyStoreAsJson(password, key, address);
           //     newfile.Write(newJson);
           //     newfile.Flush();
           // }
            
           // //Decrypting the new file created
           // file = File.OpenText(fileName);
           // json = file.ReadToEnd();

           // var newkey = service.DecryptKeyStoreFromJson(password, json);

           // //Compare the keys
           // System.Console.WriteLine("Original key: " + key.ToHex());
           // System.Console.WriteLine("New key: " + key.ToHex());
           // System.Console.ReadLine();


            //We can use EthECKey to generate a new ECKey pair, this is using SecureRandom
            var ecKey = EthECKey.GenerateKey();
            var privateKey = ecKey.GetPrivateKeyAsBytes();
            var genAddress = ecKey.GetPublicAddress();

            ////instead of the default service we can use either
            ////Scrypt
            //var scryptService = new KeyStoreScryptService();
            //var scryptResult = scryptService.EncryptAndGenerateKeyStoreAsJson(password, privateKey, genAddress);
            ////or pkbdf2
            //var pbkdf2Service = new KeyStorePbkdf2Service();
            //var pkbdf2Result = pbkdf2Service.EncryptAndGenerateKeyStoreAsJson(password, privateKey, genAddress);

            ////Both services can be configured with a new IRandomBytesGenerator for the IV and Salt, currently uses SecureRandom for both.
            ////also when encrypting we can pass custom KdfParameters
        }
    }
}
