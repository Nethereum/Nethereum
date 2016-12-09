# Key Store

The library Nethereum.KeyStore allows the decrytion and the encryption of private keys which using the [Web3 Secret Storage Definition](https://github.com/ethereum/wiki/wiki/Web3-Secret-Storage-Definition).
This is the same standard followed by the node clients like geth, eth or parity.

## Usage

### Key decryption and retrieval

To retrieve a key from a file stored using the  web3 secret storage definition you will need to do the following:

```csharp
var file = File.OpenText("UTC--2015-11-25T05-05-03.116905600Z--12890d2cce102216644c59dae5baed380d84830c");
var json = file.ReadToEnd();
```

First open the file and extract the json content, key store files can normally be found on the directory "keystore" on the following directories:

* Mac: ~/Library/Ethereum
* Linux: ~/.ethereum
* Windows: %APPDATA%\Ethereum

If using parity the keys can be found under the directory "keys" on $HOME/.parity/keys

To decrypt and extract the private key from the json file we can just use the KeyStoreService with a password.

```csharp
var password = "password";
//using the simple key store service
var service = new KeyStoreService();
//decrypt the private key
var key = service.DecryptKeyStoreFromJson(password, json);
```

### KeyStore encryption and creation

We ca

```csharp
var fileName = service.GenerateUTCFileName(address);
using (var newfile = File.CreateText(fileName))
{
    //generate the encrypted and key store content as json. (The default uses pbkdf2)
    var newJson = service.EncryptAndGenerateDefaultKeyStoreAsJson(password, key, address);
    newfile.Write(newJson);
    newfile.Flush();
}
            

```