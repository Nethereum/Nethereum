using Nethereum.KeyStore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nethereum.Tutorials
{
    public class AcccountCreator
    {
        public string CreateAccount(string password, string path)
        {
            //Generate a private key pair using SecureRandom
            var ecKey = Nethereum.Signer.EthECKey.GenerateKey();
            //Get the public address (derivied from the public key)
            var address = ecKey.GetPublicAddress();

            //Create a store service, to encrypt and save the file using the web3 standard
            var service = new KeyStoreService();
            var encryptedKey = service.EncryptAndGenerateDefaultKeyStoreAsJson(password, ecKey.GetPrivateKeyAsBytes(), address);
            var fileName = service.GenerateUTCFileName(address);
            //save the File
            using (var newfile = File.CreateText(Path.Combine(path, fileName)))
            {
                newfile.Write(encryptedKey);
                newfile.Flush();
            }

            return fileName;
        }
    }
}
